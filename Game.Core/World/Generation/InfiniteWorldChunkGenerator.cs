namespace Game.Core.World.Generation;

public sealed class InfiniteWorldChunkGenerator
{
    internal const int TreeCenterExclusionRadius = TreeSilhouettePlanner.MaximumHalfWidth * 2;

    private readonly Func<int, INoiseService> _noiseFactory;
    private readonly WorldRegionPlanner? _regionalPlanner;
    private readonly int _generationVersion;

    public InfiniteWorldChunkGenerator(
        Func<int, INoiseService>? noiseFactory = null,
        WorldRegionPlanner? regionalPlanner = null,
        int generationVersion = WorldGenerationVersions.Current)
    {
        _noiseFactory = noiseFactory ?? (seed => new FastNoiseLiteNoiseService(seed));
        _regionalPlanner = regionalPlanner;
        _generationVersion = WorldGenerationVersions.Normalize(generationVersion);
    }

    public World CreateWorld(WorldGenerationProfile profile, int seed, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var world = new World(
            widthTiles: GameConstants.ChunkSize,
            heightTiles: profile.HeightTiles,
            WorldMetadata.CreateDefault(seed) with
            {
                Name = name ?? "Infinite World",
                GenerationVersion = _generationVersion,
                GenerationProfileId = profile.Id
            },
            isHorizontallyInfinite: true);

        var spawnX = 0;
        var spawnY = Math.Max(0, ComputeSurfaceHeight(
            profile,
            _noiseFactory(seed),
            seed,
            spawnX,
            _regionalPlanner?.PlanAtTileX(spawnX),
            _generationVersion) - 2);
        world.SetMetadata(world.Metadata with { SpawnTile = new TilePos(spawnX, spawnY) });
        return world;
    }

    public Chunk GenerateChunk(WorldGenerationProfile profile, int seed, ChunkPos position)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var chunk = new Chunk(position);
        var tiles = new TileInstance[GameConstants.ChunkSize * GameConstants.ChunkSize];
        FillTiles(profile, seed, position, tiles, _generationVersion);
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
        FillTiles(
            profile,
            world.Metadata.Seed,
            position,
            tiles,
            world.Metadata.GenerationVersion);
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

