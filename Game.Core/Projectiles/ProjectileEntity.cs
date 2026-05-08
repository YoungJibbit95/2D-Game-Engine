using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Projectiles;

public sealed class ProjectileEntity : Entity
{
    private const float GravityPixelsPerSecondSquared = 980f;

    private float _age;

    public ProjectileEntity(
        string projectileId,
        Vector2 position,
        Vector2 velocity,
        int damage,
        float gravity,
        int pierce,
        float lifetime,
        int? ownerEntityId = null,
        float age = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectileId);

        ProjectileId = projectileId;
        Position = position;
        Velocity = velocity;
        Damage = damage;
        Gravity = gravity;
        Pierce = pierce;
        Lifetime = lifetime;
        OwnerEntityId = ownerEntityId;
        _age = Math.Max(0, age);
    }

    public string ProjectileId { get; }

    public Vector2 Velocity { get; set; }

    public int Damage { get; }

    public float Gravity { get; }

    public int Pierce { get; private set; }

    public float Lifetime { get; }

    public int? OwnerEntityId { get; }

    public float Age => _age;

    public DamageInfo DamageInfo => new(Damage, DamageType.Ranged, OwnerEntityId, Vector2.Normalize(Velocity == Vector2.Zero ? Vector2.UnitX : Velocity), 1f);

    public override RectI Bounds => new(
        (int)MathF.Floor(Position.X),
        (int)MathF.Floor(Position.Y),
        4,
        4);

    public override void Update(GameWorld world, float deltaSeconds)
    {
        _age += deltaSeconds;
        if (_age >= Lifetime)
        {
            IsActive = false;
            return;
        }

        Velocity += new Vector2(0, Gravity * GravityPixelsPerSecondSquared * deltaSeconds);
        Position += Velocity * deltaSeconds;

        if (OverlapsSolidTile(world))
        {
            IsActive = false;
        }
    }

    public void RegisterHit()
    {
        if (Pierce <= 0)
        {
            IsActive = false;
            return;
        }

        Pierce--;
    }

    private bool OverlapsSolidTile(GameWorld world)
    {
        var bounds = Bounds;
        var min = CoordinateUtils.WorldToTile(bounds.Left, bounds.Top);
        var max = CoordinateUtils.WorldToTile(bounds.Right - 1, bounds.Bottom - 1);

        for (var y = min.Y; y <= max.Y; y++)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                if (world.IsSolid(x, y))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
