using Game.Core.Data;

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
        return new GameProjectContentLoadResult(manifest, paths, content);
    }
}
