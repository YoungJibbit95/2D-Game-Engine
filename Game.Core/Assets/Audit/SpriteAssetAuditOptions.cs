namespace Game.Core.Assets.Audit;

public sealed record SpriteAssetAuditOptions
{
    public bool TreatMissingFilesAsErrors { get; init; }

    public bool RequireGenerationBriefs { get; init; } = true;

    public bool RequireCompleteAutoTileMasks { get; init; } = true;

    public bool SkipFallbackSprite { get; init; } = true;
}
