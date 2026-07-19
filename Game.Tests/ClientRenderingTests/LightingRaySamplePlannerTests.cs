using Game.Client.Rendering.Effects;
using Game.Client.Rendering.Lighting;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class LightingRaySamplePlannerTests
{
    [Fact]
    public void Build_ScalesPointShadowSamplesWithinProfileBudgets()
    {
        var high = PresentationQualityProfile.Create(
            PresentationQualityTier.High,
            new Rectangle(0, 0, 1280, 720));
        var hard = high with
        {
            Budget = high.Budget with { MaxPenumbraRadius = 0 }
        };

        var softPlan = LightingRaySamplePlanner.Build(high, high.Budget.MaxPointLights + 10);
        var hardPlan = LightingRaySamplePlanner.Build(hard, 2);

        Assert.Equal(3, softPlan.PointShadowSamples);
        Assert.InRange(softPlan.EndpointSpreadMaskPixels, 1, 2);
        Assert.Equal(
            (long)high.MaskPixelCount * high.Budget.MaxPointLights * softPlan.PointShadowSamples,
            softPlan.MaximumPointShadowRays);
        Assert.True(softPlan.WasBudgetClamped);
        Assert.Equal(1, hardPlan.PointShadowSamples);
        Assert.Equal(0, hardPlan.EndpointSpreadMaskPixels);
    }

    [Fact]
    public void Build_DisablesPointRaysWhenProfileDoesNotCastPointShadows()
    {
        var medium = PresentationQualityProfile.Create(
            PresentationQualityTier.Medium,
            new Rectangle(0, 0, 1280, 720));

        var plan = LightingRaySamplePlanner.Build(medium, 4);

        Assert.Equal(0, plan.PointShadowSamples);
        Assert.Equal(0, plan.MaximumPointShadowRays);
    }

    [Fact]
    public void Build_MediumPointShadowOverrideKeepsSingleRayBudget()
    {
        var medium = PresentationQualityProfile.Create(
            PresentationQualityTier.Medium,
            new Rectangle(0, 0, 1280, 720));
        medium = medium with
        {
            CastPointLightShadows = true,
            Budget = medium.Budget with
            {
                MaxRayStepsPerSample = 20,
                MaxPenumbraRadius = 2
            }
        };

        var plan = LightingRaySamplePlanner.Build(medium, 6);

        Assert.Equal(1, plan.PointShadowSamples);
        Assert.Equal(0, plan.EndpointSpreadMaskPixels);
        Assert.Equal((long)medium.MaskPixelCount * 6, plan.MaximumPointShadowRays);
    }

    [Fact]
    public void Build_HasZeroSteadyStateAllocation()
    {
        var profile = PresentationQualityProfile.Create(
            PresentationQualityTier.High,
            new Rectangle(0, 0, 1920, 1080));
        _ = LightingRaySamplePlanner.Build(profile, 12);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = LightingRaySamplePlanner.Build(profile, 12);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
