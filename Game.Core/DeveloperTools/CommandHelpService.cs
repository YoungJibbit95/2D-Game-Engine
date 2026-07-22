using Game.Core.Commands;
using System.Globalization;
using System.Text;

namespace Game.Core.DeveloperTools;

public sealed class CommandHelpService
{
    private readonly CommandRegistry _registry;
    private readonly DeveloperCommandCatalog _catalog;

    public CommandHelpService(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _catalog = new DeveloperCommandCatalog(registry);
    }

    public string BuildOverview()
    {
        var builder = new StringBuilder();
        var currentCategory = CommandCategory.All;
        for (var index = 0; index < _catalog.Entries.Count; index++)
        {
            var entry = _catalog.Entries[index];
            if (entry.Category != currentCategory)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                currentCategory = entry.Category;
                builder.Append('[').Append(currentCategory).AppendLine("]");
            }

            builder.Append(entry.Signature)
                .Append(" - ")
                .Append(entry.Description)
                .Append(entry.RequestIntentType is null
                    ? " [immediate]"
                    : $" [request: {entry.RequestIntentType.Name}]")
                .AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    public bool TryBuildCommandHelp(string? name, out string help)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            help = string.Empty;
            return false;
        }

        var normalized = name.Trim();
        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized[1..].TrimStart();
        }

        if (normalized.Length == 0 || !_registry.TryGet(normalized, out var command))
        {
            help = string.Empty;
            return false;
        }

        var specification = _registry.GetSpecification(command);
        var builder = new StringBuilder()
            .Append(specification.Usage)
            .Append(" - ")
            .Append(specification.Description)
            .AppendLine()
            .Append("Category: ")
            .Append(specification.Category)
            .AppendLine()
            .Append("Dispatch: ")
            .Append(specification.RequestIntentType is null
                ? "immediate command"
                : $"typed runtime request ({specification.RequestIntentType.Name})");

        if (specification.Aliases.Count > 0)
        {
            builder.AppendLine().Append("Aliases: ");
            for (var index = 0; index < specification.Aliases.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                builder.Append('/').Append(specification.Aliases[index]);
            }
        }

        for (var index = 0; index < specification.Arguments.Count; index++)
        {
            var argument = specification.Arguments[index];
            builder.AppendLine()
                .Append(argument.IsRequired ? "Required " : "Optional ")
                .Append(argument.Name)
                .Append(" (")
                .Append(argument.Type)
                .Append("): ")
                .Append(argument.Description);

            if (argument.Minimum.HasValue || argument.Maximum.HasValue)
            {
                builder.Append(" Range ")
                    .Append(argument.Minimum?.ToString(CultureInfo.InvariantCulture) ?? "-inf")
                    .Append("..")
                    .Append(argument.Maximum?.ToString(CultureInfo.InvariantCulture) ?? "+inf")
                    .Append('.');
            }

            if (argument.Choices.Count > 0)
            {
                builder.Append(" Values: ");
                for (var choiceIndex = 0; choiceIndex < argument.Choices.Count; choiceIndex++)
                {
                    if (choiceIndex > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(argument.Choices[choiceIndex]);
                }

                builder.Append('.');
            }

            if (argument.SuggestionSource != CommandSuggestionSource.None)
            {
                builder.Append(" Autocomplete: ").Append(argument.SuggestionSource).Append('.');
            }
        }

        if (specification.Examples.Count > 0)
        {
            builder.AppendLine().Append("Examples: ");
            for (var index = 0; index < specification.Examples.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(specification.Examples[index]);
            }
        }

        help = builder.ToString();
        return true;
    }
}
