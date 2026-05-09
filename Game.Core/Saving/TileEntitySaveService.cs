using Game.Core.Items;
using Game.Core.World;
using Game.Core.World.TileEntities;
using System.Text.Json;

namespace Game.Core.Saving;

public sealed class TileEntitySaveService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public void Save(TileEntityManager manager, string filePath)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = manager.Entities.Select(ToSaveData).ToArray();
        File.WriteAllText(filePath, JsonSerializer.Serialize(data, Options));
    }

    public TileEntityManager Load(string filePath, IItemDefinitionProvider items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(items);

        var manager = new TileEntityManager();
        if (!File.Exists(filePath))
        {
            return manager;
        }

        var data = JsonSerializer.Deserialize<TileEntitySaveData[]>(File.ReadAllText(filePath), Options)
            ?? Array.Empty<TileEntitySaveData>();

        foreach (var item in data)
        {
            manager.Add(FromSaveData(item, items));
        }

        return manager;
    }

    private static TileEntitySaveData ToSaveData(TileEntity entity)
    {
        return entity switch
        {
            ChestTileEntity chest => new TileEntitySaveData
            {
                RuntimeId = chest.RuntimeId,
                TypeId = chest.TypeId,
                TileX = chest.Position.X,
                TileY = chest.Position.Y,
                InventorySlots = chest.Inventory.Slots.Select(slot => slot.Stack).ToArray()
            },
            _ => new TileEntitySaveData
            {
                RuntimeId = entity.RuntimeId,
                TypeId = entity.TypeId,
                TileX = entity.Position.X,
                TileY = entity.Position.Y
            }
        };
    }

    private static TileEntity FromSaveData(TileEntitySaveData data, IItemDefinitionProvider items)
    {
        TileEntity entity = data.TypeId switch
        {
            ChestTileEntity.ChestTypeId => RestoreChest(data, items),
            _ => throw new InvalidDataException($"Unknown tile entity type '{data.TypeId}'.")
        };

        entity.AssignId(data.RuntimeId);
        return entity;
    }

    private static ChestTileEntity RestoreChest(TileEntitySaveData data, IItemDefinitionProvider items)
    {
        var chest = new ChestTileEntity(new TilePos(data.TileX, data.TileY), items, Math.Max(1, data.InventorySlots.Count));
        for (var index = 0; index < data.InventorySlots.Count; index++)
        {
            chest.Inventory.Slots[index].SetStack(data.InventorySlots[index]);
        }

        return chest;
    }
}
