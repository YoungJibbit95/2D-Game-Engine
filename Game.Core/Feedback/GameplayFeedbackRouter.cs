using Game.Core.Actions;
using Game.Core.Audio;
using Game.Core.Events;
using Game.Core.World;
using System.Numerics;

namespace Game.Core.Feedback;

public sealed class GameplayFeedbackRouter : IDisposable
{
    private readonly BoundedCommandQueue<GameplayFeedbackCue> _visualCommands;
    private readonly BoundedCommandQueue<GameplayAudioCue> _audioCommands;
    private readonly List<IDisposable> _subscriptions = new();
    private readonly Dictionary<TilePos, int> _miningBuckets = new();
    private readonly Func<int, Vector2?>? _entityPositionResolver;
    private readonly Func<string, bool>? _rareItemResolver;
    private readonly Func<Vector2>? _focusPositionResolver;
    private bool _disposed;

    public GameplayFeedbackRouter(
        GameEventBus events,
        Func<int, Vector2?>? entityPositionResolver = null,
        int capacity = 256,
        Func<string, bool>? rareItemResolver = null,
        Func<Vector2>? focusPositionResolver = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        Capacity = capacity;
        _entityPositionResolver = entityPositionResolver;
        _rareItemResolver = rareItemResolver;
        _focusPositionResolver = focusPositionResolver;
        _visualCommands = new BoundedCommandQueue<GameplayFeedbackCue>(capacity);
        _audioCommands = new BoundedCommandQueue<GameplayAudioCue>(capacity);
        Subscribe(events);
    }

    public int Capacity { get; }

    public int PendingCount => _visualCommands.Count;

    public int PendingAudioCount => _audioCommands.Count;

    public GameplayFeedbackQueueTelemetry Telemetry => new(
        _visualCommands.Count,
        _audioCommands.Count,
        _visualCommands.Enqueued,
        _visualCommands.Dropped,
        _visualCommands.Drained,
        _audioCommands.Enqueued,
        _audioCommands.Dropped,
        _audioCommands.Drained);

    public IReadOnlyList<GameplayFeedbackCue> Drain()
    {
        return _visualCommands.DrainToArray();
    }

    public int DrainTo(Span<GameplayFeedbackCue> destination)
    {
        return _visualCommands.DrainTo(destination);
    }

    public int DrainAudioTo(Span<GameplayAudioCue> destination)
    {
        return _audioCommands.DrainTo(destination);
    }

    public void Clear()
    {
        _visualCommands.Clear();
        _audioCommands.Clear();
        _miningBuckets.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        for (var index = 0; index < _subscriptions.Count; index++)
        {
            _subscriptions[index].Dispose();
        }

        _subscriptions.Clear();
        Clear();
        _disposed = true;
    }

