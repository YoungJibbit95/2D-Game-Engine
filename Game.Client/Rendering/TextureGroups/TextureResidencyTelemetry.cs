namespace Game.Client.Rendering.TextureGroups;

public readonly record struct TextureResidencyTelemetry(
    int ResidentResourceCount,
    long ResidentDecodedBytes,
    long EvictedResourceCount,
    long EvictedDecodedBytes,
    int PinnedResourceCount,
    long BudgetRejectedResourceCount);
