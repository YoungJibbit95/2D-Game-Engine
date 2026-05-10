using System.Numerics;
using Game.Core.Maps;
using Game.Core.Physics;
using Game.Core.World;
using Xunit;

namespace Game.Tests.MapTests;

public sealed class TopDownMapRuntimeTests
{
    [Fact]
    public void Session_CreatesBodyAtSpawnWithFacing()
    {
        var maps = MapRegistry.Create(new[] { CreateFarmMap(), CreateTownMap() });

        var session = TopDownMapSession.CreateAtSpawn(maps, "farm", "home", new Vector2(10, 12));

        Assert.Equal("farm", session.CurrentMapId);
        Assert.Equal("home", session.CurrentSpawnId);
        Assert.Equal(new Vector2(16, 16), session.Body.Position);
        Assert.Equal(new Vector2(10, 12), session.Body.Size);
        Assert.Equal(TopDownFacing.Right, session.Body.Facing);
    }

    [Fact]
    public void Movement_NormalizesDiagonalSpeedAndUpdatesFacing()
    {
        var map = CreateFarmMap();
        var body = new TopDownMapBody(new Vector2(16, 16), new Vector2(8, 8));
        var controller = new TopDownMapMovementController();

        var result = controller.Move(
            map,
            body,
            new Vector2(1, 1),
            1f,
            new TopDownMovementOptions { MoveSpeedPixelsPerSecond = 4f });

        Assert.Equal(16 + 4 / MathF.Sqrt(2), body.Position.X, precision: 4);
        Assert.Equal(16 + 4 / MathF.Sqrt(2), body.Position.Y, precision: 4);
        Assert.Equal(TopDownFacing.Down, result.Facing);
        Assert.False(result.WasBlocked);
    }

    [Fact]
    public void Movement_StopsAgainstCollisionLayer()
    {
        var map = CreateFarmMap();
        var body = new TopDownMapBody(new Vector2(16, 16), new Vector2(12, 12));
        var controller = new TopDownMapMovementController();

        var result = controller.Move(
            map,
            body,
            new Vector2(-1, 0),
            1f,
            new TopDownMovementOptions { MoveSpeedPixelsPerSecond = 8f });

        Assert.True(result.BlockedX);
        Assert.Equal(16, body.Position.X);
        Assert.Equal(0, body.Velocity.X);
    }

    [Fact]
    public void Movement_StopsAgainstBlockingObjects()
    {
        var map = CreateFarmMap();
        var body = new TopDownMapBody(new Vector2(16, 16), new Vector2(12, 12));
        var controller = new TopDownMapMovementController();

        var result = controller.Move(
            map,
            body,
            new Vector2(1, 0),
            1f,
            new TopDownMovementOptions { MoveSpeedPixelsPerSecond = 8f });

        Assert.True(result.BlockedX);
        Assert.Equal(20, body.Position.X);
        Assert.Equal(0, body.Velocity.X);
    }

    [Fact]
    public void Movement_ReportsWarpWhenBodyCenterTouchesWarpTile()
    {
        var map = CreateFarmMap();
        var body = new TopDownMapBody(new Vector2(48, 32), new Vector2(12, 12));
        var controller = new TopDownMapMovementController();

        var result = controller.Move(
            map,
            body,
            Vector2.Zero,
            0f,
            new TopDownMovementOptions { MoveSpeedPixelsPerSecond = 16f });

        Assert.True(result.HasWarp);
        Assert.Equal("town", result.Warp!.TargetMapId);
        Assert.Equal("west", result.Warp.TargetSpawnId);
    }

    [Fact]
    public void Interaction_FindsInteractableInFacingReach()
    {
        var map = CreateFarmMap();
        var body = new TopDownMapBody(new Vector2(32, 16), new Vector2(12, 12))
        {
            Facing = TopDownFacing.Right
        };
        var interactions = new TopDownMapInteractionService();

        var result = interactions.FindInteraction(map, body, reachTiles: 1);

        Assert.True(result.Success);
        Assert.Equal("sign", result.Object!.Id);
        Assert.Equal(new TilePos(2, 1), result.ActorTile);
        Assert.Equal(new TilePos(3, 1), result.TargetTile);
    }

    [Fact]
    public void Interaction_ReturnsFailureReasonWhenNothingIsReachable()
    {
        var map = CreateFarmMap();
        var body = new TopDownMapBody(new Vector2(16, 16), new Vector2(12, 12))
        {
            Facing = TopDownFacing.Up
        };
        var interactions = new TopDownMapInteractionService();

        var result = interactions.FindInteraction(map, body, reachTiles: 1, includeOverlap: false);

        Assert.False(result.Success);
        Assert.Equal("no_interactable_in_reach", result.FailureReason);
    }

    [Fact]
    public void Transition_AppliesWarpAndMovesSessionToTargetSpawn()
    {
        var maps = MapRegistry.Create(new[] { CreateFarmMap(), CreateTownMap() });
        var session = TopDownMapSession.CreateAtSpawn(maps, "farm", "home", new Vector2(12, 12));
        session.Body.Position = new Vector2(48, 32);
        var transitions = new TopDownMapTransitionSystem();

        var result = transitions.TryApplyCurrentWarp(maps, session);

        Assert.True(result.Success);
        Assert.Equal("farm", result.SourceMapId);
        Assert.Equal("town", session.CurrentMapId);
        Assert.Equal("west", session.CurrentSpawnId);
        Assert.Equal(new Vector2(16, 32), session.Body.Position);
        Assert.Equal(TopDownFacing.Left, session.Body.Facing);
    }

    [Fact]
    public void Transition_FailsWhenTargetMapIsMissing()
    {
        var maps = MapRegistry.Create(new[] { CreateFarmMap() });
        var session = TopDownMapSession.CreateAtSpawn(maps, "farm", "home", new Vector2(12, 12));
        session.Body.Position = new Vector2(48, 32);
        var transitions = new TopDownMapTransitionSystem();

        var result = transitions.TryApplyCurrentWarp(maps, session);

        Assert.False(result.Success);
        Assert.Equal("target_map_missing", result.FailureReason);
        Assert.Equal("farm", session.CurrentMapId);
    }

    private static MapDefinition CreateFarmMap()
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
                    Id = "crate",
                    Kind = MapObjectKind.Container,
                    TileX = 2,
                    TileY = 1,
                    BlocksMovement = true,
                    IsInteractable = true,
                    InteractionId = "farm_crate"
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
                    TileX = 3,
                    TileY = 2,
                    TargetMapId = "town",
                    TargetSpawnId = "west"
                }
            },
            SpawnPoints = new[]
            {
                new MapSpawnPointDefinition { Id = "home", TileX = 1, TileY = 1, Facing = "right" }
            }
        };
    }

    private static MapDefinition CreateTownMap()
    {
        return new MapDefinition
        {
            Id = "town",
            DisplayName = "Town",
            WidthTiles = 4,
            HeightTiles = 4,
            Layers = new[]
            {
                new MapTileLayerDefinition
                {
                    Id = "collision",
                    Kind = MapLayerKind.Collision,
                    Width = 4,
                    Height = 4,
                    BlocksMovement = true,
                    Tiles = new[]
                    {
                        1,1,1,1,
                        1,0,0,1,
                        0,0,0,1,
                        1,1,1,1
                    }
                }
            },
            SpawnPoints = new[]
            {
                new MapSpawnPointDefinition { Id = "west", TileX = 1, TileY = 2, Facing = "left" }
            }
        };
    }
}
