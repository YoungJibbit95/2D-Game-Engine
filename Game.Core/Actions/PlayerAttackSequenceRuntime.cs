using Game.Core.Combat;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Projectiles;
using Game.Core.Runtime;
using System.Numerics;

namespace Game.Core.Actions;

internal sealed class PlayerAttackSequenceRuntime : IAttackStartGate
{
    private readonly MeleeAttackSystem _melee;
    private readonly ProjectileFactory _projectiles;
    private readonly MeleeSweepResolver _sweepResolver = new();
    private AttackSequencer? _sequencer;
    private AttackEventBuffer? _events;
    private SweptMeleeShapeDefinition[] _activeShapes = Array.Empty<SweptMeleeShapeDefinition>();
    private ItemDefinition? _item;
    private ItemActionDefinition? _action;
    private PlayerEntity? _resourcePlayer;
    private PlayerInventory? _resourceInventory;
    private GuardRuntimeState? _resourceGuard;
    private Vector2 _pendingTarget;
    private Vector2 _queuedTarget;
    private Vector2 _activeTarget;
    private ulong _materializedProjectileAttackInstanceId;
    private ulong _lastGateInputSequence;
    private AttackInputFailure _lastGateFailure;
    private ManaSpendResult _frameManaSpend;
    private ManaReservationFinalizationResult _frameManaFinalization;
    private ManaReservationHandle _pendingManaReservation;
    private ManaSpendResult _pendingManaSpend;
    private ManaRefundPolicy _pendingManaRefundPolicy;

    public PlayerAttackSequenceRuntime(MeleeAttackSystem melee, ProjectileFactory projectiles)
    {
        _melee = melee ?? throw new ArgumentNullException(nameof(melee));
        _projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
    }

    public AttackRuntimeFrameSnapshot LatestSnapshot { get; private set; } = AttackRuntimeFrameSnapshot.Empty;

    public bool IsBusy => _sequencer is { Phase: not AttackRuntimePhase.Idle };

    public PlayerItemUseResult Tick(
        GameContentDatabase content,
        PlayerEntity player,
        PlayerInventory inventory,
        EntityManager entities,
        ItemDefinition? requestedItem,
        ItemActionDefinition? requestedAction,
        bool requestActive,
        Vector2 targetWorldPosition,
        ulong tick,
        GuardRuntimeState? guard,
        GameEventBus? events)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(entities);

        _resourcePlayer = player;
        _resourceInventory = inventory;
        _resourceGuard = guard;
        _lastGateInputSequence = 0;
        _lastGateFailure = AttackInputFailure.None;
        _frameManaSpend = ManaSpendResult.None;
        _frameManaFinalization = ManaReservationFinalizationResult.None;

        var queuedInput = default(AttackInputResult);
        var attemptedKind = requestedAction is null
            ? PlayerItemUseKind.None
            : ResolveUseKind(requestedAction.Kind);
        var requestFailure = GameplayActionFailureReason.None;
        if (requestActive)
        {
            requestFailure = PrepareInput(
                content,
                requestedItem,
                requestedAction,
                targetWorldPosition,
                tick,
                ref queuedInput);
        }

        if (_sequencer is null || _events is null)
        {
            ClearResources();
            return requestFailure == GameplayActionFailureReason.None
                ? PlayerItemUseResult.None
                : PlayerItemUseResult.BlockedResult(attemptedKind, requestFailure);
        }

        var previousAttackInstanceId = _sequencer.AttackInstanceId;
        _sequencer.AdvanceTo(tick, _events, this);
        if (_sequencer.AttackInstanceId != previousAttackInstanceId)
        {
            _activeTarget = _sequencer.ComboIndex == 0 ? _pendingTarget : _queuedTarget;
        }

        var projectile = MaterializeActiveProjectile(content, player, entities);
        var melee = ResolveActiveMelee(content, player, entities, events);
        FinalizePendingManaAfterAdvance(player);
        CaptureSnapshot();
        ClearResources();

        if (requestFailure != GameplayActionFailureReason.None)
        {
            return AttachManaFeedback(PlayerItemUseResult.BlockedResult(attemptedKind, requestFailure));
        }

        if (!requestActive)
        {
            return CreateProgressResult(ResolveUseKind(_action?.Kind ?? ItemActionKind.None), melee, projectile);
        }

        if (!queuedInput.Accepted)
        {
            return AttachManaFeedback(PlayerItemUseResult.BlockedResult(
                attemptedKind,
                MapFailure(queuedInput.Failure)));
        }

