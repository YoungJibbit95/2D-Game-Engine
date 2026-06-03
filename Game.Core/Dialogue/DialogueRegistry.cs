using Game.Core.Data;

namespace Game.Core.Dialogue;

public sealed class DialogueRegistry
{
    private readonly Dictionary<string, DialogueDefinition> _byId;

    private DialogueRegistry(IEnumerable<DialogueDefinition> definitions)
    {
        _byId = new Dictionary<string, DialogueDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<DialogueDefinition> Definitions => _byId.Values;

    public static DialogueRegistry Create(IEnumerable<DialogueDefinition> definitions)
    {
        return new DialogueRegistry(definitions);
    }

    public bool TryGetById(string id, out DialogueDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    public DialogueDefinition GetById(string id)
    {
        return TryGetById(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Dialogue definition '{id}' was not registered.");
    }

    private void AddValidated(DialogueDefinition definition)
    {
        Validate(definition);
        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate dialogue id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(DialogueDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));
        RequireText(definition.StartNodeId, nameof(definition.StartNodeId));

        if (definition.Nodes.Count == 0)
        {
            throw new RegistryValidationException($"Dialogue '{definition.Id}' must contain at least one node.");
        }

        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in definition.Nodes)
        {
            RequireText(node.Id, nameof(node.Id));
            RequireText(node.Text, nameof(node.Text));

            if (!nodeIds.Add(node.Id))
            {
                throw new RegistryValidationException($"Dialogue '{definition.Id}' has duplicate node id '{node.Id}'.");
            }

            foreach (var option in node.Options)
            {
                RequireText(option.Text, nameof(option.Text));
            }
        }

        if (!nodeIds.Contains(definition.StartNodeId))
        {
            throw new RegistryValidationException($"Dialogue '{definition.Id}' start node '{definition.StartNodeId}' does not exist.");
        }

        foreach (var node in definition.Nodes)
        {
            ValidateNodeReference(definition.Id, node.Id, "next", node.NextNodeId, nodeIds);

            foreach (var option in node.Options)
            {
                ValidateNodeReference(definition.Id, node.Id, "option target", option.TargetNodeId, nodeIds);
            }
        }
    }

    private static void ValidateNodeReference(
        string dialogueId,
        string nodeId,
        string referenceKind,
        string? targetNodeId,
        HashSet<string> nodeIds)
    {
        if (string.IsNullOrWhiteSpace(targetNodeId) || nodeIds.Contains(targetNodeId))
        {
            return;
        }

        throw new RegistryValidationException(
            $"Dialogue '{dialogueId}' node '{nodeId}' has missing {referenceKind} node '{targetNodeId}'.");
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Dialogue definition field '{name}' is required.");
        }
    }
}
