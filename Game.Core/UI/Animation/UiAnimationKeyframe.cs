namespace Game.Core.UI.Animation;

public readonly record struct UiAnimationKeyframe(
    float TimeSeconds,
    float Value,
    UiAnimationCurve Curve = UiAnimationCurve.EaseOut);
