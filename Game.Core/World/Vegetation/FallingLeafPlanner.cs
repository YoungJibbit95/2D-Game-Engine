using System.Numerics;

namespace Game.Core.World.Vegetation;

public readonly record struct FallingLeafSpawn(
    Vector2 WorldPosition,
    Vector2 InitialVelocity,
    float Lifetime,
    float Scale,
    float Phase,
    float SwayAmplitude,
    float AnimationSpeed,
    float ColorVariation);

/// <summary>
/// Selects presentation-only falling leaves from authoritative loaded leaf tiles.
/// The fixed scan window and caller-owned destination keep the hot path bounded and allocation-free.
/// </summary>
public static class FallingLeafPlanner
{
    public const int MaximumScanTiles = 1_024;

    public static int Plan(
        World world,
        RectI visibleWorldPixels,
        int surfaceTileY,
        long tickNumber,
        float wind,
        float vegetationDensity,
        Span<FallingLeafSpawn> destination)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (destination.IsEmpty || visibleWorldPixels.IsEmpty || tickNumber < 0)
        {
            return 0;
        }

        wind = float.IsFinite(wind) ? Math.Clamp(wind, -1f, 1f) : 0f;
        vegetationDensity = float.IsFinite(vegetationDensity)
            ? Math.Clamp(vegetationDensity, 0f, 3f)
            : 0f;
        if (vegetationDensity <= 0.01f)
        {
            return 0;
        }

        var minimumTileX = FloorDiv(visibleWorldPixels.Left, GameConstants.TileSize) - 1;
        var maximumTileX = FloorDiv(SaturatingSubtractOne(visibleWorldPixels.Right), GameConstants.TileSize) + 1;
        var minimumTileY = Math.Max(0, FloorDiv(visibleWorldPixels.Top, GameConstants.TileSize) - 1);
        var maximumTileY = Math.Min(
            world.HeightTiles - 1,
            FloorDiv(SaturatingSubtractOne(visibleWorldPixels.Bottom), GameConstants.TileSize) + 1);
        if (surfaceTileY > 0)
        {
            minimumTileY = Math.Max(minimumTileY, surfaceTileY - 24);
            maximumTileY = Math.Min(maximumTileY, surfaceTileY + 3);
        }

        if (!world.IsHorizontallyInfinite)
        {
            minimumTileX = Math.Max(0, minimumTileX);
            maximumTileX = Math.Min(world.WidthTiles - 1, maximumTileX);
        }

        var width = (long)maximumTileX - minimumTileX + 1L;
        var height = (long)maximumTileY - minimumTileY + 1L;
        if (width <= 0L || height <= 0L)
        {
            return 0;
        }

        var cellCount = width * height;
        var scanCount = (int)Math.Min(cellCount, MaximumScanTiles);
        var start = (long)(Hash(tickNumber, minimumTileX, minimumTileY, 0xA24BAED4u) % (ulong)cellCount);
        var step = Math.Max(
            1L,
            cellCount / scanCount + (cellCount % scanCount == 0L ? 0L : 1L));
        step %= cellCount;
        if (step == 0L)
        {
            step = 1L;
        }

        while (GreatestCommonDivisor(step, cellCount) != 1L)
        {
            step = step + 1L == cellCount ? 1L : step + 1L;
        }

        var outputCount = 0;
        var linearIndex = start;
        for (var scanIndex = 0; scanIndex < scanCount && outputCount < destination.Length; scanIndex++)
        {
            var tileX = SaturateToInt((long)minimumTileX + linearIndex % width);
            var tileY = SaturateToInt((long)minimumTileY + linearIndex / width);
            linearIndex = linearIndex >= cellCount - step
                ? linearIndex - (cellCount - step)
                : linearIndex + step;
            if (!world.TryGetTile(tileX, tileY, out var tile) || !KnownTileIds.IsFoliage(tile.TileId))
            {
                continue;
            }

            if (!IsCanopyEdge(world, tileX, tileY))
            {
                continue;
            }

            var cellHash = Hash(tickNumber, tileX, tileY, 0x9E3779B9u);
            var densityGate = Math.Clamp((int)MathF.Round(vegetationDensity * 3f), 1, 9);
            if ((int)(cellHash % 12u) >= densityGate)
            {
                continue;
            }

            var horizontalJitter = Unit(cellHash >> 8) * (GameConstants.TileSize - 2f) + 1f;
            var phase = Unit(cellHash >> 24) * MathF.Tau;
            var speedVariation = Unit(cellHash >> 40);
            destination[outputCount++] = new FallingLeafSpawn(
                new Vector2(
                    tileX * (float)GameConstants.TileSize + horizontalJitter,
                    (tileY + 0.75f) * GameConstants.TileSize),
                new Vector2(
                    wind * (18f + speedVariation * 24f) + (Unit(cellHash >> 16) - 0.5f) * 7f,
                    5f + speedVariation * 7f),
                Lifetime: 2.6f + Unit(cellHash >> 32) * 1.8f,
                Scale: 0.8f + Unit(cellHash >> 48) * 0.45f,
                Phase: phase,
                SwayAmplitude: 5f + MathF.Abs(wind) * 7f + speedVariation * 4f,
                AnimationSpeed: 3.5f + Unit(cellHash >> 52) * 4f,
                ColorVariation: Unit(cellHash >> 44));
        }

        return outputCount;
    }

    private static bool IsCanopyEdge(World world, int tileX, int tileY)
    {
        return IsLoadedAir(world, tileX, tileY + 1) ||
               IsLoadedAir(world, tileX - 1, tileY) ||
               IsLoadedAir(world, tileX + 1, tileY);
    }

    private static bool IsLoadedAir(World world, int tileX, int tileY)
    {
        return world.TryGetTile(tileX, tileY, out var tile) && tile.IsAir;
    }

    private static ulong Hash(long tick, int x, int y, uint salt)
    {
        var value = unchecked((ulong)tick) ^
                    (unchecked((ulong)(uint)x) << 32) ^
                    unchecked((uint)y) ^
                    salt;
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return value;
    }

    private static float Unit(ulong value)
    {
        return (value & 0x00FFFFFFUL) / 16777215f;
    }

    private static int FloorDiv(int value, int divisor)
    {
        var quotient = value / divisor;
        return value % divisor < 0 ? quotient - 1 : quotient;
    }

    private static long GreatestCommonDivisor(long left, long right)
    {
        while (right != 0L)
        {
            var remainder = left % right;
            left = right;
            right = remainder;
        }

        return Math.Abs(left);
    }

    private static int SaturatingSubtractOne(int value)
    {
        return value == int.MinValue ? int.MinValue : value - 1;
    }

    private static int SaturateToInt(long value)
    {
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }
}
