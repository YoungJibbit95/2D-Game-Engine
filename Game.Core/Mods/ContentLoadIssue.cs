namespace Game.Core.Mods;

public sealed record ContentLoadIssue(
    ContentIssueSeverity Severity,
    string PackId,
    string ContentKind,
    string ContentId,
    string Message);
