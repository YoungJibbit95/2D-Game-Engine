using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

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

    public static bool CoversViewportVertically(
        in Rectangle layerBounds,
        in Rectangle viewport)
    {
        if (layerBounds.Width <= 0 ||
            layerBounds.Height <= 0 ||
            viewport.Width <= 0 ||
            viewport.Height <= 0)
        {
            return false;
        }

        return layerBounds.Top <= viewport.Top && layerBounds.Bottom >= viewport.Bottom;
    }
}
