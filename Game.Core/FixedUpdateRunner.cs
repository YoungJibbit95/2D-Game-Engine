namespace Game.Core;

public sealed class FixedUpdateRunner
{
    public FixedUpdateRunner(double fixedStepSeconds = 1.0 / 60.0, int maxSubSteps = 5)
    {
        if (fixedStepSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedStepSeconds), "Fixed step must be greater than zero.");
        }

        if (maxSubSteps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSubSteps), "Max sub-steps must be greater than zero.");
        }

        FixedStepSeconds = fixedStepSeconds;
        MaxSubSteps = maxSubSteps;
    }

    public double FixedStepSeconds { get; }

    public int MaxSubSteps { get; }

    public double AccumulatorSeconds { get; private set; }

    public int Run(double elapsedSeconds, Action<float> fixedUpdate)
    {
        ArgumentNullException.ThrowIfNull(fixedUpdate);

        if (elapsedSeconds < 0)
        {
            elapsedSeconds = 0;
        }

        var maximumAccumulation = FixedStepSeconds * MaxSubSteps;
        AccumulatorSeconds = Math.Min(AccumulatorSeconds + elapsedSeconds, maximumAccumulation);

        var steps = 0;
        while (AccumulatorSeconds >= FixedStepSeconds && steps < MaxSubSteps)
        {
            fixedUpdate((float)FixedStepSeconds);
            AccumulatorSeconds -= FixedStepSeconds;
            steps++;
        }

        return steps;
    }

    public void Reset()
    {
        AccumulatorSeconds = 0;
    }
}
