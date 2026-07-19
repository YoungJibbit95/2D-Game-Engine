using Game.Core.Combat;
using Game.Core.Data;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class AttackSequenceContentTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(
        Path.GetTempPath(),
        "yjse-attack-content-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void JsonLoader_ReadsTimelineCostsAndTimedSweptShapes()
    {
        var definition = new AttackSequenceDefinitionJsonLoader().LoadDefinitionFromJson("""
        {
          "id": "blade.combo",
          "inputBufferTicks": 4,
          "eventCapacity": 12,
          "steps": [
            {
              "id": "slash",
              "timeline": {
                "startupTicks": 2,
                "activeTicks": 3,
                "recoveryTicks": 4,
                "cooldownTicks": 1
              },
              "cost": { "stamina": 5, "mana": 2 },
              "meleeShapes": [
                {
                  "id": "slash.sweep",
                  "activeStartTickInclusive": 1,
                  "activeEndTickExclusive": 3,
                  "originOffset": { "x": 2, "y": -1 },
                  "sweep": { "reach": 44, "radius": 9 }
                }
              ]
            }
          ]
        }
        """);

        Assert.Equal("blade.combo", definition.Id);
        Assert.Equal(4, definition.InputBufferTicks);
        Assert.Equal(12, definition.EventCapacity);
        var step = Assert.Single(definition.Steps);
        Assert.Equal(new AttackPhaseWindow(0, 2), step.Timeline.Startup);
        Assert.Equal(new AttackPhaseWindow(2, 5), step.Timeline.Active);
        Assert.Equal(5, step.Cost.Stamina);
        Assert.Equal(2, step.Cost.Mana);
        var shape = Assert.Single(step.MeleeShapes);
        Assert.Equal(1, shape.ActiveStartTickInclusive);
        Assert.Equal(3, shape.ActiveEndTickExclusive);
        Assert.Equal(44, shape.Sweep.Reach);
        Assert.Equal(new System.Numerics.Vector2(2, -1), shape.OriginOffset);
    }

    [Fact]
    public void JsonLoader_RejectsShapeOutsideActiveWindow()
    {
        var json = """
        {
          "id": "bad.combo",
          "steps": [
            {
              "id": "bad",
              "timeline": { "activeTicks": 2 },
              "meleeShapes": [
                {
                  "id": "late",
                  "activeStartTickInclusive": 1,
                  "activeEndTickExclusive": 3,
                  "sweep": { "reach": 30, "radius": 4 }
                }
              ]
            }
          ]
        }
        """;

        Assert.Throws<InvalidOperationException>(() =>
            new AttackSequenceDefinitionJsonLoader().LoadDefinitionFromJson(json));
    }

    [Fact]
    public void ContentLoader_ReportsMissingItemSequenceAndSequenceAmmoReferences()
    {
        Directory.CreateDirectory(Path.Combine(_contentRoot, "items"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "attacks"));
        File.WriteAllText(Path.Combine(_contentRoot, "items", "bad_blade.json"), """
        {
          "id": "bad_blade",
          "displayName": "Bad Blade",
          "type": "WeaponMelee",
          "texture": "items/bad_blade",
          "maxStack": 1,
          "actions": [
            { "kind": "Melee", "attackSequence": "missing.sequence" }
          ]
        }
        """);
        File.WriteAllText(Path.Combine(_contentRoot, "attacks", "orphan_ammo.json"), """
        {
          "id": "orphan.ammo",
          "steps": [
            {
              "id": "shot",
              "timeline": { "activeTicks": 1 },
              "cost": { "ammo": 1, "ammoItemId": "missing_arrow" }
            }
          ]
        }
        """);

        var result = new GameContentLoader().LoadWithMods(_contentRoot, modsRoot: null);

        Assert.True(result.Report.HasErrors);
        Assert.Contains(result.Report.Issues, issue =>
            issue.ContentKind == "item" &&
            issue.ContentId == "bad_blade" &&
            issue.Message.Contains("attack sequence", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, issue =>
            issue.ContentKind == "attack-sequence" &&
            issue.ContentId == "orphan.ammo" &&
            issue.Message.Contains("missing_arrow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ItemRegistry_RejectsExplicitEmptyAttackSequenceReference()
    {
        var item = new ItemDefinition
        {
            Id = "bad_blade",
            DisplayName = "Bad Blade",
            Type = ItemType.WeaponMelee,
            TexturePath = "items/bad_blade",
            Actions =
            [
                new ItemActionDefinition
                {
                    Kind = ItemActionKind.Melee,
                    AttackSequenceId = " "
                }
            ]
        };

        Assert.Throws<RegistryValidationException>(() =>
            ItemRegistry.Create([item]));
    }

    [Fact]
    public void RepositoryData_ResolvesSelectedMeleeRangedAndMagicSequences()
    {
        var result = new GameContentLoader().LoadWithMods(FindRepositoryGameData(), modsRoot: null);

        Assert.False(
            result.Report.HasErrors,
            string.Join(Environment.NewLine, result.Report.Issues.Select(issue => issue.Message)));
        Assert.Equal(3, result.Database.AttackSequences.Definitions.Count);
        Assert.Equal(
            "weapon.wooden_sword.combo",
            result.Database.Items.GetById("wooden_sword").Actions[0].AttackSequenceId);
        Assert.Equal(
            "weapon.wooden_bow.shot",
            result.Database.Items.GetById("wooden_bow").Actions[0].AttackSequenceId);
        Assert.Equal(
            "weapon.spark_wand.cast",
            result.Database.Items.GetById("spark_wand").Actions[0].AttackSequenceId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    private static string FindRepositoryGameData()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (File.Exists(Path.Combine(directory.FullName, "YjsE.sln")) && Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }
}
