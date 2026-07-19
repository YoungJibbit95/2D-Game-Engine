using Game.Core.Inventory;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities;

public sealed class DroppedItemEntity : Entity, IEntityPhysicsParticipant
{
    private const float MaxFallSpeed = 420f;
    private const float GroundDrag = 900f;

    private readonly TileCollisionResolver _standaloneCollisionResolver;
    private PhysicsWorld? _standalonePhysicsWorld;

    public DroppedItemEntity(ItemStack stack, Vector2 position, TileCollisionResolver collisionResolver)
    {
        if (stack.IsEmpty)
        {
            throw new ArgumentException("Dropped item stack must not be empty.", nameof(stack));
        }

        ArgumentNullException.ThrowIfNull(collisionResolver);
        Stack = stack;
        _standaloneCollisionResolver = collisionResolver;
        CollisionSettings = collisionResolver.Settings;
        Body = new PhysicsBody
        {
            Position = position,
            Size = new Vector2(10, 10),
            BodyType = PhysicsBodyType.Dynamic,
            CollisionLayer = PhysicsCollisionLayer.Item,
            GravityScale = EntityPhysicsRuntime.DroppedItemGravityScale,
            MaximumAbsoluteVelocity = new Vector2(float.PositiveInfinity, MaxFallSpeed)
        };
        Position = position;
    }

    public ItemStack Stack { get; private set; }

    public PhysicsBody Body { get; }

    TileCollisionSettings IEntityPhysicsParticipant.CollisionSettings => CollisionSettings;

    internal TileCollisionSettings CollisionSettings { get; }

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
        PreparePhysicsUpdate(deltaSeconds);
        GetStandalonePhysicsWorld().StepBody(
            world,
            Body,
            deltaSeconds,
            Span<PhysicsContact>.Empty);
        SynchronizePhysicsState();
    }

    internal void PreparePhysicsUpdate(float deltaSeconds)
    {
        Body.Velocity = new Vector2(
            MoveToward(Body.Velocity.X, 0, GroundDrag * deltaSeconds),
            Body.Velocity.Y);
    }

    void IEntityPhysicsParticipant.SynchronizePhysicsState()
    {
        SynchronizePhysicsState();
    }

    internal void SynchronizePhysicsState()
    {
        Position = Body.Position;
    }

    private PhysicsWorld GetStandalonePhysicsWorld()
    {
        return _standalonePhysicsWorld ??= new PhysicsWorld(
            _standaloneCollisionResolver,
            EntityPhysicsRuntime.CreateSettings(EntityPhysicsRuntime.DefaultMaximumBodies));
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
