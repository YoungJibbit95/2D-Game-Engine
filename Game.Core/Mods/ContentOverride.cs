namespace Game.Core.Mods;

public sealed record ContentOverride(
    string ContentKind,
    string ContentId,
    string PreviousPackId,
    string NewPackId);
