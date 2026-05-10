using Game.Core.World;

namespace Game.Core.Maps;

public sealed record MapWarpTarget(
    string TargetMapId,
    string TargetSpawnId,
    TilePos SourceTile,
    string ObjectId);
