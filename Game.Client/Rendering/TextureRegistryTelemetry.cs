namespace Game.Client.Rendering;

public readonly record struct TextureRegistryTelemetry(
    int ResourceCount,
    int FrameCount,
    int PlaceholderResourceCount,
    int InvalidResourceCount,
    long FileLoadCount,
    double TotalResourceLoadMilliseconds,
    long TotalResourceLoadAllocatedBytes,
    long EstimatedDecodedTextureBytes);
