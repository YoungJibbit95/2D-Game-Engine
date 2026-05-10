namespace Game.Core.Farming;

public sealed record FarmDailyTickResult(
    int AdvancedCrops,
    int NewlyMatureCrops,
    int ClearedWateredPlots,
    int WitheredCrops);
