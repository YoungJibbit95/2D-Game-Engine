namespace Game.Core.Dialogue;

public sealed record DialogueOptionDefinition
{
    public required string Text { get; init; }

    public string? TargetNodeId { get; init; }

    public string? ActionId { get; init; }

    public bool EndsConversation { get; init; }

    public IReadOnlyDictionary<string, string> Conditions { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
