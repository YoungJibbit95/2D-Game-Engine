using Game.Core.Biomes;
using Game.Core.Data;
using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class BiomeTreeMaterialTests
{
    [Fact]
    public void RepositoryBiomesBindDistinctAppendedTreeMaterialsAndLooseFoliageAssets()
    {
        var content = new GameContentLoader().LoadFromRoot(FindGameDataRoot());
        AssertMaterial(content, "forest", "normal_tree", KnownTileIds.OakTrunk, KnownTileIds.OakLeaves,
            "tiles/loose_oak_leaves_v2a_autotile");
        AssertMaterial(content, "frostwood", "frost_pine", KnownTileIds.OakTrunk, KnownTileIds.OakLeaves,
            "tiles/loose_oak_leaves_v2a_autotile");
        AssertMaterial(content, "amber_grove", "amber_tree", KnownTileIds.LivingWood, KnownTileIds.AutumnLeaves,
            "tiles/loose_autumn_leaves_v2a_autotile");
        AssertMaterial(content, "twilight_marsh", "mangrove_tree", KnownTileIds.MangroveRoot, KnownTileIds.MarshLeaves,
            "tiles/loose_marsh_leaves_v2a_autotile");

        Assert.Equal((ushort)16, KnownTileIds.OakTrunk);
        Assert.Equal((ushort)17, KnownTileIds.OakLeaves);
        Assert.Equal((ushort)18, KnownTileIds.LivingWood);
        Assert.Equal((ushort)19, KnownTileIds.AutumnLeaves);
        Assert.Equal((ushort)20, KnownTileIds.MarshLeaves);
        Assert.Equal((ushort)21, KnownTileIds.Snow);
        Assert.Equal((ushort)22, KnownTileIds.Ice);
    }

    [Fact]
    public void InfiniteGenerationUsesBiomeTreeMaterialsDeterministicallyIncludingNegativeX()
    {
        var dataRoot = FindGameDataRoot();
        var biomes = new BiomeJsonLoader().LoadRegistryFromDirectory(Path.Combine(dataRoot, "biomes"));
        var profile = WorldGenerationProfile.Small with
        {
            WidthTiles = 256,
            HeightTiles = 128,
            SurfaceBaseY = 42,
            SurfaceAmplitude = 2,
            TreeAttempts = 256,
            TreeAttemptChance = 1f,
            TreeMinHeight = 7,
            TreeMaxHeight = 8,
            WaterPocketAttempts = 0,
            Ores = Array.Empty<OreGenerationDefinition>()
        };
        var regionalProfile = new RegionalGenerationProfile
        {
            Id = "tree-material-test",
            RegionWidthTiles = 128,
            BiomeSpanRegions = 1,
            WorldHeightTiles = profile.HeightTiles,
            SurfaceBaseY = profile.SurfaceBaseY,
            CaveRegionAttempts = 0
        };
        const int seed = 73_331;
        var planner = new WorldRegionPlanner(seed, regionalProfile, biomes);
        var generator = new InfiniteWorldChunkGenerator(regionalPlanner: planner);
        var foundNegativeRegion = false;

        AssertGeneratedMaterial("forest", KnownTileIds.OakTrunk, KnownTileIds.OakLeaves);
        AssertGeneratedMaterial("amber_grove", KnownTileIds.LivingWood, KnownTileIds.AutumnLeaves);
        AssertGeneratedMaterial("twilight_marsh", KnownTileIds.MangroveRoot, KnownTileIds.MarshLeaves);
        Assert.True(foundNegativeRegion);

        void AssertGeneratedMaterial(string biomeId, ushort trunkTileId, ushort canopyTileId)
        {
            var region = Enumerable.Range(-96, 193)
                .Select(index => planner.PlanRegion(index))
                .First(value => value.Biome.Id.Equals(biomeId, StringComparison.OrdinalIgnoreCase));
            foundNegativeRegion |= region.RegionIndex < 0;
            var centerX = checked((int)(region.StartTileX + (region.EndTileXInclusive - region.StartTileX) / 2));
            var centerChunkX = CoordinateUtils.TileToChunk(centerX, 0).X;
            var first = new[]
            {
                generator.GenerateChunk(profile, seed, new ChunkPos(centerChunkX, 0)),
                generator.GenerateChunk(profile, seed, new ChunkPos(centerChunkX, 1))
            };
            var replayPlanner = new WorldRegionPlanner(seed, regionalProfile, biomes);
            var replay = new InfiniteWorldChunkGenerator(regionalPlanner: replayPlanner);
            var second = new[]
            {
                replay.GenerateChunk(profile, seed, new ChunkPos(centerChunkX, 0)),
                replay.GenerateChunk(profile, seed, new ChunkPos(centerChunkX, 1))
            };

            Assert.Equal(first[0].Tiles, second[0].Tiles);
            Assert.Equal(first[1].Tiles, second[1].Tiles);
            Assert.Contains(first.SelectMany(chunk => chunk.Tiles), tile => tile.TileId == trunkTileId);
            Assert.Contains(first.SelectMany(chunk => chunk.Tiles), tile => tile.TileId == canopyTileId);
        }
    }

    [Fact]
    public void FiniteLegacyTreeGenerationRemainsDeterministicAndFoliageAware()
    {
        var profile = WorldGenerationProfile.Small with
        {
            WidthTiles = 128,
            HeightTiles = 80,
            TreeAttempts = 80,
            TreeAttemptChance = 1f
        };

        var first = new AdvancedWorldGenerator().Generate(profile, seed: 42);
        var second = new AdvancedWorldGenerator().Generate(profile, seed: 42);

        Assert.Equal(
            first.Chunks.Values.SelectMany(chunk => chunk.Tiles).ToArray(),
            second.Chunks.Values.SelectMany(chunk => chunk.Tiles).ToArray());
        Assert.Contains(first.Chunks.Values.SelectMany(chunk => chunk.Tiles), tile => KnownTileIds.IsTreeTrunk(tile.TileId));
        Assert.Contains(first.Chunks.Values.SelectMany(chunk => chunk.Tiles), tile => KnownTileIds.IsFoliage(tile.TileId));
    }

    private static void AssertMaterial(
        GameContentDatabase content,
        string biomeId,
        string treeType,
        ushort trunkTileId,
        ushort canopyTileId,
        string canopySpriteId)
    {
        var biome = content.Biomes.GetById(biomeId);
        var material = TreeMaterialResolver.Resolve(biome);
        Assert.Equal(treeType, biome.TreeType);
        Assert.Equal(trunkTileId, material.TrunkTileId);
        Assert.Equal(canopyTileId, material.CanopyTileId);
        var canopy = content.Tiles.GetByNumericId(canopyTileId);
        Assert.Equal(canopySpriteId, canopy.TexturePath);
        Assert.False(canopy.Solid);
        Assert.False(canopy.BlocksLight);
        Assert.True(content.SpriteAssets.TryGetById(canopySpriteId, out var sprite));
        Assert.Equal(16, sprite.Frames.Count);
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

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }
}