    private void Subscribe(GameEventBus events)
    {
        _subscriptions.Add(events.Subscribe<MiningStartedEvent>(OnMiningStarted));
        _subscriptions.Add(events.Subscribe<MiningProgressEvent>(OnMiningProgress));
        _subscriptions.Add(events.Subscribe<MiningCompletedEvent>(OnMiningCompleted));
        _subscriptions.Add(events.Subscribe<MiningBlockedEvent>(OnMiningBlocked));
        _subscriptions.Add(events.Subscribe<TilePlacedEvent>(gameEvent =>
            Enqueue(new GameplayFeedbackCue(
                GameplayFeedbackCueKind.TilePlaced,
                TileCenter(gameEvent.Position),
                ContentId: gameEvent.ItemId))));
        _subscriptions.Add(events.Subscribe<MeleeHitEvent>(gameEvent =>
            EnqueueAtEntity(GameplayFeedbackCueKind.MeleeHit, gameEvent.TargetEntityId, gameEvent.Damage)));
        _subscriptions.Add(events.Subscribe<ProjectileHitEvent>(gameEvent =>
            EnqueueAtEntity(GameplayFeedbackCueKind.ProjectileHit, gameEvent.TargetEntityId, gameEvent.Damage)));
        _subscriptions.Add(events.Subscribe<EntityDiedEvent>(gameEvent =>
            EnqueueAtEntity(GameplayFeedbackCueKind.EntityDeath, gameEvent.EntityId, contentId: gameEvent.DefinitionId)));
        _subscriptions.Add(events.Subscribe<ItemPickedUpEvent>(OnItemPickedUp));
        _subscriptions.Add(events.Subscribe<LootDroppedEvent>(OnLootDropped));
        _subscriptions.Add(events.Subscribe<CraftingBatchCompletedEvent>(gameEvent =>
            Enqueue(new GameplayFeedbackCue(
                GameplayFeedbackCueKind.CraftCompleted,
                ResolveFocusPosition(),
                Math.Clamp(gameEvent.CraftedQuantity / 4f, 0.75f, 2f),
                gameEvent.Output.Count,
                gameEvent.Output.ItemId))));
        _subscriptions.Add(events.Subscribe<WorldEventActivatedEvent>(gameEvent =>
            Enqueue(new GameplayFeedbackCue(
                GameplayFeedbackCueKind.WorldEventActivated,
                ResolveFocusPosition(),
                1.5f,
                ContentId: gameEvent.EventId))));
        _subscriptions.Add(events.Subscribe<ResourceRestoredEvent>(gameEvent =>
            EnqueueAtEntity(
                GameplayFeedbackCueKind.ResourceRestored,
                gameEvent.EntityId,
                gameEvent.HealthRestored + gameEvent.ManaRestored,
                gameEvent.SourceItemId)));
        _subscriptions.Add(events.Subscribe<StatusEffectAppliedEvent>(gameEvent =>
            EnqueueAtEntity(
                GameplayFeedbackCueKind.StatusEffectApplied,
                gameEvent.TargetEntityId,
                contentId: gameEvent.EffectId)));
        _subscriptions.Add(events.Subscribe<PlayerItemUseBlockedEvent>(gameEvent =>
        {
            if (gameEvent.Reason != GameplayActionFailureReason.Cooldown)
            {
                Enqueue(new GameplayFeedbackCue(
                    GameplayFeedbackCueKind.ActionBlocked,
                    gameEvent.TargetWorldPosition,
                    0.6f,
                    ContentId: gameEvent.Reason.ToString()));
            }
        }));
        _subscriptions.Add(events.Subscribe<PlayerItemUseCompletedEvent>(gameEvent =>
        {
            if (gameEvent.Kind == PlayerItemUseKind.Consume &&
                gameEvent.HealthRestored + gameEvent.ManaRestored + gameEvent.StatusEffectsApplied > 0)
            {
                Enqueue(new GameplayFeedbackCue(
                    GameplayFeedbackCueKind.ResourceRestored,
                    gameEvent.TargetWorldPosition,
                    1f,
                    gameEvent.HealthRestored + gameEvent.ManaRestored,
                    gameEvent.ItemId));
            }
        }));
    }

    private void OnMiningStarted(MiningStartedEvent gameEvent)
    {
        _miningBuckets[gameEvent.Position] = 0;
        Enqueue(new GameplayFeedbackCue(
            GameplayFeedbackCueKind.MiningStarted,
            TileCenter(gameEvent.Position),
            0.35f,
            ContentId: gameEvent.TileId.ToString()));
    }

    private void OnMiningProgress(MiningProgressEvent gameEvent)
    {
        var bucket = Math.Clamp((int)MathF.Floor(gameEvent.Progress * 5f), 0, 5);
        var previous = _miningBuckets.GetValueOrDefault(gameEvent.Position, -1);
        if (bucket <= previous)
        {
            return;
        }

        _miningBuckets[gameEvent.Position] = bucket;
        for (var crossedBucket = Math.Max(1, previous + 1); crossedBucket <= bucket; crossedBucket++)
        {
            Enqueue(new GameplayFeedbackCue(
                GameplayFeedbackCueKind.MiningImpact,
                TileCenter(gameEvent.Position),
                crossedBucket / 5f,
                ContentId: gameEvent.TileId.ToString()));
        }
    }

    private void OnMiningCompleted(MiningCompletedEvent gameEvent)
    {
        _miningBuckets.Remove(gameEvent.Position);
        Enqueue(new GameplayFeedbackCue(
            GameplayFeedbackCueKind.TileBroken,
            TileCenter(gameEvent.Position),
            1f,
            gameEvent.DroppedItem.Count,
            gameEvent.DroppedItem.ItemId));
    }

    private void OnMiningBlocked(MiningBlockedEvent gameEvent)
    {
        _miningBuckets.Remove(gameEvent.Position);
        Enqueue(new GameplayFeedbackCue(
            GameplayFeedbackCueKind.ActionBlocked,
            TileCenter(gameEvent.Position),
            0.5f,
            ContentId: gameEvent.Reason.ToString()));
    }

    private void OnItemPickedUp(ItemPickedUpEvent gameEvent)
    {
        var kind = _rareItemResolver?.Invoke(gameEvent.Stack.ItemId) == true
            ? GameplayFeedbackCueKind.RareItemPickup
            : GameplayFeedbackCueKind.ItemPickup;
        EnqueueAtEntity(kind, gameEvent.EntityId, gameEvent.Stack.Count, gameEvent.Stack.ItemId);
    }

