namespace Game.Core.Projects;

public sealed class GameProjectResolver
{
    private readonly GameProjectManifestLoader _manifestLoader;

    public GameProjectResolver(GameProjectManifestLoader? manifestLoader = null)
    {
        _manifestLoader = manifestLoader ?? new GameProjectManifestLoader();
    }

    public GameProjectPaths Resolve(string rootOrContentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootOrContentPath);

        var fullPath = Path.GetFullPath(rootOrContentPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Game project path does not exist: {fullPath}");
        }

        var manifestPath = Path.Combine(fullPath, GameProjectManifestLoader.ManifestFileName);
        if (File.Exists(manifestPath))
        {
            var manifest = _manifestLoader.LoadFromFile(manifestPath);
            return CreatePaths(fullPath, manifestPath, manifest, hasManifest: true);
        }

        var parent = Directory.GetParent(fullPath);
        if (parent is not null)
        {
            manifestPath = Path.Combine(parent.FullName, GameProjectManifestLoader.ManifestFileName);
            if (File.Exists(manifestPath))
            {
                var manifest = _manifestLoader.LoadFromFile(manifestPath);
                var manifestContentRoot = Path.GetFullPath(Path.Combine(parent.FullName, manifest.ContentRoot));
                if (PathsEqual(manifestContentRoot, fullPath))
                {
                    return CreatePaths(parent.FullName, manifestPath, manifest, hasManifest: true);
                }
            }
        }

        var fallback = _manifestLoader.CreateFallbackForContentRoot(fullPath);
        return CreatePaths(fullPath, Path.Combine(fullPath, GameProjectManifestLoader.ManifestFileName), fallback, hasManifest: false);
    }

    public GameProjectPaths ResolveFirstExisting(IEnumerable<string> candidateRoots)
    {
        ArgumentNullException.ThrowIfNull(candidateRoots);

        foreach (var candidate in candidateRoots.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(fullPath, GameProjectManifestLoader.ManifestFileName)) ||
                LooksLikeContentRoot(fullPath))
            {
                return Resolve(fullPath);
            }
        }

        throw new DirectoryNotFoundException("Could not locate a YjsE game project manifest or content root.");
    }

    public GameProjectManifest LoadManifest(GameProjectPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return paths.HasManifest
            ? _manifestLoader.LoadFromFile(paths.ManifestPath)
            : _manifestLoader.CreateFallbackForContentRoot(paths.ContentRoot);
    }

    private static GameProjectPaths CreatePaths(string projectRoot, string manifestPath, GameProjectManifest manifest, bool hasManifest)
    {
        var contentRoot = Path.GetFullPath(Path.Combine(projectRoot, manifest.ContentRoot));
        var modsRoot = Path.GetFullPath(Path.Combine(projectRoot, manifest.ModsRoot));
        return new GameProjectPaths(
            Path.GetFullPath(projectRoot),
            Path.GetFullPath(manifestPath),
            contentRoot,
            modsRoot,
            manifest.SavesRootName,
            hasManifest);
    }

    private static bool LooksLikeContentRoot(string path)
    {
        var expectedDirectories = new[] { "items", "tiles", "maps", "recipes", "worldgen", "assets" };
        return expectedDirectories.Any(directory => Directory.Exists(Path.Combine(path, directory)));
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}
