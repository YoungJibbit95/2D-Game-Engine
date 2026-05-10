using Game.Core.World;
using Game.Core.Tiles;

namespace Game.Client.Rendering;

public readonly record struct ChunkRenderCommand(
    int LocalX,
    int LocalY,
    TileInstance Tile,
    AutoTileMask AutoTileMask);
