using Game.Client.Rendering.Effects;
using Game.Core.Combat;
using Game.Core.Runtime;
using Game.Core;
using Game.Core.Tiles;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Lighting;

public readonly record struct VisibleLightCollectionTelemetry(
    int LightsCollected,
    int TilesSampled,
    bool WasBudgetClamped);

public readonly record struct DynamicLightCollectionTelemetry(
    int LightsCollected,
    int EntitiesInspected,
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
    public static DynamicLightCollectionTelemetry CollectEntityLights(
        IReadOnlyList<EntityFrameSnapshot> entities,
        Rectangle visibleWorld,
        in PresentationQualityProfile quality,
        Span<ScreenSpaceLight> destination)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var maximumLights = Math.Min(destination.Length, quality.Budget.MaxPointLights);
        if (maximumLights == 0 || visibleWorld.IsEmpty)
        {
            return default;
        }

        var count = 0;
        var inspected = 0;
        for (var index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            inspected++;
            if (!entity.IsActive ||
                entity.Kind != EntityFrameKind.Projectile ||
                entity.DamageType is not (DamageType.Magic or DamageType.Fire))
            {
                continue;
            }

            var bounds = entity.Bounds;
            if (bounds.Right <= visibleWorld.Left ||
                bounds.Left >= visibleWorld.Right ||
                bounds.Bottom <= visibleWorld.Top ||
                bounds.Top >= visibleWorld.Bottom)
            {
                continue;
            }

            var isFire = entity.DamageType == DamageType.Fire;
            var speed = entity.Velocity.Length();
            var motionBoost = Math.Clamp(speed / 720f, 0f, 0.18f);
            var stableId = ResolveStableId(entity.Id, entity.ContentId);
            destination[count++] = new ScreenSpaceLight(
                new Vector2(
                    bounds.X + bounds.Width * 0.5f,
                    bounds.Y + bounds.Height * 0.5f),
                RadiusPixels: (isFire ? 92f : 112f) * (1f + motionBoost),
                Color: isFire
                    ? new Color(255, 122, 54)
                    : new Color(126, 172, 255),
                Intensity: isFire ? 0.82f : 0.9f,
                EmissiveStrength: isFire ? 0.92f : 1.05f,
                CastsShadows: true,
                StableId: stableId,
                FlickerAmount: isFire ? 0.06f : 0.015f);
            if (count == maximumLights)
            {
                return new DynamicLightCollectionTelemetry(count, inspected, index + 1 < entities.Count);
            }
        }

        return new DynamicLightCollectionTelemetry(count, inspected, false);
    }


    private static uint ResolveStableId(int entityId, string contentId)
    {
        var hash = 2166136261u ^ unchecked((uint)entityId);
        for (var index = 0; index < contentId.Length; index++)
        {
            hash ^= char.ToUpperInvariant(contentId[index]);
            hash *= 16777619u;
        }

        return hash;
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
