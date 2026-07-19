using Game.Core.Settings;

namespace Game.Client.Rendering.Performance;

public readonly record struct FramePacingDecision(
    bool SynchronizeWithVerticalRetrace,
    int SoftwareFrameRateLimit);

public static class FramePacingPolicy
{
    public const int DefaultLowLatencyLimit = 165;

    public static FramePacingDecision Resolve(VideoSettings video)
    {
        ArgumentNullException.ThrowIfNull(video);
        if (video.LowLatencyFramePacing)
        {
            return new FramePacingDecision(
                SynchronizeWithVerticalRetrace: false,
                SoftwareFrameRateLimit: video.FrameRateLimit == 0
                    ? DefaultLowLatencyLimit
                    : video.FrameRateLimit);
        }

        return new FramePacingDecision(
            SynchronizeWithVerticalRetrace: video.VSync,
            SoftwareFrameRateLimit: video.VSync ? 0 : video.FrameRateLimit);
    }
}
