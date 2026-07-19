using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public readonly record struct ParallaxVerticalCoverageCommand(
    Rectangle Bounds,
    Rectangle SourceRectangle);

public static class ParallaxVerticalCoveragePlanner
{
    public static bool TryBuildTopFill(
        in Rectangle layerBounds,
        in Rectangle viewport,
        out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (layerBounds.Width <= 0 ||
            layerBounds.Height <= 0 ||
            viewport.Width <= 0 ||
            viewport.Height <= 0 ||
            layerBounds.Top <= viewport.Top)
        {
            return false;
        }

        var extensionBottom = Math.Clamp(layerBounds.Top + 1, viewport.Top, viewport.Bottom);
        var extensionHeight = extensionBottom - viewport.Top;
        if (extensionHeight <= 0)
        {
            return false;
        }

        bounds = new Rectangle(viewport.X, viewport.Top, viewport.Width, extensionHeight);
        return true;
    }

    public static bool TryBuildBottomEdgeExtension(
        in Rectangle layerBounds,
        in Rectangle sourceRectangle,
        in Rectangle viewport,
        out ParallaxVerticalCoverageCommand command)
    {
        command = default;
        if (layerBounds.Width <= 0 ||
            layerBounds.Height <= 0 ||
            sourceRectangle.Width <= 0 ||
            sourceRectangle.Height <= 0 ||
            viewport.Width <= 0 ||
            viewport.Height <= 0 ||
            layerBounds.Bottom >= viewport.Bottom)
        {
            return false;
        }

        var extensionTop = Math.Clamp(layerBounds.Bottom - 1, viewport.Top, viewport.Bottom);
        var extensionHeight = viewport.Bottom - extensionTop;
        if (extensionHeight <= 0)
        {
            return false;
        }

        command = new ParallaxVerticalCoverageCommand(
            new Rectangle(layerBounds.X, extensionTop, layerBounds.Width, extensionHeight),
            new Rectangle(
                sourceRectangle.X + sourceRectangle.Width / 2,
                sourceRectangle.Bottom - 1,
                1,
                1));
        return true;
    }
}
