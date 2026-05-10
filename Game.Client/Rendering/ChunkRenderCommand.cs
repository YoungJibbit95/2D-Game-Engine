using Game.Core.World;

namespace Game.Client.Rendering;

public readonly record struct ChunkRenderCommand(
    int LocalX,
    int LocalY,
    TileInstance Tile);
