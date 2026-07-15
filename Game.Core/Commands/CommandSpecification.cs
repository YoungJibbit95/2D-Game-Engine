namespace Game.Core.Commands;

public sealed class CommandSpecification
{
    public CommandSpecification(
        string name,
        string description,
        IReadOnlyList<CommandArgumentSpecification>? arguments = null,
        IReadOnlyList<string>? aliases = null,
        IReadOnlyList<string>? examples = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Name = name;
        Description = description;
        Arguments = arguments ?? Array.Empty<CommandArgumentSpecification>();
        Aliases = aliases ?? Array.Empty<string>();
        Examples = examples ?? Array.Empty<string>();

        var optionalSeen = false;
        var argumentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var argument in Arguments)
        {
            ArgumentNullException.ThrowIfNull(argument);
            if (!argumentNames.Add(argument.Name))
            {
                throw new ArgumentException($"Duplicate argument name '{argument.Name}'.", nameof(arguments));
            }

            optionalSeen |= !argument.IsRequired;
            if (optionalSeen && argument.IsRequired)
            {
                throw new ArgumentException("Required arguments cannot follow optional arguments.", nameof(arguments));
            }
        }
    }

    public string Name { get; }

    public string Description { get; }

    public IReadOnlyList<CommandArgumentSpecification> Arguments { get; }

    public IReadOnlyList<string> Aliases { get; }

    public IReadOnlyList<string> Examples { get; }

    public string Usage => Arguments.Count == 0
        ? $"/{Name}"
        : $"/{Name} {string.Join(' ', Arguments.Select(FormatArgument))}";

    private static string FormatArgument(CommandArgumentSpecification argument)
    {
        return argument.IsRequired ? $"<{argument.Name}>" : $"[{argument.Name}]";
    }
}
