using JuiceMidiMaker.Models;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;

namespace JuiceMidiMaker.Services;

public sealed class MidiExportService
{
    public void Export(string filePath, SequencerSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsBpmValid(session.BPM))
            throw new ArgumentOutOfRangeException(nameof(session), "BPM must be between 20 and 400.");
        if (session.MidiNoteNumber > 127)
            throw new ArgumentOutOfRangeException(nameof(session), "MIDI note must be between 0 and 127.");
        if (session.Velocity > 127)
            throw new ArgumentOutOfRangeException(nameof(session), "Velocity must be between 0 and 127.");

        var noteNumber = (SevenBitNumber)session.MidiNoteNumber;
        var velocity = (SevenBitNumber)session.Velocity;
        var scheduledEvents = new List<ScheduledMidiEvent>
        {
            new(0, 0, new SequenceTrackNameEvent("JuiceMidiMaker Pattern")),
            new(0, 0, new SetTempoEvent(60_000_000L / session.BPM)),
            new(0, 0, new TimeSignatureEvent(4, 4))
        };

        for (var step = 0; step < TimingConstants.TotalSteps; step++)
        {
            if (!session.GridSteps[step])
                continue;

            var noteOnTime = (long)step * TimingConstants.TicksPerStep;
            scheduledEvents.Add(new ScheduledMidiEvent(
                noteOnTime,
                1,
                new NoteOnEvent(noteNumber, velocity)));
            scheduledEvents.Add(new ScheduledMidiEvent(
                noteOnTime + TimingConstants.NoteLength,
                0,
                new NoteOffEvent(noteNumber, (SevenBitNumber)0)));
        }

        var orderedEvents = scheduledEvents
            .OrderBy(item => item.Time)
            .ThenBy(item => item.SortOrder)
            .ToList();

        long previousTime = 0;
        foreach (var scheduledEvent in orderedEvents)
        {
            scheduledEvent.Event.DeltaTime = scheduledEvent.Time - previousTime;
            previousTime = scheduledEvent.Time;
        }

        var trackChunk = new TrackChunk(orderedEvents.Select(item => item.Event).ToArray());
        var midiFile = new MidiFile(trackChunk)
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(TimingConstants.PPQ)
        };

        midiFile.Write(filePath, overwriteFile: true);
    }

    private sealed record ScheduledMidiEvent(long Time, int SortOrder, MidiEvent Event);
}
