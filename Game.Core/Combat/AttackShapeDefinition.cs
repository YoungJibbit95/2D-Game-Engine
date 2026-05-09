namespace Game.Core.Combat;

public sealed record AttackShapeDefinition
{
    public AttackShapeKind Kind { get; init; } = AttackShapeKind.Rectangle;

    public float Range { get; init; } = 38;

    public float Width { get; init; } = 38;

    public float Height { get; init; } = 30;

    public float AngleRadians { get; init; } = MathF.PI / 2;
}
