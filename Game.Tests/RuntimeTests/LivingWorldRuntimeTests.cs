using Game.Core.Biomes;
using Game.Core.Runtime;
using Game.Core.Weather;
using Game.Core.World;
using Game.Core.World.Generation;
using Game.Core.WorldEvents;
using Xunit;

namespace Game.Tests.RuntimeTests;

public sealed class LivingWorldRuntimeTests
{
    [Fact]
    public void Capture_IsDeterministicAcrossIndependentRuntimesAndNegativeCoordinates()
    {
        var profile = CreateProfile();
        var biomes = CreateBiomes();
        var first = new LivingWorldRuntime(7331, profile, biomes);
        var second = new LivingWorldRuntime(7331, profile, biomes);
        var position = new TilePos(-513, 84);

        var firstSnapshot = first.Capture(position, 12_345, 0.7f);
        var secondSnapshot = second.Capture(position, 12_345, 0.7f);

        Assert.Equal(firstSnapshot, secondSnapshot);
        Assert.True(firstSnapshot.RegionIndex < 0);
        Assert.InRange((long)position.X, firstSnapshot.RegionStartTileX, firstSnapshot.RegionEndTileXInclusive);
        Assert.False(string.IsNullOrWhiteSpace(firstSnapshot.BiomeId));
        Assert.InRange(firstSnapshot.AmbientLight, 0f, 1f);
        Assert.InRange(firstSnapshot.Visibility, 0f, 1f);
        Assert.StartsWith("particles/", firstSnapshot.Presentation.AmbientParticleSpriteId);
        Assert.StartsWith("ui/biomes/", firstSnapshot.Presentation.BiomeIconSpriteId);
    }

    [Fact]
    public void ResolveRegion_CachesCurrentRegionAndChangesAtBoundary()
    {
        var runtime = new LivingWorldRuntime(42, CreateProfile(), CreateBiomes());

        var first = runtime.ResolveRegion(-1);
        var same = runtime.ResolveRegion(-64);
        var next = runtime.ResolveRegion(0);

        Assert.Same(first, same);
        Assert.NotSame(first, next);
        Assert.Equal(-1, first.RegionIndex);
        Assert.Equal(0, next.RegionIndex);
    }

    [Fact]
    public void Capture_UsesConfiguredLocalSurfaceInsteadOfGlobalBaseHeight()
    {
        var profile = CreateProfile() with { CaveRegionAttempts = 0 };
        var runtime = new LivingWorldRuntime(42, profile, CreateBiomes());
        runtime.ConfigureSurfaceHeightResolver(tileX => tileX < 0 ? 82 : 44);

        var highTerrainSurface = runtime.Capture(new TilePos(-20, 70), 0, 1f);
        var highTerrainUnderground = runtime.Capture(new TilePos(-20, 91), 1, 1f);
        var lowTerrainUnderground = runtime.Capture(new TilePos(20, 53), 2, 1f);

        Assert.False(highTerrainSurface.IsUnderground);
        Assert.True(highTerrainUnderground.IsUnderground);
        Assert.True(lowTerrainUnderground.IsUnderground);
        Assert.Equal(82, highTerrainSurface.SurfaceTileY);
        Assert.Equal(82, highTerrainUnderground.SurfaceTileY);
        Assert.Equal(44, lowTerrainUnderground.SurfaceTileY);
    }

    [Fact]
    public void Capture_ExposesWeatherAmbientAndScheduledWorldEvents()
    {
        var runtime = new LivingWorldRuntime(19, CreateProfile(), CreateBiomes());
        LivingWorldFrameSnapshot? active = null;
        for (var tick = 0L; tick < 3_600; tick++)
        {
            var snapshot = runtime.Capture(new TilePos(20, 90), tick, 0.8f);
            if (snapshot.IsWorldEventActive)
            {
                active = snapshot;
                break;
            }
        }

        Assert.NotNull(active);
        Assert.Equal("firefly_bloom", active.Value.WorldEventId);
        Assert.True(active.Value.IsUnderground);
        Assert.NotEqual(WeatherKind.Clear, active.Value.Weather);
        Assert.True(active.Value.WorldEventIntensity > 0f);
    }

