using Game.Core.Characters;
using System.Globalization;
using System.Numerics;
using LegacyAnimationClip = Game.Core.Animations.SpriteAnimationClip;
using LegacyAnimationLoopMode = Game.Core.Animations.SpriteAnimationLoopMode;
using LegacyAnimationRegistry = Game.Core.Animations.SpriteAnimationRegistry;

namespace Game.Core.Animation;

public static class LegacySpriteAnimationClipAdapter
{
    public static AnimationClip ToFixedTickClip(
        LegacyAnimationClip source,
        int fixedTicksPerSecond = 60,
        string trackId = "body",
        string targetLayerId = "body")
    {
        ArgumentNullException.ThrowIfNull(source);
        if (fixedTicksPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedTicksPerSecond));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLayerId);
        if (source.Frames.Count == 0)
        {
            throw new ArgumentException("Legacy animation clip must contain frames.", nameof(source));
        }

        var frames = new AnimationFrame[source.Frames.Count];
        var events = new List<AnimationEventMarker>();
        var firstSpriteId = source.Frames[0].SpriteId;
        var timelineTick = 0;

        for (var index = 0; index < source.Frames.Count; index++)
        {
            var sourceFrame = source.Frames[index];
            if (!float.IsFinite(sourceFrame.DurationSeconds) || sourceFrame.DurationSeconds <= 0f)
            {
                throw new ArgumentException(
                    $"Legacy frame {index} in clip '{source.Id}' has an invalid duration.",
                    nameof(source));
            }

            var exactTicks = sourceFrame.DurationSeconds * fixedTicksPerSecond;
            if (exactTicks > int.MaxValue)
            {
                throw new ArgumentException(
                    $"Legacy frame {index} in clip '{source.Id}' is too long for fixed-tick conversion.",
                    nameof(source));
            }

            var durationTicks = Math.Max(1, (int)MathF.Round(exactTicks, MidpointRounding.AwayFromZero));
            frames[index] = new AnimationFrame(
                sourceFrame.FrameIndex,
                durationTicks,
                transform: new AnimationTransform2D(
                    new Vector2(sourceFrame.OffsetX, sourceFrame.OffsetY),
                    0f,
                    Vector2.One),
                spriteIdOverride: string.Equals(sourceFrame.SpriteId, firstSpriteId, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : sourceFrame.SpriteId);

            if (!string.IsNullOrWhiteSpace(sourceFrame.EventId))
            {
                events.Add(new AnimationEventMarker(sourceFrame.EventId, timelineTick));
            }

            timelineTick = checked(timelineTick + durationTicks);
        }

        return new AnimationClip(
            source.Id,
            ConvertLoopMode(source.LoopMode),
            new[] { new AnimationTrack(trackId, targetLayerId, firstSpriteId, frames) },
            events);
    }

    private static AnimationLoopMode ConvertLoopMode(LegacyAnimationLoopMode loopMode)
    {
        return loopMode switch
        {
            LegacyAnimationLoopMode.Once => AnimationLoopMode.Once,
            LegacyAnimationLoopMode.PingPong => AnimationLoopMode.PingPong,
            _ => AnimationLoopMode.Loop
        };
    }
}

public static class LegacyCharacterAnimationSetAdapter
{
    public static AnimationStateLayerDefinition ToStateLayer(
        CharacterAnimationSetDefinition source,
        LegacyAnimationRegistry animationRegistry,
        int fixedTicksPerSecond = 60,
        string layerId = "base",
        int priority = 0,
        IEnumerable<AnimationTransitionDefinition>? transitions = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(animationRegistry);
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);

        var states = new List<AnimationStateDefinition>(source.StateClips.Count + 1);
        var hasIdleState = source.StateClips.ContainsKey(CharacterAnimationState.Idle) ||
            !string.IsNullOrWhiteSpace(source.DefaultClipId);
        if (!source.StateClips.ContainsKey(CharacterAnimationState.Idle) &&
            !string.IsNullOrWhiteSpace(source.DefaultClipId))
        {
            states.Add(new AnimationStateDefinition(
                "idle",
                LegacySpriteAnimationClipAdapter.ToFixedTickClip(
                    animationRegistry.GetById(source.DefaultClipId),
                    fixedTicksPerSecond)));
        }

