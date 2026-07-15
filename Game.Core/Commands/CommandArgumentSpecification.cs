namespace Game.Core.Commands;

public sealed record CommandArgumentSpecification
{
    public CommandArgumentSpecification(
        string name,
        CommandArgumentType type,
        bool isRequired = true,
        string description = "",
        IReadOnlyList<string>? choices = null,
        double? minimum = null,
        double? maximum = null,
        CommandSuggestionSource suggestionSource = CommandSuggestionSource.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (minimum > maximum)
        {
            throw new ArgumentException("Argument minimum must not exceed its maximum.", nameof(minimum));
        }

        Name = name;
        Type = type;
        IsRequired = isRequired;
        Description = description;
        Choices = choices ?? Array.Empty<string>();
        Minimum = minimum;
        Maximum = maximum;
        SuggestionSource = suggestionSource;

        if (type == CommandArgumentType.Choice && Choices.Count == 0)
        {
            throw new ArgumentException("Choice arguments require at least one choice.", nameof(choices));
        }
    }

    public string Name { get; }

    public CommandArgumentType Type { get; }

    public bool IsRequired { get; }

    public string Description { get; }

    public IReadOnlyList<string> Choices { get; }

    public double? Minimum { get; }

    public double? Maximum { get; }

    public CommandSuggestionSource SuggestionSource { get; }
}
