using JuiceMidiMaker.Models;

namespace JuiceMidiMaker.Tests;

public sealed class SequencerSessionTests
{
    [Fact]
    public void ApplyPreset_PadsShortGridWithInactiveSteps()
    {
        var session = new SequencerSession();
        var preset = new PresetData
        {
            BPM = 140,
            GridSteps = [true, false, true]
        };

        session.ApplyPreset(preset);

        Assert.Equal(TimingConstants.TotalSteps, session.GridSteps.Length);
        Assert.True(session.GridSteps[0]);
        Assert.True(session.GridSteps[2]);
        Assert.False(session.GridSteps[3]);
        Assert.Equal(140, session.BPM);
    }

    [Fact]
    public void ApplyPreset_TruncatesLongGrid()
    {
        var session = new SequencerSession();
        var preset = new PresetData
        {
            GridSteps = Enumerable.Repeat(true, TimingConstants.TotalSteps + 20).ToArray()
        };

        session.ApplyPreset(preset);

        Assert.Equal(TimingConstants.TotalSteps, session.GridSteps.Length);
        Assert.All(session.GridSteps, Assert.True);
    }

    [Fact]
    public void ToPreset_ClonesGridInsteadOfSharingIt()
    {
        var session = new SequencerSession();
        session.GridSteps[0] = true;

        var preset = session.ToPreset("Test");
        session.GridSteps[0] = false;

        Assert.True(preset.GridSteps[0]);
    }

    [Fact]
    public void ApplyPreset_ClampsInvalidMidiValues()
    {
        var session = new SequencerSession();
        var preset = new PresetData { MidiNoteNumber = 255, Velocity = 200 };

        session.ApplyPreset(preset);

        Assert.Equal(127, session.MidiNoteNumber);
        Assert.Equal(127, session.Velocity);
    }
}
