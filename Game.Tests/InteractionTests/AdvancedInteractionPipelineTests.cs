using System.Numerics;
using Game.Core;
using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.InteractionTests;

public sealed class AdvancedInteractionPipelineTests
{
    [Fact]
    public void Query_PrioritizesEntityThenHarvestTileAndPlaceable()
    {
        var world = CreateInfiniteWorld();
        LoadChunkAt(world, -2, 4);
        var bounds = TileBounds(new TilePos(-2, 4));
        var candidates = new[]
        {
            InteractionCandidate.AtTile(InteractionTargetKind.Placeable, new TilePos(-2, 4), "placement"),
            InteractionCandidate.AtTile(InteractionTargetKind.Tile, new TilePos(-2, 4), "tile"),
            InteractionCandidate.AtTile(InteractionTargetKind.Harvest, new TilePos(-2, 4), "berry"),
            InteractionCandidate.ForEntity(7, "merchant", bounds)
        };
        var query = new InteractionQuery(
            new Vector2(-56, 72),
            new RectI(-64, 56, 16, 32),
            new Vector2(-24, 72),
            96,
            InteractionKindMask.All,
            RequireLineOfSight: true,
            AimAssistRadiusPixels: 32);

        var result = new InteractionQueryService().Resolve(world, query, candidates);

        Assert.True(result.Success);
        Assert.Equal(InteractionTargetKind.Entity, result.Candidate.Identity.Kind);
        Assert.Equal(7, result.Candidate.Identity.EntityId);
    }

    [Fact]
    public void Query_ReportsObstructionAndUnloadedChunksPrecisely()
    {
        var world = CreateInfiniteWorld();
        LoadChunkAt(world, -4, 4);
        LoadChunkAt(world, 0, 4);
        world.SetTile(-2, 4, TileInstance.FromTileId(KnownTileIds.Stone, isSolid: true));
        var query = new InteractionQuery(
            new Vector2(-56, 72),
            new RectI(-64, 56, 16, 32),
            new Vector2(8, 72),
            128,
            InteractionKindMask.Entity,
            RequireLineOfSight: true,
            AimAssistRadiusPixels: 32);
        var blocked = InteractionCandidate.ForEntity(4, "npc", TileBounds(new TilePos(0, 4)));

        var blockedResult = new InteractionQueryService().Resolve(world, query, [blocked]);
        var unloaded = InteractionCandidate.AtTile(
            InteractionTargetKind.Harvest,
            new TilePos(70, 4),
            "herb");
        var unloadedQuery = query with
        {
            AimWorldPosition = new Vector2(70 * GameConstants.TileSize + 8, 72),
            ReachPixels = 2_000,
            AllowedKinds = InteractionKindMask.Harvest
        };
        var unloadedResult = new InteractionQueryService().Resolve(world, unloadedQuery, [unloaded]);

        Assert.False(blockedResult.Success);
        Assert.Equal(InteractionFailure.Obstructed, blockedResult.Failure);
        Assert.Equal(InteractionFailure.ChunkNotLoaded, unloadedResult.Failure);
    }

    [Fact]
    public void HoldTracker_SupportsProgressPauseDecayRetargetAndCompletion()
    {
        var tracker = new InteractionHoldTracker();
        var target = InteractionCandidate.AtTile(
            InteractionTargetKind.Harvest,
            new TilePos(-3, 4),
            "berry",
            requiredHoldTicks: 4);
        var hit = InteractionResult.Hit(target, 20, 0);
        var snapshot = tracker.Advance(
            InteractionHoldSnapshot.Empty,
            new InteractionHoldInput(1, true, hit, InteractionCancelPolicy.PauseOnRelease));
        snapshot = tracker.Advance(
            snapshot,
            new InteractionHoldInput(2, false, hit, InteractionCancelPolicy.PauseOnRelease));

        Assert.Equal(InteractionHoldStatus.Paused, snapshot.Status);
        Assert.Equal(1, snapshot.AccumulatedTicks);

        snapshot = tracker.Advance(
            snapshot,
            new InteractionHoldInput(3, true, hit, InteractionCancelPolicy.PauseOnRelease));
        snapshot = tracker.Advance(
            snapshot,
            new InteractionHoldInput(5, true, hit, InteractionCancelPolicy.PauseOnRelease));

        Assert.Equal(InteractionHoldStatus.Completed, snapshot.Status);
        Assert.Equal(1f, snapshot.Progress);

        var other = InteractionCandidate.AtTile(
            InteractionTargetKind.Harvest,
            new TilePos(-2, 4),
            "flower",
            requiredHoldTicks: 5);
        var retargeted = tracker.Advance(
            snapshot,
            new InteractionHoldInput(
                6,
                true,
                InteractionResult.Hit(other, 20, 0),
                InteractionCancelPolicy.Default));

        Assert.Equal(InteractionHoldStatus.Started, retargeted.Status);
        Assert.Equal(other.Identity, retargeted.Target);
        Assert.Equal(1, retargeted.AccumulatedTicks);
    }

