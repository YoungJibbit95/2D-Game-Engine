using Game.Core;
using Game.Core.Crafting;
using Game.Core.Events;
using Game.Core.Feedback;
using Game.Core.Inventory;
using Game.Core.World;
using Game.Core.WorldEvents;
using System.Numerics;
using System.Runtime.CompilerServices;
using Xunit;

namespace Game.Tests.FeedbackTests;

public sealed class GameplayFeedbackRouterTests
{
    [Fact]
    public void MiningLifecycle_EmitsThresholdedCuesAndCompletion()
    {
        var bus = new GameEventBus();
        using var router = new GameplayFeedbackRouter(bus);
        var tile = new TilePos(-2, 4);

        bus.Publish(new MiningStartedEvent(tile, KnownTileIds.Stone));
        bus.Publish(new MiningProgressEvent(tile, KnownTileIds.Stone, 0f, 0.11f));
        bus.Publish(new MiningProgressEvent(tile, KnownTileIds.Stone, 0.11f, 0.19f));
        bus.Publish(new MiningProgressEvent(tile, KnownTileIds.Stone, 0.19f, 0.41f));
        bus.Publish(new MiningCompletedEvent(tile, KnownTileIds.Stone, new ItemStack("stone_block", 1)));

        var cues = router.Drain();
        Assert.Equal(4, cues.Count);
        Assert.Equal(GameplayFeedbackCueKind.MiningStarted, cues[0].Kind);
        Assert.Equal(2, cues.Count(cue => cue.Kind == GameplayFeedbackCueKind.MiningImpact));
        Assert.Equal(GameplayFeedbackCueKind.TileBroken, cues[^1].Kind);
        Assert.Equal(
            new Vector2(-2 * GameConstants.TileSize + GameConstants.TileSize * 0.5f, 4 * GameConstants.TileSize + GameConstants.TileSize * 0.5f),
            cues[0].WorldPosition);
    }

    [Fact]
    public void EntityEvents_UsePositionResolver()
    {
        var bus = new GameEventBus();
        using var router = new GameplayFeedbackRouter(bus, id => id == 7 ? new Vector2(12, 34) : null);

        bus.Publish(new MeleeHitEvent(1, 7, 9));
        bus.Publish(new ProjectileHitEvent(2, 99, 4));

        var cue = Assert.Single(router.Drain());
        Assert.Equal(GameplayFeedbackCueKind.MeleeHit, cue.Kind);
        Assert.Equal(new Vector2(12, 34), cue.WorldPosition);
        Assert.Equal(9, cue.Amount);
    }

    [Fact]
    public void Capacity_DropsOldestCues()
    {
        var bus = new GameEventBus();
        using var router = new GameplayFeedbackRouter(bus, capacity: 2);

        bus.Publish(new TilePlacedEvent(new TilePos(1, 1), 1, "first"));
        bus.Publish(new TilePlacedEvent(new TilePos(2, 1), 1, "second"));
        bus.Publish(new TilePlacedEvent(new TilePos(3, 1), 1, "third"));

        var cues = router.Drain();
        Assert.Equal(2, cues.Count);
        Assert.Equal("second", cues[0].ContentId);
        Assert.Equal("third", cues[1].ContentId);
    }

    [Fact]
    public void RarePickupWorldEventAndCrafting_ProduceBoundedVisualAndAudioCommands()
    {
        var bus = new GameEventBus();
        var entityPosition = new Vector2(12, 34);
        var focusPosition = new Vector2(80, 96);
        using var router = new GameplayFeedbackRouter(
            bus,
            _ => entityPosition,
            capacity: 8,
            rareItemResolver: itemId => itemId == "mana_crystal",
            focusPositionResolver: () => focusPosition);

        bus.Publish(new ItemPickedUpEvent(7, new ItemStack("mana_crystal", 1)));
        bus.Publish(new WorldEventActivatedEvent(
            "crystal_surge",
            WorldEventActivationSource.PlayerAction,
            WorldEventPlayerActionKind.Cast,
            4));
        bus.Publish(new CraftingBatchCompletedEvent(
            "torch",
            2,
            2,
            new ItemStack("torch", 2),
            Array.Empty<CraftingIngredientAmount>(),
            false));

        var visuals = new GameplayFeedbackCue[4];
        var audio = new GameplayAudioCue[4];
        Assert.Equal(3, router.DrainTo(visuals));
        Assert.Equal(3, router.DrainAudioTo(audio));
        Assert.Equal(GameplayFeedbackCueKind.RareItemPickup, visuals[0].Kind);
        Assert.Equal(entityPosition, visuals[0].WorldPosition);
        Assert.Equal(GameplayFeedbackCueKind.WorldEventActivated, visuals[1].Kind);
        Assert.Equal(focusPosition, visuals[1].WorldPosition);
        Assert.Equal(GameplayFeedbackCueKind.CraftCompleted, visuals[2].Kind);
        Assert.Equal("gameplay.item.rare-pickup", audio[0].AudioId);
        Assert.Equal("gameplay.world-event.activated", audio[1].AudioId);
        Assert.False(audio[1].IsSpatial);
        Assert.Equal("gameplay.crafting.completed", audio[2].AudioId);
        Assert.Equal(0, router.PendingCount);
        Assert.Equal(0, router.PendingAudioCount);
    }

