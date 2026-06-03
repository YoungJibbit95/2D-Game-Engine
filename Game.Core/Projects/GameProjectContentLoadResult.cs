using Game.Core.Data;
using Game.Core.Mods;

namespace Game.Core.Projects;

public sealed record GameProjectContentLoadResult(
    GameProjectManifest Manifest,
    GameProjectPaths Paths,
    GameContentLoadResult Content);
