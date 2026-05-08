using Game.Core.Biomes;

namespace Game.Core.World.Generation;

public sealed record WorldGenerationResult(World World, BiomeMap Biomes, TilePos SpawnTile);