    public int GetSurfaceHeightAt(WorldGenerationProfile profile, int seed, int tileX)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return ComputeSurfaceHeight(
            profile,
            _noiseFactory(seed),
            seed,
            tileX,
            _regionalPlanner?.PlanAtTileX(tileX),
            _generationVersion);
    }

    public Func<int, int> CreateSurfaceHeightResolver(WorldGenerationProfile profile, int seed)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var noise = _noiseFactory(seed);
        var regionalPlanner = _regionalPlanner;
        WorldRegionPlan? cachedRegion = null;
        const int cacheCapacity = 2_048;
        var cachedTileXs = new int[cacheCapacity];
        var cachedHeights = new int[cacheCapacity];
        var cacheEntries = new byte[cacheCapacity];
        return tileX =>
        {
            var cacheIndex = tileX & (cacheCapacity - 1);
            if (cacheEntries[cacheIndex] != 0 && cachedTileXs[cacheIndex] == tileX)
            {
                return cachedHeights[cacheIndex];
            }

            if (regionalPlanner is not null &&
                (cachedRegion is null || !cachedRegion.ContainsTileX(tileX)))
            {
                cachedRegion = regionalPlanner.PlanAtTileX(tileX);
            }

            var height = ComputeSurfaceHeight(
                profile,
                noise,
                seed,
                tileX,
                cachedRegion,
                _generationVersion);
            cachedTileXs[cacheIndex] = tileX;
            cachedHeights[cacheIndex] = height;
            cacheEntries[cacheIndex] = 1;
            return height;
        };
    }
    private void FillTiles(
        WorldGenerationProfile profile,
        int seed,
        ChunkPos position,
        TileInstance[] tiles,
        int generationVersion)
    {
        var noise = _noiseFactory(seed);
        var dimensions = ResolveDimensions(profile);
        var tileXs = new int[GameConstants.ChunkSize];
        var regionPlans = new WorldRegionPlan?[GameConstants.ChunkSize];
        for (var localX = 0; localX < GameConstants.ChunkSize; localX++)
        {
            tileXs[localX] = SaturateToInt((long)position.X * GameConstants.ChunkSize + localX);
            regionPlans[localX] = _regionalPlanner?.PlanAtTileX(tileXs[localX]);
        }

        for (var localY = 0; localY < GameConstants.ChunkSize; localY++)
        {
            var tileY = SaturateToInt((long)position.Y * GameConstants.ChunkSize + localY);
            for (var localX = 0; localX < GameConstants.ChunkSize; localX++)
            {
                var index = localY * GameConstants.ChunkSize + localX;
                var region = regionPlans[localX];
                var biome = region is null
                    ? null
                    : _regionalPlanner?.ResolveBiome(
                        region,
                        tileXs[localX],
                        Math.Clamp(tileY, 0, profile.HeightTiles - 1));
                tiles[index] = GenerateTile(
                    profile,
                    dimensions,
                    noise,
                    seed,
                    tileXs[localX],
                    tileY,
                    region,
                    biome,
                    generationVersion);
            }
        }
    }

    private TileInstance GenerateTile(
        WorldGenerationProfile profile,
        IReadOnlyList<WorldDimensionDefinition> dimensions,
        INoiseService noise,
        int seed,
        int tileX,
        int tileY,
        WorldRegionPlan? region,
        WorldBiomeResolution? biome,
        int generationVersion)
    {
        if (tileY < 0 || tileY >= profile.HeightTiles)
        {
            return TileInstance.Air;
        }

        var dimension = ResolveDimension(dimensions, tileY);
        var surfaceY = ComputeSurfaceHeight(profile, noise, seed, tileX, region, generationVersion);
        if (TryGenerateStructureTile(
                profile,
                dimensions,
                noise,
                seed,
                tileX,
                tileY,
                region,
                generationVersion) is { } structureTile)
        {
            return structureTile;
        }

        if (TryGenerateTreeTile(
                profile,
                dimensions,
                noise,
                seed,
                tileX,
                tileY,
                region,
                generationVersion) is { } treeTile)
        {
            return treeTile;
        }

        if (tileY < surfaceY)
        {
            return new TileInstance { TileId = KnownTileIds.Air, Light = 255 };
        }

        var dirtDepth = ComputeDirtDepth(profile, seed, tileX, region, generationVersion);
        var surfaceTileId = ResolveBiomeTileId(biome?.Biome.SurfaceTile, dimension.SurfaceTileId);
        var undergroundTileId = ResolveBiomeTileId(biome?.Biome.UndergroundTile, dimension.SubsurfaceTileId);
        var tileId = tileY == surfaceY
            ? surfaceTileId
            : tileY < surfaceY + dirtDepth
                ? undergroundTileId
                : dimension.FillTileId;

        var isCave = biome?.IsCave == true || IsInsidePlannedCave(region, tileX, tileY) ||
            ShouldCarveCave(profile, dimension, noise, surfaceY, tileX, tileY, biome);
        var isWaterPocket = ShouldFillWaterPocket(profile, dimension, seed, surfaceY, tileX, tileY);
        if (isCave || isWaterPocket)
        {
            var tile = isWaterPocket ? TileInstance.Liquid(255) : TileInstance.Air;
            tile.Light = dimension.AmbientLight;
            return tile;
        }

        tileId = MaybePlaceOre(
            profile,
            dimension,
            noise,
            tileId,
            surfaceY,
            tileX,
            tileY,
            biome?.Biome.Resources.OreDensityMultiplier ?? 1f);
        var generated = TileInstance.FromTileId(tileId, TileFlags.IsNatural);
        generated.Light = dimension.AmbientLight;
        return generated;
    }

    private static TileInstance? TryGenerateStructureTile(
        WorldGenerationProfile profile,
        IReadOnlyList<WorldDimensionDefinition> dimensions,
        INoiseService noise,
        int seed,
        int tileX,
        int tileY,
        WorldRegionPlan? region,
        int generationVersion)
    {
        if (region is null || region.Structures.Count == 0)
        {
            return null;
        }

        for (var index = 0; index < region.Structures.Count; index++)
        {
            var structure = region.Structures[index];
            if (!structure.HasMaterializedTemplate)
            {
                continue;
            }

            if (tileX < structure.TileX ||
                (long)tileX >= structure.TileX + structure.WidthTiles)
            {
                continue;
            }

            var originTileY = ResolveStructureOriginTileY(
                profile,
                noise,
                seed,
                region,
                structure,
                generationVersion);
            if (!StructureTemplateMaterializer.TryResolveTile(
                    structure,
                    tileX,
                    tileY,
                    originTileY,
                    out var tileId))
            {
                continue;
            }

            if (!TryResolveKnownTileId(tileId, out var numericTileId))
            {
                continue;
            }
            if (numericTileId == KnownTileIds.Air)
            {
                var air = TileInstance.Air;
                air.Light = tileY < ComputeSurfaceHeight(
                    profile,
                    noise,
                    seed,
                    tileX,
                    region,
                    generationVersion)
                    ? byte.MaxValue
                    : ResolveDimension(dimensions, tileY).AmbientLight;
                return air;
            }

            return TileInstance.FromTileId(numericTileId);
        }

        return null;
    }

    private static int ResolveStructureOriginTileY(
        WorldGenerationProfile profile,
        INoiseService noise,
        int seed,
        WorldRegionPlan region,
        PlannedStructure structure,
        int generationVersion)
    {
        if (structure.Placement.Equals("surface", StringComparison.OrdinalIgnoreCase))
        {
            var anchorX = SaturateToInt(structure.TileX);
            return ComputeSurfaceHeight(
                profile,
                noise,
                seed,
                anchorX,
                region,
                generationVersion) - structure.HeightTiles;
        }

        if (structure.Placement.Contains("cave", StringComparison.OrdinalIgnoreCase))
        {
            return structure.TileY - structure.HeightTiles / 2;
        }

        return structure.TileY;
    }

    private static int ComputeSurfaceHeight(
        WorldGenerationProfile profile,
        INoiseService noise,
        int seed,
        int tileX,
        WorldRegionPlan? region,
        int generationVersion)
    {
        var baseSurfaceY = Math.Clamp(profile.SurfaceBaseY, 6, profile.HeightTiles - 10);
        var elevationMultiplier = (region?.Biome.Terrain.ElevationMultiplier ?? 1f) *
            (region?.SubBiome?.ElevationMultiplier ?? 1f);
        if (WorldGenerationVersions.Normalize(generationVersion) >= WorldGenerationVersions.TerrariaTopology)
        {
            return WorldSurfaceSampler.GetSurfaceHeight(profile, seed, tileX, elevationMultiplier);
        }

        var amplitude = Math.Max(1, (int)MathF.Round(profile.SurfaceAmplitude * elevationMultiplier));
        var continental = noise.GetNoise(tileX * 0.65f, seed * 0.013f);
        var detail = noise.GetNoise(tileX * 2.25f + 91, seed * 0.021f) * 0.35f;
        var surfaceY = baseSurfaceY + (int)MathF.Round((continental + detail) * amplitude);
        return Math.Clamp(surfaceY, Math.Max(3, profile.HeightTiles / 8), Math.Max(4, profile.HeightTiles / 2));
    }

    private TileInstance? TryGenerateTreeTile(
        WorldGenerationProfile profile,
        IReadOnlyList<WorldDimensionDefinition> dimensions,
        INoiseService noise,
        int seed,
        int tileX,
        int tileY,
        WorldRegionPlan? region,
        int generationVersion)
    {
        if (tileY <= 0)
        {
            return null;
        }

        var firstCenter = Math.Max(int.MinValue, (long)tileX - TreeSilhouettePlanner.MaximumHalfWidth);
        var lastCenter = Math.Min(int.MaxValue, (long)tileX + TreeSilhouettePlanner.MaximumHalfWidth);
        for (var centerValue = firstCenter; centerValue <= lastCenter; centerValue++)
        {
            var centerX = (int)centerValue;
            var treeRegion = region is not null && region.ContainsTileX(centerX)
                ? region
                : _regionalPlanner?.PlanAtTileX(centerX);
            var surfaceY = ComputeSurfaceHeight(
                profile,
                noise,
                seed,
                centerX,
                treeRegion,
                generationVersion);
            if (tileY >= surfaceY)
            {
                continue;
            }

            var surfaceDimension = ResolveDimension(dimensions, surfaceY);
            if (!surfaceDimension.AllowsSurfaceTrees || surfaceDimension.SurfaceTileId != KnownTileIds.Grass)
            {
                continue;
            }

            if (!ShouldGrowTreeAt(profile, seed, centerX, treeRegion))
            {
                continue;
            }

            var height = ComputeTreeHeight(profile, seed, centerX);
            var topY = surfaceY - height;
            if (topY <= TreeSilhouettePlanner.TopPadding)
            {
                continue;
            }

            var variation = StableHash(seed ^ unchecked((int)0x6A09E667), centerX);
            var cell = TreeSilhouettePlanner.Classify(
                tileX - centerX,
                tileY - topY,
                height,
                variation,
                generationVersion);
            var material = TreeMaterialResolver.Resolve(treeRegion?.Biome);
            if (cell == TreeSilhouetteCell.Trunk)
            {
                return LitPassThroughTile(material.TrunkTileId);
            }

            if (cell == TreeSilhouetteCell.Leaves)
            {
                return LitPassThroughTile(material.CanopyTileId);
            }
        }

        return null;
    }

    internal bool ShouldGrowTreeAt(
        WorldGenerationProfile profile,
        int seed,
        int tileX,
        WorldRegionPlan? region)
    {
        if (profile.TreeAttempts <= 0 || profile.TreeAttemptChance <= 0f)
        {
            return false;
        }

        var density = ResolveTreeDensity(profile, region);

        if (StableUnit(seed ^ unchecked((int)0x51D7348F), tileX) >= density)
        {
            return false;
        }

        var priority = unchecked((uint)StableHash(seed ^ unchecked((int)0x71E2A9D5), tileX));
        for (var offset = -TreeCenterExclusionRadius; offset <= TreeCenterExclusionRadius; offset++)
        {
            if (offset == 0)
            {
                continue;
            }

            var neighborX = SaturateToInt((long)tileX + offset);
            if (neighborX == tileX)
            {
                continue;
            }

            var neighborRegion = region is not null && region.ContainsTileX(neighborX)
                ? region
                : _regionalPlanner?.PlanAtTileX(neighborX);
            var neighborDensity = ResolveTreeDensity(profile, neighborRegion);
            if (StableUnit(seed ^ unchecked((int)0x51D7348F), neighborX) >= neighborDensity)
            {
                continue;
            }

            var neighborPriority = unchecked((uint)StableHash(
                seed ^ unchecked((int)0x71E2A9D5),
                neighborX));
            if (neighborPriority < priority || (neighborPriority == priority && neighborX < tileX))
            {
                return false;
            }
        }

        return true;
    }

    private static float ResolveTreeDensity(WorldGenerationProfile profile, WorldRegionPlan? region)
    {
        var density = Math.Clamp(
            profile.TreeAttempts * Math.Clamp(profile.TreeAttemptChance, 0f, 1f) / Math.Max(1f, profile.WidthTiles),
            0f,
            0.2f);
        return Math.Clamp(density * (region?.Biome.Terrain.FeatureDensityMultiplier ?? 1f), 0f, 0.3f);
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

    private static int ComputeDirtDepth(
        WorldGenerationProfile profile,
        int seed,
        int tileX,
        WorldRegionPlan? region,
        int generationVersion)
    {
        if (WorldGenerationVersions.Normalize(generationVersion) >= WorldGenerationVersions.TerrariaTopology)
        {
            return WorldSurfaceSampler.GetDirtDepth(
                profile,
                seed,
                tileX,
                region?.Biome.Terrain.SoilDepthMultiplier ?? 1f);
        }

        var dirtDepthMin = Math.Max(1, Math.Min(profile.DirtDepthMin, profile.DirtDepthMax));
        var dirtDepthMax = Math.Max(dirtDepthMin, Math.Max(profile.DirtDepthMin, profile.DirtDepthMax));
        var baseDepth = dirtDepthMin + Math.Abs(StableHash(seed, tileX) % (dirtDepthMax - dirtDepthMin + 1));
        return Math.Max(1, (int)MathF.Round(baseDepth * (region?.Biome.Terrain.SoilDepthMultiplier ?? 1f)));
    }

    private static bool ShouldCarveCave(
        WorldGenerationProfile profile,
        WorldDimensionDefinition dimension,
        INoiseService noise,
        int surfaceY,
        int tileX,
        int tileY,
        WorldBiomeResolution? biome)
    {
        if (tileY < surfaceY + Math.Max(4, profile.CaveMinDepthOffset))
        {
            return false;
        }

        var depth = Math.Clamp((tileY - surfaceY) / (float)Math.Max(1, profile.HeightTiles - surfaceY), 0f, 1f);
        var caveNoise = noise.GetNoise(tileX * 2.5f, tileY * 2.5f);
        var tunnelNoise = noise.GetNoise(tileX * 0.55f + 800, tileY * 0.8f - 300);
        var regionalCaveMultiplier = (biome?.Biome.Terrain.CaveDensityMultiplier ?? 1f) *
            (biome?.SubBiome?.CaveDensityMultiplier ?? 1f);
        var threshold = 0.68f - depth * 0.12f -
            Math.Clamp(dimension.CaveMultiplier * regionalCaveMultiplier - 1f, -0.5f, 2.5f) * 0.08f;
        return caveNoise > threshold && tunnelNoise > -0.2f;
    }

    private static bool IsInsidePlannedCave(WorldRegionPlan? region, int tileX, int tileY)
    {
        if (region is null)
        {
            return false;
        }

        for (var index = 0; index < region.Caves.Count; index++)
        {
            if (region.Caves[index].Contains(tileX, tileY))
            {
                return true;
            }
        }

        return false;
    }

    private static ushort ResolveBiomeTileId(string? tileId, ushort fallback)
    {
        return tileId is not null && TryResolveKnownTileId(tileId, out var resolved)
            ? resolved
            : fallback;
    }

    private static bool TryResolveKnownTileId(string tileId, out ushort resolved)
    {
        return KnownTileIds.TryResolveContentId(tileId, out resolved);
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
                var centerX = SaturateToInt((long)x * cellSize + PositiveMod(centerHash, cellSize));
                var centerY = SaturateToInt((long)y * cellSize + PositiveMod(StableHash(centerHash, y), cellSize));
                if (centerY < surfaceY + profile.WaterMinDepthOffset)
                {
                    continue;
                }

                var radiusX = RandomRange(profile.WaterMinRadiusX, profile.WaterMaxRadiusX, seed ^ unchecked((int)0x165667B1), x, y);
                var radiusY = RandomRange(profile.WaterMinRadiusY, profile.WaterMaxRadiusY, seed ^ unchecked((int)0x27D4EB2F), x, y);
                var normalizedX = ((long)tileX - centerX) / (float)Math.Max(1, radiusX);
                var normalizedY = ((long)tileY - centerY) / (float)Math.Max(1, radiusY);
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
        int tileY,
        float regionalOreDensityMultiplier)
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
            var density = Math.Clamp(ore.VeinCount / 120f, 0.02f, 0.65f) *
                Math.Clamp(dimension.OreMultiplier, 0f, 4f) *
                Math.Clamp(regionalOreDensityMultiplier, 0f, 4f);
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

    private static int SaturateToInt(long value)
    {
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }
}
