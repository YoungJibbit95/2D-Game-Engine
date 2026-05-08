using Game.Core.Combat;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
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

    public PlayerItemUseSystem(
        MiningSystem? mining = null,
        BuildingSystem? building = null,
        MeleeAttackSystem? melee = null,
        TileCollisionResolver? collisionResolver = null)
    {
        _collisionResolver = collisionResolver ?? new TileCollisionResolver();
        _mining = mining ?? new MiningSystem();
        _building = building ?? new BuildingSystem();
        _melee = melee ?? new MeleeAttackSystem(new LootRoller(new Random()), _collisionResolver);
    }

    public void Update(float deltaSeconds)
    {
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
        GameEventBus? events = null)
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

        return item.Type switch
        {
            ItemType.PlaceableTile => TryBuild(world, content, player, inventory, targetTile, selected.ItemId),
            ItemType.ToolPickaxe => TryMine(world, content, player, entities, targetTile, item, deltaSeconds, events),
            ItemType.WeaponMelee or ItemType.ToolAxe => TryMelee(player, entities, content.LootTables, item, targetWorldPosition, events),
            _ => PlayerItemUseResult.None
        };
    }

    private PlayerItemUseResult TryBuild(
        World.World world,
        GameContentDatabase content,
        PlayerEntity player,
        PlayerInventory inventory,
        TilePos targetTile,
        string itemId)
    {
        var placed = _building.PlaceTile(
            world,
            inventory,
            content.Items,
            content.Tiles,
            targetTile,
            itemId,
            player.Body.Center,
            DefaultReachPixels,
            player.Bounds);

        return placed
            ? new PlayerItemUseResult(PlayerItemUseKind.Build, MiningResult.None, true, MeleeAttackResult.None)
            : PlayerItemUseResult.None;
    }

    private PlayerItemUseResult TryMine(
        World.World world,
        GameContentDatabase content,
        PlayerEntity player,
        EntityManager entities,
        TilePos targetTile,
        ItemDefinition item,
        float deltaSeconds,
        GameEventBus? events)
    {
        var mining = _mining.Update(
            world,
            content.Tiles,
            targetTile,
            player.Body.Center,
            DefaultReachPixels,
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
        LootTableRegistry lootTables,
        ItemDefinition item,
        Vector2 targetWorldPosition,
        GameEventBus? events)
    {
        var melee = _melee.Attack(player, entities, item, lootTables, targetWorldPosition, events);
        return melee.Attacked
            ? new PlayerItemUseResult(PlayerItemUseKind.Melee, MiningResult.None, false, melee)
            : PlayerItemUseResult.None;
    }
}
