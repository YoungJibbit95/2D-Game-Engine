using Game.Core.Assets;
using Game.Core.Assets.Audit;
using Game.Core.Assets.Generation;
using Game.Core.Data;
using Xunit;

namespace Game.Tests.AssetTests;

public sealed class SpriteAssetTests
{
    [Fact]
    public void Loader_ReadsSpriteManifest()
    {
        const string json = """
        {
          "sprites": [
            {
              "id": "items/copper_pickaxe",
              "path": "sprites/tools/copper_pickaxe.png",
              "category": "Tool",
              "width": 64,
              "height": 16,
              "pixelsPerUnit": 32,
              "atlasId": "items",
              "sourceAliasOf": "items/canonical_pickaxe",
              "originX": 8,
              "originY": 16,
              "renderLayer": "ui.icon",
              "license": "YjsE-Project-Owned",
              "provenance": "representative test asset",
              "frames": [
                { "id": "mask_0", "x": 0, "y": 0, "width": 16, "height": 16, "autoTileMask": 0 },
                { "id": "mask_3", "x": 48, "y": 0, "width": 16, "height": 16, "autoTileMask": 3 }
              ],
              "tags": ["tool", "pickaxe"]
            }
          ]
        }
        """;

        var definition = Assert.Single(new SpriteAssetJsonLoader().LoadDefinitionsFromJson(json));

        Assert.Equal("items/copper_pickaxe", definition.Id);
        Assert.Equal(SpriteAssetCategory.Tool, definition.Category);
        Assert.Equal(32, definition.PixelsPerUnit);
        Assert.Equal("items", definition.AtlasId);
        Assert.Equal("items/canonical_pickaxe", definition.SourceAliasOf);
        Assert.Equal(8, definition.OriginX);
        Assert.Equal(16, definition.OriginY);
        Assert.Equal("ui.icon", definition.RenderLayer);
        Assert.Equal("YjsE-Project-Owned", definition.License);
        Assert.Equal("representative test asset", definition.Provenance);
        Assert.Equal(1, definition.ResolveFrameIndexForAutoTileMask(3));
        Assert.Equal(0, definition.ResolveFrameIndexForAutoTileMask(8));
        Assert.True(definition.HasTag("pickaxe"));
    }

    [Fact]
    public void Registry_RejectsDuplicateAssetIds()
    {
        var definitions = new[]
        {
            CreateSprite("items/wood"),
            CreateSprite("items/wood")
        };

        Assert.Throws<RegistryValidationException>(() => SpriteAssetRegistry.Create(definitions));
    }

    [Fact]
    public void Registry_RejectsFramesOutsideSpriteBounds()
    {
        var definitions = new[]
        {
            new SpriteAssetDefinition
            {
                Id = "tiles/broken",
                Path = "sprites/world/tiles/broken.png",
                Category = SpriteAssetCategory.Tile,
                Width = 16,
                Height = 16,
                Frames = new[]
                {
                    new SpriteFrameDefinition
                    {
                        X = 16,
                        Y = 0,
                        Width = 16,
                        Height = 16,
                        AutoTileMask = 1
                    }
                }
            }
        };

        Assert.Throws<RegistryValidationException>(() => SpriteAssetRegistry.Create(definitions));
    }

    [Fact]
    public void Registry_RejectsIncompleteOrOutOfBoundsSpriteOrigins()
    {
        var incomplete = CreateSprite("ui/incomplete") with { OriginX = 8 };
        var outside = CreateSprite("ui/outside") with { OriginX = 17, OriginY = 8 };

        Assert.Throws<RegistryValidationException>(() => SpriteAssetRegistry.Create(new[] { incomplete }));
        Assert.Throws<RegistryValidationException>(() => SpriteAssetRegistry.Create(new[] { outside }));
    }

    [Fact]
    public void Registry_ValidatesSourceAliasesShareCanonicalPathAndDimensions()
    {
        var canonical = CreateSprite("items/canonical") with { Path = "sprites/items/shared.png" };
        var alias = CreateSprite("projectiles/alias") with
        {
            Path = "sprites/items/shared.png",
            SourceAliasOf = canonical.Id
        };
        var wrongPath = alias with { Id = "projectiles/wrong", Path = "sprites/projectiles/other.png" };

        var registry = SpriteAssetRegistry.Create(new[] { alias, canonical });

        Assert.Equal(canonical.Id, registry.GetById(alias.Id).SourceAliasOf);
        Assert.Throws<RegistryValidationException>(() => SpriteAssetRegistry.Create(new[] { canonical, wrongPath }));
    }

