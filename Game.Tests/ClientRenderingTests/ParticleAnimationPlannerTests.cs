using Game.Client.Rendering.Effects;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class ParticleAnimationPlannerTests
{
    [Fact]
    public void QualityBudgetsScaleMonotonicallyAndRemainAbsolutelyBounded()
    {
        var low = ParticleQualityBudget.ForQuality(1);
        var medium = ParticleQualityBudget.ForQuality(2);
        var high = ParticleQualityBudget.ForQuality(3);

        Assert.True(low.MaximumParticles < medium.MaximumParticles);
        Assert.True(medium.MaximumParticles < high.MaximumParticles);
        Assert.Equal(ParticleQualityBudget.AbsoluteMaximumParticles, high.MaximumParticles);
        Assert.True(low.MaximumDrawPrimitives < medium.MaximumDrawPrimitives);
        Assert.True(medium.MaximumDrawPrimitives < high.MaximumDrawPrimitives);
        Assert.Equal(default, ParticleQualityBudget.ForQuality(0));
    }

    [Fact]
    public void Sample_ProvidesSmoothFadePulseAndSwayWithinFiniteBounds()
    {
        var start = ParticleAnimationPlanner.Sample(0f, 1f, 0.4f, 0.3f, 5f);
        var middle = ParticleAnimationPlanner.Sample(0.5f, 1f, 0.4f, 0.3f, 5f);
        var end = ParticleAnimationPlanner.Sample(1f, 1f, 0.4f, 0.3f, 5f);

        Assert.Equal(0f, start.Opacity);
        Assert.True(middle.Opacity > 0f);
        Assert.Equal(0f, end.Opacity);
        Assert.InRange(middle.Scale, 0.7f, 1.3f);
        Assert.InRange(middle.Sway, -5f, 5f);
    }

    [Fact]
    public void Sample_IsAllocationFreeForLargeAnimationPopulation()
    {
        _ = ParticleAnimationPlanner.Sample(0.25f, 1f, 0.4f, 0.3f, 5f);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0f;

        for (var index = 0; index < 100_000; index++)
        {
            var sample = ParticleAnimationPlanner.Sample(
                index % 60 / 60f,
                1f,
                index * 0.01f,
                0.3f,
                5f);
            checksum += sample.Scale;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(checksum > 0f);
    }
}
