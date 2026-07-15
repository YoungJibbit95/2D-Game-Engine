using Game.Client.UI;
using Game.Core.Inventory;
using Game.Core.Items;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class InventoryOverlayHotPathTests
{
    [Fact]
    public void HitZoneBuffer_ReturnsTopmostInteractiveZone()
    {
        var zones = new HitZoneBuffer<int>(3);
        zones.Add(new Rectangle(0, 0, 20, 20), 1);
        zones.Add(new Rectangle(5, 5, 20, 20), 2);
        zones.Add(new Rectangle(5, 5, 20, 20), 3, isInteractive: false);

        var hit = zones.FindTopmost(new Point(10, 10));

        Assert.NotNull(hit);
        Assert.Equal(2, hit.Value.Value);
    }

    [Fact]
    public void HitZoneBuffer_ReusesCapacityWithoutSteadyStateAllocations()
    {
        var zones = new HitZoneBuffer<int>(4);
        ExerciseHitZones(zones);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            ExerciseHitZones(zones);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(4, zones.Capacity);
    }

    [Fact]
    public void SectionQueryCache_InvalidatesOnInventoryVersionAndFilterChanges()
    {
        var items = CreateItems();
        var inventory = new Inventory(3, items);
        inventory.Slots[0].SetStack(new ItemStack("dirt", 10));
        var cache = new InventorySectionQueryCache("PACK");
        var weaponFilter = new[] { ItemCategory.Weapon };

        cache.Refresh(inventory, items, weaponFilter, filterKey: 1, applyFilter: true);
        var firstRefreshCount = cache.RefreshCount;
        cache.Refresh(inventory, items, weaponFilter, filterKey: 1, applyFilter: true);

        Assert.Equal(firstRefreshCount, cache.RefreshCount);
        Assert.False(cache.IsInteractive(0));
        Assert.True(cache.IsInteractive(1));
        Assert.Equal("PACK  1/3", cache.Summary);

        inventory.Slots[0].SetStack(new ItemStack("sword", 1));
        cache.Refresh(inventory, items, weaponFilter, filterKey: 1, applyFilter: true);

        Assert.Equal(firstRefreshCount + 1, cache.RefreshCount);
        Assert.True(cache.IsInteractive(0));

        cache.Refresh(inventory, items, new[] { ItemCategory.Block }, filterKey: 2, applyFilter: true);
        Assert.Equal(firstRefreshCount + 2, cache.RefreshCount);
        Assert.False(cache.IsInteractive(0));
    }

    [Fact]
    public void SectionQueryCache_SameVersionHasZeroSteadyStateAllocations()
    {
        var items = CreateItems();
        var inventory = new Inventory(3, items);
        inventory.Slots[0].SetStack(new ItemStack("dirt", 10));
        var categories = new[] { ItemCategory.Block };
        var cache = new InventorySectionQueryCache("PACK");
        cache.Refresh(inventory, items, categories, filterKey: 1, applyFilter: true);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            cache.Refresh(inventory, items, categories, filterKey: 1, applyFilter: true);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1, cache.RefreshCount);
    }

    [Fact]
    public void StatisticsCache_TracksBothInventoryVersions()
    {
        var items = CreateItems();
        var inventory = new PlayerInventory(items);
        var service = new InventoryQueryService(items);
        var cache = new InventoryStatisticsCache();

        var initial = cache.Get(service, inventory);
        var repeated = cache.Get(service, inventory);

        Assert.Same(initial, repeated);
        Assert.Equal(1, cache.RefreshCount);

        inventory.Main.Slots[0].SetStack(new ItemStack("dirt", 5));
        var changed = cache.Get(service, inventory);

        Assert.NotSame(initial, changed);
        Assert.Equal(2, cache.RefreshCount);
        Assert.Equal("USED 1   FREE 49   ITEMS 5   VALUE 10", cache.Summary);

        inventory.Main.SetFavorite(0, true);
        cache.Get(service, inventory);
        Assert.Equal(3, cache.RefreshCount);
    }

    [Fact]
    public void TooltipCache_ReusesStableAndDynamicText()
    {
        var item = CreateItems().GetById("sword");
        var cache = new InventoryTooltipCache();

        var first = cache.Get(item);
        var repeated = cache.Get(item);
        var stackLine = first.GetStackLine(1, favorite: true);

        Assert.Same(first, repeated);
        Assert.Equal(1, cache.BuildCount);
        Assert.Equal("RARE  WEAPON  VALUE 25", first.Classification);
        Assert.Equal(new[] { "A practical copper sword." }, first.DescriptionLines);
        Assert.Equal(new[] { "DMG 7" }, first.DetailLines);
        Assert.Equal("STACK 1/1  FAVORITE", stackLine);
        Assert.Same(stackLine, first.GetStackLine(1, favorite: true));
    }

    [Theory]
    [InlineData("Copper Pickaxe", "CP")]
    [InlineData("Gel", "GEL")]
    [InlineData("ManaCrystal", "MANA")]
    [InlineData(" ", "?")]
    public void ItemAbbreviation_PreservesExistingLabels(string displayName, string expected)
    {
        Assert.Equal(expected, ItemIconRenderer.Abbreviate(displayName));
    }

    [Fact]
    public void ItemTextCaches_ReturnSameInstancesAfterWarmup()
    {
        var item = CreateItems().GetById("sword");
        var abbreviation = ItemIconRenderer.GetFallbackLabel(item);
        var count = ItemIconRenderer.GetCountLabel(999);

        Assert.Same(abbreviation, ItemIconRenderer.GetFallbackLabel(item));
        Assert.Same(count, ItemIconRenderer.GetCountLabel(999));
    }

    private static void ExerciseHitZones(HitZoneBuffer<int> zones)
    {
        zones.Clear();
        zones.Add(new Rectangle(0, 0, 10, 10), 1);
        zones.Add(new Rectangle(10, 0, 10, 10), 2);
        zones.Add(new Rectangle(0, 10, 10, 10), 3);
        zones.Add(new Rectangle(10, 10, 10, 10), 4);
        _ = zones.FindTopmost(new Point(15, 15));
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "dirt",
                DisplayName = "Dirt Block",
                Type = ItemType.PlaceableTile,
                Category = ItemCategory.Block,
                Value = 2,
                TexturePath = "items/dirt",
                MaxStack = 999,
                PlacesTileId = "dirt"
            },
            new ItemDefinition
            {
                Id = "sword",
                DisplayName = "Copper Sword",
                Description = "A practical copper sword.",
                Type = ItemType.WeaponMelee,
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Rare,
                Value = 25,
                Damage = 7,
                TexturePath = "items/sword",
                MaxStack = 1
            }
        });
    }
}
