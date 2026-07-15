using Game.Core.Biomes;
using Game.Core.Weather;
using Game.Core.World;
using Game.Core.World.Generation;
using Game.Core.WorldEvents;

namespace Game.Core.Runtime;

public sealed class LivingWorldRuntime
{
    private const int EventAdvanceIntervalTicks = 60;

    private readonly RegionalGenerationProfile _profile;
    private readonly WorldRegionPlanner _regions;
    private readonly DeterministicWeatherSystem _weather;
    private readonly DeterministicWorldEventExecutor _events;
    private readonly WorldEventJournal _eventJournal = new();
    private readonly AmbientStateService _ambient = new();
    private Func<int, int>? _surfaceHeightResolver;
    private WorldRegionPlan? _cachedRegion;
    private WorldEventRuntimeSnapshot? _eventSnapshot;
    private WorldEventExecutionContext? _lastEventContext;
    private long _lastProcessedPlayerActionSequence;
    private string? _lastEventWeatherId;
    private bool _lastEventIsNight;
    private bool _lastEventIsUnderground;
    private int _cachedSurfaceTileX = int.MinValue;
    private int _cachedSurfaceTileY;

    public LivingWorldRuntime(
        int seed,
        RegionalGenerationProfile profile,
        BiomeRegistry biomes,
        IReadOnlyList<StructurePlanDefinition>? structures = null,
        WorldEventDefinitionRegistry? worldEvents = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(biomes);
        _profile = profile;
        _regions = new WorldRegionPlanner(seed, profile, biomes, structures);
        _weather = new DeterministicWeatherSystem(seed);
        var eventRegistry = MergeWorldEvents(profile.WorldEvents, worldEvents);
        _events = new DeterministicWorldEventExecutor(seed, eventRegistry);
    }

    public RegionalGenerationProfile Profile => _profile;

    public WorldEventRuntimeSnapshot? WorldEventSnapshot => _eventSnapshot;

    public long LastProcessedPlayerActionSequence => _lastProcessedPlayerActionSequence;

    public WorldEventJournalSnapshot CaptureWorldEventJournal() => _eventJournal.Capture();

    public WorldEventRuntimeStateSnapshot? CaptureWorldEventState()
    {
        return _eventSnapshot is null
            ? null
            : new WorldEventRuntimeStateSnapshot
            {
                Runtime = _eventSnapshot,
                Journal = _eventJournal.Capture(),
                LastProcessedPlayerActionSequence = _lastProcessedPlayerActionSequence
            };
    }

    public void ConfigureSurfaceHeightResolver(Func<int, int> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _surfaceHeightResolver = resolver;
        _cachedSurfaceTileX = int.MinValue;
    }

    public void RestoreWorldEvents(
        WorldEventRuntimeSnapshot snapshot,
        WorldEventJournalSnapshot? journal = null)
    {
        _eventSnapshot = _events.NormalizeForRegistry(snapshot);
        _lastProcessedPlayerActionSequence = 0;
        _lastEventWeatherId = null;
        if (journal is not null)
        {
            _eventJournal.Restore(journal);
        }
    }

    public void RestoreWorldEvents(WorldEventRuntimeStateSnapshot state)
    {
        WorldEventRuntimeStateSnapshot.Validate(state);
        RestoreWorldEvents(state.Runtime, state.Journal);
        _lastProcessedPlayerActionSequence = state.LastProcessedPlayerActionSequence;
    }

    public WorldEventPlayerActionTriggerResult TriggerPlayerAction(
        WorldEventPlayerActionKind action,
        long sequence)
    {
        if (sequence <= _lastProcessedPlayerActionSequence)
        {
            return WorldEventPlayerActionTriggerResult.Duplicate(sequence, action);
        }

        if (_eventSnapshot is null || _lastEventContext is null)
        {
            throw new InvalidOperationException(
                "Living-world state must be captured before player actions can trigger world events.");
        }

        _lastProcessedPlayerActionSequence = sequence;
        var result = _events.TriggerPlayerAction(
            _eventSnapshot,
            _lastEventContext.Value,
            action,
            sequence);
        _eventSnapshot = result.Snapshot;
        _eventJournal.Append(result);
        var activated = result.Snapshot.Status == WorldEventRuntimeStatus.Active &&
                        result.Snapshot.ActivationSource == WorldEventActivationSource.PlayerAction &&
                        result.Snapshot.TriggerSequence == sequence;
        return new WorldEventPlayerActionTriggerResult(
            Processed: true,
            Activated: activated,
            Sequence: sequence,
            Action: action,
            EventId: activated ? result.Snapshot.ActiveEventId : null);
    }