    [Fact]
    public void Capture_AppliesDataDrivenEventPhaseToSimulationAndPresentation()
    {
        var definition = new WorldEventDefinition
        {
            Id = "runtime_bloom",
            ChancePerWindow = 1f,
            MinDurationTicks = 3_600,
            MaxDurationTicks = 3_600,
            Intensity = 1f,
            Modifiers = WorldEventModifierSet.Identity with
            {
                SpawnWeightMultiplier = 1.5f,
                SkyLightMultiplier = 0.5f,
                AmbientLightAdd = 0.2f,
                WeatherIntensityMultiplier = 0.5f,
                PresentationIntensity = 1.4f,
                ParticleSpriteId = "effects/runtime_bloom",
                ColorGradeId = "runtime_bloom_grade",
                SoundscapeId = "runtime_bloom_soundscape"
            },
            Phases =
            [
                new WorldEventPhaseDefinition
                {
                    Id = "surge",
                    StartProgress = 0f,
                    EndProgress = 1f,
                    Modifiers = WorldEventModifierSet.Identity
                }
            ]
        };
        var runtime = new LivingWorldRuntime(
            19,
            CreateProfile() with { WorldEvents = Array.Empty<WorldEventDefinition>() },
            CreateBiomes(),
            worldEvents: WorldEventDefinitionRegistry.Create([definition]));

        var snapshot = runtime.Capture(new TilePos(20, 40), 0, 0.8f);

        Assert.True(snapshot.IsWorldEventActive);
        Assert.Equal("runtime_bloom", snapshot.WorldEventId);
        Assert.Equal("surge", snapshot.WorldEventPhaseId);
        Assert.Equal("runtime_bloom_soundscape", snapshot.SoundscapeId);
        Assert.Equal("runtime_bloom_grade", snapshot.ColorGradeId);
        Assert.Equal("effects/runtime_bloom", snapshot.Presentation.AmbientParticleSpriteId);
        Assert.Equal("effects/runtime_bloom", snapshot.WorldEventParticleSpriteId);
        Assert.Equal(1.4f, snapshot.WorldEventPresentationIntensity, 3);
        Assert.Equal(1.5f, snapshot.SpawnDensityMultiplier, 3);
        Assert.Equal(0.5f, snapshot.SkyLightMultiplier, 3);
        Assert.True(snapshot.AmbientLight > 0.2f);
        Assert.Single(runtime.CaptureWorldEventJournal().Entries);

        for (var tick = 1L; tick < 60; tick++)
        {
            _ = runtime.Capture(new TilePos(20, 40), tick, 0.8f);
        }

        Assert.Single(runtime.CaptureWorldEventJournal().Entries);
        _ = runtime.Capture(new TilePos(20, 40), 60, 0.8f);
        Assert.Equal(60, runtime.WorldEventSnapshot!.LastAdvancedTick);
        Assert.Single(runtime.CaptureWorldEventJournal().Entries);
    }

    [Fact]
    public void PlayerActionTrigger_IsExactlyOnceAndResumesDeterministically()
    {
        var definition = new WorldEventDefinition
        {
            Id = "mining_resonance",
            ChancePerWindow = 0f,
            PlayerActionTriggers = [WorldEventPlayerActionKind.Mine],
            PlayerActionTriggerChance = 1f,
            MinDurationTicks = 60,
            MaxDurationTicks = 60,
            Intensity = 1f,
            Modifiers = WorldEventModifierSet.Identity with
            {
                LootQuantityMultiplier = 1.5f,
                RareLootChanceMultiplier = 2f
            }
        };
        var registry = WorldEventDefinitionRegistry.Create([definition]);
        var profile = CreateProfile() with { WorldEvents = Array.Empty<WorldEventDefinition>() };
        var uninterrupted = new LivingWorldRuntime(19, profile, CreateBiomes(), worldEvents: registry);
        _ = uninterrupted.Capture(new TilePos(20, 40), 0, 0.8f);

        var first = uninterrupted.TriggerPlayerAction(WorldEventPlayerActionKind.Mine, 1);
        var duplicate = uninterrupted.TriggerPlayerAction(WorldEventPlayerActionKind.Mine, 1);

        Assert.True(first.Processed);
        Assert.True(first.Activated);
        Assert.False(duplicate.Processed);
        var firstJournal = uninterrupted.CaptureWorldEventJournal();
        Assert.Equal(2, firstJournal.Entries.Count);
        Assert.Equal(WorldEventDomainEventKind.PlayerActionTriggered, firstJournal.Entries[0].Kind);
        Assert.Equal(WorldEventDomainEventKind.Activated, firstJournal.Entries[1].Kind);
        Assert.All(firstJournal.Entries, entry => Assert.Equal(1, entry.TriggerSequence));
        Assert.Equal(1, uninterrupted.LastProcessedPlayerActionSequence);

        var persisted = uninterrupted.CaptureWorldEventState()!;
        var resumed = new LivingWorldRuntime(19, profile, CreateBiomes(), worldEvents: registry);
        resumed.RestoreWorldEvents(persisted);
        var resumedDuplicate = resumed.TriggerPlayerAction(WorldEventPlayerActionKind.Mine, 1);
        Assert.False(resumedDuplicate.Processed);

        var expectedAfterCompletion = uninterrupted.Capture(new TilePos(20, 40), 60, 0.8f);
        var actualAfterCompletion = resumed.Capture(new TilePos(20, 40), 60, 0.8f);
        Assert.Equal(expectedAfterCompletion, actualAfterCompletion);

        var expectedNext = uninterrupted.TriggerPlayerAction(WorldEventPlayerActionKind.Mine, 2);
        var actualNext = resumed.TriggerPlayerAction(WorldEventPlayerActionKind.Mine, 2);
        Assert.Equal(expectedNext, actualNext);
        Assert.True(actualNext.Activated);
        var expectedState = uninterrupted.CaptureWorldEventState()!;
        var actualState = resumed.CaptureWorldEventState()!;
        Assert.Equal(expectedState.Runtime.LastAdvancedTick, actualState.Runtime.LastAdvancedTick);
        Assert.Equal(expectedState.Runtime.Status, actualState.Runtime.Status);
        Assert.Equal(expectedState.Runtime.ActiveEventId, actualState.Runtime.ActiveEventId);
        Assert.Equal(expectedState.Runtime.TriggerSequence, actualState.Runtime.TriggerSequence);
        Assert.Equal(expectedState.Runtime.Cooldowns.ToArray(), actualState.Runtime.Cooldowns.ToArray());
        Assert.Equal(expectedState.Journal.Entries.ToArray(), actualState.Journal.Entries.ToArray());
        Assert.Equal(
            expectedState.LastProcessedPlayerActionSequence,
            actualState.LastProcessedPlayerActionSequence);
    }

