using Game.Client.Rendering.Effects;
using Game.Client.Rendering.Lighting;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Inventory;
using Game.Core.Runtime;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class DynamicVisibleLightCollectorTests
{
    [Fact]
    public void CollectEntityLights_TracksMagicProjectilePhysicsSnapshot()
    {
        var profile = PresentationQualityProfile.Create(
            PresentationQualityTier.High,
            new Rectangle(0, 0, 320, 180));
        var destination = new ScreenSpaceLight[profile.Budget.MaxPointLights];
        var entities = new[]
        {
            new EntityFrameSnapshot(
                17,
                EntityFrameKind.Projectile,
                "arcane_bolt",
                new System.Numerics.Vector2(80f, 48f),
                new System.Numerics.Vector2(360f, 0f),
                new RectI(80, 48, 8, 8),
                true,
                0,
                0,
                ItemStack.Empty,
                false,
                DamageType.Magic)
            {
                Faction = EntityFaction.Friendly
            }
        };

        var telemetry = VisibleLightCollector.CollectEntityLights(
            entities,
            new Rectangle(0, 0, 320, 180),
            profile,
            destination);

        Assert.Equal(1, telemetry.LightsCollected);
        Assert.Equal(new Vector2(84f, 52f), destination[0].WorldPosition);
        Assert.True(destination[0].RadiusPixels > 112f);
        Assert.True(destination[0].Color.B > destination[0].Color.R);
        Assert.True(destination[0].CastsShadows);
    }

    [Fact]
    public void CollectEntityLights_IgnoresInactiveNonEmissiveAndOffscreenEntities()
    {
        var profile = PresentationQualityProfile.Create(
            PresentationQualityTier.High,
            new Rectangle(0, 0, 320, 180));
        var destination = new ScreenSpaceLight[profile.Budget.MaxPointLights];
        var entities = new[]
        {
            CreateProjectile(1, DamageType.Ranged, new RectI(20, 20, 8, 8), true),
            CreateProjectile(2, DamageType.Fire, new RectI(20, 20, 8, 8), false),
            CreateProjectile(3, DamageType.Magic, new RectI(500, 20, 8, 8), true)
        };

        var telemetry = VisibleLightCollector.CollectEntityLights(
            entities,
            new Rectangle(0, 0, 320, 180),
            profile,
            destination);

        Assert.Equal(0, telemetry.LightsCollected);
        Assert.Equal(3, telemetry.EntitiesInspected);
    }

    [Fact]
    public void CollectEntityLights_IsAllocationFreeAfterSnapshotCreation()
    {
        var profile = PresentationQualityProfile.Create(
            PresentationQualityTier.High,
            new Rectangle(0, 0, 320, 180));
        var destination = new ScreenSpaceLight[profile.Budget.MaxPointLights];
        var entities = new[]
        {
            CreateProjectile(8, DamageType.Fire, new RectI(40, 40, 8, 8), true)
        };
        _ = VisibleLightCollector.CollectEntityLights(
            entities,
            new Rectangle(0, 0, 320, 180),
            profile,
            destination);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            _ = VisibleLightCollector.CollectEntityLights(
                entities,
                new Rectangle(0, 0, 320, 180),
                profile,
                destination);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static EntityFrameSnapshot CreateProjectile(
        int id,
        DamageType damageType,
        RectI bounds,
        bool isActive)
    {
        return new EntityFrameSnapshot(
            id,
            EntityFrameKind.Projectile,
            "test_projectile",
            new System.Numerics.Vector2(bounds.X, bounds.Y),
            new System.Numerics.Vector2(120f, 0f),
            bounds,
            isActive,
            0,
            0,
            ItemStack.Empty,
            false,
            damageType);
    }
}

