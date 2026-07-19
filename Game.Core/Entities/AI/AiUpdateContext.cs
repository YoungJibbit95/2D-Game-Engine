using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities.AI;

public readonly record struct AiUpdateContext(
    GameWorld World,
    IReadOnlyList<Entity> Entities,
    PlayerEntity? Player = null,
    bool IsNight = false,
    long TickNumber = 0,
    EntityManager? Manager = null)
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

        if (Manager?.FindActiveEntity(id) is { } indexed)
        {
            return indexed;
        }

        for (var index = 0; Manager is null && index < Entities.Count; index++)
        {
            if (Entities[index].Id == id && Entities[index].IsActive)
            {
                return Entities[index];
            }
        }

        return null;
    }

    internal IReadOnlyList<Entity> QueryNeighborhood(Vector2 center, float radius)
    {
        return Manager is null ? Entities : Manager.QueryAiNeighborhood(center, Math.Max(0, radius));
    }
}
