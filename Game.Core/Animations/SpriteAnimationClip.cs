using Game.Core.Data;

namespace Game.Core.Animations;

public sealed record SpriteAnimationClip
{
    public required string Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public SpriteAnimationLoopMode LoopMode { get; init; } = SpriteAnimationLoopMode.Loop;

    public IReadOnlyList<SpriteAnimationFrame> Frames { get; init; } = Array.Empty<SpriteAnimationFrame>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public float TotalDurationSeconds => Frames.Sum(frame => Math.Max(0.0001f, frame.DurationSeconds));

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }

    public SpriteAnimationFrame GetFrame(int frameIndex)
    {
        if (Frames.Count == 0)
        {
            throw new InvalidOperationException($"Animation clip '{Id}' has no frames.");
        }

        return Frames[Math.Clamp(frameIndex, 0, Frames.Count - 1)];
    }
}
