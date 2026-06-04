namespace Game.Core.Assets.Audit;

public sealed record SpriteAssetAuditIssue(
    SpriteAssetAuditSeverity Severity,
    string SpriteId,
    string Code,
    string Message);
