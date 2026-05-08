using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities;

public abstract class Entity
{
    public int Id { get; private set; }

    public Vector2 Position { get; protected set; }

    public bool IsActive { get; set; } = true;

    public abstract RectI Bounds { get; }

    public abstract void Update(GameWorld world, float deltaSeconds);

    internal void AssignId(int id)
    {
        if (Id != 0 && Id != id)
        {
            throw new InvalidOperationException($"Entity already has id {Id}.");
        }

        Id = id;
    }
}
