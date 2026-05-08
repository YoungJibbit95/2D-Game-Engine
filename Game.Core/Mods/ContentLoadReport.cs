namespace Game.Core.Mods;

public sealed class ContentLoadReport
{
    private readonly List<ContentOverride> _overrides = new();
    private readonly List<ContentLoadIssue> _issues = new();

    public IReadOnlyList<ContentOverride> Overrides => _overrides;

    public IReadOnlyList<ContentLoadIssue> Issues => _issues;

    public bool HasErrors => _issues.Any(issue => issue.Severity == ContentIssueSeverity.Error);

    public void AddOverride(string contentKind, string contentId, string previousPackId, string newPackId)
    {
        _overrides.Add(new ContentOverride(contentKind, contentId, previousPackId, newPackId));
    }

    public void AddIssue(ContentIssueSeverity severity, string packId, string contentKind, string contentId, string message)
    {
        _issues.Add(new ContentLoadIssue(severity, packId, contentKind, contentId, message));
    }
}