    [Fact]
    public void MiningProgressCalculator_IsSharedAcrossRealtimeAndFixedTickProgress()
    {
        var tile = CreateTiles().GetById("dirt");
        var tuning = new MiningTuning
        {
            BaseSpeedMultiplier = 2f,
            ToolPowerForDoubleSpeed = 100f,
            MinimumHardness = 0.05f
        };

        var perSecond = MiningProgressCalculator.GetProgressPerSecond(tile, 50, tuning, 1.2f);
        var requiredTicks = MiningProgressCalculator.GetRequiredFixedTicks(tile, 50, tuning, 60, 1.2f);

        Assert.Equal(1.8f, perSecond, 3);
        Assert.Equal(34, requiredTicks);
    }

    [Fact]
    public void MiningCandidate_UsesTheSameToolHardnessAndTuningFormula()
    {
        var world = CreateInfiniteWorld();
        world.SetTile(-2, 4, TileInstance.FromTileId(KnownTileIds.Dirt, isSolid: true));
        var tuning = new MiningTuning { BaseSpeedMultiplier = 2f };

        var candidate = MiningInteractionCandidateFactory.Create(
            world,
            CreateTiles(),
            new TilePos(-2, 4),
            50,
            tuning,
            fixedTicksPerSecond: 60);
        var expected = MiningProgressCalculator.GetRequiredFixedTicks(
            CreateTiles().GetById("dirt"),
            50,
            tuning,
            60);

        Assert.True(candidate.Success);
        Assert.Equal(expected, candidate.Candidate.RequiredHoldTicks);
        Assert.Equal(new TilePos(-2, 4), candidate.Candidate.Identity.TilePosition);
    }

    [Fact]
    public void StrictPlacement_RejectsUnloadedLiquidActorAndUnsupportedTargets()
    {
        var world = CreateInfiniteWorld();
        var validator = new BuildingPlacementValidator();
        var request = Request(new TilePos(-2, 4));

        var unloaded = validator.Validate(world, CreateItems(), CreateTiles(), request);
        LoadChunkAt(world, -2, 4);
        world.SetTile(-2, 4, TileInstance.Liquid(200));
        var liquid = validator.Validate(world, CreateItems(), CreateTiles(), request);
        world.RemoveTile(-2, 4);
        var actorCollision = validator.Validate(
            world,
            CreateItems(),
            CreateTiles(),
            request with { ActorBoundsWorld = TileBounds(new TilePos(-2, 4)) });
        var unsupported = validator.Validate(world, CreateItems(), CreateTiles(), request);

        Assert.Equal(BuildingPlacementFailure.ChunkNotLoaded, unloaded.Failure);
        Assert.Equal(BuildingPlacementFailure.LiquidOccupied, liquid.Failure);
        Assert.Equal(BuildingPlacementFailure.ActorCollision, actorCollision.Failure);
        Assert.Equal(BuildingPlacementFailure.Unsupported, unsupported.Failure);
    }

