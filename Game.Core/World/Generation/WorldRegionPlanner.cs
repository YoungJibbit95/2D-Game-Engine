using Game.Core.Biomes;

namespace Game.Core.World.Generation;

public sealed class WorldRegionPlanner
{
    private const ulong BiomeSalt = 0xB10B1EUL;
    private const ulong SubBiomeSalt = 0x5AB10B1EUL;
    private const ulong CaveSalt = 0xCA7EUL;
    private readonly int _seed;
    private readonly RegionalGenerationProfile _profile;
    private readonly IReadOnlyList<BiomeDefinition> _regionalBiomes;
    private readonly IReadOnlyDictionary<string, BiomeDefinition> _biomesById;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<BiomeDefinition>> _layerBiomesById;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<SubBiomeDefinition>> _subBiomesByBiomeId;
    private readonly IReadOnlyList<StructurePlanDefinition> _structures;

    public WorldRegionPlanner(
        int seed,
        RegionalGenerationProfile profile,
        BiomeRegistry biomes,
        IReadOnlyList<StructurePlanDefinition>? structures = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(biomes);
        if (profile.RegionWidthTiles <= 0)
        {
            throw new ArgumentException("Region width must be positive.", nameof(profile));
        }

        _regionalBiomes = biomes.Definitions
            .Where(value => value.IsRegionalBiome)
            .OrderBy(value => value.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (_regionalBiomes.Count == 0)
        {
            throw new ArgumentException("At least one regional biome is required.", nameof(biomes));
        }

        _biomesById = biomes.Definitions.ToDictionary(value => value.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var layer in profile.BiomeLayers)
        {
            foreach (var biomeId in layer.BiomeIds)
            {
                if (!_biomesById.ContainsKey(biomeId))
                {
                    throw new ArgumentException(
                        $"Regional biome layer '{layer.Id}' references unknown biome '{biomeId}'.",
                        nameof(profile));
                }
            }
        }

        _layerBiomesById = profile.BiomeLayers.ToDictionary(
            layer => layer.Id,
            layer => (IReadOnlyList<BiomeDefinition>)layer.BiomeIds
                .Select(id => _biomesById[id])
                .OrderBy(value => value.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
        _subBiomesByBiomeId = biomes.Definitions.ToDictionary(
            biome => biome.Id,
            biome => (IReadOnlyList<SubBiomeDefinition>)biome.SubBiomes
                .OrderBy(value => value.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);

        _seed = seed;
        _profile = profile;
        _structures = structures ?? Array.Empty<StructurePlanDefinition>();
    }

    public WorldRegionPlan PlanAtTileX(long tileX)
    {
        return PlanRegion(DeterministicCoordinateHash.FloorDivide(tileX, _profile.RegionWidthTiles));
    }

    public WorldRegionPlan PlanRegion(long regionIndex)
    {
        var (startX, endX) = GetRegionBounds(regionIndex);
        var selection = SelectBiome(regionIndex);
        var caves = PlanCaves(regionIndex, startX, endX, selection);
        return new WorldRegionPlan(
            regionIndex,
            startX,
            endX,
            selection.Biome,
            selection.SubBiome,
            caves,
            PlanFeatures(regionIndex, startX, endX, selection),
            PlanStructures(regionIndex, startX, endX, selection, caves));
    }

    public WorldBiomeResolution ResolveBiome(long tileX, int tileY)
    {
        if (tileY < 0 || tileY >= _profile.WorldHeightTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(tileY));
        }

        var plan = PlanAtTileX(tileX);
        return ResolveBiome(plan, tileX, tileY);
    }

    public WorldBiomeResolution ResolveBiome(WorldRegionPlan plan, long tileX, int tileY)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.ContainsTileX(tileX))
        {
            throw new ArgumentOutOfRangeException(nameof(tileX));
        }

        if (tileY < 0 || tileY >= _profile.WorldHeightTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(tileY));
        }

        CaveRegionPlan? cave = null;
        for (var index = 0; index < plan.Caves.Count; index++)
        {
            if (plan.Caves[index].Contains(tileX, tileY))
            {
                cave = plan.Caves[index];
                break;
            }
        }
        if (cave is not null && _biomesById.TryGetValue(cave.ProfileId, out var caveBiome))
        {
            var layer = FindLayer(caveBiome.Id, tileY, requiresCave: true);
            return CreateResolution(
                plan,
                tileX,
                tileY,
                caveBiome,
                SelectSubBiome(caveBiome, plan.RegionIndex, DeterministicCoordinateHash.Salt(cave.Id)),
                layer?.Id ?? "cave",
                cave);
        }

        var layerSelection = SelectLayerBiome(plan.RegionIndex, tileX, tileY, cave is not null, 0);
        if (layerSelection is not null)
        {
            return CreateResolution(
                plan,
                tileX,
                tileY,
                layerSelection.Biome,
                layerSelection.SubBiome,
                layerSelection.Layer.Id,
                cave);
        }

        return CreateResolution(
            plan,
            tileX,
            tileY,
            plan.Biome,
            plan.SubBiome,
            "surface",
            cave);
    }

