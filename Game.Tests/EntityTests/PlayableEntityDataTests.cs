using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Spawning;
using System.Numerics;
using Xunit;

namespace Game.Tests.EntityTests;

public sealed class PlayableEntityDataTests
{
    [Fact]
    public void GameData_LoadsPlayableEntitySpawnAndLootContracts()
    {
        var dataRoot = FindGameDataRoot();
        var entities = new EntityDefinitionJsonLoader().LoadRegistryFromDirectory(Path.Combine(dataRoot, "entities"));
        var spawns = new SpawnRuleJsonLoader().LoadRegistryFromDirectory(Path.Combine(dataRoot, "spawns"));
        var loot = new LootTableJsonLoader().LoadRegistryFromDirectory(Path.Combine(dataRoot, "loot"));
        var factory = new EntityFactory(new TileCollisionResolver());

        AssertActor(
            entities,
            factory,
            "squirrel",
            "entities/critters/squirrel",
            EntityFaction.Friendly,
            EntityMovementMode.Ground,
            AiState.Idle);
        AssertActor(
            entities,
            factory,
            "firefly",
            "entities/critters/firefly",
            EntityFaction.Friendly,
            EntityMovementMode.Flying,
            AiState.Idle);
        AssertActor(
            entities,
            factory,
            "forest_boar",
            "entities/enemies/forest_boar",
            EntityFaction.Hostile,
            EntityMovementMode.Ground,
            AiState.Patrol);
        AssertActor(
            entities,
            factory,
            "crystal_cave_spider",
            "entities/enemies/cave_spider",
            EntityFaction.Hostile,
            EntityMovementMode.Ground,
            AiState.Patrol);
        AssertActor(
            entities,
            factory,
            "cave_spider",
            "entities/enemies/cave_spider",
            EntityFaction.Hostile,
            EntityMovementMode.Ground,
            AiState.Patrol);
        AssertActor(
            entities,
            factory,
            "forest_flock_bird",
            "entities/critters/bird",
            EntityFaction.Friendly,
            EntityMovementMode.Flying,
            AiState.Idle);
        AssertActor(
            entities,
            factory,
            "meadow_rabbit",
            "entities/critters/rabbit",
            EntityFaction.Friendly,
            EntityMovementMode.Ground,
            AiState.Idle);
        AssertActor(
            entities,
            factory,
            "cave_bat_scout",
            "entities/enemies/bat",
            EntityFaction.Hostile,
            EntityMovementMode.Flying,
            AiState.Patrol);
        AssertActor(
            entities,
            factory,
            "marsh_frog",
            "entities/wave05/marsh_frog",
            EntityFaction.Friendly,
            EntityMovementMode.Ground,
            AiState.Idle);
        AssertActor(
            entities,
            factory,
            "canopy_owl",
            "entities/wave05/canopy_owl",
            EntityFaction.Friendly,
            EntityMovementMode.Flying,
            AiState.Idle);
        AssertActor(
            entities,
            factory,
            "amber_beetle",
            "entities/wave05/amber_beetle",
            EntityFaction.Hostile,
            EntityMovementMode.Ground,
            AiState.Patrol);
        AssertActor(
            entities,
            factory,
            "prism_wisp",
            "entities/wave05/prism_wisp",
            EntityFaction.Hostile,
            EntityMovementMode.Flying,
            AiState.Patrol);

        Assert.True(spawns.TryGetById("forest_day_squirrel", out var squirrelSpawn));
        Assert.Equal(SpawnTimeCondition.Day, squirrelSpawn.Time);
        Assert.Equal(4.5f, squirrelSpawn.CooldownSeconds);
        Assert.True(spawns.TryGetById("forest_night_firefly", out _));
        Assert.True(spawns.TryGetById("forest_boar", out _));
        Assert.True(spawns.TryGetById("deep_cave_spider", out _));
        Assert.True(spawns.TryGetById("deep_cave_crystal_spider", out var eliteSpawn));
        Assert.Equal(1, eliteSpawn.MaxActiveInRegion);
        Assert.Equal(1, eliteSpawn.MaxActiveInHabitat);
        Assert.True(spawns.TryGetById("forest_day_flock_bird", out var flockSpawn));
        Assert.Equal(6, flockSpawn.MaxActiveInLocalArea);
        Assert.True(flockSpawn.WeatherWeights["Clear"] > flockSpawn.WeatherWeights["Rain"]);
        Assert.True(spawns.TryGetById("meadow_day_rabbit", out _));
        Assert.True(spawns.TryGetById("deep_cave_bat_scout", out var batSpawn));
        Assert.True(batSpawn.NightWeight > batSpawn.DayWeight);
        Assert.True(spawns.TryGetById("twilight_marsh_frog", out var frogSpawn));
        Assert.Contains(SpawnHabitat.WaterEdge, frogSpawn.Habitats);
        Assert.True(spawns.TryGetById("amber_grove_owl", out _));
        Assert.True(spawns.TryGetById("amber_grove_beetle", out _));
        Assert.True(spawns.TryGetById("crystal_prism_wisp", out var wispSpawn));
        Assert.True(wispSpawn.WorldEventWeights["crystal_surge"] > 1f);

        Assert.True(loot.TryGetById("firefly", out var fireflyLoot));
        Assert.Empty(fireflyLoot.Entries);

        Assert.True(loot.TryGetById("forest_boar", out var boarLoot));
        Assert.Contains(boarLoot.Entries, entry => entry.Guaranteed);
        Assert.Contains(boarLoot.Entries, entry => entry.Weight > 0);
        Assert.True(loot.TryGetById("cave_spider", out var spiderLoot));
        Assert.Contains(spiderLoot.Entries, entry => entry.Guaranteed);
        Assert.Contains(spiderLoot.Entries, entry => entry.Weight > 0);
        Assert.True(loot.TryGetById("crystal_cave_spider", out var eliteLoot));
        Assert.Contains(eliteLoot.Entries, entry => entry.RequiredVictimTags.Contains("elite"));
        Assert.True(loot.TryGetById("forest_bird_forage", out _));
        Assert.True(loot.TryGetById("meadow_rabbit_forage", out _));
        Assert.True(loot.TryGetById("cave_bat_scout", out _));
        Assert.True(loot.TryGetById("marsh_frog", out _));
        Assert.True(loot.TryGetById("canopy_owl", out _));
        Assert.True(loot.TryGetById("amber_beetle", out var beetleLoot));
        Assert.Contains(beetleLoot.Entries, entry => entry.ItemId == "amberstone_block");
        Assert.True(loot.TryGetById("prism_wisp", out var wispLoot));
        Assert.Contains(wispLoot.Entries, entry => entry.ItemId == "mana_crystal");

        var elite = factory.CreateEnemy(entities.GetById("crystal_cave_spider"), Vector2.Zero);
        Assert.Equal(EntityDespawnMode.Never, elite.DespawnPolicy.Mode);
        Assert.True(elite.HasTag("elite"));
    }

    private static void AssertActor(
        EntityDefinitionRegistry registry,
        EntityFactory factory,
        string id,
        string expectedTexture,
        EntityFaction expectedFaction,
        EntityMovementMode expectedMovement,
        AiState expectedInitialState)
    {
        var definition = registry.GetById(id);
        var actor = factory.CreateEnemy(definition, Vector2.Zero);

        Assert.Equal(expectedTexture, definition.TexturePath);
        Assert.Equal(expectedFaction, actor.Faction);
        Assert.Equal(expectedMovement, actor.MovementMode);
        Assert.Equal(expectedInitialState, actor.AiState);
        Assert.NotEmpty(actor.Tags);
    }

    private static string FindGameDataRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Game.Data from the test output directory.");
    }
}