        foreach (var pair in source.StateClips.OrderBy(pair => pair.Key))
        {
            var stateId = ToStateId(pair.Key);
            var clip = LegacySpriteAnimationClipAdapter.ToFixedTickClip(
                animationRegistry.GetById(pair.Value),
                fixedTicksPerSecond);
            var isLocomotion = pair.Key == CharacterAnimationState.Walk;
            var isLockingAction = pair.Key is CharacterAnimationState.Attack or
                CharacterAnimationState.Mine or
                CharacterAnimationState.UseItem or
                CharacterAnimationState.Hurt;
            var completionStateId = isLockingAction && clip.LoopMode == AnimationLoopMode.Once && hasIdleState
                ? "idle"
                : null;

            states.Add(new AnimationStateDefinition(
                stateId,
                clip,
                scaleWithLocomotion: isLocomotion,
                actionLockMode: isLockingAction && clip.LoopMode == AnimationLoopMode.Once
                    ? AnimationActionLockMode.UntilClipComplete
                    : AnimationActionLockMode.None,
                completionStateId: completionStateId));
        }

        if (states.Count == 0)
        {
            throw new ArgumentException("Legacy character animation set contains no explicit state clips.", nameof(source));
        }

        var initialStateId = states.Any(state => string.Equals(state.Id, "idle", StringComparison.OrdinalIgnoreCase))
            ? "idle"
            : states[0].Id;

        return new AnimationStateLayerDefinition(
            layerId,
            priority,
            initialStateId,
            states,
            transitions);
    }

    public static string ToStateId(CharacterAnimationState state)
    {
        return state switch
        {
            CharacterAnimationState.Walk => "run",
            CharacterAnimationState.Attack => "swing",
            CharacterAnimationState.UseItem => "use-item",
            CharacterAnimationState.Block => "block",
            CharacterAnimationState.Dead => "death",
            _ => state.ToString().ToLowerInvariant()
        };
    }
}

public sealed record LegacyCharacterSpriteBindings
{
    public required string EyesSpriteId { get; init; }

    public required Func<string, string> HairSpriteResolver { get; init; }

    public required Func<string, string> ClothingSpriteResolver { get; init; }

    public required Func<string, string?> AccessorySpriteResolver { get; init; }

    public string? HandsSpriteId { get; init; }
}

public static class LegacyCharacterAppearanceAdapter
{
    public static CharacterAppearanceProfile ToProfile(
        Game.Core.Characters.CharacterAppearance source,
        LegacyCharacterSpriteBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindings.EyesSpriteId);

        var hairSpriteId = bindings.HairSpriteResolver(source.HairStyleId);
        var clothingSpriteId = bindings.ClothingSpriteResolver(source.ClothesStyleId);
        var accessorySpriteId = bindings.AccessorySpriteResolver(source.AccessoryId);

        return new CharacterAppearanceProfile(
            new CharacterPartAppearance(source.BodySpriteId, ParseColor(source.SkinTone)),
            eyes: new CharacterPartAppearance(bindings.EyesSpriteId, ParseColor(source.EyeColor)),
            hair: new CharacterPartAppearance(hairSpriteId, ParseColor(source.HairColor)),
            clothing: new CharacterPartAppearance(clothingSpriteId, ParseColor(source.ShirtColor)),
            hands: new CharacterPartAppearance(
                bindings.HandsSpriteId ?? source.BodySpriteId,
                ParseColor(source.SkinTone)),
            accessory: accessorySpriteId is null
                ? CharacterPartAppearance.Hidden
                : new CharacterPartAppearance(accessorySpriteId));
    }

    private static AnimationColor ParseColor(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value[0] == '#' ? value[1..] : value;
        if (normalized.Length is not (6 or 8) ||
            !uint.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            throw new FormatException($"Character color '{value}' must be #RRGGBB or #RRGGBBAA.");
        }

        if (normalized.Length == 6)
        {
            return new AnimationColor(
                (byte)(packed >> 16),
                (byte)(packed >> 8),
                (byte)packed);
        }

        return new AnimationColor(
            (byte)(packed >> 24),
            (byte)(packed >> 16),
            (byte)(packed >> 8),
            (byte)packed);
    }
}
