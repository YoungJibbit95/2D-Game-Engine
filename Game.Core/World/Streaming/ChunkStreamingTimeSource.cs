using System.Diagnostics;

namespace Game.Core.World.Streaming;

public interface IChunkStreamingTimeSource
{
    long GetTimestamp();

    TimeSpan GetElapsedTime(long startingTimestamp);
}

public sealed class StopwatchChunkStreamingTimeSource : IChunkStreamingTimeSource
{
    public static StopwatchChunkStreamingTimeSource Instance { get; } = new();

    public long GetTimestamp()
    {
        return Stopwatch.GetTimestamp();
    }

    public TimeSpan GetElapsedTime(long startingTimestamp)
    {
        return Stopwatch.GetElapsedTime(startingTimestamp);
    }
}
