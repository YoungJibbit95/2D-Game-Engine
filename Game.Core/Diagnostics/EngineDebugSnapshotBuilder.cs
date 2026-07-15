using Game.Core.Entities;
using Game.Core.Time;
using Game.Core.World;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Diagnostics;

public sealed class EngineDebugSnapshotBuilder
{
    public EngineDebugSnapshot Build(GameWorld world, EntityManager entities, WorldTime? time = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entities);

        var chunks = world.Chunks.Values.ToArray();
        var entityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities.Entities)
        {
            var name = entity.GetType().Name;
            entityCounts[name] = entityCounts.GetValueOrDefault(name) + 1;
        }

        var surfaces = new Dictionary<long, int>();
        var air = 0;
        var solid = 0;
        var liquid = 0;
        var dirty = 0;
        var meshDirty = 0;
        var lightDirty = 0;
        foreach (var chunk in chunks)
        {
            if (chunk.IsDirty) dirty++;
            if (chunk.NeedsMeshRebuild) meshDirty++;
            if (chunk.NeedsLightUpdate) lightDirty++;

            var chunkOriginX = (long)chunk.Position.X * GameConstants.ChunkSize;
            var chunkOriginY = (long)chunk.Position.Y * GameConstants.ChunkSize;
            for (var localY = 0; localY < GameConstants.ChunkSize; localY++)
            {
                var globalY = chunkOriginY + localY;
                if (globalY < 0 || globalY >= world.HeightTiles)
                {
                    continue;
                }

                for (var localX = 0; localX < GameConstants.ChunkSize; localX++)
                {
                    var tile = chunk.GetTile(localX, localY);
                    if (tile.IsAir) air++;
                    if (tile.IsSolid)
                    {
                        solid++;
                        var globalX = chunkOriginX + localX;
                        if (!surfaces.TryGetValue(globalX, out var surface) || globalY < surface)
                        {
                            surfaces[globalX] = (int)globalY;
                        }
                    }
                    if (tile.HasLiquid) liquid++;
                }
            }
        }

        var minSurface = 0;
        var maxSurface = 0;
        var averageSurface = 0f;
        if (surfaces.Count > 0)
        {
            minSurface = int.MaxValue;
            maxSurface = int.MinValue;
            long surfaceSum = 0;
            foreach (var surface in surfaces.Values)
            {
                minSurface = Math.Min(minSurface, surface);
                maxSurface = Math.Max(maxSurface, surface);
                surfaceSum += surface;
            }
            averageSurface = (float)((double)surfaceSum / surfaces.Count);
        }

        var activeEntities = 0;
        foreach (var entity in entities.Entities)
        {
            if (entity.IsActive) activeEntities++;
        }

        return new EngineDebugSnapshot(
            world.Metadata.Name,
            world.Metadata.Seed,
            world.WidthTiles,
            world.HeightTiles,
            chunks.Length,
            dirty,
            meshDirty,
            lightDirty,
            entities.Entities.Count,
            activeEntities,
            liquid,
            solid,
            air,
            minSurface,
            maxSurface,
            averageSurface,
            time?.Day ?? 0,
            time?.NormalizedTimeOfDay ?? 0,
            time?.IsNight ?? false,
            entityCounts);
    }
}