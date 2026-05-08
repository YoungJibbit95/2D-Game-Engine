using Game.Core.Projectiles;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.ProjectileTests;

public sealed class ProjectileEntityTests
{
    [Fact]
    public void Factory_CreatesProjectileVelocityFromDirectionAndSpeed()
    {
        var definition = CreateDefinition();

        var projectile = new ProjectileFactory().Create(definition, Vector2.Zero, Vector2.UnitY, ownerEntityId: 7);

        Assert.Equal(new Vector2(0, 320), projectile.Velocity);
        Assert.Equal(7, projectile.OwnerEntityId);
    }

    [Fact]
    public void Update_DeactivatesProjectileAfterLifetime()
    {
        var projectile = new ProjectileEntity("arrow", Vector2.Zero, Vector2.Zero, 1, 0, 0, 0.5f);
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));

        projectile.Update(world, 0.6f);

        Assert.False(projectile.IsActive);
    }

    [Fact]
    public void Update_DeactivatesProjectileOnSolidTileCollision()
    {
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(1, 0, KnownTileIds.Stone);
        var projectile = new ProjectileEntity("arrow", new Vector2(0, 0), new Vector2(40, 0), 1, 0, 0, 5f);

        projectile.Update(world, 0.5f);

        Assert.False(projectile.IsActive);
    }

    private static ProjectileDefinition CreateDefinition()
    {
        return new ProjectileDefinition
        {
            Id = "arrow",
            TexturePath = "projectiles/arrow",
            Speed = 320,
            Damage = 5,
            Lifetime = 5
        };
    }
}
