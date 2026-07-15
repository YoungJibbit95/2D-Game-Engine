using System.Numerics;

namespace Game.Core.Feedback;

public readonly record struct GameplayFeedbackCue(
    GameplayFeedbackCueKind Kind,
    Vector2 WorldPosition,
    float Intensity = 1f,
    int Amount = 0,
    string? ContentId = null);
