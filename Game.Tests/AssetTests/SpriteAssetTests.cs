using Game.Core.Assets;
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

    private static string FindRepositoryGameData()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }
}
