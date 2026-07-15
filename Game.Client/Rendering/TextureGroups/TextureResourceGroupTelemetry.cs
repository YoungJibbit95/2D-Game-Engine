namespace Game.Client.Rendering.TextureGroups;

public readonly record struct TextureResourceGroupTelemetry(
    TextureResourceGroup Group,
    long DecodedByteBudget,
    int ResidentResourceCount,
    long ResidentDecodedBytes,
    long EvictedResourceCount,
    long EvictedDecodedBytes,
    int PinnedResourceCount,
    long BudgetRejectedResourceCount);
