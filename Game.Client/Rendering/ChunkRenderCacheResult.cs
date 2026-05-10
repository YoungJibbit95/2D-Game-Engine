namespace Game.Client.Rendering;

public readonly record struct ChunkRenderCacheResult(
    IReadOnlyList<ChunkRenderCommand> Commands,
    bool Rebuilt);
