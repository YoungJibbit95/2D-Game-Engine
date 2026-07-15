using Game.Core.Commands;
using System.Text;

namespace Game.Core.DeveloperTools;

public sealed class CommandHelpService
{
    private readonly CommandRegistry _registry;

    public CommandHelpService(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    public string BuildOverview()
    {
        var commands = _registry.Commands
            .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
            .Select(command => $"/{command.Name} - {command.Description}");
        return string.Join(Environment.NewLine, commands);
    }

    public bool TryBuildCommandHelp(string name, out string help)
    {
        if (!_registry.TryGet(name, out var command))
        {
            help = string.Empty;
            return false;
        }

        var specification = _registry.GetSpecification(command);
        var builder = new StringBuilder()
            .Append(specification.Usage)
            .Append(" - ")
            .Append(specification.Description);

        if (specification.Aliases.Count > 0)
        {
            builder.AppendLine().Append("Aliases: ")
                .Append(string.Join(", ", specification.Aliases.Select(alias => $"/{alias}")));
        }

        foreach (var argument in specification.Arguments)
        {
            builder.AppendLine()
                .Append(argument.IsRequired ? "Required " : "Optional ")
                .Append(argument.Name)
                .Append(": ")
                .Append(argument.Description);
        }

        if (specification.Examples.Count > 0)
        {
            builder.AppendLine().Append("Examples: ")
                .Append(string.Join(", ", specification.Examples));
        }

        help = builder.ToString();
        return true;
    }
}
