using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public readonly record struct ParallaxTerrainTileRange(
    int MinimumTileX,
    int MaximumTileX);

public readonly record struct ParallaxTerrainEnvelope(
    int MinimumTileX,
    int MaximumTileX,
    int DeepestSurfaceTileY,
    int SampleCount);

public static class ParallaxTerrainEnvelopePlanner
{
    public const int DefaultHorizontalMarginTiles = 32;
    public const int SurfaceOverlapTiles = 2;
    public const int MaximumSamples = 512;

    public static ParallaxTerrainTileRange GetVisibleTileRange(
        in Rectangle visibleWorldBounds,
        int tileSize)
    {
        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize));
        }

        var minimum = FloorDivide(visibleWorldBounds.Left, tileSize);
        var lastWorldX = visibleWorldBounds.Width > 0
            ? (long)visibleWorldBounds.Right - 1
            : visibleWorldBounds.Left;
        var maximum = FloorDivide(SaturatingToInt(lastWorldX), tileSize);
        return new ParallaxTerrainTileRange(
            Math.Min(minimum, maximum),
            Math.Max(minimum, maximum));
    }

    public static ParallaxTerrainEnvelope Build(
        int fallbackSurfaceTileY,
        in Rectangle visibleWorldBounds,
        int tileSize,
        Func<int, int>? surfaceHeightResolver,
        int horizontalMarginTiles = DefaultHorizontalMarginTiles)
    {
        var visibleRange = GetVisibleTileRange(visibleWorldBounds, tileSize);
        var margin = Math.Max(0, horizontalMarginTiles);
        var minimum = SaturatingAdd(visibleRange.MinimumTileX, -margin);
        var maximum = SaturatingAdd(visibleRange.MaximumTileX, margin);
        if (surfaceHeightResolver is null)
        {
            return new ParallaxTerrainEnvelope(minimum, maximum, fallbackSurfaceTileY, 0);
        }

        var span = Math.Max(1L, (long)maximum - minimum + 1L);
        var stride = Math.Max(1L, (span + MaximumSamples - 1L) / MaximumSamples);
        var deepest = fallbackSurfaceTileY;
        var sampleCount = 0;
        var lastSampled = long.MinValue;
        for (long tileX = minimum; tileX <= maximum; tileX += stride)
        {
            deepest = Math.Max(deepest, surfaceHeightResolver((int)tileX));
            sampleCount++;
            lastSampled = tileX;
        }

        if (lastSampled != maximum)
        {
            deepest = Math.Max(deepest, surfaceHeightResolver(maximum));
            sampleCount++;
        }

        var overlap = Math.Max(SurfaceOverlapTiles, SaturatingToInt(stride));
        return new ParallaxTerrainEnvelope(
            minimum,
            maximum,
            SaturatingAdd(deepest, overlap),
            sampleCount);
    }

    private static int FloorDivide(int value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    private static int SaturatingAdd(int left, int right)
    {
        return SaturatingToInt((long)left + right);
    }

    private static int SaturatingToInt(long value)
    {
        return value <= int.MinValue
            ? int.MinValue
            : value >= int.MaxValue
                ? int.MaxValue
                : (int)value;
    }
}
