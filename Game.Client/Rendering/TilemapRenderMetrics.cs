namespace Game.Client.Rendering;

public readonly record struct TilemapRenderMetrics(
    int VisibleChunks,
    int CachedChunks,
    int RebuiltChunks,
    int EvictedChunks,
    int RenderedTileCommands,
    int RenderedLiquidCommands);
