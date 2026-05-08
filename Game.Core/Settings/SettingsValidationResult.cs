namespace Game.Core.Settings;

public sealed class SettingsValidationResult
{
    private readonly List<SettingsValidationIssue> _issues = new();

    public IReadOnlyList<SettingsValidationIssue> Issues => _issues;

    public bool IsValid => _issues.Count == 0;

    public void Add(string path, string message)
    {
        _issues.Add(new SettingsValidationIssue(path, message));
    }
}
