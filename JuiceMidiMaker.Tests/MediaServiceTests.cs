using JuiceMidiMaker.Models;
using JuiceMidiMaker.Services;
using Melanchall.DryWetMidi.Core;
using NAudio.Wave;

namespace JuiceMidiMaker.Tests;

public sealed class MediaServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "JuiceMidiMaker.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Export_CreatesMidiWithFixedPpqAndNoteEvents()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "Pattern.mid");
        var session = new SequencerSession { BPM = 128, MidiNoteNumber = 36, Velocity = 100 };
        session.GridSteps[0] = true;
        session.GridSteps[4] = true;

        new MidiExportService().Export(path, session);
        var midiFile = MidiFile.Read(path);

        var division = Assert.IsType<TicksPerQuarterNoteTimeDivision>(midiFile.TimeDivision);
        var events = midiFile.GetTrackChunks().SelectMany(chunk => chunk.Events).ToList();
        Assert.Equal(TimingConstants.PPQ, division.TicksPerQuarterNote);
        Assert.Equal(2, events.OfType<NoteOnEvent>().Count());
        Assert.Equal(2, events.OfType<NoteOffEvent>().Count());
    }

    [Fact]
    public void LoadSample_CachesMonoWavWithoutOpeningAudioDevice()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "Sample.wav");
        using (var writer = new WaveFileWriter(path, new WaveFormat(44100, 16, 1)))
        {
            for (var index = 0; index < 4410; index++)
                writer.WriteSample((float)Math.Sin(index * 2 * Math.PI * 440 / 44100) * 0.25f);
        }

        using var engine = new AudioPlaybackEngine();
        var track = engine.LoadSample(path);

        Assert.True(engine.HasSample);
        Assert.Equal(44100, track.SampleRate);
        Assert.Equal(1, track.Channels);
        Assert.Equal(256, track.AudioWaveBuffer?.Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
