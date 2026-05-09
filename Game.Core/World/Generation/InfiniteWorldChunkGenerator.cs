namespace Game.Core.World.Generation;

public sealed class InfiniteWorldChunkGenerator
{
    private readonly Func<int, INoiseService> _noiseFactory;

    public InfiniteWorldChunkGenerator(Func<int, INoiseService>? noiseFactory = null)
    {
        _noiseFactory = noiseFactory ?? (seed => new FastNoiseLiteNoiseService(seed));
    }

    public World CreateWorld(WorldGenerationProfile profile, int seed, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var world = new World(
            widthTiles: GameConstants.ChunkSize,
            heightTiles: profile.HeightTiles,
            WorldMetadata.CreateDefault(seed) with { Name = name ?? "Infinite World" },
            isHorizontallyInfinite: true);

        var spawnX = 0;
        var spawnY = Math.Max(0, ComputeSurfaceHeight(profile, _noiseFactory(seed), seed, spawnX) - 2);
        world.SetMetadata(world.Metadata with { SpawnTile = new TilePos(spawnX, spawnY) });
        return world;
    }

    public Chunk GenerateChunk(WorldGenerationProfile profile, int seed, ChunkPos position)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var chunk = new Chunk(position);
        var tiles = new TileInstance[GameConstants.ChunkSize * GameConstants.ChunkSize];
        FillTiles(profile, seed, position, tiles);
        chunk.LoadTiles(tiles);
        return chunk;
    }

    public bool EnsureChunk(World world, WorldGenerationProfile profile, ChunkPos position)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(profile);

        if (!world.IsHorizontallyInfinite)
        {
            throw new InvalidOperationException("Infinite chunk generation requires a horizontally infinite world.");
        }

        if (world.TryGetChunk(position, out _))
        {
            return false;
        }

        var chunk = world.GetOrCreateChunk(position);
        var tiles = new TileInstance[GameConstants.ChunkSize * GameConstants.ChunkSize];
        FillTiles(profile, world.Metadata.Seed, position, tiles);
        chunk.LoadTiles(tiles);
        return true;
    }

    public int EnsureChunks(World world, WorldGenerationProfile profile, IEnumerable<ChunkPos> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);

        var generated = 0;
        foreach (var position in positions)
        {
            if (EnsureChunk(world, profile, position))
            {
                generated++;
            }
        }

        return generated;
    }

    public WorldDimensionDefinition GetDimensionAt(WorldGenerationProfile profile, int tileY)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return ResolveDimensions(profile).First(dimension => dimension.Contains(tileY));
    }

    private void FillTiles(WorldGenerationProfile profile, int seed, ChunkPos position, TileInstance[] tiles)
    {
        var noise = _noiseFactory(seed);
        var dimensions = ResolveDimensions(profile);

        for (var localY = 0; localY < GameConstants.ChunkSize; localY++)
        {
            var tileY = position.Y * GameConstants.ChunkSize + localY;
            for (var localX = 0; localX < GameConstants.ChunkSize; localX++)
            {
                var tileX = position.X * GameConstants.ChunkSize + localX;
                var index = localY * GameConstants.ChunkSize + localX;
                tiles[index] = GenerateTile(profile, dimensions, noise, seed, tileX, tileY);
            }
        }
    }

    private static TileInstance GenerateTile(
        WorldGenerationProfile profile,
        IReadOnlyList<WorldDimensionDefinition> dimensions,
        INoiseService noise,
        int seed,
        int tileX,
        int tileY)
    {
        if (tileY < 0 || tileY >= profile.HeightTiles)
        {
            return TileInstance.Air;
        }

        var dimension = ResolveDimension(dimensions, tileY);
        var surfaceY = ComputeSurfaceHeight(profile, noise, seed, tileX);
        if (TryGenerateTreeTile(profile, dimensions, noise, seed, tileX, tileY) is { } treeTile)
        {
            return treeTile;
        }

        if (tileY < surfaceY)
        {
            return new TileInstance { TileId = KnownTileIds.Air, Light = 255 };
        }

        var dirtDepth = ComputeDirtDepth(profile, seed, tileX);
        var tileId = tileY == surfaceY
            ? dimension.SurfaceTileId
            : tileY < surfaceY + dirtDepth
                ? dimension.SubsurfaceTileId
                : dimension.FillTileId;

        var isCave = ShouldCarveCave(profile, dimension, noise, surfaceY, tileX, tileY);
        var isWaterPocket = ShouldFillWaterPocket(profile, dimension, seed, surfaceY, tileX, tileY);
        if (isCave || isWaterPocket)
        {
            var tile = isWaterPocket ? TileInstance.Liquid(255) : TileInstance.Air;
            tile.Light = dimension.AmbientLight;
            return tile;
        }

        tileId = MaybePlaceOre(profile, dimension, noise, tileId, surfaceY, tileX, tileY);
        var generated = TileInstance.FromTileId(tileId, TileFlags.IsNatural);
        generated.Light = dimension.AmbientLight;
        return generated;
    }

    private static int ComputeSurfaceHeight(WorldGenerationProfile profile, INoiseService noise, int seed, int tileX)
    {
        var baseSurfaceY = Math.Clamp(profile.SurfaceBaseY, 6, profile.HeightTiles - 10);
        var amplitude = Math.Max(1, profile.SurfaceAmplitude);
        var continental = noise.GetNoise(tileX * 0.65f, seed * 0.013f);
        var detail = noise.GetNoise(tileX * 2.25f + 91, seed * 0.021f) * 0.35f;
        var surfaceY = baseSurfaceY + (int)MathF.Round((continental + detail) * amplitude);
        return Math.Clamp(surfaceY, Math.Max(3, profile.HeightTiles / 8), Math.Max(4, profile.HeightTiles / 2));
    }

    private static TileInstance? TryGenerateTreeTile(
        WorldGenerationProfile profile,
        IReadOnlyList<WorldDimensionDefinition> dimensions,
        INoiseService noise,
        int seed,
        int tileX,
        int tileY)
    {
        if (tileY <= 0)
        {
            return null;
        }

        for (var centerX = tileX - 2; centerX <= tileX + 2; centerX++)
        {
            var surfaceY = ComputeSurfaceHeight(profile, noise, seed, centerX);
            if (tileY >= surfaceY)
            {
                continue;
            }

            var surfaceDimension = ResolveDimension(dimensions, surfaceY);
            if (!surfaceDimension.AllowsSurfaceTrees || surfaceDimension.SurfaceTileId != KnownTileIds.Grass)
            {
                continue;
            }

            if (!ShouldGrowTreeAt(profile, seed, centerX))
            {
                continue;
            }

            var height = ComputeTreeHeight(profile, seed, centerX);
            var topY = surfaceY - height;
            if (topY <= 2)
            {
                continue;
            }

            if (tileX == centerX && tileY >= topY && tileY < surfaceY)
            {
                return LitPassThroughTile(KnownTileIds.Wood);
            }

            var dx = Math.Abs(tileX - centerX);
            var dy = Math.Abs(tileY - topY);
            if (tileY >= topY - 2 && tileY <= topY + 1 && dx + dy <= 3)
            {
                return LitPassThroughTile(KnownTileIds.Leaves);
            }
        }

        return null;
    }

    private static bool ShouldGrowTreeAt(WorldGenerationProfile profile, int seed, int tileX)
    {
        if (profile.TreeAttempts <= 0 || profile.TreeAttemptChance <= 0f)
        {
            return false;
        }

        var density = Math.Clamp(
            profile.TreeAttempts * Math.Clamp(profile.TreeAttemptChance, 0f, 1f) / Math.Max(1f, profile.WidthTiles),
            0f,
            0.2f);

        if (StableUnit(seed ^ unchecked((int)0x51D7348F), tileX) >= density)
        {
            return false;
        }

        for (var offset = -2; offset <= 2; offset++)
        {
            if (offset == 0)
            {
                continue;
            }

            if (StableUnit(seed ^ unchecked((int)0x71E2A9D5), tileX + offset) < density * 0.45f)
            {
                return false;
            }
        }

        return true;
    }

    private static int ComputeTreeHeight(WorldGenerationProfile profile, int seed, int tileX)
    {
        var minHeight = Math.Max(1, Math.Min(profile.TreeMinHeight, profile.TreeMaxHeight));
        var maxHeight = Math.Max(minHeight, Math.Max(profile.TreeMinHeight, profile.TreeMaxHeight));
        return minHeight + PositiveMod(StableHash(seed ^ unchecked((int)0x32D1A04D), tileX), maxHeight - minHeight + 1);
    }

    private static TileInstance LitPassThroughTile(ushort tileId)
    {
        var tile = TileInstance.FromTileId(tileId, TileFlags.IsNatural, isSolid: false);
        tile.Light = 255;
        return tile;
    }

    private static int ComputeDirtDepth(WorldGenerationProfile profile, int seed, int tileX)
    {
        var dirtDepthMin = Math.Max(1, Math.Min(profile.DirtDepthMin, profile.DirtDepthMax));
        var dirtDepthMax = Math.Max(dirtDepthMin, Math.Max(profile.DirtDepthMin, profile.DirtDepthMax));
        return dirtDepthMin + Math.Abs(StableHash(seed, tileX) % (dirtDepthMax - dirtDepthMin + 1));
    }

    private static bool ShouldCarveCave(
        WorldGenerationProfile profile,
        WorldDimensionDefinition dimension,
        INoiseService noise,
        int surfaceY,
        int tileX,
        int tileY)
    {
        if (tileY < surfaceY + Math.Max(4, profile.CaveMinDepthOffset))
        {
            return false;
        }

        var depth = Math.Clamp((tileY - surfaceY) / (float)Math.Max(1, profile.HeightTiles - surfaceY), 0f, 1f);
        var caveNoise = noise.GetNoise(tileX * 2.5f, tileY * 2.5f);
        var tunnelNoise = noise.GetNoise(tileX * 0.55f + 800, tileY * 0.8f - 300);
        var threshold = 0.68f - depth * 0.12f - Math.Clamp(dimension.CaveMultiplier - 1f, -0.5f, 1.5f) * 0.08f;
        return caveNoise > threshold && tunnelNoise > -0.2f;
    }

    private static bool ShouldFillWaterPocket(
        WorldGenerationProfile profile,
        WorldDimensionDefinition dimension,
        int seed,
        int surfaceY,
        int tileX,
        int tileY)
    {
        if (profile.WaterPocketAttempts <= 0 || tileY < surfaceY + Math.Max(0, profile.WaterMinDepthOffset))
        {
            return false;
        }

        var cellSize = 48;
        var cellX = FloorDiv(tileX, cellSize);
        var cellY = FloorDiv(tileY, cellSize);
        var cellsWide = Math.Max(1, (int)MathF.Ceiling(profile.WidthTiles / (float)cellSize));
        var cellsTall = Math.Max(1, (int)MathF.Ceiling(profile.HeightTiles / (float)cellSize));
        var chance = Math.Clamp(
            profile.WaterPocketAttempts / (float)(cellsWide * cellsTall) * Math.Clamp(dimension.CaveMultiplier, 0.25f, 2.5f),
            0f,
            0.75f);

        for (var y = cellY - 1; y <= cellY + 1; y++)
        {
            for (var x = cellX - 1; x <= cellX + 1; x++)
            {
                var roll = StableUnit(seed ^ unchecked((int)0x6C8E9CF5), x, y);
                if (roll > chance)
                {
                    continue;
                }

                var centerHash = StableHash(seed ^ unchecked((int)0x4F1BBCDC), x, y);
                var centerX = x * cellSize + PositiveMod(centerHash, cellSize);
                var centerY = y * cellSize + PositiveMod(StableHash(centerHash, y), cellSize);
                if (centerY < surfaceY + profile.WaterMinDepthOffset)
                {
                    continue;
                }

                var radiusX = RandomRange(profile.WaterMinRadiusX, profile.WaterMaxRadiusX, seed ^ unchecked((int)0x165667B1), x, y);
                var radiusY = RandomRange(profile.WaterMinRadiusY, profile.WaterMaxRadiusY, seed ^ unchecked((int)0x27D4EB2F), x, y);
                var normalizedX = (tileX - centerX) / (float)Math.Max(1, radiusX);
                var normalizedY = (tileY - centerY) / (float)Math.Max(1, radiusY);
                if (normalizedX * normalizedX + normalizedY * normalizedY <= 1f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ushort MaybePlaceOre(
        WorldGenerationProfile profile,
        WorldDimensionDefinition dimension,
        INoiseService noise,
        ushort currentTileId,
        int surfaceY,
        int tileX,
        int tileY)
    {
        foreach (var ore in profile.Ores)
        {
            if (!ore.CanGenerate || currentTileId != ore.ReplaceTileId)
            {
                continue;
            }

            var minY = surfaceY + Math.Max(0, ore.MinDepthOffset);
            var maxY = ore.MaxDepthOffset > 0 ? surfaceY + ore.MaxDepthOffset : profile.HeightTiles - 1;
            if (tileY < minY || tileY > maxY)
            {
                continue;
            }

            var oreNoise = noise.GetNoise(tileX * 3.75f + ore.TileId * 37, tileY * 3.75f - ore.TileId * 19);
            var veinNoise = noise.GetNoise(tileX * 0.85f - ore.TileId * 23, tileY * 0.85f + ore.TileId * 41);
            var density = Math.Clamp(ore.VeinCount / 120f, 0.02f, 0.65f) * Math.Clamp(dimension.OreMultiplier, 0f, 4f);
            var threshold = 0.84f - density * 0.22f;
            if (oreNoise > threshold && veinNoise > 0.05f)
            {
                return ore.TileId;
            }
        }

        return currentTileId;
    }

    private static IReadOnlyList<WorldDimensionDefinition> ResolveDimensions(WorldGenerationProfile profile)
    {
        if (profile.Dimensions.Count > 0)
        {
            return profile.Dimensions
                .OrderBy(dimension => dimension.MinTileY)
                .ToArray();
        }

        return CreateDefaultDimensions(profile.HeightTiles);
    }

    private static WorldDimensionDefinition ResolveDimension(IReadOnlyList<WorldDimensionDefinition> dimensions, int tileY)
    {
        foreach (var dimension in dimensions)
        {
            if (dimension.Contains(tileY))
            {
                return dimension;
            }
        }

        return tileY < dimensions[0].MinTileY
            ? dimensions[0]
            : dimensions[^1];
    }

    private static IReadOnlyList<WorldDimensionDefinition> CreateDefaultDimensions(int heightTiles)
    {
        var skyEnd = Math.Max(0, heightTiles / 5);
        var surfaceEnd = Math.Max(skyEnd, heightTiles / 2);
        var deepEnd = Math.Max(surfaceEnd, heightTiles * 4 / 5);

        return new[]
        {
            new WorldDimensionDefinition
            {
                Id = "sky",
                DisplayName = "Sky",
                MinTileY = 0,
                MaxTileYInclusive = skyEnd,
                SurfaceTileId = KnownTileIds.Grass,
                SubsurfaceTileId = KnownTileIds.Dirt,
                FillTileId = KnownTileIds.Stone,
                CaveMultiplier = 0.2f,
                OreMultiplier = 0.1f,
                AmbientLight = 255
            },
            new WorldDimensionDefinition
            {
                Id = "surface",
                DisplayName = "Surface",
                MinTileY = skyEnd + 1,
                MaxTileYInclusive = surfaceEnd,
                SurfaceTileId = KnownTileIds.Grass,
                SubsurfaceTileId = KnownTileIds.Dirt,
                FillTileId = KnownTileIds.Stone,
                CaveMultiplier = 0.8f,
                OreMultiplier = 0.7f,
                AmbientLight = 160,
                AllowsSurfaceTrees = true
            },
            new WorldDimensionDefinition
            {
                Id = "underground",
                DisplayName = "Underground",
                MinTileY = surfaceEnd + 1,
                MaxTileYInclusive = deepEnd,
                SurfaceTileId = KnownTileIds.Stone,
                SubsurfaceTileId = KnownTileIds.Stone,
                FillTileId = KnownTileIds.Stone,
                CaveMultiplier = 1.15f,
                OreMultiplier = 1.2f,
                AmbientLight = 42
            },
            new WorldDimensionDefinition
            {
                Id = "deep",
                DisplayName = "Deep",
                MinTileY = deepEnd + 1,
                MaxTileYInclusive = heightTiles - 1,
                SurfaceTileId = KnownTileIds.Stone,
                SubsurfaceTileId = KnownTileIds.Stone,
                FillTileId = KnownTileIds.Stone,
                CaveMultiplier = 1.45f,
                OreMultiplier = 1.55f,
                AmbientLight = 10
            }
        };
    }

    private static int StableHash(int seed, int x)
    {
        unchecked
        {
            var hash = seed;
            hash = (hash * 397) ^ x;
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            return hash;
        }
    }

    private static int StableHash(int seed, int x, int y)
    {
        unchecked
        {
            var hash = seed;
            hash = (hash * 397) ^ x;
            hash = (hash * 397) ^ y;
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            return hash;
        }
    }

    private static float StableUnit(int seed, int x)
    {
        return PositiveMod(StableHash(seed, x), 10_000) / 10_000f;
    }

    private static float StableUnit(int seed, int x, int y)
    {
        return PositiveMod(StableHash(seed, x, y), 10_000) / 10_000f;
    }

    private static int RandomRange(int min, int max, int seed, int x, int y)
    {
        var low = Math.Max(1, Math.Min(min, max));
        var high = Math.Max(low, Math.Max(min, max));
        return low + PositiveMod(StableHash(seed, x, y), high - low + 1);
    }

    private static int FloorDiv(int value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;
        if (remainder != 0 && ((remainder < 0) != (divisor < 0)))
        {
            quotient--;
        }

        return quotient;
    }

    private static int PositiveMod(int value, int divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + Math.Abs(divisor) : remainder;
    }
}