    public WorldRegionPlan ResolveRegion(int tileX)
    {
        if (_cachedRegion is null || !_cachedRegion.ContainsTileX(tileX))
        {
            _cachedRegion = _regions.PlanAtTileX(tileX);
        }

        return _cachedRegion;
    }

    public LivingWorldFrameSnapshot Capture(TilePos playerTile, long worldTick, float daylight)
    {
        var region = ResolveRegion(playerTile.X);
        var cave = FindCave(region, playerTile);
        var resolution = _regions.ResolveBiome(
            region,
            playerTile.X,
            Math.Clamp(playerTile.Y, 0, _profile.WorldHeightTiles - 1));
        var surfaceTileY = ResolveSurfaceHeight(playerTile.X);
        var isUnderground = resolution.IsCave || playerTile.Y >= surfaceTileY + 8;
        var weather = _weather.GetState(resolution.Biome, worldTick);
        var ambient = _ambient.Resolve(
            resolution.Biome,
            resolution.SubBiome,
            weather,
            isUnderground,
            Math.Clamp(daylight, 0f, 1f));
        var weatherId = weather.Kind.ToString();
        var eventContext = new WorldEventExecutionContext(
            worldTick,
            region.RegionIndex,
            resolution.Biome.Id,
            resolution.SubBiome?.Id,
            weatherId,
            weather.Intensity,
            daylight <= 0.15f,
            isUnderground,
            Math.Clamp(daylight, 0f, 1f),
            playerTile);
        _lastEventContext = eventContext;
        _eventSnapshot ??= WorldEventRuntimeSnapshot.Inactive(eventContext);
        if (ShouldAdvanceWorldEvent(_eventSnapshot, eventContext))
        {
            var eventResult = _events.Advance(_eventSnapshot, eventContext);
            _eventSnapshot = eventResult.Snapshot;
            _eventJournal.Append(eventResult);
            _lastEventWeatherId = weatherId;
            _lastEventIsNight = eventContext.IsNight;
            _lastEventIsUnderground = eventContext.IsUnderground;
        }

        var worldEvent = _eventSnapshot;
        var modified = WorldEventModifierApplier.Apply(
            worldEvent.EffectiveModifiers,
            resolution.Biome.Spawning.DensityMultiplier,
            resolution.Biome.Lighting.SkyLightMultiplier,
            ambient.Light,
            weather.Intensity,
            1f,
            1f);
        var eventActive = worldEvent.Status == WorldEventRuntimeStatus.Active;
        var soundscapeId = eventActive && !string.IsNullOrWhiteSpace(modified.SoundscapeId)
            ? modified.SoundscapeId
            : ambient.SoundscapeId;
        var colorGradeId = eventActive && !string.IsNullOrWhiteSpace(modified.ColorGradeId)
            ? modified.ColorGradeId
            : resolution.Biome.Lighting.ColorGradeId;
        var ambientParticleId = eventActive && !string.IsNullOrWhiteSpace(modified.ParticleSpriteId)
            ? modified.ParticleSpriteId
            : resolution.Biome.Presentation.AmbientParticleSpriteId;

        return new LivingWorldFrameSnapshot(
            region.RegionIndex,
            region.StartTileX,
            region.EndTileXInclusive,
            resolution.Biome.Id,
            resolution.Biome.DisplayName,
            resolution.SubBiome?.Id,
            resolution.SubBiome?.DisplayName,
            resolution.LayerId,
            cave?.ProfileId,
            isUnderground,
            soundscapeId,
            modified.AmbientLight,
            ambient.Visibility,
            ambient.Temperature,
            ambient.Humidity,
            colorGradeId,
            modified.SkyLight,
            resolution.Biome.Lighting.EmissiveLightMultiplier,
            resolution.Biome.Lighting.FogDensity,
            modified.SpawnWeight,
            resolution.Biome.Resources.OreDensityMultiplier,
            resolution.Biome.Resources.VegetationDensityMultiplier,
            resolution.Biome.Resources.ForageDensityMultiplier,
            weather.Kind,
            modified.WeatherIntensity,
            weather.Wind,
            weather.CloudCover,
            weather.StartTick,
            weather.EndTickExclusive,
            eventActive,
            worldEvent.ActiveEventId,
            worldEvent.Progress,
            worldEvent.Intensity,
            new LivingWorldPresentationFrameSnapshot(
                resolution.Biome.Presentation.BackgroundSpriteId,
                ambientParticleId,
                resolution.Biome.Presentation.AmbientCritterSpriteId,
                resolution.Biome.Presentation.BiomeIconSpriteId,
                resolution.Biome.Presentation.EliteSpriteId,
                resolution.Biome.Presentation.AmbientParticleDensity *
                Math.Max(1f, modified.PresentationIntensity),
                resolution.Biome.Presentation.CaveReverb,
                resolution.Biome.Presentation.SurfaceReflectionStrength,
                resolution.Biome.Presentation.WindResponse))
        {
            WorldEventPhaseId = worldEvent.PhaseId,
            WorldEventPhaseProgress = worldEvent.PhaseProgress,
            WorldEventParticleSpriteId = eventActive ? modified.ParticleSpriteId : null,
            WorldEventPresentationIntensity = eventActive ? modified.PresentationIntensity : 0f,
            LootQuantityMultiplier = modified.LootQuantity,
            RareLootChanceMultiplier = modified.RareLootChance
        };
    }

