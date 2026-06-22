namespace JuiceMidiMaker.Models;

public static class SequencerLayout
{
    public const int StepsPerBar = 16;
    public const int BarsPerGroup = 4;
    public const int TotalBars = 16;

    public static IReadOnlyList<BarGroupViewModel> CreateBarGroups(
        IReadOnlyList<StepViewModel> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count != TimingConstants.TotalSteps)
            throw new ArgumentException(
                $"The sequencer requires exactly {TimingConstants.TotalSteps} steps.",
                nameof(steps));

        var bars = Enumerable.Range(0, TotalBars)
            .Select(barIndex => new BarViewModel(
                barIndex + 1,
                steps.Skip(barIndex * StepsPerBar).Take(StepsPerBar).ToArray()))
            .ToArray();

        return Enumerable.Range(0, TotalBars / BarsPerGroup)
            .Select(groupIndex => new BarGroupViewModel(
                (groupIndex * BarsPerGroup) + 1,
                bars.Skip(groupIndex * BarsPerGroup).Take(BarsPerGroup).ToArray()))
            .ToArray();
    }
}

public sealed class BarViewModel
{
    public BarViewModel(int barNumber, IReadOnlyList<StepViewModel> steps)
    {
        BarNumber = barNumber;
        Steps = steps;
    }

    public int BarNumber { get; }
    public IReadOnlyList<StepViewModel> Steps { get; }
}

public sealed class BarGroupViewModel
{
    public BarGroupViewModel(int startBar, IReadOnlyList<BarViewModel> bars)
    {
        StartBar = startBar;
        EndBar = startBar + bars.Count - 1;
        Bars = bars;
    }

    public int StartBar { get; }
    public int EndBar { get; }
    public string Label => $"BARS {StartBar}-{EndBar}";
    public IReadOnlyList<BarViewModel> Bars { get; }
}
