using Game.Core.Maps;
using Game.Core.World;
using Xunit;

namespace Game.Tests.MapTests;

public sealed class TopDownMapQueryServiceTests
{
    [Fact]
    public void IsBlocked_UsesCollisionLayersObjectsAndBounds()
    {
        var map = CreateMap();
        var queries = new TopDownMapQueryService();

        Assert.True(queries.IsBlocked(map, new TilePos(-1, 1)));
        Assert.True(queries.IsBlocked(map, new TilePos(0, 0)));
        Assert.True(queries.IsBlocked(map, new TilePos(2, 1)));
        Assert.False(queries.IsBlocked(map, new TilePos(1, 1)));
    }

    [Fact]
    public void QueryObjects_FindsInteractablesInRegion()
    {
        var map = CreateMap();
        var queries = new TopDownMapQueryService();

        var objects = queries.QueryObjects(map, new RectI(3, 1, 2, 2), interactableOnly: true);

        var sign = Assert.Single(objects);
        Assert.Equal("sign", sign.Id);
    }

    [Fact]
    public void TryResolveWarp_ReturnsTarget()
    {
        var map = CreateMap();
        var queries = new TopDownMapQueryService();

        var resolved = queries.TryResolveWarp(map, new TilePos(4, 2), out var warp);

        Assert.True(resolved);
        Assert.Equal("town", warp.TargetMapId);
        Assert.Equal("west", warp.TargetSpawnId);
        Assert.Equal("east_exit", warp.ObjectId);
    }

    private static MapDefinition CreateMap()
    {
        return new MapDefinition
        {
            Id = "farm",
            DisplayName = "Farm",
            WidthTiles = 5,
            HeightTiles = 4,
            Layers = new[]
            {
                new MapTileLayerDefinition
                {
                    Id = "collision",
                    Kind = MapLayerKind.Collision,
                    Width = 5,
                    Height = 4,
                    BlocksMovement = true,
                    Tiles = new[]
                    {
                        1,1,1,1,1,
                        1,0,0,0,1,
                        1,0,0,0,0,
                        1,1,1,1,1
                    }
                }
            },
            Objects = new[]
            {
                new MapObjectDefinition
                {
                    Id = "rock",
                    Kind = MapObjectKind.Generic,
                    TileX = 2,
                    TileY = 1,
                    BlocksMovement = true
                },
                new MapObjectDefinition
                {
                    Id = "sign",
                    Kind = MapObjectKind.Sign,
                    TileX = 3,
                    TileY = 1,
                    IsInteractable = true,
                    InteractionId = "farm_sign"
                },
                new MapObjectDefinition
                {
                    Id = "east_exit",
                    Kind = MapObjectKind.Warp,
                    TileX = 4,
                    TileY = 2,
                    TargetMapId = "town",
                    TargetSpawnId = "west"
                }
            },
            SpawnPoints = new[]
            {
                new MapSpawnPointDefinition { Id = "home", TileX = 1, TileY = 1 }
            }
        };
    }
}
