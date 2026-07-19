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
}
