using Game.Client.Rendering.Effects;
using Game.Core;
using Game.Core.Tiles;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Lighting;

public readonly record struct VisibleLightCollectionTelemetry(
    int LightsCollected,
    int TilesSampled,
    bool WasBudgetClamped);

public static class VisibleLightCollector
{
    public static VisibleLightCollectionTelemetry CollectTileLights(
        World world,
        TileRegistry tiles,
        Rectangle visibleWorld,
        in PresentationQualityProfile quality,
        Span<ScreenSpaceLight> destination,
        int maximumTileSamples = 65_536)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tiles);
        if (maximumTileSamples <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTileSamples));
        }

        var maximumLights = Math.Min(destination.Length, quality.Budget.MaxPointLights);
        if (maximumLights == 0 || visibleWorld.IsEmpty)
        {
            return default;
        }

        var minX = WorldPixelToTile(visibleWorld.X);
        var maxX = WorldPixelToTile((long)visibleWorld.X + visibleWorld.Width - 1L);
        var minY = Math.Max(0, WorldPixelToTile(visibleWorld.Y));
        var maxY = Math.Min(
            world.HeightTiles - 1,
            WorldPixelToTile((long)visibleWorld.Y + visibleWorld.Height - 1L));
        if (!world.IsHorizontallyInfinite)
        {
            minX = Math.Max(0, minX);
            maxX = Math.Min(world.WidthTiles - 1, maxX);
        }

        if (minX > maxX || minY > maxY)
        {
            return default;
        }

        var width = (long)maxX - minX + 1L;
        var height = (long)maxY - minY + 1L;
        var area = width * height;
        var step = area <= maximumTileSamples
            ? 1
            : Math.Max(1, (int)Math.Ceiling(Math.Sqrt(area / (double)maximumTileSamples)));
        var count = 0;
        var sampled = 0;
        for (long y = minY; y <= maxY; y += step)
        {
            for (long x = minX; x <= maxX; x += step)
            {
                sampled++;
                var tile = world.GetTile((int)x, (int)y);
                if (tile.IsAir ||
                    !tiles.TryGetByNumericId(tile.TileId, out var definition) ||
                    definition.EmittedLight == 0 ||
                    definition.LightRadius <= 0)
                {
                    continue;
                }

                var stableId = unchecked((uint)x * 0x9E3779B9u ^ (uint)y * 0x85EBCA6Bu ^ tile.TileId);
                var color = ResolveColor(definition);
                destination[count++] = new ScreenSpaceLight(
                    new Vector2(
                        ((int)x + 0.5f) * GameConstants.TileSize,
                        ((int)y + 0.5f) * GameConstants.TileSize),
                    definition.LightRadius * GameConstants.TileSize,
                    color,
                    definition.EmittedLight / 255f,
                    EmissiveStrength: 0.85f,
                    CastsShadows: true,
                    stableId,
                    FlickerAmount: string.Equals(definition.Id, "torch", StringComparison.OrdinalIgnoreCase)
                        ? 0.08f
                        : 0.02f);
                if (count == maximumLights)
                {
                    return new VisibleLightCollectionTelemetry(count, sampled, true);
                }
            }
        }

        return new VisibleLightCollectionTelemetry(count, sampled, step > 1);
    }

    private static Color ResolveColor(TileDefinition definition)
    {
        if (definition.Id.Contains("crystal", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(112, 213, 255);
        }

        if (definition.Id.Contains("mushroom", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(213, 121, 235);
        }

        return string.Equals(definition.Id, "torch", StringComparison.OrdinalIgnoreCase)
            ? new Color(255, 164, 82)
            : new Color(255, 214, 156);
    }

    private static int WorldPixelToTile(long worldPixel)
    {
        var tile = Math.Floor(worldPixel / (double)GameConstants.TileSize);
        return tile <= int.MinValue
            ? int.MinValue
            : tile >= int.MaxValue
                ? int.MaxValue
                : (int)tile;
    }
}
