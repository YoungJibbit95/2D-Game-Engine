using Game.Client.Rendering.Effects;
using Game.Core.Settings;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class PresentationPassPlannerTests
{
    [Fact]
    public void Build_HighQualityPlansBoundedLightingBloomReflectionAndAtmospherePasses()
    {
        var profile = PresentationQualityProfile.Create(
            PresentationQualityTier.High,
            new Rectangle(0, 0, 1920, 1080));
        var passes = new PresentationPassDescriptor[PresentationPassPlanner.MaximumPassCount];

        var plan = PresentationPassPlanner.Build(
            profile,
            new PresentationPassRequest(
                PresentationFeature.Lighting |
                PresentationFeature.AmbientOcclusion |
                PresentationFeature.Bloom |
                PresentationFeature.Reflections |
                PresentationFeature.Atmosphere,
                RequestedPointLights: 100,
                RequestedReflectionSurfaces: 100,
                BloomStrength: 0.8f,
                ReflectionStrength: 0.5f),
            passes);

        Assert.InRange(profile.MaskPixelCount, 1, profile.Budget.MaxMaskPixels);
        Assert.Equal(profile.Budget.MaxPointLights, plan.PointLights);
        Assert.Equal(profile.Budget.MaxReflectionSurfaces, plan.ReflectionSurfaces);
        Assert.True(plan.WasBudgetClamped);
        Assert.True(plan.EstimatedWorkItems > 0);
        Assert.True(Contains(passes, plan.PassCount, PresentationPassKind.CpuTileRayCastSunShadow));
        Assert.True(Contains(passes, plan.PassCount, PresentationPassKind.PenumbraHorizontal));
        Assert.True(Contains(passes, plan.PassCount, PresentationPassKind.EmissiveBloomVertical));
        Assert.True(Contains(passes, plan.PassCount, PresentationPassKind.WaterReflection));
        Assert.True(Contains(passes, plan.PassCount, PresentationPassKind.AtmosphereComposite));
    }

    [Fact]
    public void Build_LowQualityUsesDeterministicFallbackWithoutBlurPasses()
    {
        var profile = PresentationQualityProfile.Create(
            PresentationQualityTier.Low,
            new Rectangle(0, 0, 640, 360));
        var passes = new PresentationPassDescriptor[PresentationPassPlanner.MaximumPassCount];

        var plan = PresentationPassPlanner.Build(
            profile,
            new PresentationPassRequest(
                PresentationFeature.Lighting | PresentationFeature.AmbientOcclusion | PresentationFeature.Bloom,
                RequestedPointLights: 8,
                RequestedReflectionSurfaces: 0,
                BloomStrength: 1f,
                ReflectionStrength: 0f),
            passes);

        Assert.True(Contains(passes, plan.PassCount, PresentationPassKind.LowQualityLightingFallback));
        Assert.False(Contains(passes, plan.PassCount, PresentationPassKind.PenumbraHorizontal));
        Assert.False(Contains(passes, plan.PassCount, PresentationPassKind.EmissiveBloomHorizontal));
        Assert.InRange(plan.PointLights, 0, profile.Budget.MaxPointLights);
    }

    [Fact]
    public void SettingsAdapter_DisablesPassesAndClampsNumericBudgets()
    {
        var settings = new RenderingSettings
        {
            LightingQuality = 3,
            ShadowQuality = 0,
            ReflectionQuality = 3,
            UiEffectQuality = 1,
            SoftShadows = false,
            SunLightShafts = false,
            ScreenSpaceReflections = false,
            AmbientOcclusion = false,
            TorchBloomStrength = 0f,
            RaymarchStepBudget = 128,
            BlurRadiusPixels = 24
        };

        var configuration = PresentationSettingsAdapter.Create(
            settings,
            new Rectangle(0, 0, 1280, 720));

        Assert.False(configuration.Lighting.CastSunShadows);
        Assert.False(configuration.Lighting.CastPointLightShadows);
        Assert.Equal(0, configuration.Lighting.AmbientOcclusionRadius);
        Assert.Equal(0, configuration.Lighting.Budget.MaxPenumbraRadius);
        Assert.False(configuration.Lighting.EnableBloom);
        Assert.Equal(PresentationQualityTier.Disabled, configuration.Reflections.Tier);
        Assert.Equal(PresentationQualityTier.Low, configuration.UiEffects);
        Assert.InRange(
            configuration.Lighting.Budget.MaxRayStepsPerSample,
            0,
            PresentationBudget.ForTier(PresentationQualityTier.High).MaxRayStepsPerSample);
    }

    [Fact]
    public void Build_ReusesCallerBufferWithoutSteadyStateAllocation()
    {
        var profile = PresentationQualityProfile.Create(
            PresentationQualityTier.High,
            new Rectangle(0, 0, 1280, 720));
        var request = new PresentationPassRequest(
            PresentationFeature.Lighting | PresentationFeature.AmbientOcclusion | PresentationFeature.Bloom,
            RequestedPointLights: 6,
            RequestedReflectionSurfaces: 0,
            BloomStrength: 0.6f,
            ReflectionStrength: 0f);
        var passes = new PresentationPassDescriptor[PresentationPassPlanner.MaximumPassCount];
        _ = PresentationPassPlanner.Build(profile, request, passes);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = PresentationPassPlanner.Build(profile, request, passes);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void QualityProfile_BoundsExtremeViewportAndDisablesOverBudgetSceneCapture()
    {
        var extreme = PresentationQualityProfile.Create(
            PresentationQualityTier.High,
            new Rectangle(0, 0, int.MaxValue, int.MaxValue));
        var lowFourK = PresentationQualityProfile.Create(
            PresentationQualityTier.Low,
            new Rectangle(0, 0, 3840, 2160));

        Assert.InRange(extreme.MaskPixelCount, 1, extreme.Budget.MaxMaskPixels);
        Assert.False(extreme.EnableReflections);
        Assert.False(lowFourK.EnableReflections);
        Assert.True((long)3840 * 2160 > lowFourK.Budget.MaxSceneCapturePixels);
    }

    private static bool Contains(
        PresentationPassDescriptor[] passes,
        int count,
        PresentationPassKind kind)
    {
        for (var index = 0; index < count; index++)
        {
            if (passes[index].Kind == kind)
            {
                return true;
            }
        }

        return false;
    }
}
