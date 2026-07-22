using Game.Core.Assets;
using Game.Core.Assets.Audit;
using Game.Core.Assets.Generation;
using Game.Core.Combat;
using Game.Core.Data;
using Game.Core.Equipment;
using Game.Core.Items;
using Game.Core.Mods;
using Xunit;

namespace Game.Tests.AssetTests;

public sealed class AdventureArsenalV1IntegrationTests
{
    private static readonly ArsenalContract[] Contracts =
    [
        new("cinderbloom_staff", "items/adventure_v1/cinderbloom_staff", ItemType.WeaponMagic, "firebolt", "anvil"),
        new("frostglass_scepter", "items/adventure_v1/frostglass_scepter", ItemType.WeaponMagic, "ice_shard", "anvil"),
        new("ambercore_pickaxe", "items/adventure_v1/ambercore_pickaxe", ItemType.ToolPickaxe, null, "anvil"),
        new("wayfinder_charm", "items/adventure_v1/wayfinder_charm", ItemType.Accessory, null, "workbench")
    ];

    [Fact]
    public void RepositoryArsenal_ItemsRecipesProjectilesAndSpritesFormCompleteRuntimeContracts()
    {
        var dataRoot = FindRepositoryGameData();
        var loadResult = new GameContentLoader().LoadWithMods(dataRoot, modsRoot: null);
        Assert.DoesNotContain(loadResult.Report.Issues, issue => issue.Severity == ContentIssueSeverity.Error);
        var database = loadResult.Database;
        var focusedDefinitions = new List<SpriteAssetDefinition>(Contracts.Length);

        foreach (var contract in Contracts)
        {
            var item = database.Items.GetById(contract.ItemId);
            Assert.Equal(contract.Type, item.Type);
            Assert.Equal(contract.SpriteId, item.TexturePath);
            Assert.True(item.HasTag("adventure-arsenal-v1"));

            var sprite = database.SpriteAssets.GetById(contract.SpriteId);
            focusedDefinitions.Add(sprite);
            Assert.Equal(32, sprite.Width);
            Assert.Equal(32, sprite.Height);
            Assert.Equal(16, sprite.OriginX);
            Assert.Equal(16, sprite.OriginY);
            Assert.Contains("adventure_arsenal_v1", sprite.Provenance, StringComparison.Ordinal);
            Assert.True(sprite.HasTag("native-32px"));

            var recipe = database.Recipes.GetById(contract.ItemId);
            Assert.Equal(contract.ItemId, recipe.Result.ItemId);
            Assert.Equal(1, recipe.Result.Count);
            Assert.Equal(contract.Station, recipe.Station);
            Assert.NotEmpty(recipe.Ingredients);
            var recipeStation = Assert.IsType<string>(recipe.Station);
            Assert.True(database.Items.TryGetById(recipeStation, out _));
            Assert.All(recipe.Ingredients, ingredient => Assert.True(database.Items.TryGetById(ingredient.ItemId, out _)));

            var projectileId = contract.ProjectileId;
            if (projectileId is null)
            {
                Assert.DoesNotContain(
                    item.Actions,
                    action => action.Kind is ItemActionKind.Cast or ItemActionKind.Shoot);
                continue;
            }

            var action = Assert.Single(item.Actions);
            Assert.Equal(ItemActionKind.Cast, action.Kind);
            Assert.Equal(projectileId, action.ProjectileId);
            var projectile = database.Projectiles.GetById(projectileId);
            Assert.Equal(DamageType.Magic, projectile.DamageType);
            Assert.True(projectile.Damage > 0);
            Assert.True(projectile.Speed > 0);
            Assert.True(database.SpriteAssets.TryGetById(projectile.TexturePath, out _));
            if (projectileId == "ice_shard")
            {
                var chill = Assert.Single(projectile.OnHitEffects);
                Assert.Equal("arcane_chill", chill.EffectId);
                Assert.True(chill.Chance > 0);
            }
        }

        var pickaxe = database.Items.GetById("ambercore_pickaxe");
        Assert.Equal(82, pickaxe.ToolPower);
        Assert.True(pickaxe.UseTime < database.Items.GetById("sunsteel_pickaxe").UseTime);

        var charm = database.Items.GetById("wayfinder_charm");
        Assert.Equal(EquipmentSlotType.Accessory1, charm.EquipmentSlot);
        Assert.True(charm.MovementSpeedBonus > 0);
        Assert.True(charm.MiningSpeedBonus > 0);
        Assert.True(charm.MaxManaBonus > 0);

        var briefs = SpriteGenerationBriefRegistry.Create(
            new SpriteGenerationBriefJsonLoader().LoadBriefsFromDirectory(Path.Combine(dataRoot, "asset_briefs")));
        var audit = new SpriteAssetAuditService().Audit(
            dataRoot,
            SpriteAssetRegistry.Create(focusedDefinitions),
            briefs);

        Assert.False(audit.HasErrors, string.Join(Environment.NewLine, audit.Issues.Select(issue => issue.Message)));
        Assert.Equal(Contracts.Length, audit.Entries.Count);
        Assert.All(audit.Entries, entry =>
        {
            Assert.Equal(SpriteAssetFileStatus.Present, entry.FileStatus);
            Assert.Equal(32, entry.ActualWidth);
            Assert.Equal(32, entry.ActualHeight);
            Assert.True(entry.HasGenerationBrief);
        });
    }

    private static string FindRepositoryGameData()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (File.Exists(Path.Combine(directory.FullName, "YjsE.sln")) &&
                Directory.Exists(Path.Combine(candidate, "assets")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }

    private readonly record struct ArsenalContract(
        string ItemId,
        string SpriteId,
        ItemType Type,
        string? ProjectileId,
        string Station);
}
