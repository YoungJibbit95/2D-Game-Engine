using Game.Core.Data;
using Game.Core.Entities.AI;
using Xunit;

namespace Game.Tests.DataTests;

public sealed class EncounterContentTests
{
    [Fact]
    public void RepositoryData_LoadsDistinctBiomeCompositionsWithoutNullAiRules()
    {
        var result = new GameContentLoader().LoadWithMods(FindGameDataRoot(), modsRoot: null);

        Assert.False(result.Report.HasErrors, string.Join(Environment.NewLine, result.Report.Issues.Select(issue => issue.Message)));
        Assert.True(result.Database.Encounters.Definitions.Count >= 7);
        var forestDay = GetEncounter(result.Database, "forest_day_foragers");
        var forestNight = GetEncounter(result.Database, "forest_night_lanterns");
        var amberDay = GetEncounter(result.Database, "amber_grove_day_patrol");
        Assert.False(
            forestDay.Roles.Select(role => role.SpawnRuleId).Order().SequenceEqual(
                forestNight.Roles.Select(role => role.SpawnRuleId).Order(),
                StringComparer.OrdinalIgnoreCase));
        Assert.False(
            forestDay.Roles.Select(role => role.SpawnRuleId).Order().SequenceEqual(
                amberDay.Roles.Select(role => role.SpawnRuleId).Order(),
                StringComparer.OrdinalIgnoreCase));
        Assert.True(result.Database.Encounters.TryGetById("twilight_marsh_lantern_bog", out _));
        Assert.True(result.Database.Encounters.TryGetById("forest_underground_hunt", out _));
        Assert.True(result.Database.Encounters.TryGetById("crystal_depths_resonance", out _));

        foreach (var encounter in result.Database.Encounters.Definitions)
        {
            foreach (var role in encounter.Roles)
            {
                Assert.True(result.Database.SpawnRules.TryGetById(role.SpawnRuleId, out var spawnRule));
                Assert.True(result.Database.Entities.TryGetById(spawnRule.EntityId, out var entity));
                Assert.True(
                    entity.Ai is { Kind: not AiBehaviorKind.None } || !string.IsNullOrWhiteSpace(entity.AiBehavior),
                    $"Encounter '{encounter.Id}' role '{role.Id}' resolves to null AI through '{spawnRule.Id}'.");
            }
        }
    }

    private static Game.Core.Spawning.EncounterDefinition GetEncounter(
        GameContentDatabase database,
        string id)
    {
        Assert.True(database.Encounters.TryGetById(id, out var encounter));
        return encounter;
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
