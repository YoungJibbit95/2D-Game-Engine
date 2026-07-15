namespace Game.Core.Time;

public sealed class WorldTime
{
    public WorldTime(double dayLengthSeconds = 24 * 60)
    {
        if (dayLengthSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dayLengthSeconds), "Day length must be greater than zero.");
        }

        DayLengthSeconds = dayLengthSeconds;
    }

    public double DayLengthSeconds { get; }

    public double TimeOfDaySeconds { get; private set; }

    public int Day { get; private set; } = 1;

    public double NormalizedTimeOfDay => TimeOfDaySeconds / DayLengthSeconds;

    public bool IsNight => NormalizedTimeOfDay < 0.25 || NormalizedTimeOfDay >= 0.75;

    public void Update(double deltaSeconds)
    {
        if (deltaSeconds <= 0)
        {
            return;
        }

        TimeOfDaySeconds += deltaSeconds;
        while (TimeOfDaySeconds >= DayLengthSeconds)
        {
            TimeOfDaySeconds -= DayLengthSeconds;
            Day++;
        }
    }

    public void SetTimeNormalized(double normalizedTime)
    {
        normalizedTime = normalizedTime - Math.Floor(normalizedTime);
        TimeOfDaySeconds = normalizedTime * DayLengthSeconds;
    }

    public void SetDay()
    {
        SetTimeNormalized(0.5);
    }

    public void SetNight()
    {
        SetTimeNormalized(0.0);
    }

    public void RestoreState(int day, double timeOfDaySeconds)
    {
        if (day < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(day), "Day must be at least one.");
        }

        if (!double.IsFinite(timeOfDaySeconds) || timeOfDaySeconds < 0 || timeOfDaySeconds >= DayLengthSeconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeOfDaySeconds),
                "Time of day must be finite and within the configured day length.");
        }

        Day = day;
        TimeOfDaySeconds = timeOfDaySeconds;
    }
}
