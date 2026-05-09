namespace Game.Core.World.Generation;

public sealed record WorldGenerationBuildResult(
    WorldGenerationProfile Profile,
    WorldGenerationResult Generation,
    WorldGenerationAnalysis Analysis,
    WorldGenerationQualityReport Quality);
