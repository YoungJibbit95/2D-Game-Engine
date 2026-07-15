namespace Game.Core.Loot;

public readonly record struct LootRollKey(int WorldSeed, long DeathSequence, int VictimEntityId);
