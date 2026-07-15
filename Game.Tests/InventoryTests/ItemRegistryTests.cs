using Game.Core.Data;
using Game.Core.Combat;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.InventoryTests;

public sealed class ItemRegistryTests
{
    [Fact]
    public void Create_MapsItemsCaseInsensitively()
    {
        var registry = ItemRegistry.Create(new[] { CreateDirtBlockDefinition() });

        Assert.Equal("dirt_block", registry.GetById("DIRT_BLOCK").Id);
    }

    [Fact]
    public void Create_RejectsDuplicateIds()
    {
        var first = CreateDirtBlockDefinition();
        var second = CreateDirtBlockDefinition();

        Assert.Throws<RegistryValidationException>(() => ItemRegistry.Create(new[] { first, second }));
    }

    [Fact]
    public void Loader_ReadsItemDefinitionJson()
    {
        const string json = """
        {
          "id": "copper_pickaxe",
          "displayName": "Copper Pickaxe",
          "type": "ToolPickaxe",
          "texture": "items/copper_pickaxe",
          "maxStack": 1,
          "useTime": 0.35,
          "toolPower": 35,
          "damage": 3,
          "knockback": 1.5,
          "attackShape": {
            "kind": "Rectangle",
            "range": 40,
            "width": 36,
            "height": 24
          },
          "onHitEffects": [
            { "effect": "poisoned", "chance": 0.25, "durationSeconds": 3.0 }
          ]
        }
        """;

        var loader = new ItemDefinitionJsonLoader();

        var definition = loader.LoadDefinitionFromJson(json);

        Assert.Equal("copper_pickaxe", definition.Id);
        Assert.Equal(ItemType.ToolPickaxe, definition.Type);
        Assert.Equal("items/copper_pickaxe", definition.TexturePath);
        Assert.Equal(35, definition.ToolPower);
        Assert.Equal(ItemActionKind.Mine, Assert.Single(definition.Actions).Kind);
        Assert.NotNull(definition.AttackShape);
        Assert.Equal(AttackShapeKind.Rectangle, definition.AttackShape.Kind);
        var effect = Assert.Single(definition.OnHitEffects);
        Assert.Equal("poisoned", effect.EffectId);
        Assert.Equal(0.25f, effect.Chance);
    }

    [Fact]
    public void Loader_ReadsDataDrivenItemActions()
    {
        const string json = """
        {
          "id": "wooden_bow",
          "displayName": "Wooden Bow",
          "type": "WeaponRanged",
          "texture": "items/wooden_bow",
          "maxStack": 1,
          "actions": [
            {
              "kind": "Shoot",
              "projectile": "wooden_arrow",
              "ammo": "wooden_arrow",
              "ammoCost": 1,
              "projectileSpeedMultiplier": 1.25
            }
          ]
        }
        """;

        var definition = new ItemDefinitionJsonLoader().LoadDefinitionFromJson(json);
        var action = Assert.Single(definition.Actions);

        Assert.Equal(ItemActionKind.Shoot, action.Kind);
        Assert.Equal("wooden_arrow", action.ProjectileId);
        Assert.Equal("wooden_arrow", action.AmmoItemId);
        Assert.Equal(1.25f, action.ProjectileSpeedMultiplier);
    }

    [Fact]
    public void Create_RejectsShootActionWithoutProjectile()
    {
        var badItem = new ItemDefinition
        {
            Id = "bad_bow",
            DisplayName = "Bad Bow",
            Type = ItemType.WeaponRanged,
            TexturePath = "items/bad_bow",
            MaxStack = 1,
            Actions = new[] { ItemActionDefinition.Create(ItemActionKind.Shoot) }
        };

        Assert.Throws<RegistryValidationException>(() => ItemRegistry.Create(new[] { badItem }));
    }

    [Fact]
    public void Loader_ReadsMagicItemFields()
    {
        const string json = """
        {
          "id": "spark_wand",
          "displayName": "Spark Wand",
          "type": "WeaponMagic",
          "texture": "items/spark_wand",
          "maxStack": 1,
          "damage": 3,
          "manaCost": 6,
          "actions": [
            { "kind": "Cast", "projectile": "spark_bolt", "projectileSpeedMultiplier": 1.1 }
          ]
        }
        """;

        var definition = new ItemDefinitionJsonLoader().LoadDefinitionFromJson(json);
        var action = Assert.Single(definition.Actions);

        Assert.Equal(ItemType.WeaponMagic, definition.Type);
        Assert.Equal(6, definition.ManaCost);
        Assert.Equal(ItemActionKind.Cast, action.Kind);
        Assert.Equal("spark_bolt", action.ProjectileId);
    }

    [Fact]
    public void Loader_ReadsInventoryMetadata()
    {
        const string json = """
        {
          "id": "royal_gem",
          "displayName": "Royal Gem",
          "description": "A gem treasured by collectors.",
          "type": "Material",
          "rarity": "Epic",
          "value": 750,
          "category": "Quest",
          "sortPriority": -10,
          "canFavorite": false,
          "canTrash": false,
          "texture": "items/royal_gem",
          "maxStack": 20
        }
        """;

        var definition = new ItemDefinitionJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal("A gem treasured by collectors.", definition.Description);
        Assert.Equal(ItemRarity.Epic, definition.Rarity);
        Assert.Equal(750, definition.Value);
        Assert.Equal(ItemCategory.Quest, definition.ResolvedCategory);
        Assert.Equal(-10, definition.SortPriority);
        Assert.False(definition.CanFavorite);
        Assert.False(definition.CanTrash);
    }

    [Fact]
    public void Create_RejectsNegativeItemValue()
    {
        var definition = CreateDirtBlockDefinition() with { Value = -1 };

        Assert.Throws<RegistryValidationException>(() => ItemRegistry.Create(new[] { definition }));
    }

    private static ItemDefinition CreateDirtBlockDefinition()
    {
        return new ItemDefinition
        {
            Id = "dirt_block",
            DisplayName = "Dirt Block",
            Type = ItemType.PlaceableTile,
            TexturePath = "items/dirt_block",
            MaxStack = 999,
            PlacesTileId = "dirt"
        };
    }
}