    [Fact]
    public void RareLootDrop_ProducesWorldPositionedVisualAndAudioCommands()
    {
        var bus = new GameEventBus();
        var position = new Vector2(-42, 128);
        using var router = new GameplayFeedbackRouter(
            bus,
            rareItemResolver: itemId => itemId == "ancient_relic");

        bus.Publish(new LootDroppedEvent(17, new ItemStack("ancient_relic", 2), position));

        var visual = Assert.Single(router.Drain());
        var audio = new GameplayAudioCue[1];
        Assert.Equal(1, router.DrainAudioTo(audio));
        Assert.Equal(GameplayFeedbackCueKind.RareLootDropped, visual.Kind);
        Assert.Equal(position, visual.WorldPosition);
        Assert.Equal(2, visual.Amount);
        Assert.Equal("gameplay.loot.rare-drop", audio[0].AudioId);
        Assert.True(audio[0].IsSpatial);
    }

    [Fact]
    public void Capacity_DropsOldestVisualAndAudioCommandsWithTelemetry()
    {
        var bus = new GameEventBus();
        using var router = new GameplayFeedbackRouter(bus, capacity: 2);

        bus.Publish(new TilePlacedEvent(new TilePos(1, 1), 1, "first"));
        bus.Publish(new TilePlacedEvent(new TilePos(2, 1), 1, "second"));
        bus.Publish(new TilePlacedEvent(new TilePos(3, 1), 1, "third"));

        var visuals = new GameplayFeedbackCue[2];
        var audio = new GameplayAudioCue[2];
        Assert.Equal(2, router.DrainTo(visuals));
        Assert.Equal(2, router.DrainAudioTo(audio));
        Assert.Equal("second", visuals[0].ContentId);
        Assert.Equal("third", visuals[1].ContentId);
        Assert.Equal("gameplay.tile.place", audio[0].AudioId);
        Assert.Equal("gameplay.tile.place", audio[1].AudioId);
        Assert.Equal(1, router.Telemetry.VisualCommandsDropped);
        Assert.Equal(1, router.Telemetry.AudioCommandsDropped);
        Assert.Equal(2, router.Telemetry.VisualCommandsDrained);
        Assert.Equal(2, router.Telemetry.AudioCommandsDrained);
    }

    [Fact]
    public void DrainTo_ReusesCallerBuffersWithoutSteadyStateAllocation()
    {
        var bus = new GameEventBus();
        using var router = new GameplayFeedbackRouter(bus, capacity: 4);
        var gameEvent = new TilePlacedEvent(new TilePos(-4, 6), 1, "stone_block");
        var visuals = new GameplayFeedbackCue[4];
        var audio = new GameplayAudioCue[4];

        Assert.Equal(512, ExerciseDrain(bus, router, gameEvent, visuals, audio, 256));
        Assert.Equal(0, MeasureUntilConsecutiveAllocationFreeWindows(
            bus,
            router,
            gameEvent,
            visuals,
            audio));
    }

    private static long MeasureUntilConsecutiveAllocationFreeWindows(
        GameEventBus bus,
        GameplayFeedbackRouter router,
        TilePlacedEvent gameEvent,
        GameplayFeedbackCue[] visuals,
        GameplayAudioCue[] audio)
    {
        var consecutiveAllocationFreeWindows = 0;
        var lastAllocated = long.MaxValue;
        for (var window = 0; window < 6; window++)
        {
            lastAllocated = MeasureAllocationWindow(bus, router, gameEvent, visuals, audio);
            consecutiveAllocationFreeWindows = lastAllocated == 0
                ? consecutiveAllocationFreeWindows + 1
                : 0;
            if (consecutiveAllocationFreeWindows == 2)
            {
                return 0;
            }
        }

        return lastAllocated;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureAllocationWindow(
        GameEventBus bus,
        GameplayFeedbackRouter router,
        TilePlacedEvent gameEvent,
        GameplayFeedbackCue[] visuals,
        GameplayAudioCue[] audio)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = ExerciseDrain(bus, router, gameEvent, visuals, audio, 1_000);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ExerciseDrain(
        GameEventBus bus,
        GameplayFeedbackRouter router,
        TilePlacedEvent gameEvent,
        GameplayFeedbackCue[] visuals,
        GameplayAudioCue[] audio,
        int iterations)
    {
        var drained = 0;
        for (var index = 0; index < iterations; index++)
        {
            bus.Publish(gameEvent);
            drained += router.DrainTo(visuals);
            drained += router.DrainAudioTo(audio);
        }

        return drained;
    }
}
