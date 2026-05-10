namespace Game.Core.UI.Animation;

public sealed class UiAnimationTrack
{
    public UiAnimationTrack(UiAnimationProperty property, IEnumerable<UiAnimationKeyframe> keyframes)
    {
        ArgumentNullException.ThrowIfNull(keyframes);

        Property = property;
        Keyframes = keyframes.OrderBy(keyframe => keyframe.TimeSeconds).ToArray();
        if (Keyframes.Count == 0)
        {
            throw new ArgumentException("Animation tracks require at least one keyframe.", nameof(keyframes));
        }

        if (Keyframes.Any(keyframe => keyframe.TimeSeconds < 0))
        {
            throw new ArgumentException("Animation keyframe time must not be negative.", nameof(keyframes));
        }
    }

    public UiAnimationProperty Property { get; }

    public IReadOnlyList<UiAnimationKeyframe> Keyframes { get; }

    public float DurationSeconds => Keyframes[^1].TimeSeconds;

    public float Evaluate(float timeSeconds)
    {
        if (timeSeconds <= Keyframes[0].TimeSeconds || Keyframes.Count == 1)
        {
            return Keyframes[0].Value;
        }

        for (var index = 1; index < Keyframes.Count; index++)
        {
            var previous = Keyframes[index - 1];
            var next = Keyframes[index];
            if (timeSeconds > next.TimeSeconds)
            {
                continue;
            }

            var span = next.TimeSeconds - previous.TimeSeconds;
            var t = span <= 0 ? 1f : Math.Clamp((timeSeconds - previous.TimeSeconds) / span, 0f, 1f);
            t = ApplyCurve(t, next.Curve);
            return previous.Value + (next.Value - previous.Value) * t;
        }

        return Keyframes[^1].Value;
    }

    public static float ApplyCurve(float t, UiAnimationCurve curve)
    {
        t = Math.Clamp(t, 0f, 1f);
        return curve switch
        {
            UiAnimationCurve.EaseIn => t * t,
            UiAnimationCurve.EaseOut => 1f - (1f - t) * (1f - t),
            UiAnimationCurve.EaseInOut => t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f,
            UiAnimationCurve.SmoothStep => t * t * (3f - 2f * t),
            _ => t
        };
    }
}
