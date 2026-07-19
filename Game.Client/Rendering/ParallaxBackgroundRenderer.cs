using Game.Client.Rendering.Effects;
using Game.Core.Runtime;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public sealed class ParallaxBackgroundRenderer
{
    private readonly ParallaxLayerDescriptor[] _layers =
        new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];
    private readonly ParallaxCompositionCommand[] _commands =
        new ParallaxCompositionCommand[ParallaxCompositionPlanner.MaximumCommandCount];
    private readonly ParallaxDetailCommand[] _details =
        new ParallaxDetailCommand[ParallaxDetailPlanner.MaximumCommandCount];
    private Func<int, int>? _cachedSurfaceHeightResolver;
    private ParallaxTerrainEnvelope _cachedTerrainEnvelope;

    public string SurfaceSpriteId { get; set; } = "world/backgrounds/forest_parallax_layer_v3";

    public string CaveSpriteId { get; set; } = "world/backgrounds/cave_parallax_layer_v3";

    public float SurfaceParallax { get; set; } = 0.18f;

    public float CaveParallax { get; set; } = 0.08f;

    public float Opacity { get; set; } = 0.92f;

    public int DetailQuality { get; set; } = 3;

    public int LastDetailCommandCount { get; private set; }

    public void Draw(
        RenderContext context,
        ClientTextureRegistry? textures,
        Camera2D camera,
        World world,
        bool isNight,
        LivingWorldFrameSnapshot livingWorld,
        Func<int, int>? surfaceHeightResolver = null)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(world);
        var localSurfaceTileY = livingWorld.SurfaceTileY > 0
            ? livingWorld.SurfaceTileY
            : world.Metadata.SpawnTile.Y;
        var localSurfaceY = localSurfaceTileY * Game.Core.GameConstants.TileSize;
        var panoramaSurfaceTileY = ResolvePanoramaSurfaceTileY(
            localSurfaceTileY,
            camera.VisibleWorldRect,
            surfaceHeightResolver);
        var panoramaSurfaceY = panoramaSurfaceTileY * Game.Core.GameConstants.TileSize;
        var cameraDepth = camera.Position.Y - localSurfaceY;
        var scene = ParallaxLayerPlanner.Build(
            livingWorld,
            isNight,
            cameraDepth,
            SurfaceParallax,
            CaveParallax,
            SurfaceSpriteId,
            CaveSpriteId,
            _layers);
        LastDetailCommandCount = ParallaxDetailPlanner.Build(
            livingWorld,
            scene,
            isNight,
            context.ViewportBounds,
            camera.Position.X,
            context.Time.TotalSeconds,
            DetailQuality,
            _details);
        DrawGradientSky(context, scene);
        DrawDetails(context, ParallaxDetailDepth.Backdrop);

        if (textures is null)
        {
            DrawDetails(context, ParallaxDetailDepth.Overlay);
            DrawWeatherDepth(context, camera, scene.UndergroundBlend, livingWorld);
            return;
        }

        context.SpriteBatch.End();
        context.SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            // Parallax art is authored as pixel art. Authored panoramas use whole-pixel
            // scaling, so point sampling preserves their delivered colors and detail.
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);
        for (var index = 0; index < scene.LayerCount; index++)
        {
            TryDrawLayer(
                context,
                textures,
                camera,
                localSurfaceY,
                panoramaSurfaceY,
                scene.UndergroundBlend,
                surfaceHeightResolver is null,
                _layers[index]);
        }

        DrawDetails(context, ParallaxDetailDepth.Overlay);
        DrawWeatherDepth(context, camera, scene.UndergroundBlend, livingWorld);
    }

    private bool TryDrawLayer(
        RenderContext context,
        ClientTextureRegistry textures,
        Camera2D camera,
        float localSurfaceY,
        float panoramaSurfaceY,
        float undergroundBlend,
        bool coverViewport,
        in ParallaxLayerDescriptor layer)
    {
        var sprite = textures.Get(layer.SpriteId);
        var source = sprite.SourceRectangle;
        if (sprite.IsPlaceholder || source.Width <= 0 || source.Height <= 0)
        {
            return false;
        }

        var alpha = layer.PreserveAuthoredRepeat
            ? Math.Clamp(layer.Opacity, 0f, 1f)
            : Math.Clamp(layer.Opacity * Opacity, 0f, 1f);
        if (alpha <= 0.001f)
        {
            return false;
        }

        var verticalScroll = (camera.Position.Y - localSurfaceY) * layer.VerticalParallax;
        var surfaceHorizon = layer.ProjectionMode == ParallaxProjectionMode.DistantHorizonBand
            ? ParallaxViewportLayoutPlanner.ResolveDistantSurfaceHorizon(
                context.ViewportBounds,
                (panoramaSurfaceY - localSurfaceY) * camera.Zoom,
                layer.DepthPlane)
            : camera.WorldToScreen(
                new Vector2(camera.Position.X, panoramaSurfaceY),
                context.ViewportBounds).Y;
        var layout = ParallaxViewportLayoutPlanner.Build(
            source.Width,
            source.Height,
            context.ViewportBounds,
            surfaceHorizon,
            undergroundBlend,
            layer.VerticalOffset,
            verticalScroll,
            layer.ScaleMultiplier,
            layer.PreserveAuthoredRepeat && coverViewport,
            layer.ProjectionMode,
            layer.DepthPlane);
        var tint = layer.Tint * alpha;
        SpriteTexture? alternate = null;
        if (!string.IsNullOrWhiteSpace(layer.AlternateSpriteId))
        {
            var candidate = textures.Get(layer.AlternateSpriteId);
            if (!candidate.IsPlaceholder && !candidate.SourceRectangle.IsEmpty)
            {
                alternate = candidate;
            }
        }

        SpriteTexture? landmark = null;
        if (!string.IsNullOrWhiteSpace(layer.LandmarkSpriteId))
        {
            var candidate = textures.Get(layer.LandmarkSpriteId, layer.LandmarkFrameIndex);
            if (!candidate.IsPlaceholder && !candidate.SourceRectangle.IsEmpty)
            {
                landmark = candidate;
            }
        }

        var commandCount = ParallaxCompositionPlanner.Build(
            layer,
            camera.Position.X,
            context.ViewportBounds,
            layout.Width,
            layout.Height,
            layout.Y,
            _commands);
        if (layer.VerticalFillMode == ParallaxVerticalFillMode.ExtendOuterEdges &&
            layer.TopFillColor.A > 0 &&
            ParallaxVerticalCoveragePlanner.TryBuildTopFill(
                new Rectangle(context.ViewportBounds.X, layout.Y, layout.Width, layout.Height),
                context.ViewportBounds,
                out var topFillBounds))
        {
            var fillOpacity = MathHelper.Lerp(0f, 0.86f, Math.Clamp(undergroundBlend, 0f, 1f));
            context.SpriteBatch.Draw(
                context.Pixel,
                topFillBounds,
                layer.TopFillColor * (alpha * fillOpacity));
        }

        for (var index = 0; index < commandCount; index++)
        {
            ref readonly var command = ref _commands[index];
            if (command.Kind == ParallaxCompositionCommandKind.Landmark)
            {
                DrawLandmark(context, landmark, command, layer, alpha);
                continue;
            }

            var repeat = command.UseAlternateSprite && alternate is not null ? alternate : sprite;
            var effects = command.FlipHorizontally ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            DrawRepeat(
                context,
                repeat,
                command.Bounds,
                tint,
                effects,
                featherTop: layer.FeatherTop &&
                    command.Bounds.Y > context.ViewportBounds.Y);
            if ((layer.VerticalFillMode is ParallaxVerticalFillMode.ExtendBottomEdge or
                    ParallaxVerticalFillMode.ExtendOuterEdges) &&
                ParallaxVerticalCoveragePlanner.TryBuildBottomEdgeExtension(
                    command.Bounds,
                    repeat.SourceRectangle,
                    context.ViewportBounds,
                    out var verticalCoverage))
            {
                context.SpriteBatch.Draw(
                    repeat.Texture,
                    verticalCoverage.Bounds,
                    verticalCoverage.SourceRectangle,
                    tint,
                    0f,
                    Vector2.Zero,
                    effects,
                    0f);
            }
        }

        return true;
    }

    private static void DrawRepeat(
        RenderContext context,
        SpriteTexture repeat,
        in Rectangle bounds,
        Color tint,
        SpriteEffects effects,
        bool featherTop)
    {
        if (!featherTop || bounds.Height < 32)
        {
            context.SpriteBatch.Draw(
                repeat.Texture,
                bounds,
                repeat.SourceRectangle,
                tint,
                0f,
                Vector2.Zero,
                effects,
                0f);
            return;
        }

        // Preserve authored pixel density while blending the opaque horizon plane into
        // the procedural sky. Slicing changes opacity only; it never stretches, mirrors
        // or re-samples the panorama outside the same source-to-destination mapping.
        const int maximumBands = 24;
        var featherHeight = Math.Clamp(bounds.Height / 4, 48, 96);
        var bandCount = Math.Min(maximumBands, featherHeight);
        for (var band = 0; band < bandCount; band++)
        {
            var destinationStart = band * featherHeight / bandCount;
            var destinationEnd = (band + 1) * featherHeight / bandCount;
            DrawRepeatSlice(
                context,
                repeat,
                bounds,
                destinationStart,
                destinationEnd,
                tint * MathHelper.SmoothStep(0f, 1f, (band + 1f) / bandCount),
                effects);
        }

        DrawRepeatSlice(
            context,
            repeat,
            bounds,
            featherHeight,
            bounds.Height,
            tint,
            effects);
    }

    private static void DrawRepeatSlice(
        RenderContext context,
        SpriteTexture repeat,
        in Rectangle bounds,
        int destinationStart,
        int destinationEnd,
        Color tint,
        SpriteEffects effects)
    {
        if (destinationEnd <= destinationStart)
        {
            return;
        }

        var source = repeat.SourceRectangle;
        var sourceStart = source.Y + destinationStart * source.Height / bounds.Height;
        var sourceEnd = source.Y + destinationEnd * source.Height / bounds.Height;
        sourceEnd = Math.Max(sourceStart + 1, Math.Min(source.Bottom, sourceEnd));
        context.SpriteBatch.Draw(
            repeat.Texture,
            new Rectangle(bounds.X, bounds.Y + destinationStart, bounds.Width, destinationEnd - destinationStart),
            new Rectangle(source.X, sourceStart, source.Width, sourceEnd - sourceStart),
            tint,
            0f,
            Vector2.Zero,
            effects,
            0f);
    }

    private int ResolvePanoramaSurfaceTileY(
        int fallbackSurfaceTileY,
        in Rectangle visibleWorldBounds,
        Func<int, int>? surfaceHeightResolver)
    {
        if (surfaceHeightResolver is null)
        {
            return fallbackSurfaceTileY;
        }

        var visibleRange = ParallaxTerrainEnvelopePlanner.GetVisibleTileRange(
            visibleWorldBounds,
            Game.Core.GameConstants.TileSize);
        if (ReferenceEquals(surfaceHeightResolver, _cachedSurfaceHeightResolver) &&
            visibleRange.MinimumTileX >= _cachedTerrainEnvelope.MinimumTileX &&
            visibleRange.MaximumTileX <= _cachedTerrainEnvelope.MaximumTileX)
        {
            return _cachedTerrainEnvelope.DeepestSurfaceTileY;
        }

        _cachedSurfaceHeightResolver = surfaceHeightResolver;
        _cachedTerrainEnvelope = ParallaxTerrainEnvelopePlanner.Build(
            fallbackSurfaceTileY,
            visibleWorldBounds,
            Game.Core.GameConstants.TileSize,
            surfaceHeightResolver);
        return _cachedTerrainEnvelope.DeepestSurfaceTileY;
    }

    private static void DrawGradientSky(
        RenderContext context,
        in ParallaxSceneProfile scene)
    {
        const int bandCount = 24;
        var bandHeight = Math.Max(1, (context.ViewportBounds.Height + bandCount - 1) / bandCount);
        for (var band = 0; band < bandCount; band++)
        {
            var start = band / (float)bandCount;
            var color = start < 0.5f
                ? Color.Lerp(scene.SkyTop, scene.SkyMiddle, start * 2f)
                : Color.Lerp(scene.SkyMiddle, scene.SkyBottom, (start - 0.5f) * 2f);
            var y = context.ViewportBounds.Y + band * bandHeight;
            var height = Math.Min(bandHeight + 1, context.ViewportBounds.Bottom - y);
            if (height > 0)
            {
                context.SpriteBatch.Draw(
                    context.Pixel,
                    new Rectangle(context.ViewportBounds.X, y, context.ViewportBounds.Width, height),
                    color);
            }
        }
    }

    private void DrawDetails(RenderContext context, ParallaxDetailDepth depth)
    {
        for (var index = 0; index < LastDetailCommandCount; index++)
        {
            ref readonly var detail = ref _details[index];
            if (detail.Depth != depth || detail.Color.A == 0 || detail.Bounds.IsEmpty)
            {
                continue;
            }

            switch (detail.Kind)
            {
                case ParallaxDetailKind.CloudWisp:
                    DrawCloudWisp(context, detail);
                    break;
                case ParallaxDetailKind.Star:
                    DrawStar(context, detail);
                    break;
                case ParallaxDetailKind.AmbientMote:
                    DrawAmbientMote(context, detail);
                    break;
                default:
                    context.SpriteBatch.Draw(context.Pixel, detail.Bounds, detail.Color);
                    break;
            }
        }
    }

    private static void DrawCloudWisp(RenderContext context, in ParallaxDetailCommand detail)
    {
        var bounds = detail.Bounds;
        var stepHeight = Math.Clamp(bounds.Height / 6, 1, 4);
        var centerY = bounds.Y + bounds.Height / 2;
        var edge = detail.Color * 0.55f;
        DrawRect(
            context,
            new Rectangle(
                bounds.X + bounds.Width / 10,
                centerY + stepHeight,
                Math.Max(2, bounds.Width * 4 / 5),
                stepHeight),
            edge);
        DrawRect(
            context,
            new Rectangle(
                bounds.X + bounds.Width / 5,
                centerY,
                Math.Max(2, bounds.Width * 3 / 5),
                stepHeight),
            detail.Color);
        DrawRect(
            context,
            new Rectangle(
                bounds.X + bounds.Width / 3,
                centerY - stepHeight,
                Math.Max(2, bounds.Width / 3),
                stepHeight),
            edge);
        if ((detail.Variation & 1u) != 0u)
        {
            DrawRect(
                context,
                new Rectangle(
                    bounds.X + bounds.Width * 2 / 3,
                    centerY - stepHeight * 2,
                    Math.Max(2, bounds.Width / 6),
                    stepHeight),
                edge);
        }
    }

    private static void DrawStar(RenderContext context, in ParallaxDetailCommand detail)
    {
        DrawRect(context, detail.Bounds, detail.Color);
        if (detail.Bounds.Width <= 1)
        {
            return;
        }

        var glow = detail.Color * 0.42f;
        DrawRect(
            context,
            new Rectangle(detail.Bounds.X - 2, detail.Bounds.Y, detail.Bounds.Width + 4, 1),
            glow);
        DrawRect(
            context,
            new Rectangle(detail.Bounds.X, detail.Bounds.Y - 2, 1, detail.Bounds.Height + 4),
            glow);
    }

    private static void DrawAmbientMote(RenderContext context, in ParallaxDetailCommand detail)
    {
        DrawRect(context, detail.Bounds, detail.Color);
        if (detail.Bounds.Width <= 1 || (detail.Variation & 3u) != 0u)
        {
            return;
        }

        var glow = detail.Color * 0.35f;
        DrawRect(
            context,
            new Rectangle(detail.Bounds.X - 1, detail.Bounds.Y, detail.Bounds.Width + 2, 1),
            glow);
        DrawRect(
            context,
            new Rectangle(detail.Bounds.X, detail.Bounds.Y - 1, 1, detail.Bounds.Height + 2),
            glow);
    }

    private static void DrawLandmark(
        RenderContext context,
        SpriteTexture? sprite,
        in ParallaxCompositionCommand command,
        in ParallaxLayerDescriptor layer,
        float layerAlpha)
    {
        var tint = layer.LandmarkTint * Math.Clamp(layerAlpha * 0.76f, 0f, 1f);
        if (sprite is not null)
        {
            context.SpriteBatch.Draw(
                sprite.Texture,
                command.Bounds,
                sprite.SourceRectangle,
                tint,
                0f,
                Vector2.Zero,
                command.FlipHorizontally ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                0f);
            return;
        }

        DrawLandmarkSilhouette(context, command.Bounds, command.LandmarkStyle, tint, command.FlipHorizontally);
    }

    private static void DrawLandmarkSilhouette(
        RenderContext context,
        in Rectangle bounds,
        ParallaxLandmarkStyle style,
        Color tint,
        bool flipped)
    {
        var dark = Color.Lerp(tint, new Color((byte)18, (byte)19, (byte)26, tint.A), 0.3f);
        var centerX = bounds.X + bounds.Width / 2;
        switch (style)
        {
            case ParallaxLandmarkStyle.Canopy:
                DrawRect(context, new Rectangle(centerX - Math.Max(2, bounds.Width / 14), bounds.Y + bounds.Height / 3, Math.Max(4, bounds.Width / 7), bounds.Height * 2 / 3), dark);
                DrawRect(context, new Rectangle(bounds.X + bounds.Width / 8, bounds.Y + bounds.Height / 8, bounds.Width * 3 / 4, bounds.Height / 3), tint);
                DrawRect(context, new Rectangle(bounds.X, bounds.Y + bounds.Height / 4, bounds.Width, bounds.Height / 4), tint);
                break;
            case ParallaxLandmarkStyle.Ruin:
            case ParallaxLandmarkStyle.AmberWorkshop:
                DrawRect(context, new Rectangle(bounds.X, bounds.Y + bounds.Height / 4, bounds.Width, Math.Max(3, bounds.Height / 7)), tint);
                DrawRect(context, new Rectangle(bounds.X + bounds.Width / 8, bounds.Y + bounds.Height / 3, Math.Max(3, bounds.Width / 8), bounds.Height * 2 / 3), dark);
                DrawRect(context, new Rectangle(bounds.Right - bounds.Width / 4, bounds.Y + bounds.Height / 3, Math.Max(3, bounds.Width / 8), bounds.Height * 2 / 3), dark);
                break;
            case ParallaxLandmarkStyle.CrystalCluster:
                DrawSteppedSpire(context, bounds, centerX, bounds.Height, tint);
                DrawSteppedSpire(context, bounds, centerX - bounds.Width / 4, bounds.Height * 2 / 3, dark);
                DrawSteppedSpire(context, bounds, centerX + bounds.Width / 4, bounds.Height / 2, dark);
                break;
            case ParallaxLandmarkStyle.MushroomColony:
                DrawMushroom(context, bounds, centerX, bounds.Height, tint);
                DrawMushroom(context, bounds, flipped ? centerX + bounds.Width / 3 : centerX - bounds.Width / 3, bounds.Height * 2 / 3, dark);
                break;
            case ParallaxLandmarkStyle.Mangrove:
                var trunkX = flipped ? bounds.X + bounds.Width * 2 / 3 : bounds.X + bounds.Width / 3;
                DrawRect(context, new Rectangle(trunkX, bounds.Y + bounds.Height / 4, Math.Max(4, bounds.Width / 8), bounds.Height * 3 / 4), dark);
                DrawRect(context, new Rectangle(bounds.X, bounds.Y + bounds.Height / 6, bounds.Width, bounds.Height / 4), tint);
                DrawRect(context, new Rectangle(bounds.X + bounds.Width / 5, bounds.Y, bounds.Width * 3 / 5, bounds.Height / 4), tint);
                break;
            case ParallaxLandmarkStyle.CaveSpire:
                DrawSteppedSpire(context, bounds, centerX, bounds.Height, tint);
                break;
        }
    }

    private static void DrawSteppedSpire(
        RenderContext context,
        in Rectangle bounds,
        int centerX,
        int height,
        Color color)
    {
        var width = Math.Max(4, bounds.Width / 4);
        var top = bounds.Bottom - height;
        DrawRect(context, new Rectangle(centerX - width / 2, top + height / 3, width, height * 2 / 3), color);
        DrawRect(context, new Rectangle(centerX - width / 4, top, Math.Max(2, width / 2), height / 3 + 1), color);
    }

    private static void DrawMushroom(
        RenderContext context,
        in Rectangle bounds,
        int centerX,
        int height,
        Color color)
    {
        var top = bounds.Bottom - height;
        var capWidth = Math.Max(6, bounds.Width / 2);
        DrawRect(context, new Rectangle(centerX - Math.Max(2, capWidth / 10), top + height / 3, Math.Max(4, capWidth / 5), height * 2 / 3), color);
        DrawRect(context, new Rectangle(centerX - capWidth / 2, top + height / 5, capWidth, Math.Max(4, height / 4)), color);
    }

    private static void DrawRect(RenderContext context, in Rectangle rectangle, Color color)
    {
        if (rectangle.Width > 0 && rectangle.Height > 0)
        {
            context.SpriteBatch.Draw(context.Pixel, rectangle, color);
        }
    }

    private static void DrawWeatherDepth(
        RenderContext context,
        Camera2D camera,
        float undergroundBlend,
        in LivingWorldFrameSnapshot livingWorld)
    {
        var surfaceVisibility = 1f - undergroundBlend;
        var intensity = Math.Clamp(livingWorld.WeatherIntensity * surfaceVisibility, 0f, 1f);
        if (intensity <= 0.001f)
        {
            return;
        }

        if (livingWorld.Weather == Game.Core.Weather.WeatherKind.Fog)
        {
            var bandHeight = Math.Max(16, context.ViewportBounds.Height / 8);
            for (var band = 0; band < 4; band++)
            {
                var drift = MathF.Sin(
                    (float)context.Time.TotalSeconds * (0.09f + band * 0.02f) +
                    camera.Position.X * 0.0005f +
                    band) * 18f;
                var y = context.ViewportBounds.Y + context.ViewportBounds.Height / 4 + band * bandHeight;
                context.SpriteBatch.Draw(
                    context.Pixel,
                    new Rectangle(
                        context.ViewportBounds.X + SaturatingRound(drift) - 24,
                        y,
                        context.ViewportBounds.Width + 48,
                        bandHeight),
                    new Color(170, 190, 196, Math.Clamp((int)(intensity * 34f), 0, 255)));
            }

            return;
        }

        if (livingWorld.Weather is not (Game.Core.Weather.WeatherKind.Rain or Game.Core.Weather.WeatherKind.Storm))
        {
            return;
        }

        var streaks = livingWorld.Weather == Game.Core.Weather.WeatherKind.Storm ? 30 : 18;
        var windOffset = SaturatingRound(livingWorld.Wind * 6f);
        var frame = (int)Math.Floor(context.Time.TotalSeconds * 24d);
        for (var index = 0; index < streaks; index++)
        {
            var x = PositiveModulo(index * 97 + frame * 5, Math.Max(1, context.ViewportBounds.Width + 48)) - 24;
            var y = PositiveModulo(index * 53 + frame * 11, Math.Max(1, context.ViewportBounds.Height + 64)) - 64;
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(
                    context.ViewportBounds.X + x + windOffset,
                    context.ViewportBounds.Y + y,
                    1,
                    livingWorld.Weather == Game.Core.Weather.WeatherKind.Storm ? 14 : 9),
                new Color(154, 192, 215, Math.Clamp((int)(intensity * 92f), 0, 255)));
        }
    }

    private static int PositiveModulo(int value, int modulo)
    {
        var remainder = value % modulo;
        return remainder < 0 ? remainder + modulo : remainder;
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
