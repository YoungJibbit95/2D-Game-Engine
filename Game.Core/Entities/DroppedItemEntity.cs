using Game.Core.Inventory;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities;

public sealed class DroppedItemEntity : Entity
{
    private const float Gravity = 850f;
    private const float MaxFallSpeed = 420f;
    private const float GroundDrag = 900f;

    private readonly TileCollisionResolver _collisionResolver;

    public DroppedItemEntity(ItemStack stack, Vector2 position, TileCollisionResolver collisionResolver)
    {
        if (stack.IsEmpty)
        {
            throw new ArgumentException("Dropped item stack must not be empty.", nameof(stack));
        }

        Stack = stack;
        _collisionResolver = collisionResolver;
        Body = new PhysicsBody
        {
            Position = position,
            Size = new Vector2(10, 10)
        };
        Position = position;
    }

    public ItemStack Stack { get; private set; }

    public PhysicsBody Body { get; }

    public override RectI Bounds => Body.Bounds;

    public void SetStack(ItemStack stack)
    {
        Stack = stack;
        if (Stack.IsEmpty)
        {
            IsActive = false;
        }
    }

    public override void Update(GameWorld world, float deltaSeconds)
    {
        Body.Velocity = new Vector2(
            MoveToward(Body.Velocity.X, 0, GroundDrag * deltaSeconds),
            Math.Min(Body.Velocity.Y + Gravity * deltaSeconds, MaxFallSpeed));

        _collisionResolver.Move(world, Body, deltaSeconds);
        Position = Body.Position;
    }

    private static float MoveToward(float current, float target, float maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + Math.Sign(target - current) * maxDelta;
    }
}
