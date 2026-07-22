using Game.Core.Animation;
using Game.Core.Assets;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Randomness;
using Game.Core.Spawning;
using Game.Core.Time;
using Game.Core.World;
using Xunit;

namespace Game.Tests.EntityTests;

public sealed class LivingWildlifeContentTests
{
    private static readonly WildlifeContract[] Contracts =
    [
        new(
            "meadow_butterfly",
            "entities/critters/meadow_butterfly",
            "meadow_day_butterfly",
            "meadow_day_pollinators",
            "meadow",
            "surface",
            SpawnTimeCondition.Day),
        new(
            "forest_moth",
            "entities/critters/forest_moth",
            "forest_night_moth",
            "forest_night_moth_glade",
            "forest",
            "surface",
            SpawnTimeCondition.Night),
        new(
            "cave_glowbug",
            "entities/critters/cave_glowbug",
            "deep_cave_glowbug",
            "deep_cave_glow_garden",
            "deep_cave",
            "cavern",
            SpawnTimeCondition.Any)
    ];

    [Fact]
    public void RepositoryData_ActivatesAmbientSheetsAsBoundedAnimatedWildlife()
    {
        var dataRoot = FindGameDataRoot();
        var result = new GameContentLoader().LoadWithMods(dataRoot, modsRoot: null);

        Assert.False(
            result.Report.HasErrors,
            string.Join(Environment.NewLine, result.Report.Issues.Select(issue => issue.Message)));
        var runtimeAnimations = Assert.IsType<AnimationContentRegistry>(result.Database.RuntimeAnimations);
        var brief = File.ReadAllText(Path.Combine(
            dataRoot,
            "asset_briefs",
            "production_wave_03_briefs.json"));
        var provenance = File.ReadAllText(Path.Combine(
            dataRoot,
            "art_direction",
            "wave_03_provenance.json"));

        foreach (var contract in Contracts)
        {
            var entity = result.Database.Entities.GetById(contract.EntityId);
            Assert.Equal(contract.SpriteId, entity.TexturePath);
            Assert.Equal(EntityFaction.Friendly, entity.Faction);
            Assert.Equal(EntityMovementMode.Flying, entity.MovementMode);
            Assert.NotNull(entity.Ai);
            Assert.NotEqual(AiBehaviorKind.None, entity.Ai.Kind);

            Assert.True(result.Database.SpawnRules.TryGetById(contract.SpawnRuleId, out var spawn));
            Assert.Equal(contract.EntityId, spawn.EntityId);
            Assert.Equal(contract.BiomeId, spawn.BiomeId);
            Assert.True(spawn.MaxActiveInRegion is > 0);
            Assert.True(spawn.MaxActiveInLocalArea is > 0);
            Assert.InRange(spawn.Chance, 0.01f, 1f);

            Assert.True(result.Database.SpriteAssets.TryGetById(contract.SpriteId, out var sprite));
            Assert.Equal(SpriteAssetCategory.Entity, sprite.Category);
            Assert.Equal(64, sprite.Width);
            Assert.Equal(16, sprite.Height);
            Assert.Equal(4, sprite.Frames.Count);
            Assert.Collection(
                sprite.Frames,
                frame => Assert.Equal("flight_0", frame.Id),
                frame => Assert.Equal("flight_1", frame.Id),
                frame => Assert.Equal("flight_2", frame.Id),
                frame => Assert.Equal("flight_3", frame.Id));
            Assert.All(sprite.Frames, frame =>
            {
                Assert.Equal(16, frame.Width);
                Assert.Equal(16, frame.Height);
            });

            var profile = runtimeAnimations.ResolveEntity(
                contract.EntityId,
                AnimationEntityKind.Enemy);
            Assert.False(profile.UsedFallback);
            Assert.Equal(contract.SpriteId, profile.Profile.SpriteId);
            Assert.True(profile.Profile.IsFlying);
            Assert.Equal(4, profile.Profile.GetAnimation(AnimationEntityVisualState.Fly).FrameCount);

            Assert.Contains(contract.SpriteId, brief, StringComparison.Ordinal);
            Assert.Contains(contract.SpriteId, provenance, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WildlifeEncounters_AreBiomeTimeSpecificAndDeterministic()
    {
        var result = new GameContentLoader().LoadWithMods(FindGameDataRoot(), modsRoot: null);
        Assert.False(result.Report.HasErrors);

        foreach (var contract in Contracts)
        {
            Assert.True(result.Database.Encounters.TryGetById(contract.EncounterId, out var encounter));
            var registry = EncounterDefinitionRegistry.Create([encounter]);
            var time = new WorldTime();
            if (contract.Time == SpawnTimeCondition.Night)
            {
                time.SetNight();
            }
            else
            {
                time.SetDay();
            }

            var environment = new SpawnEnvironment(
                contract.BiomeId,
                contract.VerticalLayerId,
                "Clear",
                null,
                1f);
            Assert.True(new EncounterPlanner(new Random(1)).HasApplicableEncounter(
                registry,
                environment,
                time));

            if (contract.Time != SpawnTimeCondition.Any)
            {
                var oppositeTime = new WorldTime();
                if (contract.Time == SpawnTimeCondition.Day)
                {
                    oppositeTime.SetNight();
                }
                else
                {
                    oppositeTime.SetDay();
                }

                Assert.False(new EncounterPlanner(new Random(1)).HasApplicableEncounter(
                    registry,
                    environment,
                    oppositeTime));
            }

            var first = Plan(registry, environment, time, seed: 8_419);
            var second = Plan(registry, environment, time, seed: 8_419);
            Assert.Equal(Describe(first), Describe(second));
            Assert.Contains(first.Spawns, spawn => spawn.SpawnRuleId == contract.SpawnRuleId);
            Assert.InRange(first.Spawns.Count, 1, encounter.MaxActiveInRegion);
        }
    }

    private static EncounterPlan Plan(
        EncounterDefinitionRegistry encounters,
        SpawnEnvironment environment,
        WorldTime time,
        ulong seed)
    {
        var planner = new EncounterPlanner(
            new SessionRandomRegistry(seed).GetStream("spawning.encounters.wildlife-test"));
        return Assert.IsType<EncounterPlan>(planner.TryPlan(
            encounters,
            new EntityManager(),
            environment,
            time,
            new TilePos(-37, 96)));
    }

    private static string Describe(EncounterPlan plan)
    {
        return string.Join(
            ',',
            plan.Spawns.Select(spawn => $"{spawn.RoleId}/{spawn.SpawnRuleId}/{spawn.RoleOrdinal}"));
    }

    private static string FindGameDataRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (File.Exists(Path.Combine(
                    candidate,
                    "asset_briefs",
                    "production_wave_03_briefs.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Game.Data from the test output directory.");
    }

    private readonly record struct WildlifeContract(
        string EntityId,
        string SpriteId,
        string SpawnRuleId,
        string EncounterId,
        string BiomeId,
        string VerticalLayerId,
        SpawnTimeCondition Time);
}
