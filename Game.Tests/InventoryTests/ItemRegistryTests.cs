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