    [Fact]
    public void PlacementTransaction_IsOptimisticAtomicAndWorksAtNegativeX()
    {
        var world = CreateInfiniteWorld();
        LoadChunkAt(world, -2, 5);
        world.SetTile(-2, 5, TileInstance.FromTileId(KnownTileIds.Stone, isSolid: true));
        var items = CreateItems();
        var tiles = CreateTiles();
        var inventory = new Inventory(4, items);
        inventory.AddItem(new ItemStack("dirt_block", 2));
        var transactions = new BuildingPlacementTransactionService();

        var prepared = transactions.Prepare(world, inventory, items, tiles, Request(new TilePos(-2, 4)));
        Assert.True(prepared.Success);
        inventory.AddItem(new ItemStack("dirt_block", 1));
        var stale = transactions.Commit(prepared.Plan!);

        Assert.Equal(BuildingPlacementFailure.InventoryChanged, stale.Failure);
        Assert.True(world.GetTile(-2, 4).IsAir);

        var fresh = transactions.Prepare(world, inventory, items, tiles, Request(new TilePos(-2, 4)));
        var committed = transactions.Commit(fresh.Plan!);

        Assert.True(committed.Success);
        Assert.Equal(KnownTileIds.Dirt, world.GetTile(-2, 4).TileId);
        Assert.Equal(2, inventory.CountItem("dirt_block"));
    }

    [Fact]
    public void PlacementTransaction_SupportsAuthoritativePlayerInventory()
    {
        var world = CreateInfiniteWorld();
        LoadChunkAt(world, -2, 5);
        world.SetTile(-2, 5, TileInstance.FromTileId(KnownTileIds.Stone, isSolid: true));
        var items = CreateItems();
        var inventory = new PlayerInventory(items);
        inventory.AddItem(new ItemStack("dirt_block", 1));
        var building = new BuildingSystem();

        var prepared = building.PreparePlacement(
            world,
            inventory,
            items,
            CreateTiles(),
            Request(new TilePos(-2, 4)));
        var committed = building.CommitPlacement(prepared.Plan!);

        Assert.True(prepared.Success);
        Assert.True(committed.Success);
        Assert.Equal(0, inventory.CountItem("dirt_block"));
        Assert.Equal(KnownTileIds.Dirt, world.GetTile(-2, 4).TileId);
    }

    private static World CreateInfiniteWorld()
    {
        return new World(32, 32, WorldMetadata.CreateDefault(seed: 42), isHorizontallyInfinite: true);
    }

    private static void LoadChunkAt(World world, int tileX, int tileY)
    {
        world.GetOrCreateChunk(CoordinateUtils.TileToChunk(tileX, tileY));
    }

    private static RectI TileBounds(TilePos tile)
    {
        return new RectI(
            tile.X * GameConstants.TileSize,
            tile.Y * GameConstants.TileSize,
            GameConstants.TileSize,
            GameConstants.TileSize);
    }

    private static BuildingPlacementRequest Request(TilePos target)
    {
        return new BuildingPlacementRequest(
            target,
            new ItemStack("dirt_block", 1),
            new Vector2(-56, 72),
            new RectI(-64, 56, 16, 32),
            96,
            BuildingPlacementOptions.Strict);
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(
        [
            new ItemDefinition
            {
                Id = "dirt_block",
                DisplayName = "Dirt Block",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/dirt",
                MaxStack = 999,
                PlacesTileId = "dirt",
                PlacementSupport = PlacementSupportRule.AdjacentSolid
            }
        ]);
    }

    private static TileRegistry CreateTiles()
    {
        return TileRegistry.Create(
        [
            new TileDefinition
            {
                NumericId = KnownTileIds.Dirt,
                Id = "dirt",
                DisplayName = "Dirt",
                TexturePath = "tiles/dirt",
                Solid = true,
                BlocksLight = true,
                Hardness = 2f,
                DropItemId = "dirt_block"
            },
            new TileDefinition
            {
                NumericId = KnownTileIds.Stone,
                Id = "stone",
                DisplayName = "Stone",
                TexturePath = "tiles/stone",
                Solid = true,
                BlocksLight = true,
                Hardness = 3f,
                DropItemId = "stone_block"
            }
        ]);
    }
}