        if (_lastGateInputSequence == queuedInput.Command.Sequence &&
            _lastGateFailure != AttackInputFailure.None)
        {
            return AttachManaFeedback(PlayerItemUseResult.BlockedResult(
                attemptedKind,
                MapFailure(_lastGateFailure)));
        }

        var started = ContainsEvent(
            AttackRuntimeEventKind.AttackStarted,
            queuedInput.Command.Sequence);
        if (started)
        {
            return new PlayerItemUseResult(
                attemptedKind,
                default,
                false,
                melee,
                projectile)
            {
                Status = PlayerItemUseStatus.Succeeded,
                AttemptedKind = attemptedKind,
                SuccessReason = GameplayActionSuccessReason.ActionStarted,
                ManaSpend = _frameManaSpend,
                ManaFinalization = _frameManaFinalization
            };
        }

        if (ContainsEvent(AttackRuntimeEventKind.InputRejected, queuedInput.Command.Sequence))
        {
            return AttachManaFeedback(PlayerItemUseResult.BlockedResult(
                attemptedKind,
                MapFailure(FindInputFailure(queuedInput.Command.Sequence))));
        }

        return CreateProgressResult(attemptedKind, melee, projectile);
    }

    public AttackInputFailure TryAccept(in AttackStartRequest request)
    {
        _lastGateInputSequence = request.InputSequence;
        var player = _resourcePlayer;
        var inventory = _resourceInventory;
        if (player is null || inventory is null || _pendingManaReservation.IsValid)
        {
            return _lastGateFailure = AttackInputFailure.LockedOut;
        }

        var manaCost = Math.Max(
            0,
            (int)MathF.Ceiling(request.Cost.Mana * player.Stats.ManaCostMultiplier));
        if (request.Cost.Stamina > 0 &&
            (_resourceGuard is null || _resourceGuard.Stamina < request.Cost.Stamina))
        {
            return _lastGateFailure = AttackInputFailure.InsufficientStamina;
        }

        if (request.Cost.Ammo > 0 &&
            CountRemovableItems(inventory, request.Cost.AmmoItemId!) < request.Cost.Ammo)
        {
            return _lastGateFailure = AttackInputFailure.InsufficientAmmo;
        }

        var manaSpend = player.ManaComponent.TryReserve(
            manaCost,
            _action?.ManaRegenerationDelaySeconds ?? ManaComponent.DefaultRegenerationDelaySeconds);
        _frameManaSpend = manaSpend;
        if (!manaSpend.Succeeded)
        {
            return _lastGateFailure = MapManaSpendFailure(manaSpend.Status);
        }

        if (request.Cost.Ammo > 0 &&
            !inventory.RemoveItem(request.Cost.AmmoItemId!, request.Cost.Ammo))
        {
            _frameManaFinalization = player.ManaComponent.FinalizeWithRefund(
                manaSpend.Reservation,
                ManaRefundPolicy.Always,
                ManaRefundReason.ResourceCommitFailed);
            return _lastGateFailure = AttackInputFailure.InsufficientAmmo;
        }

        if (request.Cost.Stamina > 0 &&
            _resourceGuard!.SpendStamina(request.Cost.Stamina) != request.Cost.Stamina)
        {
            if (request.Cost.Ammo > 0 &&
                !inventory.AddItem(new ItemStack(request.Cost.AmmoItemId!, request.Cost.Ammo)))
            {
                throw new InvalidOperationException("Attack resource rollback could not restore removed ammunition.");
            }

            _frameManaFinalization = player.ManaComponent.FinalizeWithRefund(
                manaSpend.Reservation,
                ManaRefundPolicy.Always,
                ManaRefundReason.ResourceCommitFailed);
            return _lastGateFailure = AttackInputFailure.InsufficientStamina;
        }

        _pendingManaReservation = manaSpend.Reservation;
        _pendingManaSpend = manaSpend;
        _pendingManaRefundPolicy = _action?.ManaRefundPolicy ?? ManaRefundPolicy.BeforeEffect;
        return _lastGateFailure = AttackInputFailure.None;
    }
    private GameplayActionFailureReason PrepareInput(
        GameContentDatabase content,
        ItemDefinition? requestedItem,
        ItemActionDefinition? requestedAction,
        Vector2 targetWorldPosition,
        ulong tick,
        ref AttackInputResult queuedInput)
    {
        if (requestedItem is null || requestedAction is null)
        {
            return GameplayActionFailureReason.NoSelectedItem;
        }

        if (string.IsNullOrWhiteSpace(requestedAction.AttackSequenceId) ||
            !content.AttackSequences.TryGetById(requestedAction.AttackSequenceId, out var definition))
        {
            return GameplayActionFailureReason.InvalidItem;
        }

        if (!IsValidTarget(targetWorldPosition, _resourcePlayer!.Body.Center))
        {
            return GameplayActionFailureReason.InvalidTarget;
        }

        if (requestedAction.Kind is ItemActionKind.Shoot or ItemActionKind.Cast &&
            (string.IsNullOrWhiteSpace(requestedAction.ProjectileId) ||
             !content.Projectiles.TryGetById(requestedAction.ProjectileId, out _)))
        {
            return GameplayActionFailureReason.InvalidItem;
        }

        if (_sequencer is null ||
            _sequencer.Phase == AttackRuntimePhase.Idle &&
            (!string.Equals(_sequencer.Definition.Id, definition.Id, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(_item?.Id, requestedItem.Id, StringComparison.OrdinalIgnoreCase)))
        {
            Configure(definition, requestedItem, requestedAction);
        }
        else if (!string.Equals(_sequencer.Definition.Id, definition.Id, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(_item?.Id, requestedItem.Id, StringComparison.OrdinalIgnoreCase))
        {
            return GameplayActionFailureReason.Cooldown;
        }

        if (_sequencer!.Phase == AttackRuntimePhase.Idle)
        {
            _pendingTarget = targetWorldPosition;
        }
        else
        {
            _queuedTarget = targetWorldPosition;
            if (_sequencer.Phase == AttackRuntimePhase.Cooldown)
            {
                _pendingTarget = targetWorldPosition;
            }
        }

        queuedInput = _sequencer.QueueInput(tick);
        return queuedInput.Accepted
            ? GameplayActionFailureReason.None
            : MapFailure(queuedInput.Failure);
    }

    private void Configure(
        AttackSequenceDefinition definition,
        ItemDefinition item,
        ItemActionDefinition action)
    {
        _sequencer = new AttackSequencer(definition);
        _events = _sequencer.CreateEventBuffer();
        _item = item;
        _action = action;
        _materializedProjectileAttackInstanceId = 0;
        var maximumShapes = 0;
        foreach (var step in definition.Steps)
        {
            maximumShapes = Math.Max(maximumShapes, step.MeleeShapes.Count);
        }

        _activeShapes = maximumShapes == 0
            ? Array.Empty<SweptMeleeShapeDefinition>()
            : new SweptMeleeShapeDefinition[maximumShapes];
    }

    private ProjectileEntity? MaterializeActiveProjectile(
        GameContentDatabase content,
        PlayerEntity player,
        EntityManager entities)
    {
        if (_sequencer is not { Phase: AttackRuntimePhase.Active } sequencer ||
            _action is not { Kind: ItemActionKind.Shoot or ItemActionKind.Cast } action ||
            _item is null ||
            sequencer.AttackInstanceId == _materializedProjectileAttackInstanceId ||
            string.IsNullOrWhiteSpace(action.ProjectileId) ||
            !content.Projectiles.TryGetById(action.ProjectileId, out var definition))
        {
            return null;
        }

        try
        {
            var runtimeDefinition = action.Kind == ItemActionKind.Cast
                ? MagicAttackResolver.ResolveProjectile(definition, _item, player.Stats)
                : definition with
                {
                    Damage = Math.Max(
                        1,
                        (int)MathF.Round(
                            (definition.Damage + _item.Damage) *
                            player.Stats.RangedDamageMultiplier))
                };
            var direction = _activeTarget - player.Body.Center;
            var projectile = _projectiles.Create(
                runtimeDefinition,
                player.Body.Center,
                direction,
                player.Id == 0 ? null : player.Id);
            projectile.Velocity *= action.ProjectileSpeedMultiplier;
            entities.Add(projectile);
            _materializedProjectileAttackInstanceId = sequencer.AttackInstanceId;
            return projectile;
        }
        catch
        {
            RefundPendingMana(player, ManaRefundReason.MaterializationFailed);
            throw;
        }
    }
    private MeleeAttackResult ResolveActiveMelee(
        GameContentDatabase content,
        PlayerEntity player,
        EntityManager entities,
        GameEventBus? events)
    {
        if (_sequencer is not { Phase: AttackRuntimePhase.Active } sequencer ||
            _events is null ||
            _item is null ||
            _action?.Kind != ItemActionKind.Melee ||
            _activeShapes.Length == 0)
        {
            return MeleeAttackResult.None;
        }

        var shapeCount = sequencer.CopyActiveMeleeShapes(_activeShapes);
        if (shapeCount == 0)
        {
            return MeleeAttackResult.None;
        }

        var hits = 0;
        var deaths = 0;
        var effects = 0;
        for (var index = 0; index < shapeCount; index++)
        {
            var definition = _activeShapes[index];
            var shapeTick = sequencer.ActivePhaseTick - definition.ActiveStartTickInclusive;
            var duration = definition.ActiveEndTickExclusive - definition.ActiveStartTickInclusive;
            var previousProgress = Math.Clamp(shapeTick / (float)duration, 0f, 1f);
            var currentProgress = Math.Clamp((shapeTick + 1) / (float)duration, 0f, 1f);
            var origin = player.Body.Center + definition.OriginOffset;
            var shape = _sweepResolver.Resolve(
                origin,
                _activeTarget - origin,
                definition.Sweep,
                previousProgress,
                currentProgress);
            var result = _melee.AttackSweep(
                player,
                entities,
                _item,
                shape,
                sequencer,
                _events,
                events,
                content.StatusEffects);
            hits += result.Hits;
            deaths += result.EnemyDeaths;
            effects += result.StatusEffectsApplied;
        }

        return new MeleeAttackResult(true, hits, deaths, 0, effects);
    }

    private void FinalizePendingManaAfterAdvance(PlayerEntity player)
    {
        if (!_pendingManaReservation.IsValid)
        {
            return;
        }

        if (TryFindEvent(AttackRuntimeEventKind.AttackCancelled, out var cancelled))
        {
            var reason = cancelled.PreviousPhase == AttackRuntimePhase.Startup
                ? ManaRefundReason.CancelledBeforeEffect
                : ManaRefundReason.CancelledAfterEffect;
            RefundPendingMana(player, reason);
            return;
        }

        if (_sequencer?.Phase == AttackRuntimePhase.Active)
        {
            _frameManaFinalization = player.ManaComponent.Commit(_pendingManaReservation);
            if (_frameManaFinalization.Status != ManaReservationFinalizationStatus.Committed)
            {
                throw new InvalidOperationException("An active attack could not commit its reserved mana.");
            }

            _frameManaSpend = _pendingManaSpend with
            {
                Status = ManaSpendStatus.Spent,
                Reservation = default,
                CurrentMana = player.Mana
            };
            ClearPendingMana();
            return;
        }

        if (_sequencer?.Phase is AttackRuntimePhase.Idle or AttackRuntimePhase.Cooldown)
        {
            RefundPendingMana(player, ManaRefundReason.RuntimeReset);
        }
    }

    private void RefundPendingMana(PlayerEntity player, ManaRefundReason reason)
    {
        if (!_pendingManaReservation.IsValid)
        {
            return;
        }

        _frameManaFinalization = player.ManaComponent.FinalizeWithRefund(
            _pendingManaReservation,
            _pendingManaRefundPolicy,
            reason);
        _frameManaSpend = _pendingManaSpend with
        {
            Status = ManaSpendStatus.Spent,
            Reservation = default,
            CurrentMana = player.Mana
        };
        ClearPendingMana();
    }

    private void ClearPendingMana()
    {
        _pendingManaReservation = default;
        _pendingManaSpend = ManaSpendResult.None;
        _pendingManaRefundPolicy = ManaRefundPolicy.None;
    }

    private bool TryFindEvent(AttackRuntimeEventKind kind, out AttackRuntimeEvent found)
    {
        var buffer = _events!;
        for (var index = 0; index < buffer.Count; index++)
        {
            if (buffer[index].Kind == kind)
            {
                found = buffer[index];
                return true;
            }
        }

        found = default;
        return false;
    }
    private void CaptureSnapshot()
    {
        var sequencer = _sequencer!;
        var buffer = _events!;
        var feedback = buffer.Count == 0
            ? ImmutableSnapshotList<AttackRuntimeEvent>.Empty
            : CopyFeedback(buffer);
        LatestSnapshot = new AttackRuntimeFrameSnapshot(
            _item?.Id,
            sequencer.Definition.Id,
            sequencer.CurrentStep?.Id,
            sequencer.AttackInstanceId,
            sequencer.Phase,
            sequencer.ComboIndex,
            sequencer.ActivePhaseTick,
            sequencer.HasQueuedCombo,
            sequencer.HasBufferedInput,
            feedback,
            buffer.DroppedCount);
    }

    private static ImmutableSnapshotList<AttackRuntimeEvent> CopyFeedback(AttackEventBuffer buffer)
    {
        var feedback = new AttackRuntimeEvent[buffer.Count];
        buffer.AsSpan().CopyTo(feedback);
        return ImmutableSnapshotList<AttackRuntimeEvent>.FromOwned(feedback);
    }

    private bool ContainsEvent(AttackRuntimeEventKind kind, ulong inputSequence)
    {
        var buffer = _events!;
        for (var index = 0; index < buffer.Count; index++)
        {
            if (buffer[index].Kind == kind && buffer[index].InputSequence == inputSequence)
            {
                return true;
            }
        }

        return false;
    }

    private AttackInputFailure FindInputFailure(ulong inputSequence)
    {
        var buffer = _events!;
        for (var index = 0; index < buffer.Count; index++)
        {
            if (buffer[index].Kind == AttackRuntimeEventKind.InputRejected &&
                buffer[index].InputSequence == inputSequence)
            {
                return buffer[index].Failure;
            }
        }

        return AttackInputFailure.LockedOut;
    }

    private PlayerItemUseResult CreateProgressResult(
        PlayerItemUseKind kind,
        MeleeAttackResult melee,
        ProjectileEntity? projectile)
    {
        if (kind == PlayerItemUseKind.None || melee == MeleeAttackResult.None && projectile is null)
        {
            return PlayerItemUseResult.None;
        }

        return new PlayerItemUseResult(kind, default, false, melee, projectile)
        {
            Status = PlayerItemUseStatus.InProgress,
            AttemptedKind = kind,
            SuccessReason = GameplayActionSuccessReason.ActionProgressed,
            ManaSpend = _frameManaSpend,
            ManaFinalization = _frameManaFinalization
        };
    }

    private PlayerItemUseResult AttachManaFeedback(PlayerItemUseResult result)
    {
        return result with
        {
            ManaSpend = _frameManaSpend,
            ManaFinalization = _frameManaFinalization
        };
    }
    private static long CountRemovableItems(PlayerInventory inventory, string itemId)
    {
        var count = 0L;
        foreach (var slot in inventory.Hotbar.Slots)
        {
            if (!slot.IsFavorite && string.Equals(slot.Stack.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                count += slot.Stack.Count;
            }
        }

        foreach (var slot in inventory.Main.Slots)
        {
            if (!slot.IsFavorite && string.Equals(slot.Stack.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                count += slot.Stack.Count;
            }
        }

        return count;
    }

    private static AttackInputFailure MapManaSpendFailure(ManaSpendStatus status)
    {
        return status switch
        {
            ManaSpendStatus.InsufficientMana => AttackInputFailure.InsufficientMana,
            _ => AttackInputFailure.LockedOut
        };
    }
    private static GameplayActionFailureReason MapFailure(AttackInputFailure failure)
    {
        return failure switch
        {
            AttackInputFailure.InsufficientStamina => GameplayActionFailureReason.InsufficientStamina,
            AttackInputFailure.InsufficientMana => GameplayActionFailureReason.InsufficientMana,
            AttackInputFailure.InsufficientAmmo => GameplayActionFailureReason.InsufficientAmmo,
            AttackInputFailure.None => GameplayActionFailureReason.None,
            _ => GameplayActionFailureReason.Cooldown
        };
    }

    private static PlayerItemUseKind ResolveUseKind(ItemActionKind kind)
    {
        return kind switch
        {
            ItemActionKind.Melee => PlayerItemUseKind.Melee,
            ItemActionKind.Shoot => PlayerItemUseKind.Shoot,
            ItemActionKind.Cast => PlayerItemUseKind.Cast,
            _ => PlayerItemUseKind.None
        };
    }

    private static bool IsValidTarget(Vector2 target, Vector2 origin)
    {
        return float.IsFinite(target.X) &&
               float.IsFinite(target.Y) &&
               Vector2.DistanceSquared(target, origin) > float.Epsilon;
    }

    private void ClearResources()
    {
        _resourcePlayer = null;
        _resourceInventory = null;
        _resourceGuard = null;
    }
}
