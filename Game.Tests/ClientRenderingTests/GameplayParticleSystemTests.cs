using Game.Client.Rendering;
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
}
