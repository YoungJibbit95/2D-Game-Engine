namespace Game.Core.World.Generation;

public sealed record WorldGenerationRequest
{
    public string ProfileId { get; init; } = WorldGenerationProfile.Small.Id;

    public int Seed { get; init; }

    public int? WidthTiles { get; init; }

    public int? HeightTiles { get; init; }

    public WorldGenerationQualityRules? QualityRules { get; init; }
}
