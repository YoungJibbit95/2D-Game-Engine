using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public readonly record struct ParallaxViewportLayout(
    float Scale,
    int Width,
    int Height,
    float Horizon,
    int Y);

public static class ParallaxViewportLayoutPlanner
{
    private const float MinimumSurfaceHorizonRatio = 0.38f;
    private const float MaximumSurfaceHorizonRatio = 0.96f;
    private const float VerticalBleedPixels = 48f;
    private const float FixedVerticalTravelReservePixels = 96f;
    private const float FarHorizonRatio = 0.38f;
    private const float MidHorizonRatio = 0.44f;
    private const float NearHorizonRatio = 0.5f;
    private const int AuthoredReferenceViewportHeight = 1080;
    private const float MinimumAuthoredScale = 0.25f;
    private const float MaximumAuthoredScale = 4f;

    public static ParallaxViewportLayout Build(
        int sourceWidth,
        int sourceHeight,
        in Rectangle viewport,
        float surfaceHorizon,
        float undergroundBlend,
        int verticalOffset,
        float verticalScroll,
        float scaleMultiplier,
        bool coverViewport,
        ParallaxProjectionMode projectionMode,
        ParallaxDepthPlane depthPlane = ParallaxDepthPlane.Unspecified)
    {
        if (sourceWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth));
        }

        if (sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceHeight));
        }

        var clampedSurfaceHorizon = Math.Clamp(
            surfaceHorizon,
            viewport.Top + viewport.Height * MinimumSurfaceHorizonRatio,
            viewport.Top + viewport.Height * MaximumSurfaceHorizonRatio);
        var undergroundHorizon = viewport.Bottom - Math.Clamp(viewport.Height * 0.067f, 40f, 96f);
        var horizon = MathHelper.Lerp(
            clampedSurfaceHorizon,
            undergroundHorizon,
            Math.Clamp(undergroundBlend, 0f, 1f));
        if (projectionMode == ParallaxProjectionMode.DistantHorizonBand)
        {
            // Distance scale is derived from viewport density and the authored plane only.
            // Camera translation never participates, so jumps can move a layer by its tiny
            // vertical parallax without pumping its width or height.
            var bandScale = ResolveAuthoredPixelScale(viewport.Height, scaleMultiplier);
            var bandWidth = Math.Max(8, SaturatingRound(sourceWidth * bandScale));
            var bandHeight = Math.Max(8, SaturatingRound(sourceHeight * bandScale));
            var maximumScroll = Math.Clamp(viewport.Height * 0.04f, 24f, 96f);
            var boundedScroll = Math.Clamp(verticalScroll, -maximumScroll, maximumScroll);
            var distantHorizon = Math.Clamp(
                surfaceHorizon,
                viewport.Top + viewport.Height * MinimumSurfaceHorizonRatio,
                viewport.Top + viewport.Height * MaximumSurfaceHorizonRatio);
            var bandY = SaturatingRound(distantHorizon - bandHeight + verticalOffset - boundedScroll);
            return new ParallaxViewportLayout(bandScale, bandWidth, bandHeight, distantHorizon, bandY);
        }

        var resolutionScale = Math.Clamp(viewport.Height / 540f, 1f, 4f) * scaleMultiplier;
        var verticalMargin = VerticalBleedPixels + FixedVerticalTravelReservePixels + Math.Abs(verticalOffset);
        var coverageHeight = coverViewport
            ? Math.Max(
                sourceHeight * resolutionScale,
                viewport.Height + verticalMargin * 2f)
            : Math.Max(
                sourceHeight * resolutionScale,
                viewport.Height + verticalMargin * 2f);
        var scale = Math.Clamp(coverageHeight / sourceHeight, 0.5f, 16f);
        var width = Math.Max(8, SaturatingRound(sourceWidth * scale));
        var height = Math.Max(8, SaturatingRound(sourceHeight * scale));
        var y = coverViewport
            ? SaturatingRound(viewport.Top - VerticalBleedPixels + verticalOffset - verticalScroll)
            : SaturatingRound(horizon - height + verticalOffset - verticalScroll);
        y = Math.Min(y, viewport.Top - 1);
        return new ParallaxViewportLayout(scale, width, height, horizon, y);
    }

    public static float ResolveDistantSurfaceHorizon(
        in Rectangle viewport,
        float terrainEnvelopeDepthPixels,
        ParallaxDepthPlane depthPlane = ParallaxDepthPlane.Far)
    {
        var horizonRatio = depthPlane switch
        {
            ParallaxDepthPlane.Mid => MidHorizonRatio,
            ParallaxDepthPlane.Near => NearHorizonRatio,
            _ => FarHorizonRatio
        };
        var depthInfluence = depthPlane switch
        {
            ParallaxDepthPlane.Mid => 0.04f,
            ParallaxDepthPlane.Near => 0.06f,
            _ => 0.025f
        };
        var maximumAdjustmentRatio = depthPlane switch
        {
            ParallaxDepthPlane.Mid => 0.02f,
            ParallaxDepthPlane.Near => 0.025f,
            _ => 0.015f
        };
        var baseline = viewport.Top + viewport.Height * horizonRatio;
        var maximumAdjustment = viewport.Height * maximumAdjustmentRatio;
        var adjustment = Math.Clamp(
            Math.Max(0f, terrainEnvelopeDepthPixels) * depthInfluence,
            0f,
            maximumAdjustment);
        return baseline + adjustment;
    }

    public static float ResolveAuthoredPixelScale(int viewportHeight, float scaleMultiplier)
    {
        if (!float.IsFinite(scaleMultiplier))
        {
            return 1;
        }

        var densityScale = Math.Max(1f, viewportHeight / (float)AuthoredReferenceViewportHeight);
        return Math.Clamp(densityScale * scaleMultiplier, MinimumAuthoredScale, MaximumAuthoredScale);
    }

    private static int SaturatingRound(float value)
    {
        if (!float.IsFinite(value))
        {
            return 0;
        }

        return value <= int.MinValue
            ? int.MinValue
            : value >= int.MaxValue
                ? int.MaxValue
                : (int)MathF.Round(value);
    }
}
