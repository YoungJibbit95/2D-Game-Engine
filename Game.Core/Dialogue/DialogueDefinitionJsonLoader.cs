using Game.Core.Data;
using System.Text.Json;

namespace Game.Core.Dialogue;

public sealed class DialogueDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public DialogueRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return DialogueRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<DialogueDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<DialogueDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public DialogueDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return LoadDefinitionFromJson(File.ReadAllText(filePath), filePath);
    }

    public DialogueDefinition LoadDefinitionFromJson(string json)
    {
        return LoadDefinitionFromJson(json, "inline json");
    }

    private static DialogueDefinition LoadDefinitionFromJson(string json, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var dto = JsonSerializer.Deserialize<DialogueDefinitionDto>(json, Options);
        if (dto is null)
        {
            throw new JsonException($"Dialogue definition was empty: {source}");
        }

        return dto.ToDefinition();
    }

    private sealed record DialogueDefinitionDto
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public string? StartNodeId { get; init; }

        public DialogueNodeDto[] Nodes { get; init; } = Array.Empty<DialogueNodeDto>();

        public string[] Tags { get; init; } = Array.Empty<string>();

        public DialogueDefinition ToDefinition()
        {
            return new DialogueDefinition
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                StartNodeId = StartNodeId ?? string.Empty,
                Nodes = Nodes.Select(node => node.ToDefinition()).ToArray(),
                Tags = DefinitionTags.Normalize(Tags)
            };
        }
    }

    private sealed record DialogueNodeDto
    {
        public string? Id { get; init; }

        public string? SpeakerId { get; init; }

        public string? Text { get; init; }

        public string? NextNodeId { get; init; }

        public bool EndsConversation { get; init; }

        public DialogueOptionDto[] Options { get; init; } = Array.Empty<DialogueOptionDto>();

        public DialogueNodeDefinition ToDefinition()
        {
            return new DialogueNodeDefinition
            {
                Id = Id ?? string.Empty,
                SpeakerId = SpeakerId,
                Text = Text ?? string.Empty,
                NextNodeId = NextNodeId,
                EndsConversation = EndsConversation,
                Options = Options.Select(option => option.ToDefinition()).ToArray()
            };
        }
    }

    private sealed record DialogueOptionDto
    {
        public string? Text { get; init; }

        public string? TargetNodeId { get; init; }

        public string? ActionId { get; init; }

        public bool EndsConversation { get; init; }

        public Dictionary<string, string> Conditions { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);

        public DialogueOptionDefinition ToDefinition()
        {
            return new DialogueOptionDefinition
            {
                Text = Text ?? string.Empty,
                TargetNodeId = TargetNodeId,
                ActionId = ActionId,
                EndsConversation = EndsConversation,
                Conditions = new Dictionary<string, string>(Conditions, StringComparer.OrdinalIgnoreCase)
            };
        }
    }
}