    public static LivingWorldRuntime CreateDefault(
        int seed,
        int worldHeightTiles,
        int surfaceBaseY,
        BiomeRegistry biomes,
        WorldEventDefinitionRegistry? worldEvents = null)
    {
        ArgumentNullException.ThrowIfNull(biomes);
        if (biomes.Definitions.Count == 0)
        {
            biomes = BiomeRegistry.Create(
            [
                new BiomeDefinition
                {
                    Id = "forest",
                    DisplayName = "Forest",
                    SurfaceTile = "grass",
                    UndergroundTile = "stone"
                }
            ]);
        }

        var profile = new RegionalGenerationProfile
        {
            Id = "runtime_default",
            WorldHeightTiles = Math.Max(GameConstants.ChunkSize, worldHeightTiles),
            SurfaceBaseY = Math.Clamp(surfaceBaseY, 6, Math.Max(6, worldHeightTiles - 10)),
            CaveMinDepth = Math.Clamp(surfaceBaseY + 8, 0, Math.Max(0, worldHeightTiles - 1)),
            CaveMaxDepth = Math.Max(0, worldHeightTiles - 8)
        };
        return new LivingWorldRuntime(seed, profile, biomes, worldEvents: worldEvents);
    }

    private static CaveRegionPlan? FindCave(WorldRegionPlan region, TilePos playerTile)
    {
        for (var index = 0; index < region.Caves.Count; index++)
        {
            if (region.Caves[index].Contains(playerTile.X, playerTile.Y))
            {
                return region.Caves[index];
            }
        }

        return null;
    }

    private int ResolveSurfaceHeight(int tileX)
    {
        if (_cachedSurfaceTileX == tileX)
        {
            return _cachedSurfaceTileY;
        }

        var resolved = _surfaceHeightResolver?.Invoke(tileX) ?? _profile.SurfaceBaseY;
        _cachedSurfaceTileX = tileX;
        _cachedSurfaceTileY = Math.Clamp(resolved, 0, _profile.WorldHeightTiles - 1);
        return _cachedSurfaceTileY;
    }

    private bool ShouldAdvanceWorldEvent(
        WorldEventRuntimeSnapshot snapshot,
        in WorldEventExecutionContext context)
    {
        return _lastEventWeatherId is null ||
            context.RegionIndex != snapshot.RegionIndex ||
            !string.Equals(context.BiomeId, snapshot.BiomeId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(context.SubBiomeId, snapshot.SubBiomeId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(context.WeatherId, _lastEventWeatherId, StringComparison.OrdinalIgnoreCase) ||
            context.IsNight != _lastEventIsNight ||
            context.IsUnderground != _lastEventIsUnderground ||
            context.WorldTick - snapshot.LastAdvancedTick >= EventAdvanceIntervalTicks;
    }

    private static WorldEventDefinitionRegistry MergeWorldEvents(
        IReadOnlyList<WorldEventDefinition> profileEvents,
        WorldEventDefinitionRegistry? contentEvents)
    {
        var merged = new Dictionary<string, WorldEventDefinition>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < profileEvents.Count; index++)
        {
            merged[profileEvents[index].Id] = profileEvents[index];
        }

        if (contentEvents is not null)
        {
            foreach (var definition in contentEvents.Definitions)
            {
                merged[definition.Id] = definition;
            }
        }

        return WorldEventDefinitionRegistry.Create(merged.Values);
    }
}
