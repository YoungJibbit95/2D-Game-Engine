namespace Game.Core.Settings;

public sealed record SettingsValidationIssue(string Path, string Message);
