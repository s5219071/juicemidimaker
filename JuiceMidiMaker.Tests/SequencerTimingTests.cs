using JuiceMidiMaker.Models;
using JuiceMidiMaker.Services;

namespace JuiceMidiMaker.Tests;

public sealed class SequencerTimingTests
{
    [Fact]
    public void Layout_MapsBarGroupsToTheirRealStepIndexes()
    {
        var steps = Enumerable.Range(0, TimingConstants.TotalSteps)
            .Select(index => new StepViewModel(index))
            .ToArray();

        var groups = SequencerLayout.CreateBarGroups(steps);

        Assert.Equal(4, groups.Count);
        Assert.Equal("BARS 1-4", groups[0].Label);
        Assert.Equal(0, groups[0].Bars[0].Steps[0].Index);
        Assert.Equal(64, groups[1].Bars[0].Steps[0].Index);
        Assert.Equal(128, groups[2].Bars[0].Steps[0].Index);
        Assert.Equal(192, groups[3].Bars[0].Steps[0].Index);
        Assert.Equal(255, groups[3].Bars[3].Steps[15].Index);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(64, 64)]
    [InlineData(128, 128)]
    [InlineData(192, 192)]
    [InlineData(256, 0)]
    public void AbsoluteTimeline_KeepsEveryBarGroupOnTheGrid(
        int elapsedStepIntervals,
        int expectedStepIndex)
    {
        const int bpm = 128;
        const long initialProcessedStepCount = 1;
        var elapsed = SequencerTimeline.GetStepDurationMilliseconds(bpm) * elapsedStepIntervals;

        var expectedProcessedSteps = SequencerTimeline.GetExpectedProcessedStepCount(
            initialProcessedStepCount,
            elapsed,
            bpm);
        var dueStepIndex = SequencerTimeline.GetStepIndex(expectedProcessedSteps - 1);

        Assert.Equal(expectedStepIndex, dueStepIndex);
    }
}
