namespace Game.Core.Assets.Audit;

public sealed record SpriteAssetAuditEntry(
    string SpriteId,
    string ManifestPath,
    string ResolvedPath,
    SpriteAssetCategory Category,
    int DeclaredWidth,
    int DeclaredHeight,
    SpriteAssetFileStatus FileStatus,
    int? ActualWidth,
    int? ActualHeight,
    bool HasGenerationBrief,
    bool GenerationBriefPathMatches,
    bool GenerationBriefSizeMatches,
    bool HasCompleteAutoTileMasks);
