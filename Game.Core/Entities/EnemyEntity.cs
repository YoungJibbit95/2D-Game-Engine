using Game.Core.Combat;
using Game.Core.Entities.AI;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities;

public sealed class EnemyEntity : Entity
{
    private const float Gravity = 1050f;
    private const float MaxFallSpeed = 620f;

    private readonly TileCollisionResolver _collisionResolver;
    private readonly IAiBehavior _aiBehavior;

    public EnemyEntity(
        string definitionId,
        Vector2 spawnPosition,
        Vector2 size,
        HealthComponent health,
        IAiBehavior aiBehavior,
        TileCollisionResolver collisionResolver,
        string? lootTableId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);

        DefinitionId = definitionId;
        Health = health;
        LootTableId = lootTableId;
        _aiBehavior = aiBehavior;
        _collisionResolver = collisionResolver;
        Body = new PhysicsBody
        {
            Position = spawnPosition,
            Size = size
        };
        Position = spawnPosition;
    }

    public string DefinitionId { get; }

    public string? LootTableId { get; }

    public PhysicsBody Body { get; }

    public HealthComponent Health { get; }

    public override RectI Bounds => Body.Bounds;

    public bool ApplyDamage(DamageInfo damage)
    {
        return Health.ApplyDamage(damage);
    }

    public override void Update(GameWorld world, float deltaSeconds)
    {
        Health.Update(deltaSeconds);
        if (Health.IsDead)
        {
            IsActive = false;
            return;
        }

        _aiBehavior.Update(this, world, deltaSeconds);
        Body.Velocity = new Vector2(Body.Velocity.X, Math.Min(Body.Velocity.Y + Gravity * deltaSeconds, MaxFallSpeed));
        _collisionResolver.Move(world, Body, deltaSeconds);
        Position = Body.Position;
    }
}
