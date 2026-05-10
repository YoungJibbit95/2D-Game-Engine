using Game.Core.Combat;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
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
    private float _useCooldownRemaining;

    public PlayerItemUseSystem(
        MiningSystem? mining = null,
        BuildingSystem? building = null,
        MeleeAttackSystem? melee = null,
        TileCollisionResolver? collisionResolver = null,
        ProjectileFactory? projectiles = null)
    {
        _collisionResolver = collisionResolver ?? new TileCollisionResolver();
        _mining = mining ?? new MiningSystem();
        _building = building ?? new BuildingSystem();
        _melee = melee ?? new MeleeAttackSystem(new LootRoller(new Random()), _collisionResolver);
        _projectiles = projectiles ?? new ProjectileFactory();
    }

    public void Update(float deltaSeconds)
    {
        _useCooldownRemaining = Math.Max(0, _useCooldownRemaining - Math.Max(0, deltaSeconds));
        _melee.Update(deltaSeconds);
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
        Random? farmingRandom = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(entities);

        var selected = inventory.SelectedStack;
        if (selected.IsEmpty || !content.Items.TryGetById(selected.ItemId, out var item))
        {
            _mining.Reset();
            return PlayerItemUseResult.None;
        }

        var action = ItemActionResolver.GetPrimaryAction(item);
        if (UsesDiscreteCooldown(action) && _useCooldownRemaining > 0)
        {
            return PlayerItemUseResult.None;
        }

        return action.Kind switch
        {
            ItemActionKind.Place => TryBuild(world, content, player, inventory, targetTile, selected.ItemId, action, events),
            ItemActionKind.Mine => TryMine(world, content, player, entities, targetTile, item, action, deltaSeconds, events),
            ItemActionKind.Melee => TryMelee(player, entities, content, item, targetWorldPosition, events),
            ItemActionKind.Shoot => TryShoot(content, player, inventory, entities, item, action, targetWorldPosition),
            ItemActionKind.Till => TryTill(world, content, farmPlots, targetTile, item),
            ItemActionKind.Water => TryWater(world, farmPlots, targetTile, item),
            ItemActionKind.Plant => TryPlant(world, content, inventory, farmPlots, targetTile, selected.ItemId, item, farmSeason, currentDay),
            ItemActionKind.Harvest => TryHarvest(content, inventory, farmPlots, targetTile, item, farmingRandom),
            _ => PlayerItemUseResult.None
        };
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
        var placed = _building.PlaceTile(
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

        return placed
            ? StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Build, MiningResult.None, true, MeleeAttackResult.None), content.Items.GetById(itemId))
            : PlayerItemUseResult.None;
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
            deltaSeconds,
            events);

        if (mining.Completed && !mining.DroppedItem.IsEmpty)
        {
            entities.Add(new DroppedItemEntity(
                mining.DroppedItem,
                CoordinateUtils.TileToWorld(mining.TilePosition),
                _collisionResolver));
        }

        return mining.Completed
            ? new PlayerItemUseResult(PlayerItemUseKind.Mine, mining, false, MeleeAttackResult.None)
            : PlayerItemUseResult.None;
    }

    private PlayerItemUseResult TryMelee(
        PlayerEntity player,
        EntityManager entities,
        GameContentDatabase content,
        ItemDefinition item,
        Vector2 targetWorldPosition,
        GameEventBus? events)
    {
        var melee = _melee.Attack(player, entities, item, content.LootTables, targetWorldPosition, events, content.StatusEffects);
        return melee.Attacked
            ? StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Melee, MiningResult.None, false, melee), item)
            : PlayerItemUseResult.None;
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
            return PlayerItemUseResult.None;
        }

        if (!string.IsNullOrWhiteSpace(action.AmmoItemId))
        {
            if (inventory.CountItem(action.AmmoItemId) < action.AmmoCost)
            {
                return PlayerItemUseResult.None;
            }

            inventory.RemoveItem(action.AmmoItemId, action.AmmoCost);
        }

        var direction = targetWorldPosition - player.Body.Center;
        var projectile = _projectiles.Create(
            definition,
            player.Body.Center,
            direction,
            player.Id == 0 ? null : player.Id);

        projectile.Velocity *= action.ProjectileSpeedMultiplier;
        entities.Add(projectile);

        return StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Shoot, MiningResult.None, false, MeleeAttackResult.None, projectile), item);
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
            return PlayerItemUseResult.None;
        }

        var result = new FarmingSystem().Till(world, content.Tiles, farmPlots, targetTile);
        return result.Status == FarmActionStatus.Completed
            ? StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Till, MiningResult.None, false, MeleeAttackResult.None, Farming: result), item)
            : PlayerItemUseResult.None with { Farming = result };
    }

    private PlayerItemUseResult TryWater(
        World.World world,
        FarmPlotManager? farmPlots,
        TilePos targetTile,
        ItemDefinition item)
    {
        if (farmPlots is null)
        {
            return PlayerItemUseResult.None;
        }

        var result = new FarmingSystem().Water(world, farmPlots, targetTile);
        return result.Status == FarmActionStatus.Completed
            ? StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Water, MiningResult.None, false, MeleeAttackResult.None, Farming: result), item)
            : PlayerItemUseResult.None with { Farming = result };
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
            return PlayerItemUseResult.None;
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
            : PlayerItemUseResult.None with { Farming = result };
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
            return PlayerItemUseResult.None;
        }

        var result = new FarmingSystem().Harvest(content.Crops, farmPlots, inventory, targetTile, farmingRandom);
        return result.Status == FarmActionStatus.Completed
            ? StartCooldown(new PlayerItemUseResult(PlayerItemUseKind.Harvest, MiningResult.None, false, MeleeAttackResult.None, Farming: result), item)
            : PlayerItemUseResult.None with { Farming = result };
    }

    private static float ResolveReach(ItemActionDefinition action)
    {
        return action.ReachPixels > 0 ? action.ReachPixels : DefaultReachPixels;
    }

    private PlayerItemUseResult StartCooldown(PlayerItemUseResult result, ItemDefinition item)
    {
        if (result.Kind != PlayerItemUseKind.None && item.UseTime > 0)
        {
            _useCooldownRemaining = Math.Max(_useCooldownRemaining, item.UseTime);
        }

        return result;
    }

    private static bool UsesDiscreteCooldown(ItemActionDefinition action)
    {
        return action.Kind is ItemActionKind.Place or ItemActionKind.Melee or ItemActionKind.Shoot or ItemActionKind.Consume or ItemActionKind.Cast or
            ItemActionKind.Till or ItemActionKind.Water or ItemActionKind.Plant or ItemActionKind.Harvest;
    }
}
