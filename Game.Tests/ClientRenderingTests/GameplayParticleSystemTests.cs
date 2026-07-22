using Game.Client.Rendering;
using Game.Client.Rendering.Effects;
using Game.Core.Feedback;
using Game.Core.Runtime;
using Game.Core.Weather;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class GameplayParticleSystemTests
{
    [Fact]
    public void EmitAndUpdate_RemainWithinFixedCapacityWithoutSteadyStateAllocation()
    {
        var particles = new GameplayParticleSystem();
        var cue = new GameplayFeedbackCue(
            GameplayFeedbackCueKind.EntityDeath,
            new System.Numerics.Vector2(42f, 18f));
        for (var index = 0; index < 100; index++)
        {
            particles.Emit(cue, quality: 3);
            particles.Update(1f / 60f);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++)
        {
            particles.Emit(cue, quality: 3);
            particles.Update(1f / 60f);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.InRange(particles.ActiveCount, 0, particles.Capacity);
    }

    [Fact]
    public void EmitAmbient_IsDeterministicForSameSnapshotAndTickSequence()
    {
        var living = default(LivingWorldFrameSnapshot) with
        {
            BiomeId = "forest",
            Weather = WeatherKind.Storm,
            WeatherIntensity = 0.8f,
            Wind = 0.35f
        };
        var first = new GameplayParticleSystem();
        var second = new GameplayParticleSystem();
        for (var tick = 1L; tick <= 120; tick++)
        {
            var frame = new AmbientParticleFrame(
                living,
                new Rectangle(-320, -180, 640, 360),
                tick,
                Quality: 3);
            first.EmitAmbient(frame);
            second.EmitAmbient(frame);
            first.Update(1f / 60f);
            second.Update(1f / 60f);
        }

        Assert.Equal(first.ActiveCount, second.ActiveCount);
        Assert.Equal(first.ComputeStateHash(), second.ComputeStateHash());
    }

    [Fact]
    public void AmbientSnowfall_IsDeterministicBoundedAndSuppressedUnderground()
    {
        var living = default(LivingWorldFrameSnapshot) with
        {
            BiomeId = "frostwood",
            Weather = WeatherKind.Snow,
            WeatherIntensity = 1f,
            AllowsFrozenPrecipitation = true,
            Wind = -0.6f,
            Presentation = default(LivingWorldPresentationFrameSnapshot) with
            {
                AmbientParticleSpriteId = "effects/weather/snow_flurry"
            }
        };
        var first = new GameplayParticleSystem();
        var second = new GameplayParticleSystem();
        var underground = new GameplayParticleSystem();
        var visible = new Rectangle(-960, -540, 1920, 1080);
        for (var tick = 1L; tick <= 180; tick++)
        {
            first.EmitAmbient(new AmbientParticleFrame(living, visible, tick, Quality: 3));
            second.EmitAmbient(new AmbientParticleFrame(living, visible, tick, Quality: 3));
            underground.EmitAmbient(new AmbientParticleFrame(
                living with { IsUnderground = true },
                visible,
                tick,
                Quality: 3));
            first.Update(1f / 60f);
            second.Update(1f / 60f);
            underground.Update(1f / 60f);
        }

        Assert.InRange(first.ActiveCount, 1, ParticleQualityBudget.ForQuality(3).MaximumParticles);
        Assert.Equal(first.ActiveCount, second.ActiveCount);
        Assert.Equal(first.ComputeStateHash(), second.ComputeStateHash());
        Assert.Equal(0, underground.ActiveCount);
    }

    [Fact]
    public void HighQualityBurstPopulationUsesExpandedCapacityAndLowQualityTrimsImmediately()
    {
        var particles = new GameplayParticleSystem();
        var cue = new GameplayFeedbackCue(
            GameplayFeedbackCueKind.WorldEventActivated,
            new System.Numerics.Vector2(80f, -20f));

        for (var index = 0; index < 100; index++)
        {
            particles.Emit(cue, quality: 3);
        }

        Assert.Equal(ParticleQualityBudget.AbsoluteMaximumParticles, particles.Capacity);
        Assert.Equal(ParticleQualityBudget.AbsoluteMaximumParticles, particles.ActiveCount);

        particles.Emit(cue, quality: 1);

        Assert.InRange(
            particles.ActiveCount,
            1,
            ParticleQualityBudget.ForQuality(1).MaximumParticles);
    }

    [Fact]
    public void AmbientForestCanopy_EmitsBoundedDeterministicFallingLeaves()
    {
        var world = new Game.Core.World.World(
            128,
            96,
            new Game.Core.World.WorldMetadata("leaf-particles", 7, DateTimeOffset.UnixEpoch));
        for (var x = 18; x <= 30; x++)
        {
            world.SetTile(
                x,
                20,
                Game.Core.World.TileInstance.FromTileId(
                    Game.Core.World.KnownTileIds.Leaves,
                    Game.Core.World.TileFlags.IsNatural,
                    isSolid: false));
        }

        var living = default(LivingWorldFrameSnapshot) with
        {
            BiomeId = "forest",
            SurfaceTileY = 32,
            Wind = 0.55f,
            VegetationDensityMultiplier = 3f
        };
        var first = new GameplayParticleSystem();
        var second = new GameplayParticleSystem();
        for (var tick = 1L; tick <= 180; tick++)
        {
            var frame = new AmbientParticleFrame(
                living,
                new Rectangle(0, 0, 64 * Game.Core.GameConstants.TileSize, 48 * Game.Core.GameConstants.TileSize),
                tick,
                Quality: 3,
                world);
            first.EmitAmbient(frame);
            second.EmitAmbient(frame);
            first.Update(1f / 60f);
            second.Update(1f / 60f);
        }

        Assert.InRange(first.ActiveCount, 1, ParticleQualityBudget.ForQuality(3).MaximumParticles);
        Assert.Equal(first.ActiveCount, second.ActiveCount);
        Assert.Equal(first.ComputeStateHash(), second.ComputeStateHash());
    }

    [Fact]
    public void AmbientForestCanopy_RemainsAllocationFreeAfterWarmup()
    {
        var world = new Game.Core.World.World(
            128,
            96,
            new Game.Core.World.WorldMetadata("leaf-particle-allocation", 9, DateTimeOffset.UnixEpoch));
        for (var x = 16; x <= 32; x++)
        {
            world.SetTile(
                x,
                20,
                Game.Core.World.TileInstance.FromTileId(
                    Game.Core.World.KnownTileIds.Leaves,
                    Game.Core.World.TileFlags.IsNatural,
                    isSolid: false));
        }

        var living = default(LivingWorldFrameSnapshot) with
        {
            BiomeId = "forest",
            SurfaceTileY = 32,
            Wind = -0.4f,
            VegetationDensityMultiplier = 2f
        };
        var particles = new GameplayParticleSystem();
        var visible = new Rectangle(
            0,
            0,
            64 * Game.Core.GameConstants.TileSize,
            48 * Game.Core.GameConstants.TileSize);
        for (var tick = 1L; tick <= 180; tick++)
        {
            particles.EmitAmbient(new AmbientParticleFrame(living, visible, tick, Quality: 3, world));
            particles.Update(1f / 60f);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 181L; tick <= 1_180; tick++)
        {
            particles.EmitAmbient(new AmbientParticleFrame(living, visible, tick, Quality: 3, world));
            particles.Update(1f / 60f);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void TileBreakFeedback_UsesCorePhysicsAgainstWorldTiles()
    {
        var world = new Game.Core.World.World(
            16,
            16,
            new Game.Core.World.WorldMetadata("physical-debris", 41, DateTimeOffset.UnixEpoch));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            world.SetTile(x, 8, Game.Core.World.TileInstance.FromTileId(1));
        }

        var particles = new GameplayParticleSystem();
        particles.Emit(
            new GameplayFeedbackCue(
                GameplayFeedbackCueKind.TileBroken,
                new System.Numerics.Vector2(4f * Game.Core.GameConstants.TileSize, 7f * Game.Core.GameConstants.TileSize)),
            quality: 3);

        Assert.InRange(particles.PhysicalParticleCount, 1, particles.Capacity);
        for (var tick = 0; tick < 12; tick++)
        {
            particles.Update(1f / 60f, world);
        }

        Assert.True(particles.LastPhysicsStep.UpdatedParticles > 0);
        Assert.True(particles.LastPhysicsStep.TileTests > 0);
        Assert.False(particles.LastPhysicsStep.UpdateBudgetExhausted);
        Assert.False(particles.LastPhysicsStep.CollisionBudgetExhausted);
    }

    [Fact]
    public void PhysicalFeedbackPath_RemainsAllocationFreeAfterWarmup()
    {
        var world = new Game.Core.World.World(
            16,
            16,
            new Game.Core.World.WorldMetadata("physical-debris-allocation", 43, DateTimeOffset.UnixEpoch));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            world.SetTile(x, 8, Game.Core.World.TileInstance.FromTileId(1));
        }

        var particles = new GameplayParticleSystem();
        var cue = new GameplayFeedbackCue(
            GameplayFeedbackCueKind.ProjectileHit,
            new System.Numerics.Vector2(5f * Game.Core.GameConstants.TileSize, 7f * Game.Core.GameConstants.TileSize));
        for (var tick = 0; tick < 120; tick++)
        {
            particles.Emit(cue, quality: 3);
            particles.Update(1f / 60f, world);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < 1_000; tick++)
        {
            particles.Emit(cue, quality: 3);
            particles.Update(1f / 60f, world);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.InRange(particles.ActiveCount, 0, particles.Capacity);
    }

    [Fact]
    public void PhysicalFeedback_IgnoresPartialTilesUntilParticleShapesAreSupported()
    {
        var emptyWorld = new Game.Core.World.World(
            16,
            16,
            new Game.Core.World.WorldMetadata("physical-debris-empty", 47, DateTimeOffset.UnixEpoch));
        var halfBlockWorld = new Game.Core.World.World(
            16,
            16,
            new Game.Core.World.WorldMetadata("physical-debris-half-block", 47, DateTimeOffset.UnixEpoch));
        var solidWorld = new Game.Core.World.World(
            16,
            16,
            new Game.Core.World.WorldMetadata("physical-debris-solid", 47, DateTimeOffset.UnixEpoch));
        for (var x = 0; x < emptyWorld.WidthTiles; x++)
        {
            halfBlockWorld.SetTile(
                x,
                8,
                Game.Core.World.TileInstance.FromTileId(1, Game.Core.World.TileFlags.HalfBlock));
            solidWorld.SetTile(x, 8, Game.Core.World.TileInstance.FromTileId(1));
        }

        var cue = new GameplayFeedbackCue(
            GameplayFeedbackCueKind.TileBroken,
            new System.Numerics.Vector2(4f * Game.Core.GameConstants.TileSize, 7f * Game.Core.GameConstants.TileSize));
        var emptyParticles = new GameplayParticleSystem();
        var halfBlockParticles = new GameplayParticleSystem();
        var solidParticles = new GameplayParticleSystem();
        emptyParticles.Emit(cue, quality: 3);
        halfBlockParticles.Emit(cue, quality: 3);
        solidParticles.Emit(cue, quality: 3);

        for (var tick = 0; tick < 30; tick++)
        {
            emptyParticles.Update(1f / 60f, emptyWorld);
            halfBlockParticles.Update(1f / 60f, halfBlockWorld);
            solidParticles.Update(1f / 60f, solidWorld);
        }

        Assert.Equal(emptyParticles.ComputeStateHash(), halfBlockParticles.ComputeStateHash());
        Assert.NotEqual(emptyParticles.ComputeStateHash(), solidParticles.ComputeStateHash());
    }
}
