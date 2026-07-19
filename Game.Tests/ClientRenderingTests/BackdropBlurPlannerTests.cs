using Game.Client.Rendering.Effects;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class BackdropBlurPlannerTests
{
    [Theory]
    [InlineData(PresentationQualityTier.Low, 4)]
    [InlineData(PresentationQualityTier.Medium, 3)]
    [InlineData(PresentationQualityTier.High, 2)]
    public void Build_UsesBoundedQualitySpecificDownsample(
        PresentationQualityTier quality,
        int expectedDivisor)
    {
        var plan = BackdropBlurPlanner.Build(quality, new Rectangle(0, 0, 2560, 1440), 8);

        Assert.True(plan.IsEnabled);
        Assert.Equal(expectedDivisor, plan.DownsampleDivisor);
        Assert.Equal(2560 / expectedDivisor + (2560 % expectedDivisor == 0 ? 0 : 1), plan.TargetSize.X);
        Assert.Equal(1440 / expectedDivisor + (1440 % expectedDivisor == 0 ? 0 : 1), plan.TargetSize.Y);
        Assert.True(plan.PlannedSampleCount < plan.LegacySampleCount);
        Assert.True(plan.SavedSampleCount > 0);
    }

    [Fact]
    public void Build_HighQualityUsesSecondIterationForWideBlur()
    {
        var plan = BackdropBlurPlanner.Build(
            PresentationQualityTier.High,
            new Rectangle(0, 0, 3840, 2160),
            12);

        Assert.Equal(2, plan.Iterations);
        Assert.InRange(plan.RadiusPerIteration, 1, 3);
        Assert.True(plan.PlannedSampleCount * 2 < plan.LegacySampleCount);
    }

    [Theory]
    [InlineData(PresentationQualityTier.Disabled, 1920, 1080, 8)]
    [InlineData(PresentationQualityTier.High, 0, 1080, 8)]
    [InlineData(PresentationQualityTier.High, 1920, 1080, 0)]
    public void Build_DisablesInvalidOrUnrequestedWork(
        PresentationQualityTier quality,
        int width,
        int height,
        int radius)
    {
        Assert.False(BackdropBlurPlanner.Build(
            quality,
            new Rectangle(0, 0, width, height),
            radius).IsEnabled);
    }

    [Fact]
    public void Build_ReusesValueStateWithoutSteadyStateAllocation()
    {
        _ = BackdropBlurPlanner.Build(
            PresentationQualityTier.High,
            new Rectangle(0, 0, 1920, 1080),
            8);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = BackdropBlurPlanner.Build(
                PresentationQualityTier.High,
                new Rectangle(0, 0, 1920, 1080),
                8);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
