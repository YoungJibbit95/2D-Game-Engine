namespace Game.Core.World.Generation;

public sealed record WorldGenerationQualityRules(
    float MinSolidRatio = 0.25f,
    float MaxSolidRatio = 0.85f,
    float MinAirRatio = 0.10f,
    int MinSurfaceVariance = 3,
    int MinLiquidTiles = 0);
