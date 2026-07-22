using Game.Core.Combat;
using Game.Core.Data;
using Game.Core.Equipment;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Movement;
using Xunit;

namespace Game.Tests.EquipmentTests;

public sealed class MobilityAbilityContentTests
{
    [Fact]
    public void JsonLoader_LoadsStatDrivenMobilityAndManaActionContracts()
    {
        var definition = new ItemDefinitionJsonLoader().LoadDefinitionFromJson("""
        {
          "id": "aether_boots",
          "displayName": "Aether Boots",
          "type": "Accessory",
          "texture": "items/aether_boots",
          "mobility": {
            "extraJumpCount": 2,
            "airJumpVelocityMultiplier": 1.05,
            "canWallJump": true,
            "flightDurationSeconds": 1.75,
            "flightVerticalSpeedMultiplier": 1.1,
            "flightAccelerationMultiplier": 1.2,
            "glideEnabled": true,
            "glideGravityScale": 0.16,
            "glideTerminalVelocity": 96
          },
          "actions": [
            {
              "kind": "Cast",
              "projectile": "aether_bolt",
              "manaRegenerationDelaySeconds": 0.85,
              "manaRefundPolicy": "Always"
            }
          ]
        }
        """);

        var mobility = Assert.IsType<MobilityAbilityDefinition>(definition.Mobility);
        Assert.Equal(2, mobility.ExtraJumpCount);
        Assert.Equal(1.75f, mobility.FlightDurationSeconds);
        Assert.True(mobility.HasGlide);
        Assert.True(definition.CanDoubleJump);
        Assert.True(definition.CanWallJump);
        Assert.True(definition.CanFly);
        Assert.True(definition.CanGlide);
        Assert.Equal(0.85f, definition.Actions[0].ManaRegenerationDelaySeconds);
        Assert.Equal(ManaRefundPolicy.Always, definition.Actions[0].ManaRefundPolicy);
    }

    [Fact]
    public void LoadoutResolver_ComposesThreeAccessoriesWithoutBooleanOnlyTuning()
    {
        var items = ItemRegistry.Create(
        [
            CreateAccessory(
                "boots",
                new MobilityAbilityDefinition
                {
                    ExtraJumpCount = 1,
                    AirJumpVelocityMultiplier = 0.94f
                }),
            CreateAccessory(
                "wings",
                new MobilityAbilityDefinition
                {
                    FlightDurationSeconds = 2.4f,
                    FlightVerticalSpeedMultiplier = 1.08f,
                    FlightAccelerationMultiplier = 1.1f
                }),
            CreateAccessory(
                "glider",
                new MobilityAbilityDefinition
                {
                    GlideEnabled = true,
                    GlideGravityScale = 0.18f,
                    GlideTerminalVelocity = 105f
                })
        ]);
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(new ItemStack("boots", 1), items, EquipmentSlotType.Accessory1).Success);
        Assert.True(loadout.TryEquip(new ItemStack("wings", 1), items, EquipmentSlotType.Accessory2).Success);
        Assert.True(loadout.TryEquip(new ItemStack("glider", 1), items, EquipmentSlotType.Accessory3).Success);

        var profile = new MobilityAbilityLoadoutResolver().Resolve(loadout, items);
        var stats = new EquipmentStatCalculator().Calculate(PlayerStatBlock.Base, loadout, items);

        Assert.Equal(1, profile.ExtraJumpCount);
        Assert.Equal(0.94f, profile.AirJumpVelocityMultiplier);
        Assert.Equal(2.4f, profile.FlightDurationSeconds);
        Assert.Equal(1.08f, profile.FlightVerticalSpeedMultiplier);
        Assert.True(profile.GlideEnabled);
        Assert.Equal(0.18f, profile.GlideGravityScale);
        Assert.Equal(105f, profile.GlideTerminalVelocity);
        Assert.True(stats.CanDoubleJump);
        Assert.True(stats.CanFly);
        Assert.True(stats.CanGlide);
    }

    [Fact]
    public void Registry_RejectsInvalidMobilityAndManaDelay()
    {
        var invalidMobility = CreateAccessory(
            "invalid_mobility",
            new MobilityAbilityDefinition { ExtraJumpCount = 99 });
        var invalidDelay = CreateAccessory("invalid_delay", null) with
        {
            Actions =
            [
                new ItemActionDefinition
                {
                    Kind = ItemActionKind.Cast,
                    ProjectileId = "bolt",
                    ManaRegenerationDelaySeconds = float.NaN
                }
            ]
        };

        Assert.Throws<RegistryValidationException>(() => ItemRegistry.Create([invalidMobility]));
        Assert.Throws<RegistryValidationException>(() => ItemRegistry.Create([invalidDelay]));
    }

    private static ItemDefinition CreateAccessory(
        string id,
        MobilityAbilityDefinition? mobility)
    {
        return new ItemDefinition
        {
            Id = id,
            DisplayName = id,
            Type = ItemType.Accessory,
            TexturePath = $"items/{id}",
            Mobility = mobility
        };
    }
}