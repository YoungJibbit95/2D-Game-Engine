using Game.Core.Combat;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Effects;
using Game.Core.Farming;
using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Runtime;
using Game.Core.World;
using System.Numerics;

namespace Game.Core.Actions;

public sealed class PlayerItemUseSystem
{
    private const float DefaultReachPixels = 96f;

    private readonly MiningSystem _mining;
    private readonly BuildingSystem _building;
    private readonly MeleeAttackSystem _melee;
    private readonly TileCollisionResolver _collisionResolver;
    private readonly ProjectileFactory _projectiles;
    private readonly StatusEffectApplier _statusEffects;
    private readonly PlayerAttackSequenceRuntime _attackSequences;
    private float _useCooldownRemaining;
    private float _useCooldownDuration;
    private BlockedFeedbackSignature? _lastBlockedFeedback;

    public PlayerItemUseSystem(
        MiningSystem? mining = null,
        BuildingSystem? building = null,
        MeleeAttackSystem? melee = null,
        TileCollisionResolver? collisionResolver = null,
        ProjectileFactory? projectiles = null,
        StatusEffectApplier? statusEffects = null)
    {
        _collisionResolver = collisionResolver ?? new TileCollisionResolver();
        _mining = mining ?? new MiningSystem();
        _building = building ?? new BuildingSystem();
        _melee = melee ?? new MeleeAttackSystem(new LootRoller(new Random()), _collisionResolver);
        _projectiles = projectiles ?? new ProjectileFactory();
        _statusEffects = statusEffects ?? new StatusEffectApplier();
        _attackSequences = new PlayerAttackSequenceRuntime(_melee, _projectiles);
    }

    public AttackRuntimeFrameSnapshot LatestAttackSnapshot => _attackSequences.LatestSnapshot;

    public void Update(float deltaSeconds)
    {
        _useCooldownRemaining = Math.Max(0, _useCooldownRemaining - Math.Max(0, deltaSeconds));
        _melee.Update(deltaSeconds);
    }

    public PlayerItemUseResult TickSelectedItem(
        World.World world,
        GameContentDatabase content,
        PlayerEntity player,
        PlayerInventory inventory,
        EntityManager entities,
        TilePos targetTile,
        Vector2 targetWorldPosition,
        bool requestActive,
        ulong tick,
        float deltaSeconds,
        GuardRuntimeState? guard = null,
        GameEventBus? events = null,
        FarmPlotManager? farmPlots = null,
        FarmSeason farmSeason = FarmSeason.Any,
        int currentDay = 1,
        Random? farmingRandom = null,
        LootKillContext? lootContext = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(entities);

        ItemDefinition? item = null;
        ItemActionDefinition? action = null;
        var selected = inventory.SelectedStack;
        if (requestActive && !selected.IsEmpty && content.Items.TryGetById(selected.ItemId, out item))
        {
            action = ItemActionResolver.GetPrimaryAction(item);
        }

        var usesAttackSequence = !string.IsNullOrWhiteSpace(action?.AttackSequenceId);
        if (!_attackSequences.IsBusy && !usesAttackSequence)
        {
            return requestActive
                ? UseSelectedItem(
                    world,
                    content,
                    player,
                    inventory,
                    entities,
                    targetTile,
                    targetWorldPosition,
                    deltaSeconds,
                    events,
                    farmPlots,
                    farmSeason,
                    currentDay,
                    farmingRandom,
                    lootContext)
                : PlayerItemUseResult.None;
        }

        var result = _attackSequences.Tick(
            content,
            player,
            inventory,
            entities,
            item,
            action,
            requestActive,
            targetWorldPosition,
            tick,
            guard,
            events);
        PublishFeedback(
            events,
            player,
            selected.IsEmpty ? null : selected.ItemId,
            targetTile,
            targetWorldPosition,
            result);
        return result;
    }

