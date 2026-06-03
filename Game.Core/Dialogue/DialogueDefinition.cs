using Game.Core.Data;

namespace Game.Core.Dialogue;

public sealed record DialogueDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string StartNodeId { get; init; }

    public IReadOnlyList<DialogueNodeDefinition> Nodes { get; init; } =
        Array.Empty<DialogueNodeDefinition>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public bool TryGetNode(string nodeId, out DialogueNodeDefinition node)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        node = Nodes.FirstOrDefault(item => string.Equals(item.Id, nodeId, StringComparison.OrdinalIgnoreCase))!;
        return node is not null;
    }

    public DialogueNodeDefinition GetNode(string nodeId)
    {
        return TryGetNode(nodeId, out var node)
            ? node
            : throw new KeyNotFoundException($"Dialogue '{Id}' does not contain node '{nodeId}'.");
    }

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }
}
