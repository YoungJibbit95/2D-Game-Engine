namespace Game.Client.Rendering.Atlas;

public readonly record struct TileAtlasTelemetry(
    int SourceFrameCount,
    int SourceTextureCount,
    int PageCount,
    int TextureBucketCount,
    int TextureBucketsSaved,
    long EstimatedPageBytes);
