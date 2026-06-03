namespace Game.Core.Projects;

public sealed record GameProjectPaths(
    string ProjectRoot,
    string ManifestPath,
    string ContentRoot,
    string ModsRoot,
    string SavesRootName,
    bool HasManifest);
