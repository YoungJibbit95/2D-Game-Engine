using Game.Core.Data;
using Game.Core.Projects;
using Xunit;

namespace Game.Tests.ProjectTests;

public sealed class GameProjectManifestTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yjse-project-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ManifestLoader_ReadsProjectMetadata()
    {
        var manifest = new GameProjectManifestLoader().LoadFromJson("""
        {
          "schemaVersion": 1,
          "id": "my_game",
          "displayName": "My Game",
          "version": "0.2.0",
          "engineVersion": "1.0.0",
          "contentRoot": "Content",
          "modsRoot": "UserMods",
          "savesRootName": "Profiles",
          "defaultWorldProfileId": "medium",
          "startupMapId": "farm",
          "tags": ["RPG", "Farm"]
        }
        """);

        Assert.Equal("my_game", manifest.Id);
        Assert.Equal("My Game", manifest.DisplayName);
        Assert.Equal("Content", manifest.ContentRoot);
        Assert.Equal("UserMods", manifest.ModsRoot);
        Assert.Equal("Profiles", manifest.SavesRootName);
        Assert.Equal("medium", manifest.DefaultWorldProfileId);
        Assert.Equal("farm", manifest.StartupMapId);
        Assert.True(manifest.HasTag("rpg"));
    }

    [Fact]
    public void ManifestLoader_RejectsMissingRequiredFields()
    {
        Assert.Throws<RegistryValidationException>(() => new GameProjectManifestLoader().LoadFromJson("""
        {
          "id": "",
          "displayName": "Broken"
        }
        """));
    }

    [Fact]
    public void Resolver_ResolvesGameRootManifestToContentAndMods()
    {
        WriteManifest();
        Directory.CreateDirectory(Path.Combine(_root, "Content"));

        var paths = new GameProjectResolver().Resolve(_root);

        Assert.True(paths.HasManifest);
        Assert.Equal(Path.GetFullPath(_root), paths.ProjectRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(_root, "Content")), paths.ContentRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(_root, "Mods")), paths.ModsRoot);
        Assert.Equal("Profiles", paths.SavesRootName);
    }

    [Fact]
    public void Resolver_ResolvesContentRootBackToParentManifest()
    {
        WriteManifest();
        var contentRoot = Path.Combine(_root, "Content");
        Directory.CreateDirectory(contentRoot);

        var paths = new GameProjectResolver().Resolve(contentRoot);

        Assert.True(paths.HasManifest);
        Assert.Equal(Path.GetFullPath(_root), paths.ProjectRoot);
        Assert.Equal(Path.GetFullPath(contentRoot), paths.ContentRoot);
    }

    [Fact]
    public void Resolver_CreatesFallbackProjectForLooseContentRoot()
    {
        var contentRoot = Path.Combine(_root, "LooseContent");
        Directory.CreateDirectory(Path.Combine(contentRoot, "items"));

        var paths = new GameProjectResolver().Resolve(contentRoot);
        var manifest = new GameProjectResolver().LoadManifest(paths);

        Assert.False(paths.HasManifest);
        Assert.Equal(Path.GetFullPath(contentRoot), paths.ProjectRoot);
        Assert.Equal(Path.GetFullPath(contentRoot), paths.ContentRoot);
        Assert.Equal("LooseContent", manifest.Id);
    }

    [Fact]
    public void ProjectContentLoader_LoadsBaseContentAndModOverrides()
    {
        WriteManifest();
        var contentItems = Path.Combine(_root, "Content", "items");
        Directory.CreateDirectory(contentItems);
        File.WriteAllText(Path.Combine(contentItems, "starter_seed.json"), """
        {
          "id": "starter_seed",
          "displayName": "Starter Seed",
          "type": "Material",
          "texture": "items/starter_seed",
          "maxStack": 12
        }
        """);

        var modItems = Path.Combine(_root, "Mods", "StackMod", "items");
        Directory.CreateDirectory(modItems);
        File.WriteAllText(Path.Combine(_root, "Mods", "StackMod", "mod.json"), """
        {
          "id": "stack_mod",
          "displayName": "Stack Mod"
        }
        """);
        File.WriteAllText(Path.Combine(modItems, "starter_seed.json"), """
        {
          "id": "starter_seed",
          "displayName": "Starter Seed",
          "type": "Material",
          "texture": "items/starter_seed",
          "maxStack": 99
        }
        """);

        var result = new GameProjectContentLoader().Load(_root);

        Assert.Equal("separate_game", result.Manifest.Id);
        Assert.Equal(99, result.Content.Database.Items.GetById("starter_seed").MaxStack);
        Assert.Contains(result.Content.Report.Overrides, item => item.ContentKind == "item" && item.ContentId == "starter_seed");
        Assert.False(result.Content.Report.HasErrors);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void WriteManifest()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, GameProjectManifestLoader.ManifestFileName), """
        {
          "schemaVersion": 1,
          "id": "separate_game",
          "displayName": "Separate Game",
          "version": "0.1.0",
          "contentRoot": "Content",
          "modsRoot": "Mods",
          "savesRootName": "Profiles",
          "defaultWorldProfileId": "small",
          "tags": ["external"]
        }
        """);
    }
}
