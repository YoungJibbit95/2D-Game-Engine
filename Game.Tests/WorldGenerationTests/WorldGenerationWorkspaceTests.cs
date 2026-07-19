using Game.Core;
using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldGenerationTests;

public sealed class WorldGenerationWorkspaceTests
{
    [Fact]
    public void FiniteGenerators_PreserveDeterministicMaterializationHashes()
    {
        var simple = new SimpleWorldGenerator().Generate(256, 128, seed: 1337);
        var advanced = new AdvancedWorldGenerator().Generate(256, 128, seed: 1337);

        Assert.Equal(0x8402E5E667368676UL, ComputeHash(simple));
        // Shared multi-band surface sampling and the sparse V3 branch/crown
        // silhouettes intentionally establish this finite-world topology baseline.
        Assert.Equal(0xCBC6708E7949C7B3UL, ComputeHash(advanced));
    }

    [Fact]
    public void TileWriter_IsBoundsSafeChunkLocalAndLeavesGeneratedChunksClean()
    {
        var world = new World(65, 33, WorldMetadata.CreateDefault(seed: 9));
        var tiles = new WorldGenerationWorkspace(world);
        var dirt = TileInstance.FromTileId(KnownTileIds.Dirt, TileFlags.IsNatural);

        Assert.True(tiles.SetTile(0, 0, dirt));
        Assert.True(tiles.SetTile(32, 32, dirt));
        Assert.True(tiles.SetTile(64, 32, dirt));
        Assert.False(tiles.SetTile(64, 32, dirt));
        Assert.False(tiles.TrySetTile(-1, 0, dirt));
        Assert.False(tiles.TrySetTile(65, 0, dirt));
        Assert.False(tiles.TrySetTile(0, 33, dirt));

        Assert.Equal(3L, tiles.ChangedTiles);
        Assert.Equal(3, world.Chunks.Count);
        Assert.Equal(dirt, tiles.GetTile(64, 32));
        Assert.All(world.Chunks.Values, chunk =>
        {
            Assert.False(chunk.IsDirty);
            Assert.False(chunk.NeedsMeshRebuild);
            Assert.False(chunk.NeedsLightUpdate);
        });
    }

    [Fact]
    public void TileWriter_RejectsInfiniteWorldMaterialization()
    {
        var world = new World(
            GameConstants.ChunkSize,
            64,
            WorldMetadata.CreateDefault(seed: 9),
            isHorizontallyInfinite: true);

        Assert.Throws<ArgumentException>(() => new WorldGenerationWorkspace(world));
    }

    [Fact]
    public void TileWriter_ObservesChunksCreatedByCompatibleCustomSteps()
    {
        var world = new World(64, 64, WorldMetadata.CreateDefault(seed: 9));
        var tiles = new WorldGenerationWorkspace(world);

        Assert.False(tiles.TryGetTile(40, 40, out _));
        world.SetTile(40, 40, KnownTileIds.Stone);

        Assert.True(tiles.TryGetTile(40, 40, out var tile));
        Assert.Equal(KnownTileIds.Stone, tile.TileId);
    }

    private static ulong ComputeHash(World world)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;

        Add(unchecked((ulong)(uint)world.WidthTiles));
        Add(unchecked((ulong)(uint)world.HeightTiles));
        Add(unchecked((ulong)(uint)world.Metadata.Seed));
        Add(unchecked((ulong)(uint)world.Metadata.SpawnTile.X));
        Add(unchecked((ulong)(uint)world.Metadata.SpawnTile.Y));
        for (var y = 0; y < world.HeightTiles; y++)
        {
            for (var x = 0; x < world.WidthTiles; x++)
            {
                var tile = world.GetTile(x, y);
                Add(tile.TileId);
                Add(tile.WallId);
                Add(tile.LiquidAmount);
                Add(tile.Light);
                Add((ushort)tile.Flags);
            }
        }

        return hash;

        void Add(ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= prime;
            }
        }
    }
}