    public PlayerItemUseResult UseSelectedItem(
        World.World world,
        GameContentDatabase content,
        PlayerEntity player,
        PlayerInventory inventory,
        EntityManager entities,
        TilePos targetTile,
        Vector2 targetWorldPosition,
        float deltaSeconds,
        GameEventBus? events = null,
        FarmPlotManager? farmPlots = null,
        FarmSeason farmSeason = FarmSeason.Any,
        int currentDay = 1,
        Random? farmingRandom = null,
        LootKillContext? lootContext = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(entities);

        var selected = inventory.SelectedStack;
        if (selected.IsEmpty)
        {
            _mining.Reset();
            var blocked = PlayerItemUseResult.BlockedResult(
                PlayerItemUseKind.None,
                GameplayActionFailureReason.NoSelectedItem);
            PublishFeedback(events, player, null, targetTile, targetWorldPosition, blocked);
            return blocked;
        }

        if (!content.Items.TryGetById(selected.ItemId, out var item))
        {
            _mining.Reset();
            var blocked = PlayerItemUseResult.BlockedResult(
                PlayerItemUseKind.None,
                GameplayActionFailureReason.ItemNotFound);
            PublishFeedback(events, player, selected.ItemId, targetTile, targetWorldPosition, blocked);
            return blocked;
        }

        var action = ItemActionResolver.GetPrimaryAction(item);
        var attemptedKind = ResolveUseKind(action.Kind);
        if (action.Kind != ItemActionKind.Mine)
        {
            _mining.Reset();
        }

        if (UsesDiscreteCooldown(action) && _useCooldownRemaining > 0)
        {
            var blocked = PlayerItemUseResult.BlockedResult(
                attemptedKind,
                GameplayActionFailureReason.Cooldown,
                _useCooldownRemaining,
                _useCooldownDuration);
            PublishFeedback(events, player, selected.ItemId, targetTile, targetWorldPosition, blocked);
            return blocked;
        }

        var result = action.Kind switch
        {
            ItemActionKind.Place => TryBuild(world, content, player, inventory, targetTile, selected.ItemId, action, events),
            ItemActionKind.Mine => TryMine(world, content, player, entities, targetTile, item, action, deltaSeconds, events),
            ItemActionKind.Melee => TryMelee(
                player,
                entities,
                content,
                item,
                targetWorldPosition,
                events,
                lootContext),
            ItemActionKind.Shoot => TryShoot(content, player, inventory, entities, item, action, targetWorldPosition),
            ItemActionKind.Cast => TryCast(content, player, entities, item, action, targetWorldPosition),
            ItemActionKind.Consume => TryConsume(content, player, inventory, selected.ItemId, item, events),
            ItemActionKind.Till => TryTill(world, content, farmPlots, targetTile, item),
            ItemActionKind.Water => TryWater(world, farmPlots, targetTile, item),
            ItemActionKind.Plant => TryPlant(world, content, inventory, farmPlots, targetTile, selected.ItemId, item, farmSeason, currentDay),
            ItemActionKind.Harvest => TryHarvest(content, inventory, farmPlots, targetTile, item, farmingRandom),
            _ => PlayerItemUseResult.BlockedResult(PlayerItemUseKind.None, GameplayActionFailureReason.NoAction)
        };

        PublishFeedback(events, player, selected.ItemId, targetTile, targetWorldPosition, result);
        return result;
    }

