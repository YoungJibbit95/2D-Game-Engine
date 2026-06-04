using Game.Core.Data;
using Game.Core.Mods;

namespace Game.Core.Projects;

public sealed class GameProjectContentLoader
{
    private readonly GameProjectResolver _resolver;
    private readonly GameContentLoader _contentLoader;

    public GameProjectContentLoader(GameProjectResolver? resolver = null, GameContentLoader? contentLoader = null)
    {
        _resolver = resolver ?? new GameProjectResolver();
        _contentLoader = contentLoader ?? new GameContentLoader();
    }

    public GameProjectContentLoadResult Load(string projectRootOrContentRoot)
    {
        var paths = _resolver.Resolve(projectRootOrContentRoot);
        var manifest = _resolver.LoadManifest(paths);
        var content = _contentLoader.LoadWithMods(paths.ContentRoot, paths.ModsRoot);
        ValidateManifestReferences(manifest, content);
        return new GameProjectContentLoadResult(manifest, paths, content);
    }

    private static void ValidateManifestReferences(GameProjectManifest manifest, GameContentLoadResult content)
    {
        if (!string.IsNullOrWhiteSpace(manifest.DefaultWorldProfileId) &&
            !content.Database.WorldGenerationProfiles.TryGetById(manifest.DefaultWorldProfileId, out _))
        {
            content.Report.AddIssue(
                ContentIssueSeverity.Error,
                "manifest",
                "game_project",
                manifest.Id,
                $"Missing default world profile reference '{manifest.DefaultWorldProfileId}'.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.StartupMapId) &&
            !content.Database.Maps.TryGetById(manifest.StartupMapId, out _))
        {
            content.Report.AddIssue(
                ContentIssueSeverity.Error,
                "manifest",
                "game_project",
                manifest.Id,
                $"Missing startup map reference '{manifest.StartupMapId}'.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.StartupDefinitionId) &&
            !content.Database.GameStartups.TryGetById(manifest.StartupDefinitionId, out _))
        {
            content.Report.AddIssue(
                ContentIssueSeverity.Error,
                "manifest",
                "game_project",
                manifest.Id,
                $"Missing startup definition reference '{manifest.StartupDefinitionId}'.");
        }
    }
}