    [Fact]
    public void RestoreWorldEvents_DropsRemovedActiveDefinitionAndOrphanCooldowns()
    {
        var runtime = new LivingWorldRuntime(
            19,
            CreateProfile() with { WorldEvents = Array.Empty<WorldEventDefinition>() },
            CreateBiomes(),
            worldEvents: WorldEventDefinitionRegistry.Create(Array.Empty<WorldEventDefinition>()));
        var removed = new WorldEventRuntimeSnapshot
        {
            LastAdvancedTick = 120,
            RegionIndex = 0,
            BiomeId = "forest",
            Status = WorldEventRuntimeStatus.Active,
            ActiveEventId = "removed_mod_event",
            LastEventId = "removed_mod_event",
            StartTick = 60,
            EndTickExclusive = 600,
            PhaseId = "active",
            Progress = 0.1f,
            PhaseProgress = 0.1f,
            Intensity = 1f,
            Cooldowns = [new WorldEventCooldownState("also_removed", 900)]
        };

        runtime.RestoreWorldEvents(removed);
        var restored = runtime.WorldEventSnapshot!;
        var captured = runtime.Capture(new TilePos(20, 40), 180, 0.8f);

        Assert.Equal(WorldEventRuntimeStatus.Inactive, restored.Status);
        Assert.Null(restored.ActiveEventId);
        Assert.Empty(restored.Cooldowns);
        Assert.False(captured.IsWorldEventActive);
    }

    private static RegionalGenerationProfile CreateProfile()
    {
        return new RegionalGenerationProfile
        {
            Id = "living-test",
            RegionWidthTiles = 128,
            BiomeSpanRegions = 2,
            WorldHeightTiles = 160,
            SurfaceBaseY = 58,
            CaveMinDepth = 70,
            CaveMaxDepth = 145,
            WorldEvents =
            [
                new WorldEventDefinition
                {
                    Id = "firefly_bloom",
                    ChancePerWindow = 1f,
                    MinDurationTicks = 3_600,
                    MaxDurationTicks = 3_600,
                    Intensity = 0.75f
                }
            ]
        };
    }

    private static BiomeRegistry CreateBiomes()
    {
        return BiomeRegistry.Create(
        [
            new BiomeDefinition
            {
                Id = "forest",
                DisplayName = "Forest",
                SurfaceTile = "grass",
                UndergroundTile = "stone",
                SelectionWeight = 3,
                Weather = new BiomeWeatherProfile
                {
                    ClearWeight = 0,
                    RainWeight = 1,
                    StormWeight = 1,
                    FogWeight = 1,
                    MinDurationTicks = 60,
                    MaxDurationTicks = 60
                },
                Presentation = new BiomePresentationProfile
                {
                    BackgroundSpriteId = "world/backgrounds/forest_parallax_layer",
                    AmbientParticleSpriteId = "particles/forest_leaf_drift",
                    AmbientCritterSpriteId = "entities/critters/forest_moth",
                    BiomeIconSpriteId = "ui/biomes/forest",
                    AmbientParticleDensity = 0.42f
                },
                SubBiomes =
                [
                    new SubBiomeDefinition
                    {
                        Id = "mushroom_cave",
                        DisplayName = "Mushroom Cave",
                        CaveProfileId = "mushroom",
                        SoundscapeId = "mushroom_cave"
                    }
                ]
            },
            new BiomeDefinition
            {
                Id = "meadow",
                DisplayName = "Meadow",
                SurfaceTile = "grass",
                UndergroundTile = "dirt",
                Presentation = new BiomePresentationProfile
                {
                    AmbientParticleSpriteId = "particles/meadow_pollen",
                    BiomeIconSpriteId = "ui/biomes/meadow"
                }
            }
        ]);
    }
}
