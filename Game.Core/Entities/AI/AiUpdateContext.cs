using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities.AI;

public readonly record struct AiUpdateContext(
    GameWorld World,
    IReadOnlyList<Entity> Entities,
    PlayerEntity? Player = null,
    bool IsNight = false,
    long TickNumber = 0)
{
    public static AiUpdateContext WithoutEntities(GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new AiUpdateContext(world, Array.Empty<Entity>());
    }

    public Entity? FindEntity(int id)
    {
        if (Player is { IsActive: true } player && player.Id == id)
        {
            return player;
        }

        for (var index = 0; index < Entities.Count; index++)
        {
            if (Entities[index].Id == id && Entities[index].IsActive)
            {
                return Entities[index];
            }
        }

        return null;
    }
}
