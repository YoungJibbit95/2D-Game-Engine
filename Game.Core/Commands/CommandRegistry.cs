namespace Game.Core.Commands;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, IConsoleCommand> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IConsoleCommand> _commands = new();

    public IReadOnlyList<IConsoleCommand> Commands => _commands;

    public static CommandRegistry CreateDefault()
    {
        var registry = new CommandRegistry();
        registry.Register(new GiveItemCommand());
        registry.Register(new TimeCommand());
        registry.Register(new SpawnCommand());
        return registry;
    }

    public void Register(IConsoleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        AddName(command.Name, command);

        foreach (var alias in command.Aliases)
        {
            AddName(alias, command);
        }

        _commands.Add(command);
    }

    public bool TryGet(string name, out IConsoleCommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _byName.TryGetValue(name, out command!);
    }

    private void AddName(string name, IConsoleCommand command)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Command names and aliases must not be empty.", nameof(name));
        }

        if (_byName.ContainsKey(name))
        {
            throw new InvalidOperationException($"A command named '{name}' is already registered.");
        }

        _byName.Add(name, command);
    }
}
