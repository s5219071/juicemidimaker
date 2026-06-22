using JuiceMidiMaker.Models;

namespace JuiceMidiMaker.Services;

public static class SequencerTimeline
{
    public static double GetStepDurationMilliseconds(int bpm)
    {
        if (bpm is < 20 or > 400)
            throw new ArgumentOutOfRangeException(nameof(bpm), "BPM must be between 20 and 400.");

        return 15_000d / bpm;
    }

    public static long GetExpectedProcessedStepCount(
        long anchorProcessedStepCount,
        double elapsedMilliseconds,
        int bpm)
    {
        if (anchorProcessedStepCount < 0)
            throw new ArgumentOutOfRangeException(nameof(anchorProcessedStepCount));
        if (elapsedMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));

        var elapsedSteps = (long)Math.Floor(
            elapsedMilliseconds / GetStepDurationMilliseconds(bpm));
        return anchorProcessedStepCount + elapsedSteps;
    }

    public static int GetStepIndex(long processedStepCount)
    {
        if (processedStepCount < 0)
            throw new ArgumentOutOfRangeException(nameof(processedStepCount));

        return (int)(processedStepCount % TimingConstants.TotalSteps);
    }
}