    private void OnLootDropped(LootDroppedEvent gameEvent)
    {
        var kind = _rareItemResolver?.Invoke(gameEvent.Stack.ItemId) == true
            ? GameplayFeedbackCueKind.RareLootDropped
            : GameplayFeedbackCueKind.LootDropped;
        Enqueue(new GameplayFeedbackCue(
            kind,
            gameEvent.WorldPosition,
            1f,
            gameEvent.Stack.Count,
            gameEvent.Stack.ItemId));
    }

    private void EnqueueAtEntity(
        GameplayFeedbackCueKind kind,
        int entityId,
        int amount = 0,
        string? contentId = null)
    {
        var position = _entityPositionResolver?.Invoke(entityId);
        if (position.HasValue)
        {
            Enqueue(new GameplayFeedbackCue(kind, position.Value, 1f, amount, contentId));
        }
    }

    private void Enqueue(in GameplayFeedbackCue cue)
    {
        _visualCommands.Enqueue(cue);
        if (TryCreateAudioCue(cue, out var audioCue))
        {
            _audioCommands.Enqueue(audioCue);
        }
    }

    private Vector2 ResolveFocusPosition()
    {
        return _focusPositionResolver?.Invoke() ?? Vector2.Zero;
    }

    private static bool TryCreateAudioCue(in GameplayFeedbackCue cue, out GameplayAudioCue audio)
    {
        var audioId = cue.Kind switch
        {
            GameplayFeedbackCueKind.MiningStarted => "gameplay.mining.start",
            GameplayFeedbackCueKind.MiningImpact => "gameplay.mining.impact",
            GameplayFeedbackCueKind.TileBroken => "gameplay.tile.break",
            GameplayFeedbackCueKind.TilePlaced => "gameplay.tile.place",
            GameplayFeedbackCueKind.MeleeHit => "gameplay.combat.melee-hit",
            GameplayFeedbackCueKind.ProjectileHit => "gameplay.combat.projectile-hit",
            GameplayFeedbackCueKind.EntityDeath => "gameplay.entity.death",
            GameplayFeedbackCueKind.ItemPickup => "gameplay.item.pickup",
            GameplayFeedbackCueKind.RareItemPickup => "gameplay.item.rare-pickup",
            GameplayFeedbackCueKind.LootDropped => "gameplay.loot.drop",
            GameplayFeedbackCueKind.RareLootDropped => "gameplay.loot.rare-drop",
            GameplayFeedbackCueKind.CraftCompleted => "gameplay.crafting.completed",
            GameplayFeedbackCueKind.ResourceRestored => "gameplay.resource.restored",
            GameplayFeedbackCueKind.StatusEffectApplied => "gameplay.status.applied",
            GameplayFeedbackCueKind.WorldEventActivated => "gameplay.world-event.activated",
            GameplayFeedbackCueKind.ActionBlocked => "gameplay.action.blocked",
            _ => null
        };
        if (audioId is null)
        {
            audio = default;
            return false;
        }

        var important = cue.Kind is GameplayFeedbackCueKind.WorldEventActivated or
            GameplayFeedbackCueKind.RareItemPickup or
            GameplayFeedbackCueKind.RareLootDropped or
            GameplayFeedbackCueKind.EntityDeath;
        var volume = Math.Clamp(0.45f + cue.Intensity * 0.25f, 0.25f, 1f);
        audio = new GameplayAudioCue(
            cue.Kind,
            audioId,
            AudioBus.Sfx,
            cue.WorldPosition,
            volume,
            Pitch: cue.Kind == GameplayFeedbackCueKind.ActionBlocked ? -0.15f : 0f,
            Priority: important ? 90 : 55,
            CooldownSeconds: ResolveCooldown(cue.Kind),
            IsSpatial: cue.Kind != GameplayFeedbackCueKind.WorldEventActivated,
            MaximumDistance: important ? 960f : 640f);
        return true;
    }

    private static float ResolveCooldown(GameplayFeedbackCueKind kind)
    {
        return kind switch
        {
            GameplayFeedbackCueKind.MiningImpact => 0.04f,
            GameplayFeedbackCueKind.ActionBlocked => 0.15f,
            GameplayFeedbackCueKind.ItemPickup => 0.03f,
            GameplayFeedbackCueKind.RareItemPickup or GameplayFeedbackCueKind.LootDropped or
                GameplayFeedbackCueKind.RareLootDropped => 0.025f,
            _ => 0f
        };
    }

    private static Vector2 TileCenter(TilePos position)
    {
        return new Vector2(
            position.X * GameConstants.TileSize + GameConstants.TileSize * 0.5f,
            position.Y * GameConstants.TileSize + GameConstants.TileSize * 0.5f);
    }
}
