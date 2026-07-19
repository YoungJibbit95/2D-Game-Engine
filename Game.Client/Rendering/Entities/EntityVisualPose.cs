using System.Numerics;

namespace Game.Client.Rendering.Entities;

public readonly record struct EntityVisualColor(byte R, byte G, byte B, byte A = byte.MaxValue)
{
    public static EntityVisualColor White { get; } = new(255, 255, 255);

    public static EntityVisualColor HitFlash { get; } = new(255, 182, 176);

    public static EntityVisualColor Elite { get; } = new(184, 224, 255);

    public static EntityVisualColor EliteOutline { get; } = new(91, 207, 255, 220);

    public static EntityVisualColor MagicProjectile { get; } = new(170, 210, 255);

    public static EntityVisualColor Shadow(float opacity)
    {
        return new EntityVisualColor(6, 10, 18, (byte)Math.Clamp((int)(opacity * 255f), 0, 255));
    }
}

public readonly record struct EntityVisualPose(
    int EntityId,
    EntityVisualState State,
    string SpriteId,
    int FrameIndex,
    Vector2 Position,
    Vector2 OriginNormalized,
    Vector2 Scale,
    float RotationRadians,
    bool FlipHorizontally,
    EntityVisualColor Tint,
    bool DrawOutline,
    EntityVisualColor OutlineColor,
    float OutlineThickness,
    bool CastsShadow,
    Vector2 ShadowPosition,
    Vector2 ShadowSize,
    EntityVisualColor ShadowColor,
    float LayerDepth);

public enum EntityVisualDrawCommandKind
{
    ShadowEllipse,
    Outline,
    Sprite
}

public readonly record struct EntityVisualDrawCommand(
    EntityVisualDrawCommandKind Kind,
    int EntityId,
    string SpriteId,
    int FrameIndex,
    Vector2 Position,
    Vector2 OriginNormalized,
    Vector2 Scale,
    float RotationRadians,
    bool FlipHorizontally,
    EntityVisualColor Color,
    float Thickness,
    float LayerDepth);

public readonly record struct EntityVisualTelemetry(
    int InputEntities,
    int VisibleEntities,
    int CulledEntities,
    int InactiveEntities,
    int PreparedCommands,
    int CommandOverflowEntities,
    int TrackOverflowEntities)
{
    public EntityVisualSubmissionTelemetry Submission { get; init; }

    public bool WasBudgetClamped => CommandOverflowEntities > 0 || TrackOverflowEntities > 0;

    public bool UsedSubmissionFallback => Submission.WasCapacityExceeded;
}
