using Game.Core.World;
using Game.Core.Tiles;

namespace Game.Client.Rendering;

public readonly record struct ChunkRenderCommand(
    int LocalX,
    int LocalY,
    TileInstance Tile,
    AutoTileMask AutoTileMask,
    byte VisualVariant = 0,
    TileVisualTransform VisualTransform = TileVisualTransform.None);

[Flags]
public enum TileVisualTransform : byte
{
    None = 0,
    FlipHorizontal = 1
}
