using System.Numerics;
using Game.Core.Events;
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

    [Fact]
    public void QueryService_RuntimeStateOpenDoorDoesNotBlockMovement()
    {
        var map = CreateActionMap();
        var state = new TopDownMapRuntimeState("actions");
        var queries = new TopDownMapQueryService();

        Assert.True(queries.IsBlocked(map, new TilePos(2, 1), state));

        state.GetOrCreateObject("cabin_door").IsOpen = true;

        Assert.False(queries.IsBlocked(map, new TilePos(2, 1), state));
    }

    [Fact]
    public void QueryService_DisabledObjectsDoNotBlockOrTarget()
    {
        var map = CreateActionMap();
        var state = new TopDownMapRuntimeState("actions");
        state.GetOrCreateObject("crate").IsEnabled = false;
        var queries = new TopDownMapQueryService();

        Assert.False(queries.IsBlocked(map, new TilePos(4, 1), state));
        Assert.Empty(queries.FindObjectsAt(map, new TilePos(4, 1), interactableOnly: true, runtimeState: state));
    }

    [Fact]
    public void ObjectInteractionSystem_ShowMessageUsesTextAndPublishesEvent()
    {
        var maps = MapRegistry.Create(new[] { CreateActionMap(), CreateTownMap() });
        var session = TopDownMapSession.CreateAtSpawn(maps, "actions", "sign_reader", new Vector2(12, 12));
        session.Body.Facing = TopDownFacing.Right;
        var states = new TopDownMapRuntimeStateStore();
        var events = new GameEventBus();
        TopDownMapObjectInteractedEvent? published = null;
        using var subscription = events.Subscribe<TopDownMapObjectInteractedEvent>(item => published = item);

        var result = new TopDownMapObjectInteractionSystem().Interact(maps, session, states, events: events);

        Assert.True(result.Success);
        Assert.Equal(TopDownMapObjectActionKind.ShowMessage, result.ActionKind);
        Assert.Equal("Read me.", result.Text);
        Assert.Equal("sign_text", result.PayloadId);
        Assert.NotNull(published);
        Assert.Equal("sign", published!.ObjectId);
        Assert.Equal(TopDownMapObjectActionKind.ShowMessage, published.ActionKind);
    }

    [Fact]
    public void ObjectInteractionSystem_OpenContainerReturnsContainerPayload()
    {
        var maps = MapRegistry.Create(new[] { CreateActionMap(), CreateTownMap() });
        var session = TopDownMapSession.CreateAtSpawn(maps, "actions", "crate_opener", new Vector2(12, 12));
        session.Body.Facing = TopDownFacing.Right;
        var states = new TopDownMapRuntimeStateStore();

        var result = new TopDownMapObjectInteractionSystem().Interact(maps, session, states);

        Assert.True(result.Success);
        Assert.Equal(TopDownMapObjectActionKind.OpenContainer, result.ActionKind);
        Assert.Equal("crate_inventory", result.PayloadId);
    }

    [Fact]
    public void ObjectInteractionSystem_ToggleDoorUpdatesRuntimeCollision()
    {
        var map = CreateActionMap();
        var maps = MapRegistry.Create(new[] { map, CreateTownMap() });
        var session = TopDownMapSession.CreateAtSpawn(maps, "actions", "door_user", new Vector2(12, 12));
        session.Body.Facing = TopDownFacing.Right;
        var states = new TopDownMapRuntimeStateStore();
        var queries = new TopDownMapQueryService();
        var system = new TopDownMapObjectInteractionSystem();
        var mapState = states.GetOrCreateMap("actions");

        var opened = system.Interact(maps, session, states);

        Assert.True(opened.Success);
        Assert.Equal(TopDownMapObjectActionKind.ToggleDoor, opened.ActionKind);
        Assert.True(opened.IsOpen);
        Assert.False(queries.IsBlocked(map, new TilePos(2, 1), mapState));

        var closed = system.Interact(maps, session, states);

        Assert.True(closed.Success);
        Assert.False(closed.IsOpen);
        Assert.True(queries.IsBlocked(map, new TilePos(2, 1), mapState));
    }

    [Fact]
    public void ObjectInteractionSystem_WarpObjectAppliesTransitionAndPublishesTransitionEvent()
    {
        var maps = MapRegistry.Create(new[] { CreateActionMap(), CreateTownMap() });
        var session = TopDownMapSession.CreateAtSpawn(maps, "actions", "warp_user", new Vector2(12, 12));
        session.Body.Facing = TopDownFacing.Right;
        var states = new TopDownMapRuntimeStateStore();
        var events = new GameEventBus();
        TopDownMapTransitionedEvent? transitionEvent = null;
        using var subscription = events.Subscribe<TopDownMapTransitionedEvent>(item => transitionEvent = item);

        var result = new TopDownMapObjectInteractionSystem().Interact(maps, session, states, events: events);

        Assert.True(result.Success);
        Assert.Equal(TopDownMapObjectActionKind.Warp, result.ActionKind);
        Assert.Equal("town", session.CurrentMapId);
        Assert.Equal("west", session.CurrentSpawnId);
        Assert.NotNull(result.Transition);
        Assert.NotNull(transitionEvent);
        Assert.True(transitionEvent!.Result.Success);
    }

    [Fact]
    public void ObjectInteractionSystem_ActionOverrideSupportsShopsAndTriggers()
    {
        var maps = MapRegistry.Create(new[] { CreateActionMap(), CreateTownMap() });
        var states = new TopDownMapRuntimeStateStore();
        var system = new TopDownMapObjectInteractionSystem();
        var shopSession = TopDownMapSession.CreateAtSpawn(maps, "actions", "shopper", new Vector2(12, 12));
        shopSession.Body.Facing = TopDownFacing.Right;

        var shop = system.Interact(maps, shopSession, states);

        Assert.True(shop.Success);
        Assert.Equal(TopDownMapObjectActionKind.OpenShop, shop.ActionKind);
        Assert.Equal("seed_shop", shop.PayloadId);

        var triggerSession = TopDownMapSession.CreateAtSpawn(maps, "actions", "trigger_user", new Vector2(12, 12));
        triggerSession.Body.Facing = TopDownFacing.Right;

        var trigger = system.Interact(maps, triggerSession, states);

        Assert.True(trigger.Success);
        Assert.Equal(TopDownMapObjectActionKind.Trigger, trigger.ActionKind);
        Assert.Equal("intro_cutscene", trigger.PayloadId);
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

    private static MapDefinition CreateActionMap()
    {
        return new MapDefinition
        {
            Id = "actions",
            DisplayName = "Actions",
            WidthTiles = 8,
            HeightTiles = 4,
            Layers = new[]
            {
                new MapTileLayerDefinition
                {
                    Id = "collision",
                    Kind = MapLayerKind.Collision,
                    Width = 8,
                    Height = 4,
                    BlocksMovement = true,
                    Tiles = new[]
                    {
                        1,1,1,1,1,1,1,1,
                        1,0,0,0,0,0,0,1,
                        1,0,0,0,0,0,0,1,
                        1,1,1,1,1,1,1,1
                    }
                }
            },
            Objects = new[]
            {
                new MapObjectDefinition
                {
                    Id = "cabin_door",
                    Kind = MapObjectKind.Door,
                    TileX = 2,
                    TileY = 1,
                    BlocksMovement = true,
                    IsInteractable = true,
                    InteractionId = "door",
                    Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["doorId"] = "cabin"
                    }
                },
                new MapObjectDefinition
                {
                    Id = "sign",
                    Kind = MapObjectKind.Sign,
                    TileX = 2,
                    TileY = 2,
                    IsInteractable = true,
                    InteractionId = "fallback_message",
                    Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["text"] = "Read me.",
                        ["textKey"] = "sign_text"
                    }
                },
                new MapObjectDefinition
                {
                    Id = "crate",
                    Kind = MapObjectKind.Container,
                    TileX = 4,
                    TileY = 1,
                    BlocksMovement = true,
                    IsInteractable = true,
                    Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["containerId"] = "crate_inventory"
                    }
                },
                new MapObjectDefinition
                {
                    Id = "shop_counter",
                    Kind = MapObjectKind.Furniture,
                    TileX = 4,
                    TileY = 2,
                    IsInteractable = true,
                    Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["action"] = "OpenShop",
                        ["shopId"] = "seed_shop"
                    }
                },
                new MapObjectDefinition
                {
                    Id = "warp_door",
                    Kind = MapObjectKind.Warp,
                    TileX = 6,
                    TileY = 1,
                    IsInteractable = true,
                    TargetMapId = "town",
                    TargetSpawnId = "west"
                },
                new MapObjectDefinition
                {
                    Id = "script_marker",
                    Kind = MapObjectKind.Trigger,
                    TileX = 6,
                    TileY = 2,
                    IsInteractable = true,
                    Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["triggerId"] = "intro_cutscene"
                    }
                }
            },
            SpawnPoints = new[]
            {
                new MapSpawnPointDefinition { Id = "door_user", TileX = 1, TileY = 1, Facing = "right" },
                new MapSpawnPointDefinition { Id = "sign_reader", TileX = 1, TileY = 2, Facing = "right" },
                new MapSpawnPointDefinition { Id = "crate_opener", TileX = 3, TileY = 1, Facing = "right" },
                new MapSpawnPointDefinition { Id = "shopper", TileX = 3, TileY = 2, Facing = "right" },
                new MapSpawnPointDefinition { Id = "warp_user", TileX = 5, TileY = 1, Facing = "right" },
                new MapSpawnPointDefinition { Id = "trigger_user", TileX = 5, TileY = 2, Facing = "right" }
            }
        };
    }
}
