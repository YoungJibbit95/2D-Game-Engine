namespace Game.Core.Dialogue;

public sealed record DialogueNodeDefinition
{
    public required string Id { get; init; }

    public string? SpeakerId { get; init; }

    public required string Text { get; init; }

    public string? NextNodeId { get; init; }

    public bool EndsConversation { get; init; }

    public IReadOnlyList<DialogueOptionDefinition> Options { get; init; } =
        Array.Empty<DialogueOptionDefinition>();
}
