using Game.Core.Data;

namespace Game.Core.Animations;

public sealed class SpriteAnimationRegistry
{
    private readonly Dictionary<string, SpriteAnimationClip> _byId;

    private SpriteAnimationRegistry(IEnumerable<SpriteAnimationClip> clips)
    {
        _byId = new Dictionary<string, SpriteAnimationClip>(StringComparer.OrdinalIgnoreCase);
        foreach (var clip in clips)
        {
            AddValidated(clip);
        }
    }

    public IReadOnlyCollection<SpriteAnimationClip> Clips => _byId.Values;

    public static SpriteAnimationRegistry Create(IEnumerable<SpriteAnimationClip> clips)
    {
        return new SpriteAnimationRegistry(clips);
    }

    public SpriteAnimationClip GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!_byId.TryGetValue(id, out var clip))
        {
            throw new KeyNotFoundException($"Animation clip '{id}' was not registered.");
        }

        return clip;
    }

    public bool TryGetById(string id, out SpriteAnimationClip clip)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out clip!);
    }

    private void AddValidated(SpriteAnimationClip clip)
    {
        Validate(clip);
        if (_byId.ContainsKey(clip.Id))
        {
            throw new RegistryValidationException($"Duplicate animation clip id '{clip.Id}'.");
        }

        _byId.Add(clip.Id, clip);
    }

    private static void Validate(SpriteAnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (string.IsNullOrWhiteSpace(clip.Id))
        {
            throw new RegistryValidationException("Animation clip id is required.");
        }

        if (clip.Frames.Count == 0)
        {
            throw new RegistryValidationException($"Animation clip '{clip.Id}' must contain at least one frame.");
        }

        foreach (var frame in clip.Frames)
        {
            if (string.IsNullOrWhiteSpace(frame.SpriteId))
            {
                throw new RegistryValidationException($"Animation clip '{clip.Id}' contains a frame without a sprite id.");
            }

            if (frame.FrameIndex < 0)
            {
                throw new RegistryValidationException($"Animation clip '{clip.Id}' contains a frame with a negative frame index.");
            }

            if (frame.DurationSeconds <= 0)
            {
                throw new RegistryValidationException($"Animation clip '{clip.Id}' contains a non-positive frame duration.");
            }
        }
    }
}
