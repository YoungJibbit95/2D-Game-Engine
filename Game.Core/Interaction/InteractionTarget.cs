using Game.Core.World;

namespace Game.Core.Interaction;

public readonly record struct InteractionTarget(bool Found, TilePos TilePosition)
{
    public static InteractionTarget None { get; } = new(false, TilePos.Zero);
}
