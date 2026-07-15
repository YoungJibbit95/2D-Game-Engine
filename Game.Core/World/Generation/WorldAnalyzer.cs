using System;
using System.Collections.Generic;

namespace Game.Core.World.Generation;

public sealed class WorldAnalyzer
{
    private const int CavernRegionMinimumTiles = 64;
    private const int SurfaceLiquidDepthTolerance = 4;
    private const int UndergroundAirDepthOffset = 2;

    public WorldGenerationAnalysis Analyze(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        // Dimensionen einmalig erfassen, damit während der gesamten Analyse
        // dieselben Werte verwendet werden.
        var width = world.WidthTiles;
        var height = world.HeightTiles;
        var totalTileCount = GetValidatedTileCount(width, height);

        var surfaceHeights = new int[width];
        Array.Fill(surfaceHeights, -1);

        var tileCounts = new Dictionary<ushort, int>();
        var wallCounts = new Dictionary<ushort, int>();

        var air = 0;
        var solid = 0;
        var liquid = 0;
        var natural = 0;
        var wall = 0;
        var exposedWall = 0;

        // long verhindert einen möglichen Overflow bei großen Welten.
        long surfaceSum = 0;

        var surfaceColumns = 0;
        var minSurface = int.MaxValue;
        var maxSurface = int.MinValue;

        /*
         * Erster Durchlauf:
         * Allgemeine Tile-Statistiken und Oberflächenhöhe jeder Spalte.
         */
        for (var x = 0; x < width; x++)
        {
            var columnSurface = -1;

            for (var y = 0; y < height; y++)
            {
                var tile = world.GetTile(x, y);

                tileCounts[tile.TileId] =
                    tileCounts.GetValueOrDefault(tile.TileId) + 1;

                if (tile.IsAir)
                {
                    air++;
                }

                if (tile.IsSolid)
                {
                    solid++;

                    if (columnSurface < 0)
                    {
                        columnSurface = y;
                    }
                }

                if (tile.HasLiquid)
                {
                    liquid++;
                }

                if ((tile.Flags & TileFlags.IsNatural) != 0)
                {
                    natural++;
                }

                if (tile.WallId == 0)
                {
                    continue;
                }

                wall++;
                wallCounts[tile.WallId] =
                    wallCounts.GetValueOrDefault(tile.WallId) + 1;

                if (!tile.IsSolid)
                {
                    exposedWall++;
                }
            }

            if (columnSurface < 0)
            {
                continue;
            }

            surfaceHeights[x] = columnSurface;
            surfaceColumns++;
            surfaceSum += columnSurface;
            minSurface = Math.Min(minSurface, columnSurface);
            maxSurface = Math.Max(maxSurface, columnSurface);
        }

        /*
         * Zweiter Durchlauf:
         * Unterirdische Luft und Flüssigkeitsklassifizierung.
         */
        var undergroundAir = 0;
        var surfaceLiquid = 0;
        var caveLiquid = 0;

        for (var x = 0; x < width; x++)
        {
            var surfaceHeight = surfaceHeights[x];

            for (var y = 0; y < height; y++)
            {
                var tile = world.GetTile(x, y);

                if (IsUndergroundDryAir(
                        tile,
                        x,
                        y,
                        surfaceHeights))
                {
                    undergroundAir++;
                }

                if (!tile.HasLiquid)
                {
                    continue;
                }

                // long verhindert einen theoretischen Overflow bei
                // surfaceHeight + SurfaceLiquidDepthTolerance.
                var isNearSurface =
                    surfaceHeight >= 0 &&
                    (long)y <=
                    (long)surfaceHeight + SurfaceLiquidDepthTolerance;

                if (isNearSurface)
                {
                    surfaceLiquid++;
                }
                else
                {
                    caveLiquid++;
                }
            }
        }

        /*
         * Zusammenhängende unterirdische Luftregionen analysieren.
         */
        var undergroundAirRegions = FindRegionSizes(
            world,
            width,
            height,
            totalTileCount,
            (x, y, tile) =>
                IsUndergroundDryAir(
                    tile,
                    x,
                    y,
                    surfaceHeights));

        var cavernTileCount = 0;
        var cavernRegionCount = 0;
        var largestCavernTileCount = 0;

        foreach (var regionSize in undergroundAirRegions)
        {
            if (regionSize < CavernRegionMinimumTiles)
            {
                continue;
            }

            cavernRegionCount++;
            cavernTileCount = checked(cavernTileCount + regionSize);
            largestCavernTileCount =
                Math.Max(largestCavernTileCount, regionSize);
        }

        /*
         * Zusammenhängende Flüssigkeitsregionen analysieren.
         */
        var liquidBodies = FindRegionSizes(
            world,
            width,
            height,
            totalTileCount,
            static (_, _, tile) => tile.HasLiquid);

        var largestLiquidBodyTileCount = 0;

        foreach (var regionSize in liquidBodies)
        {
            largestLiquidBodyTileCount =
                Math.Max(largestLiquidBodyTileCount, regionSize);
        }

        var averageSurfaceHeight =
            surfaceColumns == 0
                ? 0f
                : (float)((double)surfaceSum / surfaceColumns);

        return new WorldGenerationAnalysis(
            width,
            height,
            air,
            solid,
            liquid,
            natural,
            surfaceColumns == 0 ? 0 : minSurface,
            surfaceColumns == 0 ? 0 : maxSurface,
            averageSurfaceHeight,
            tileCounts)
        {
            UndergroundAirTileCount = undergroundAir,

            CavernTileCount = cavernTileCount,
            CavernRegionCount = cavernRegionCount,
            LargestCavernTileCount = largestCavernTileCount,

            LiquidBodyCount = liquidBodies.Count,
            LargestLiquidBodyTileCount = largestLiquidBodyTileCount,

            SurfaceLiquidTileCount = surfaceLiquid,
            CaveLiquidTileCount = caveLiquid,

            WallTileCount = wall,
            ExposedWallTileCount = exposedWall,
            WallCounts = wallCounts
        };
    }