    private WorldBiomeResolution CreateResolution(
        WorldRegionPlan plan,
        long tileX,
        int tileY,
        BiomeDefinition biome,
        SubBiomeDefinition? subBiome,
        string layerId,
        CaveRegionPlan? cave)
    {
        return new WorldBiomeResolution(
            plan.RegionIndex,
            tileX,
            tileY,
            plan.Biome.Id,
            biome,
            subBiome,
            layerId,
            cave is not null,
            cave?.Id);
    }

    private BiomeSelection SelectBiome(long regionIndex)
    {
        var biomeCell = DeterministicCoordinateHash.FloorDivide(regionIndex, _profile.BiomeSpanRegions);
        var biome = SelectWeighted(
            _regionalBiomes,
            value => value.SelectionWeight,
            DeterministicCoordinateHash.Hash(_seed, biomeCell, salt: BiomeSalt));
        return new BiomeSelection(
            biome,
            SelectSubBiome(biome, regionIndex, SubBiomeSalt));
    }

    private SubBiomeDefinition? SelectSubBiome(BiomeDefinition biome, long regionIndex, ulong salt)
    {
        if (!_subBiomesByBiomeId.TryGetValue(biome.Id, out var candidates) || candidates.Count == 0)
        {
            return null;
        }

        return SelectWeighted(
            candidates,
            value => value.SelectionWeight,
            DeterministicCoordinateHash.Hash(_seed, regionIndex, salt: salt));
    }

    private IReadOnlyList<CaveRegionPlan> PlanCaves(
        long regionIndex,
        long startX,
        long endX,
        BiomeSelection selection)
    {
        var density = selection.Biome.Terrain.CaveDensityMultiplier *
            (selection.SubBiome?.CaveDensityMultiplier ?? 1f);
        var count = Math.Clamp((int)MathF.Round(_profile.CaveRegionAttempts * density), 0, 16);
        var result = new CaveRegionPlan[count];
        var maxDepth = Math.Min(_profile.CaveMaxDepth, _profile.WorldHeightTiles - 1);
        for (var index = 0; index < count; index++)
        {
            var salt = CaveSalt + (ulong)index * 17UL;
            var centerX = DeterministicCoordinateHash.LongRange(_seed, regionIndex, index, salt, startX, endX);
            var centerY = DeterministicCoordinateHash.Range(
                _seed,
                regionIndex,
                index,
                salt + 1,
                Math.Min(_profile.CaveMinDepth, maxDepth),
                maxDepth);
            var radiusX = DeterministicCoordinateHash.Range(
                _seed, regionIndex, index, salt + 2, _profile.CaveMinRadiusX, _profile.CaveMaxRadiusX);
            var radiusY = DeterministicCoordinateHash.Range(
                _seed, regionIndex, index, salt + 3, _profile.CaveMinRadiusY, _profile.CaveMaxRadiusY);
            var layer = SelectLayerBiome(regionIndex, centerX, centerY, isCave: true, index);
            result[index] = new CaveRegionPlan(
                $"{regionIndex}:cave:{index}",
                layer?.Biome.Id ?? selection.SubBiome?.CaveProfileId ?? "default_cave",
                centerX,
                centerY,
                radiusX,
                radiusY,
                Math.Clamp((layer?.Biome ?? selection.Biome).Climate.Humidity + 0.2f, 0f, 1f),
                Math.Clamp((layer?.Biome ?? selection.Biome).Ambient.BaseLight * 0.18f, 0.02f, 0.4f));
        }

        return result;
    }

