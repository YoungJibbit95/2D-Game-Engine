using Game.Core.World;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class SimpleWorldGeneratorTests
{
    [Fact]
    public void Generate_ProducesSameWorldForSameSeed()
    {
        var generator = new SimpleWorldGenerator();

        var first = generator.Generate(96, 64, seed: 42);
        var second = generator.Generate(96, 64, seed: 42);

        for (var x = 0; x < first.WidthTiles; x++)
        {
            for (var y = 0; y < first.HeightTiles; y++)
            {
                Assert.Equal(first.GetTile(x, y).TileId, second.GetTile(x, y).TileId);
            }
        }
    }

    [Fact]
    public void Generate_PlacesGrassOnSurfaceWithDirtAndStoneBelow()
    {
        var generator = new SimpleWorldGenerator();
        var world = generator.Generate(96, 64, seed: 99);

        for (var x = 0; x < world.WidthTiles; x++)
        {
            var surfaceY = FindFirstSolidTileY(world, x);

            Assert.True(surfaceY > 0);
            Assert.Equal(KnownTileIds.Grass, world.GetTile(x, surfaceY).TileId);
            Assert.Equal(KnownTileIds.Dirt, world.GetTile(x, surfaceY + 1).TileId);
            Assert.Equal(KnownTileIds.Stone, world.GetTile(x, world.HeightTiles - 1).TileId);
        }
    }

    [Fact]
    public void Generate_ClearsDirtyFlagsAfterInitialGeneration()
    {
        var generator = new SimpleWorldGenerator();

        var world = generator.Generate(64, 64, seed: 7);

        Assert.All(world.Chunks.Values, chunk =>
        {
            Assert.False(chunk.IsDirty);
            Assert.False(chunk.NeedsMeshRebuild);
            Assert.False(chunk.NeedsLightUpdate);
        });
    }

    [Fact]
    public void Generate_StoresSurfaceSpawnTileInMetadata()
    {
        var world = new SimpleWorldGenerator().Generate(96, 64, seed: 123);
        var spawn = world.Metadata.SpawnTile;

        Assert.True(world.IsInBounds(spawn.X, spawn.Y));
        Assert.False(world.IsSolid(spawn.X, spawn.Y));
        Assert.True(world.IsSolid(spawn.X, spawn.Y + 3));
    }

    private static int FindFirstSolidTileY(World world, int x)
    {
        for (var y = 0; y < world.HeightTiles; y++)
        {
            if (world.IsSolid(x, y))
            {
                return y;
            }
        }

        throw new InvalidOperationException($"Column {x} has no solid tiles.");
    }
}
