using Game.Core.Inventory;
using Game.Core.Items;

namespace Game.Core.World.TileEntities;

public sealed class ChestTileEntity : TileEntity
{
    public const string ChestTypeId = "chest";

    public ChestTileEntity(TilePos position, IItemDefinitionProvider items, int slotCount = 20)
        : base(ChestTypeId, position)
    {
        Inventory = new Inventory.Inventory(slotCount, items);
    }

    public Inventory.Inventory Inventory { get; }

    public bool AddItem(ItemStack stack)
    {
        return Inventory.AddItem(stack);
    }
}