    private IReadOnlyList<PlannedWorldFeature> PlanFeatures(
        long regionIndex,
        long startX,
        long endX,
        BiomeSelection selection)
    {
        var result = new List<PlannedWorldFeature>();
        foreach (var feature in _profile.Features.OrderBy(value => value.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (!IsAllowed(feature.AllowedBiomeIds, feature.AllowedSubBiomeIds, selection))
            {
                continue;
            }

            var salt = DeterministicCoordinateHash.Salt(feature.Id);
            var chance = Math.Clamp(
                feature.ChancePerRegion * selection.Biome.Terrain.FeatureDensityMultiplier,
                0f,
                1f);
            if (DeterministicCoordinateHash.Unit(_seed, regionIndex, salt: salt) >= chance)
            {
                continue;
            }

            var count = DeterministicCoordinateHash.Range(
                _seed, regionIndex, 0, salt + 1, feature.MinCount, feature.MaxCount);
            var minY = Math.Clamp(feature.MinTileY, 0, _profile.WorldHeightTiles - 1);
            var maxY = Math.Clamp(feature.MaxTileY, minY, _profile.WorldHeightTiles - 1);
            for (var index = 0; index < count; index++)
            {
                var tileX = DeterministicCoordinateHash.LongRange(
                    _seed, regionIndex, index, salt + 2, startX, endX);
                if (result.Any(existing =>
                    existing.DefinitionId == feature.Id &&
                    Math.Abs(existing.TileX - tileX) < feature.MinSpacingTiles))
                {
                    continue;
                }

                result.Add(new PlannedWorldFeature(
                    feature.Id,
                    feature.Kind,
                    tileX,
                    DeterministicCoordinateHash.Range(_seed, regionIndex, index, salt + 3, minY, maxY),
                    selection.Biome.Id,
                    selection.SubBiome?.Id));
            }
        }

        return result;
    }

    private IReadOnlyList<PlannedStructure> PlanStructures(
        long regionIndex,
        long startX,
        long endX,
        BiomeSelection selection,
        IReadOnlyList<CaveRegionPlan> caves)
    {
        var result = new List<PlannedStructure>();
        foreach (var definition in _structures.OrderBy(value => value.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (!WinsRegionalElection(definition, regionIndex))
            {
                continue;
            }

            var availableWidth = (ulong)(endX - startX) + 1UL;
            if ((ulong)definition.WidthTiles > availableWidth)
            {
                continue;
            }

            var salt = DeterministicCoordinateHash.Salt(definition.Id);
            var maxX = endX - definition.WidthTiles + 1L;
            var minY = Math.Clamp(definition.MinTileY, 0, _profile.WorldHeightTiles - 1);
            var maxY = Math.Clamp(definition.MaxTileY, minY, _profile.WorldHeightTiles - 1);
            var tileX = DeterministicCoordinateHash.LongRange(_seed, regionIndex, 0, salt + 5, startX, maxX);
            var tileY = DeterministicCoordinateHash.Range(_seed, regionIndex, 0, salt + 6, minY, maxY);

            if (definition.Placement.Contains("cave", StringComparison.OrdinalIgnoreCase))
            {
                var eligibleCaves = caves
                    .Where(cave => cave.CenterTileY >= minY && cave.CenterTileY <= maxY)
                    .ToArray();
                if (eligibleCaves.Length == 0)
                {
                    continue;
                }

                var caveIndex = DeterministicCoordinateHash.Range(
                    _seed, regionIndex, 0, salt + 7, 0, eligibleCaves.Length - 1);
                tileX = Math.Clamp(eligibleCaves[caveIndex].CenterTileX, startX, maxX);
                tileY = eligibleCaves[caveIndex].CenterTileY;
            }
            else if (definition.Placement.Equals("surface", StringComparison.OrdinalIgnoreCase))
            {
                tileY = Math.Clamp(_profile.SurfaceBaseY, minY, maxY);
            }

            var temporaryPlan = new WorldRegionPlan(
                regionIndex,
                startX,
                endX,
                selection.Biome,
                selection.SubBiome,
                caves,
                Array.Empty<PlannedWorldFeature>(),
                Array.Empty<PlannedStructure>());
            var activeBiome = ResolveBiome(temporaryPlan, tileX, tileY);
            if (!IsAllowed(definition, activeBiome))
            {
                continue;
            }

            result.Add(new PlannedStructure(
                definition.Id,
                definition.TemplateId,
                definition.Placement,
                tileX,
                tileY,
                definition.WidthTiles,
                definition.HeightTiles)
            {
                Rows = definition.Rows,
                Legend = definition.Legend,
                TransparentSymbol = definition.TransparentSymbol[0]
            });
        }

        return result;
    }

    private LayerBiomeSelection? SelectLayerBiome(
        long regionIndex,
        long tileX,
        int tileY,
        bool isCave,
        int discriminator)
    {
        RegionalBiomeLayerDefinition? layer = null;
        for (var index = 0; index < _profile.BiomeLayers.Count; index++)
        {
            var candidate = _profile.BiomeLayers[index];
            if (candidate.RequiresCave != isCave ||
                tileY < candidate.MinTileY ||
                tileY > candidate.MaxTileYInclusive)
            {
                continue;
            }

            if (layer is null || candidate.Priority > layer.Priority ||
                (candidate.Priority == layer.Priority &&
                 StringComparer.OrdinalIgnoreCase.Compare(candidate.Id, layer.Id) < 0))
            {
                layer = candidate;
            }
        }

        if (layer is null)
        {
            return null;
        }

        var candidates = _layerBiomesById[layer.Id];
        var salt = DeterministicCoordinateHash.Salt($"biome-layer:{layer.Id}");
        var biome = SelectWeighted(
            candidates,
            value => value.SelectionWeight,
            DeterministicCoordinateHash.Hash(_seed, regionIndex, tileX ^ discriminator, salt));
        return new LayerBiomeSelection(
            layer,
            biome,
            SelectSubBiome(biome, regionIndex, salt + 1));
    }

    private RegionalBiomeLayerDefinition? FindLayer(string biomeId, int tileY, bool requiresCave)
    {
        RegionalBiomeLayerDefinition? match = null;
        for (var index = 0; index < _profile.BiomeLayers.Count; index++)
        {
            var candidate = _profile.BiomeLayers[index];
            if (candidate.RequiresCave != requiresCave ||
                tileY < candidate.MinTileY ||
                tileY > candidate.MaxTileYInclusive ||
                !candidate.BiomeIds.Contains(biomeId, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (match is null || candidate.Priority > match.Priority ||
                (candidate.Priority == match.Priority &&
                 StringComparer.OrdinalIgnoreCase.Compare(candidate.Id, match.Id) < 0))
            {
                match = candidate;
            }
        }

        return match;
    }

    private bool WinsRegionalElection(StructurePlanDefinition definition, long regionIndex)
    {
        var salt = DeterministicCoordinateHash.Salt(definition.Id);
        var priority = DeterministicCoordinateHash.Unit(_seed, regionIndex, salt: salt);
        if (priority >= definition.ChancePerRegion)
        {
            return false;
        }

        var minimumRegion = DeterministicCoordinateHash.FloorDivide(long.MinValue, _profile.RegionWidthTiles);
        var maximumRegion = DeterministicCoordinateHash.FloorDivide(long.MaxValue, _profile.RegionWidthTiles);
        for (var offset = -definition.MinSpacingRegions; offset <= definition.MinSpacingRegions; offset++)
        {
            if (offset == 0 ||
                (offset < 0 && regionIndex < minimumRegion - (long)offset) ||
                (offset > 0 && regionIndex > maximumRegion - (long)offset))
            {
                continue;
            }

            var neighbor = regionIndex + offset;
            var neighborPriority = DeterministicCoordinateHash.Unit(_seed, neighbor, salt: salt);
            if (neighborPriority < definition.ChancePerRegion &&
                (neighborPriority < priority || (neighborPriority == priority && neighbor < regionIndex)))
            {
                return false;
            }
        }

        return true;
    }

    private (long Start, long End) GetRegionBounds(long regionIndex)
    {
        var minimumRegion = DeterministicCoordinateHash.FloorDivide(long.MinValue, _profile.RegionWidthTiles);
        var maximumRegion = DeterministicCoordinateHash.FloorDivide(long.MaxValue, _profile.RegionWidthTiles);
        if (regionIndex < minimumRegion || regionIndex > maximumRegion)
        {
            throw new ArgumentOutOfRangeException(nameof(regionIndex));
        }

        long start;
        try
        {
            start = checked(regionIndex * _profile.RegionWidthTiles);
        }
        catch (OverflowException)
        {
            start = long.MinValue;
        }

        if (regionIndex == maximumRegion)
        {
            return (start, long.MaxValue);
        }

        var end = checked((regionIndex + 1L) * _profile.RegionWidthTiles - 1L);
        return (start, end);
    }

    private static bool IsAllowed(
        IReadOnlyList<string> allowedBiomes,
        IReadOnlyList<string> allowedSubBiomes,
        BiomeSelection selection)
    {
        return (allowedBiomes.Count == 0 ||
                allowedBiomes.Contains(selection.Biome.Id, StringComparer.OrdinalIgnoreCase)) &&
            (allowedSubBiomes.Count == 0 ||
             (selection.SubBiome is not null &&
              allowedSubBiomes.Contains(selection.SubBiome.Id, StringComparer.OrdinalIgnoreCase)));
    }

    private static bool IsAllowed(StructurePlanDefinition definition, WorldBiomeResolution resolution)
    {
        return (definition.AllowedBiomeIds.Count == 0 ||
                definition.AllowedBiomeIds.Contains(resolution.Biome.Id, StringComparer.OrdinalIgnoreCase)) &&
            (definition.AllowedSubBiomeIds.Count == 0 ||
             (resolution.SubBiome is not null &&
              definition.AllowedSubBiomeIds.Contains(
                  resolution.SubBiome.Id,
                  StringComparer.OrdinalIgnoreCase))) &&
            (definition.AllowedLayerIds.Count == 0 ||
             definition.AllowedLayerIds.Contains(resolution.LayerId, StringComparer.OrdinalIgnoreCase));
    }

    private static T SelectWeighted<T>(IReadOnlyList<T> values, Func<T, int> getWeight, ulong hash)
    {
        var total = values.Sum(value => (long)getWeight(value));
        var roll = (long)(hash % (ulong)total);
        foreach (var value in values)
        {
            roll -= getWeight(value);
            if (roll < 0)
            {
                return value;
            }
        }

        return values[^1];
    }

    private sealed record BiomeSelection(BiomeDefinition Biome, SubBiomeDefinition? SubBiome);

    private sealed record LayerBiomeSelection(
        RegionalBiomeLayerDefinition Layer,
        BiomeDefinition Biome,
        SubBiomeDefinition? SubBiome);
}