    private static bool IsUndergroundDryAir(
        TileInstance tile,
        int x,
        int y,
        int[] surfaceHeights)
    {
        if (!tile.IsAir || tile.HasLiquid)
        {
            return false;
        }

        if (y < 0)
        {
            return false;
        }

        // Dieser Check fängt sowohl negative X-Werte als auch zu große
        // X-Werte mit nur einem Vergleich ab.
        if ((uint)x >= (uint)surfaceHeights.Length)
        {
            return false;
        }

        var surfaceHeight = surfaceHeights[x];

        if (surfaceHeight < 0)
        {
            return false;
        }

        // long verhindert einen theoretischen Overflow bei surfaceHeight + 2.
        return (long)y >
               (long)surfaceHeight + UndergroundAirDepthOffset;
    }

    private static List<int> FindRegionSizes(
        World world,
        int width,
        int height,
        int totalTileCount,
        Func<int, int, TileInstance, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(predicate);

        if (totalTileCount == 0)
        {
            return [];
        }

        var visited = new bool[totalTileCount];
        var regionSizes = new List<int>();
        var pending = new Queue<TilePos>();

        ReadOnlySpan<TilePos> offsets =
        [
            new TilePos(-1, 0),
            new TilePos(1, 0),
            new TilePos(0, -1),
            new TilePos(0, 1)
        ];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = GetTileIndex(x, y, width);

                if (visited[index])
                {
                    continue;
                }

                // Jedes Tile wird genau einmal verarbeitet.
                visited[index] = true;

                var tile = world.GetTile(x, y);

                if (!predicate(x, y, tile))
                {
                    continue;
                }

                pending.Enqueue(new TilePos(x, y));

                var regionSize = 0;

                while (pending.Count > 0)
                {
                    var current = pending.Dequeue();
                    regionSize++;

                    foreach (var offset in offsets)
                    {
                        var nextX = current.X + offset.X;
                        var nextY = current.Y + offset.Y;

                        /*
                         * Direkter Bounds-Check statt world.IsInBounds().
                         *
                         * Der uint-Vergleich lehnt gleichzeitig folgende
                         * Werte ab:
                         * - negative Koordinaten
                         * - x >= width
                         * - y >= height
                         */
                        if ((uint)nextX >= (uint)width ||
                            (uint)nextY >= (uint)height)
                        {
                            continue;
                        }

                        var nextIndex =
                            GetTileIndex(nextX, nextY, width);

                        if (visited[nextIndex])
                        {
                            continue;
                        }

                        visited[nextIndex] = true;

                        var nextTile =
                            world.GetTile(nextX, nextY);

                        if (predicate(nextX, nextY, nextTile))
                        {
                            pending.Enqueue(
                                new TilePos(nextX, nextY));
                        }
                    }
                }

                regionSizes.Add(regionSize);
            }
        }

        return regionSizes;
    }

    private static int GetTileIndex(
        int x,
        int y,
        int width)
    {
        return checked((y * width) + x);
    }

    private static int GetValidatedTileCount(
        int width,
        int height)
    {
        if (width < 0)
        {
            throw new InvalidOperationException(
                $"The world has an invalid width: {width}.");
        }

        if (height < 0)
        {
            throw new InvalidOperationException(
                $"The world has an invalid height: {height}.");
        }

        try
        {
            return checked(width * height);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException(
                $"The world dimensions {width}x{height} are too large " +
                "for the analyzer's indexed tile storage.",
                exception);
        }
    }
}
