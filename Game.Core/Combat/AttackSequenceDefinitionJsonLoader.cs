using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Combat;

public sealed class AttackSequenceDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AttackSequenceRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return AttackSequenceRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<AttackSequenceDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<AttackSequenceDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public AttackSequenceDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public AttackSequenceDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static AttackSequenceDefinition LoadDefinition(Stream stream, string source)
    {
        var dto = JsonSerializer.Deserialize<AttackSequenceDefinitionDto>(stream, Options);
        if (dto is null)
        {
            throw new JsonException($"Attack sequence definition was empty: {source}");
        }

        var definition = dto.ToDefinition();
        definition.Validate();
        return definition;
    }

    private sealed record AttackSequenceDefinitionDto
    {
        public string? Id { get; init; }

        public List<AttackComboStepDefinitionDto> Steps { get; init; } = new();

        public int InputBufferTicks { get; init; } = 6;

        public int CommandCapacity { get; init; } = 16;

        public int EventCapacity { get; init; } = 64;

        public AttackSequenceDefinition ToDefinition()
        {
            return new AttackSequenceDefinition
            {
                Id = Id ?? string.Empty,
                Steps = Steps.Select(step => step.ToDefinition()).ToArray(),
                InputBufferTicks = InputBufferTicks,
                CommandCapacity = CommandCapacity,
                EventCapacity = EventCapacity
            };
        }
    }

    private sealed record AttackComboStepDefinitionDto
    {
        public string? Id { get; init; }

        public AttackTimelineDefinitionDto? Timeline { get; init; }

        public string? NextStepId { get; init; }

        public AttackResourceCost Cost { get; init; }

        public int MaxTargetsPerSwing { get; init; } = 32;

        public List<AttackCancelRuleDefinition> CancelRules { get; init; } = new();

        public List<SweptMeleeShapeDefinition> MeleeShapes { get; init; } = new();

        public AttackComboStepDefinition ToDefinition()
        {
            return new AttackComboStepDefinition
            {
                Id = Id ?? string.Empty,
                Timeline = (Timeline ?? new AttackTimelineDefinitionDto()).ToDefinition(),
                NextStepId = NextStepId,
                Cost = Cost,
                MaxTargetsPerSwing = MaxTargetsPerSwing,
                CancelRules = CancelRules.ToArray(),
                MeleeShapes = MeleeShapes.ToArray()
            };
        }
    }

    private sealed record AttackTimelineDefinitionDto
    {
        public int StartupTicks { get; init; }

        public int ActiveTicks { get; init; }

        public int RecoveryTicks { get; init; }

        public int CooldownTicks { get; init; }

        public AttackPhaseWindow? ComboWindow { get; init; }

        public AttackTimelineDefinition ToDefinition()
        {
            return AttackTimelineDefinition.Create(
                StartupTicks,
                ActiveTicks,
                RecoveryTicks,
                CooldownTicks,
                ComboWindow);
        }
    }
}
