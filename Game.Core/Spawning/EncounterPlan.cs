namespace Game.Core.Spawning;

public sealed record EncounterPlan(
    EncounterDefinition Definition,
    IReadOnlyList<EncounterSpawnIntent> Spawns);

public readonly record struct EncounterSpawnIntent(
    string RoleId,
    string SpawnRuleId,
    int RoleOrdinal);
