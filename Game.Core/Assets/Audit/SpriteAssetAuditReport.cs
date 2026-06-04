namespace Game.Core.Assets.Audit;

public sealed record SpriteAssetAuditReport(
    IReadOnlyList<SpriteAssetAuditEntry> Entries,
    IReadOnlyList<SpriteAssetAuditIssue> Issues)
{
    public int TotalSprites => Entries.Count;

    public int PresentFiles => Entries.Count(entry => entry.FileStatus == SpriteAssetFileStatus.Present);

    public int MissingFiles => Entries.Count(entry => entry.FileStatus == SpriteAssetFileStatus.Missing);

    public int UnreadableFiles => Entries.Count(entry => entry.FileStatus == SpriteAssetFileStatus.Unreadable);

    public int MissingGenerationBriefs => Entries.Count(entry => !entry.HasGenerationBrief);

    public int DimensionMismatches => Issues.Count(issue => issue.Code == SpriteAssetAuditCodes.DimensionMismatch);

    public bool HasErrors => Issues.Any(issue => issue.Severity == SpriteAssetAuditSeverity.Error);

    public IReadOnlyList<SpriteAssetAuditEntry> MissingFileEntries()
    {
        return Entries
            .Where(entry => entry.FileStatus == SpriteAssetFileStatus.Missing)
            .ToArray();
    }
}