    private PlayerItemUseResult TryBuild(
        World.World world,
        GameContentDatabase content,
        PlayerEntity player,
        PlayerInventory inventory,
        TilePos targetTile,
        string itemId,
        ItemActionDefinition action,
        GameEventBus? events)
    {
        var building = _building.PlaceTileWithResult(
            world,
            inventory,
            content.Items,
            content.Tiles,
            targetTile,
            itemId,
            player.Body.Center,
            ResolveReach(action),
            player.Bounds,
            events);

        return building.Success
            ? StartCooldown(
                new PlayerItemUseResult(PlayerItemUseKind.Build, MiningResult.None, true, MeleeAttackResult.None),
                content.Items.GetById(itemId))
            : PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Build, building.FailureReason);
    }

    private PlayerItemUseResult TryMine(
        World.World world,
        GameContentDatabase content,
        PlayerEntity player,
        EntityManager entities,
        TilePos targetTile,
        ItemDefinition item,
        ItemActionDefinition action,
        float deltaSeconds,
        GameEventBus? events)
    {
        var mining = _mining.Update(
            world,
            content.Tiles,
            targetTile,
            player.Body.Center,
            ResolveReach(action),
            item.ToolPower,
            deltaSeconds * player.Stats.MiningSpeedMultiplier,
            events);

        if (mining.Completed && !mining.DroppedItem.IsEmpty)
        {
            entities.Add(new DroppedItemEntity(
                mining.DroppedItem,
                CoordinateUtils.TileToWorld(mining.TilePosition),
                _collisionResolver));
        }

        if (mining.Completed)
        {
            return MarkSucceeded(new PlayerItemUseResult(
                PlayerItemUseKind.Mine,
                mining,
                false,
                MeleeAttackResult.None)) with
            {
                ActionProgress = 1f
            };
        }

        return mining.Blocked
            ? PlayerItemUseResult.BlockedResult(
                PlayerItemUseKind.Mine,
                mining.FailureReason,
                mining: mining)
            : PlayerItemUseResult.Progressing(PlayerItemUseKind.Mine, mining);
    }

    private PlayerItemUseResult TryMelee(
        PlayerEntity player,
        EntityManager entities,
        GameContentDatabase content,
        ItemDefinition item,
        Vector2 targetWorldPosition,
        GameEventBus? events,
        LootKillContext? lootContext)
    {
        var melee = _melee.Attack(
            player,
            entities,
            item,
            content.LootTables,
            targetWorldPosition,
            events,
            content.StatusEffects,
            lootContext);
        return melee.Attacked
            ? StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Melee, MiningResult.None, false, melee), item)
            : PlayerItemUseResult.BlockedResult(
                PlayerItemUseKind.Melee,
                melee.FailureReason,
                melee.CooldownRemaining,
                melee.CooldownDuration,
                melee: melee);
    }

    private PlayerItemUseResult TryShoot(
        GameContentDatabase content,
        PlayerEntity player,
        PlayerInventory inventory,
        EntityManager entities,
        ItemDefinition item,
        ItemActionDefinition action,
        Vector2 targetWorldPosition)
    {
        if (string.IsNullOrWhiteSpace(action.ProjectileId) || !content.Projectiles.TryGetById(action.ProjectileId, out var definition))
        {
            return PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Shoot, GameplayActionFailureReason.InvalidItem);
        }

        if (!IsValidTarget(targetWorldPosition, player.Body.Center))
        {
            return PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Shoot, GameplayActionFailureReason.InvalidTarget);
        }

        if (!string.IsNullOrWhiteSpace(action.AmmoItemId))
        {
            if (inventory.CountItem(action.AmmoItemId) < action.AmmoCost)
            {
                return PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Shoot, GameplayActionFailureReason.InsufficientAmmo);
            }

            inventory.RemoveItem(action.AmmoItemId, action.AmmoCost);
        }

        var direction = targetWorldPosition - player.Body.Center;
        var projectileDamage = Math.Max(1, (int)MathF.Round((definition.Damage + item.Damage) * player.Stats.RangedDamageMultiplier));
        var projectile = _projectiles.Create(
            definition,
            player.Body.Center,
            direction,
            player.Id == 0 ? null : player.Id,
            damageOverride: projectileDamage);

        projectile.Velocity *= action.ProjectileSpeedMultiplier;
        entities.Add(projectile);

        return StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Shoot, MiningResult.None, false, MeleeAttackResult.None, projectile), item);
    }

    private PlayerItemUseResult TryCast(
        GameContentDatabase content,
        PlayerEntity player,
        EntityManager entities,
        ItemDefinition item,
        ItemActionDefinition action,
        Vector2 targetWorldPosition)
    {
        if (string.IsNullOrWhiteSpace(action.ProjectileId) || !content.Projectiles.TryGetById(action.ProjectileId, out var definition))
        {
            return PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Cast, GameplayActionFailureReason.InvalidItem);
        }

        if (!IsValidTarget(targetWorldPosition, player.Body.Center))
        {
            return PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Cast, GameplayActionFailureReason.InvalidTarget);
        }

        var manaCost = Math.Max(0, (int)MathF.Ceiling(item.ManaCost * player.Stats.ManaCostMultiplier));
        if (!player.ManaComponent.TrySpend(manaCost))
        {
            return PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Cast, GameplayActionFailureReason.InsufficientMana);
        }

        var damage = Math.Max(1, (int)MathF.Round((definition.Damage + item.Damage) * player.Stats.MagicDamageMultiplier));
        var direction = targetWorldPosition - player.Body.Center;
        var projectile = _projectiles.Create(
            definition,
            player.Body.Center,
            direction,
            player.Id == 0 ? null : player.Id,
            damageOverride: damage,
            damageTypeOverride: DamageType.Magic);

        projectile.Velocity *= action.ProjectileSpeedMultiplier;
        entities.Add(projectile);

        return StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Cast, MiningResult.None, false, MeleeAttackResult.None, projectile), item);
    }

    private PlayerItemUseResult TryConsume(
        GameContentDatabase content,
        PlayerEntity player,
        PlayerInventory inventory,
        string itemId,
        ItemDefinition item,
        GameEventBus? events)
    {
        var healthBefore = player.Health;
        if (item.HealthRestore > 0 && player.Health > 0 && player.Health < player.MaxHealth)
        {
            player.HealthComponent.Heal(item.HealthRestore);
        }

        var healthRestored = Math.Max(0, player.Health - healthBefore);
        var manaBefore = player.Mana;
        if (item.ManaRestore > 0 && player.Mana < player.MaxMana)
        {
            player.ManaComponent.Restore(item.ManaRestore);
        }

        var manaRestored = Math.Max(0, player.Mana - manaBefore);
        var effectResult = _statusEffects.ApplyDetailed(
            player.StatusEffects,
            content.StatusEffects,
            item.StatusEffectApplications);

        if (healthRestored == 0 && manaRestored == 0 && !effectResult.Changed)
        {
            return PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Consume, GameplayActionFailureReason.NoBenefit);
        }

        if (!inventory.RemoveItem(itemId, 1))
        {
            return PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Consume, GameplayActionFailureReason.InsufficientItem);
        }

        if (healthRestored > 0 || manaRestored > 0)
        {
            events?.Publish(new ResourceRestoredEvent(player.Id, itemId, healthRestored, manaRestored));
        }

        PublishStatusEffects(events, player.Id, itemId, effectResult);
        return StartCooldown(
            new PlayerItemUseResult(PlayerItemUseKind.Consume, MiningResult.None, false, MeleeAttackResult.None)
            {
                HealthRestored = healthRestored,
                ManaRestored = manaRestored,
                StatusEffectsApplied = effectResult.AppliedCount,
                ConsumedItem = true
            },
            item);
    }

    private PlayerItemUseResult TryTill(
        World.World world,
        GameContentDatabase content,
        FarmPlotManager? farmPlots,
        TilePos targetTile,
        ItemDefinition item)
    {
        if (farmPlots is null)
        {
            return PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Till, GameplayActionFailureReason.InvalidTarget);
        }

        var result = new FarmingSystem().Till(world, content.Tiles, farmPlots, targetTile);
        return result.Status == FarmActionStatus.Completed
            ? StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Till, MiningResult.None, false, MeleeAttackResult.None, Farming: result), item)
            : PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Till, MapFarmingFailure(result.Status)) with { Farming = result };
    }

    private PlayerItemUseResult TryWater(
        World.World world,
        FarmPlotManager? farmPlots,
        TilePos targetTile,
        ItemDefinition item)
    {
        if (farmPlots is null)
        {
            return PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Water, GameplayActionFailureReason.InvalidTarget);
        }

        var result = new FarmingSystem().Water(world, farmPlots, targetTile);
        return result.Status == FarmActionStatus.Completed
            ? StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Water, MiningResult.None, false, MeleeAttackResult.None, Farming: result), item)
            : PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Water, MapFarmingFailure(result.Status)) with { Farming = result };
    }

    private PlayerItemUseResult TryPlant(
        World.World world,
        GameContentDatabase content,
        PlayerInventory inventory,
        FarmPlotManager? farmPlots,
        TilePos targetTile,
        string seedItemId,
        ItemDefinition item,
        FarmSeason farmSeason,
        int currentDay)
    {
        if (farmPlots is null)
        {
            return PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Plant, GameplayActionFailureReason.InvalidTarget);
        }

        var result = new FarmingSystem().PlantSeed(
            world,
            content.Crops,
            farmPlots,
            inventory,
            targetTile,
            seedItemId,
            currentDay,
            farmSeason);

        return result.Status == FarmActionStatus.Completed
            ? StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Plant, MiningResult.None, false, MeleeAttackResult.None, Farming: result), item)
            : PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Plant, MapFarmingFailure(result.Status)) with { Farming = result };
    }

    private PlayerItemUseResult TryHarvest(
        GameContentDatabase content,
        PlayerInventory inventory,
        FarmPlotManager? farmPlots,
        TilePos targetTile,
        ItemDefinition item,
        Random? farmingRandom)
    {
        if (farmPlots is null)
        {
            return PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Harvest, GameplayActionFailureReason.InvalidTarget);
        }

        var result = new FarmingSystem().Harvest(content.Crops, farmPlots, inventory, targetTile, farmingRandom);
        return result.Status == FarmActionStatus.Completed
            ? StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Harvest, MiningResult.None, false, MeleeAttackResult.None, Farming: result), item)
            : PlayerItemUseResult.BlockedResult(PlayerItemUseKind.Harvest, MapFarmingFailure(result.Status)) with { Farming = result };
    }

    private static float ResolveReach(ItemActionDefinition action)
    {
        return action.ReachPixels > 0 ? action.ReachPixels : DefaultReachPixels;
    }

    private PlayerItemUseResult StartCooldown(PlayerItemUseResult result, ItemDefinition item)
    {
        if (result.Kind != PlayerItemUseKind.None && item.UseTime > 0)
        {
            _useCooldownDuration = Math.Max(0, item.UseTime);
            _useCooldownRemaining = Math.Max(_useCooldownRemaining, item.UseTime);
        }
        else if (item.UseTime <= 0)
        {
            _useCooldownDuration = 0;
            _useCooldownRemaining = 0;
        }

        return MarkSucceeded(result) with
        {
            CooldownRemaining = _useCooldownRemaining,
            CooldownDuration = _useCooldownDuration
        };
    }

    private static bool UsesDiscreteCooldown(ItemActionDefinition action)
    {
        return action.Kind is ItemActionKind.Place or ItemActionKind.Melee or ItemActionKind.Shoot or ItemActionKind.Consume or ItemActionKind.Cast or
            ItemActionKind.Till or ItemActionKind.Water or ItemActionKind.Plant or ItemActionKind.Harvest;
    }

    private static PlayerItemUseResult MarkSucceeded(PlayerItemUseResult result)
    {
        return result with
        {
            Status = PlayerItemUseStatus.Succeeded,
            AttemptedKind = result.Kind,
            SuccessReason = ResolveSuccessReason(result.Kind),
            FailureReason = GameplayActionFailureReason.None
        };
    }

    private static GameplayActionSuccessReason ResolveSuccessReason(PlayerItemUseKind kind)
    {
        return kind switch
        {
            PlayerItemUseKind.Mine => GameplayActionSuccessReason.TileMined,
            PlayerItemUseKind.Build => GameplayActionSuccessReason.TilePlaced,
            PlayerItemUseKind.Melee => GameplayActionSuccessReason.AttackPerformed,
            PlayerItemUseKind.Shoot or PlayerItemUseKind.Cast => GameplayActionSuccessReason.ProjectileSpawned,
            PlayerItemUseKind.Consume => GameplayActionSuccessReason.ConsumableApplied,
            PlayerItemUseKind.Till or PlayerItemUseKind.Water or PlayerItemUseKind.Plant or PlayerItemUseKind.Harvest =>
                GameplayActionSuccessReason.FarmingCompleted,
            _ => GameplayActionSuccessReason.None
        };
    }

    private static PlayerItemUseKind ResolveUseKind(ItemActionKind actionKind)
    {
        return actionKind switch
        {
            ItemActionKind.Place => PlayerItemUseKind.Build,
            ItemActionKind.Mine => PlayerItemUseKind.Mine,
            ItemActionKind.Melee => PlayerItemUseKind.Melee,
            ItemActionKind.Shoot => PlayerItemUseKind.Shoot,
            ItemActionKind.Cast => PlayerItemUseKind.Cast,
            ItemActionKind.Consume => PlayerItemUseKind.Consume,
            ItemActionKind.Till => PlayerItemUseKind.Till,
            ItemActionKind.Water => PlayerItemUseKind.Water,
            ItemActionKind.Plant => PlayerItemUseKind.Plant,
            ItemActionKind.Harvest => PlayerItemUseKind.Harvest,
            _ => PlayerItemUseKind.None
        };
    }

    private static GameplayActionFailureReason MapFarmingFailure(FarmActionStatus status)
    {
        return status switch
        {
            FarmActionStatus.AlreadyOccupied => GameplayActionFailureReason.Occupied,
            FarmActionStatus.MissingSeed => GameplayActionFailureReason.InsufficientItem,
            FarmActionStatus.OutOfBounds => GameplayActionFailureReason.InvalidTarget,
            _ => GameplayActionFailureReason.InvalidTarget
        };
    }

    private static bool IsValidTarget(Vector2 target, Vector2 origin)
    {
        return float.IsFinite(target.X) &&
               float.IsFinite(target.Y) &&
               Vector2.DistanceSquared(target, origin) > float.Epsilon;
    }

    private void PublishFeedback(
        GameEventBus? events,
        PlayerEntity player,
        string? itemId,
        TilePos targetTile,
        Vector2 targetWorldPosition,
        PlayerItemUseResult result)
    {
        if (result.Success)
        {
            _lastBlockedFeedback = null;
            events?.Publish(new PlayerItemUseCompletedEvent(
                player.Id,
                itemId ?? string.Empty,
                result.Kind,
                result.SuccessReason,
                targetTile,
                targetWorldPosition,
                result.CooldownDuration,
                result.HealthRestored,
                result.ManaRestored,
                result.StatusEffectsApplied));
            return;
        }

        if (result.InProgress)
        {
            _lastBlockedFeedback = null;
            return;
        }

        if (!result.Blocked || events is null)
        {
            return;
        }

        var signature = new BlockedFeedbackSignature(
            events,
            itemId,
            result.AttemptedKind,
            result.FailureReason,
            targetTile);
        if (_lastBlockedFeedback == signature)
        {
            return;
        }

        _lastBlockedFeedback = signature;
        events.Publish(new PlayerItemUseBlockedEvent(
            player.Id,
            itemId,
            result.AttemptedKind,
            result.FailureReason,
            targetTile,
            targetWorldPosition,
            result.CooldownRemaining,
            result.CooldownDuration,
            result.ActionProgress));
    }

    private static void PublishStatusEffects(
        GameEventBus? events,
        int targetEntityId,
        string sourceItemId,
        StatusEffectApplyResult result)
    {
        if (events is null)
        {
            return;
        }

        foreach (var effect in result.AppliedEffects)
        {
            events.Publish(new StatusEffectAppliedEvent(
                targetEntityId,
                effect.EffectId,
                StatusEffectSourceKind.Item,
                sourceItemId,
                effect.Refreshed,
                effect.DurationSeconds));
        }
    }

    private readonly record struct BlockedFeedbackSignature(
        GameEventBus EventBus,
        string? ItemId,
        PlayerItemUseKind Kind,
        GameplayActionFailureReason Reason,
        TilePos TargetTile);
}