    [Fact]
    public void GenerationBriefLoader_MergesGlobalStyleAndValidatesRegistry()
    {
        const string json = """
        {
          "globalStyle": "crisp pixel art",
          "globalNegativePrompt": "blur",
          "globalRequirements": ["transparent background"],
          "briefs": [
            {
              "spriteId": "items/copper_pickaxe",
              "outputPath": "sprites/tools/copper_pickaxe.png",
              "width": 16,
              "height": 16,
              "subject": "copper pickaxe",
              "prompt": "diagonal tool",
              "negativePrompt": "text",
              "tags": ["tool", "pickaxe"]
            }
          ]
        }
        """;

        var brief = Assert.Single(new SpriteGenerationBriefJsonLoader().LoadBriefsFromJson(json));
        var registry = SpriteGenerationBriefRegistry.Create(new[] { brief });

        Assert.Contains("crisp pixel art", brief.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("diagonal tool", brief.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blur", brief.NegativePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("transparent background", brief.Requirements);
        Assert.True(registry.TryGetBySpriteId("items/copper_pickaxe", out _));
    }

    [Fact]
    public void RepositorySpriteGenerationBriefs_CoverEverySpriteAsset()
    {
        var dataRoot = FindRepositoryGameData();
        var sprites = new SpriteAssetJsonLoader().LoadDefinitionsFromDirectory(Path.Combine(dataRoot, "assets"));
        var briefs = SpriteGenerationBriefRegistry.Create(
            new SpriteGenerationBriefJsonLoader().LoadBriefsFromDirectory(Path.Combine(dataRoot, "asset_briefs")));

        var missing = sprites
            .Where(sprite => !briefs.TryGetBySpriteId(sprite.Id, out _))
            .Select(sprite => sprite.Id)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void RepositorySpriteAudit_RecognizesGeneratedStarterAssets()
    {
        var dataRoot = FindRepositoryGameData();
        var sprites = SpriteAssetRegistry.Create(new SpriteAssetJsonLoader().LoadDefinitionsFromDirectory(Path.Combine(dataRoot, "assets")));
        var briefs = SpriteGenerationBriefRegistry.Create(
            new SpriteGenerationBriefJsonLoader().LoadBriefsFromDirectory(Path.Combine(dataRoot, "asset_briefs")));

        var report = new SpriteAssetAuditService().Audit(dataRoot, sprites, briefs);
        var starterSpriteIds = new[]
        {
            "tiles/dirt",
            "tiles/grass",
            "tiles/stone",
            "tiles/copper_ore",
            "tiles/iron_ore",
            "items/copper_pickaxe",
            "items/dirt_block",
            "items/stone_block",
            "items/parsnip_seeds",
            "tiles/wood",
            "tiles/leaves",
            "tiles/workbench",
            "items/copper_coin",
            "items/copper_ore",
            "items/iron_ore",
            "items/wood",
            "items/gel",
            "items/iron_pickaxe",
            "items/wooden_sword",
            "items/copper_sword",
            "items/iron_sword",
            "items/wooden_bow",
            "items/wooden_arrow",
            "projectiles/wooden_arrow",
            "entities/slime"
        };

        Assert.False(report.HasErrors, string.Join(Environment.NewLine, report.Issues.Select(issue => issue.Message)));
        foreach (var spriteId in starterSpriteIds)
        {
            var entry = Assert.Single(report.Entries, entry => entry.SpriteId == spriteId);
            Assert.Equal(SpriteAssetFileStatus.Present, entry.FileStatus);
            Assert.Equal(entry.DeclaredWidth, entry.ActualWidth);
            Assert.Equal(entry.DeclaredHeight, entry.ActualHeight);
            Assert.True(entry.HasGenerationBrief);
        }
    }

    [Fact]
    public void AssetAudit_ReportsPresentFilesAndMatchingBriefs()
    {
        using var temp = new TempAssetRoot();
        WritePngHeader(Path.Combine(temp.Root, "sprites", "tools", "pickaxe.png"), width: 16, height: 16);
        var assets = SpriteAssetRegistry.Create(new[]
        {
            new SpriteAssetDefinition
            {
                Id = "items/pickaxe",
                Path = "sprites/tools/pickaxe.png",
                Category = SpriteAssetCategory.Tool,
                Width = 16,
                Height = 16
            }
        });
        var briefs = SpriteGenerationBriefRegistry.Create(new[]
        {
            new SpriteGenerationBrief
            {
                SpriteId = "items/pickaxe",
                OutputPath = "sprites/tools/pickaxe.png",
                Width = 16,
                Height = 16
            }
        });

        var report = new SpriteAssetAuditService().Audit(temp.Root, assets, briefs);

        var entry = Assert.Single(report.Entries);
        Assert.Equal(SpriteAssetFileStatus.Present, entry.FileStatus);
        Assert.Equal(16, entry.ActualWidth);
        Assert.Equal(16, entry.ActualHeight);
        Assert.True(entry.HasGenerationBrief);
        Assert.False(report.HasErrors);
        Assert.Empty(report.Issues);
    }

    [Fact]
    public void AssetAudit_ReportsMissingFilesAndBriefProblems()
    {
        using var temp = new TempAssetRoot();
        var assets = SpriteAssetRegistry.Create(new[]
        {
            new SpriteAssetDefinition
            {
                Id = "items/pickaxe",
                Path = "sprites/tools/pickaxe.png",
                Category = SpriteAssetCategory.Tool,
                Width = 16,
                Height = 16
            }
        });
        var briefs = SpriteGenerationBriefRegistry.Create(new[]
        {
            new SpriteGenerationBrief
            {
                SpriteId = "items/pickaxe",
                OutputPath = "sprites/tools/wrong.png",
                Width = 32,
                Height = 16
            }
        });

        var report = new SpriteAssetAuditService().Audit(
            temp.Root,
            assets,
            briefs,
            new SpriteAssetAuditOptions { TreatMissingFilesAsErrors = true });

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Code == SpriteAssetAuditCodes.MissingFile);
        Assert.Contains(report.Issues, issue => issue.Code == SpriteAssetAuditCodes.BriefPathMismatch);
        Assert.Contains(report.Issues, issue => issue.Code == SpriteAssetAuditCodes.BriefSizeMismatch);
    }

    [Fact]
    public void AssetAudit_ReportsPngDimensionMismatchAndIncompleteAutotiles()
    {
        using var temp = new TempAssetRoot();
        WritePngHeader(Path.Combine(temp.Root, "sprites", "world", "tiles", "dirt.png"), width: 16, height: 16);
        var assets = SpriteAssetRegistry.Create(new[]
        {
            new SpriteAssetDefinition
            {
                Id = "tiles/dirt",
                Path = "sprites/world/tiles/dirt.png",
                Category = SpriteAssetCategory.Tile,
                Width = 256,
                Height = 16,
                Tags = new[] { "autotile" },
                Frames = new[]
                {
                    new SpriteFrameDefinition
                    {
                        Id = "mask_0",
                        X = 0,
                        Y = 0,
                        Width = 16,
                        Height = 16,
                        AutoTileMask = 0
                    }
                }
            }
        });

        var report = new SpriteAssetAuditService().Audit(
            temp.Root,
            assets,
            generationBriefs: null,
            new SpriteAssetAuditOptions { RequireGenerationBriefs = false });

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, issue => issue.Code == SpriteAssetAuditCodes.DimensionMismatch);
        Assert.Contains(report.Issues, issue => issue.Code == SpriteAssetAuditCodes.IncompleteAutoTileMasks);
    }

    private static SpriteAssetDefinition CreateSprite(string id)
    {
        return new SpriteAssetDefinition
        {
            Id = id,
            Path = $"sprites/{id}.png",
            Category = SpriteAssetCategory.Item,
            Width = 16,
            Height = 16
        };
    }

    private static void WritePngHeader(string path, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = new byte[24];
        var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        Array.Copy(signature, bytes, signature.Length);
        bytes[8] = 0;
        bytes[9] = 0;
        bytes[10] = 0;
        bytes[11] = 13;
        bytes[12] = (byte)'I';
        bytes[13] = (byte)'H';
        bytes[14] = (byte)'D';
        bytes[15] = (byte)'R';
        WriteBigEndian(bytes, 16, width);
        WriteBigEndian(bytes, 20, height);
        File.WriteAllBytes(path, bytes);
    }

    private static void WriteBigEndian(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)((value >> 24) & 0xff);
        bytes[offset + 1] = (byte)((value >> 16) & 0xff);
        bytes[offset + 2] = (byte)((value >> 8) & 0xff);
        bytes[offset + 3] = (byte)(value & 0xff);
    }

    private static string FindRepositoryGameData()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (File.Exists(Path.Combine(directory.FullName, "YjsE.sln")) &&
                Directory.Exists(Path.Combine(candidate, "assets")) &&
                Directory.Exists(Path.Combine(candidate, "asset_briefs")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }

    private sealed class TempAssetRoot : IDisposable
    {
        public TempAssetRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), "yjse-asset-audit-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
