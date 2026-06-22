using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Entities;
using System.Text.Json;
using InventoryModel = Game.Core.Inventory.Inventory;

namespace Game.Core.Saving;

public sealed class PlayerSaveService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public void Save(PlayerSaveData data, string filePath)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(data, Options));
    }

    public PlayerSaveData Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Player save file was not found.", filePath);
        }

        return JsonSerializer.Deserialize<PlayerSaveData>(File.ReadAllText(filePath), Options)
            ?? throw new InvalidDataException("Player save file was empty.");
    }

    public PlayerSaveData CreateSaveData(
        PlayerEntity player,
        PlayerInventory inventory,
        string playerId,
        string displayName,
        int? mana = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new PlayerSaveData
        {
            PlayerId = playerId,
            DisplayName = displayName,
            PositionX = player.Body.Position.X,
            PositionY = player.Body.Position.Y,
            Health = player.Health,
            MaxHealth = player.MaxHealth,
            Mana = mana ?? player.Mana,
            SelectedHotbarSlot = inventory.SelectedHotbarSlot,
            InventorySlots = inventory.Hotbar.Slots
                .Concat(inventory.Main.Slots)
                .Select(slot => slot.Stack)
                .ToArray()
        };
    }

    public InventoryModel ToInventory(PlayerSaveData data, IItemDefinitionProvider itemDefinitions)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(itemDefinitions);

        var inventory = new InventoryModel(data.InventorySlots.Count, itemDefinitions);
        for (var index = 0; index < data.InventorySlots.Count; index++)
        {
            inventory.Slots[index].SetStack(data.InventorySlots[index]);
        }

        return inventory;
    }

    public PlayerInventory ToPlayerInventory(PlayerSaveData data, IItemDefinitionProvider itemDefinitions)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(itemDefinitions);

        var hotbar = new InventoryModel(PlayerInventory.HotbarSlotCount, itemDefinitions);
        var main = new InventoryModel(PlayerInventory.MainSlotCount, itemDefinitions);

        for (var index = 0; index < Math.Min(PlayerInventory.HotbarSlotCount, data.InventorySlots.Count); index++)
        {
            hotbar.Slots[index].SetStack(data.InventorySlots[index]);
        }

        var mainSourceStart = PlayerInventory.HotbarSlotCount;
        for (var index = 0; index < PlayerInventory.MainSlotCount && mainSourceStart + index < data.InventorySlots.Count; index++)
        {
            main.Slots[index].SetStack(data.InventorySlots[mainSourceStart + index]);
        }

        var inventory = new PlayerInventory(hotbar, main, itemDefinitions);
        inventory.SelectHotbarSlot(Math.Clamp(data.SelectedHotbarSlot, 0, PlayerInventory.HotbarSlotCount - 1));
        return inventory;
    }
}
