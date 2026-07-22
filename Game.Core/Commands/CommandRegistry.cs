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
        registry.Register(new RemoveItemCommand());
        registry.Register(new ClearInventoryCommand());
        registry.Register(new TimeCommand());
        registry.Register(new WeatherCommand());
        registry.Register(new BiomeCommand());
        registry.Register(new DeveloperSaveCommand());
        registry.Register(new RenderingFeatureCommand());
        registry.Register(new LightingDebugCommand());
        registry.Register(new GameRuleCommand());
        registry.Register(new PlayerResourceCommand("health", DeveloperTools.PlayerResourceKind.Health));
        registry.Register(new PlayerResourceCommand("mana", DeveloperTools.PlayerResourceKind.Mana));
        registry.Register(new SpawnCommand());
        registry.Register(new SpawnProjectileCommand());
        registry.Register(new DespawnCommand());
        registry.Register(new TeleportCommand());
        registry.Register(new PositionCommand());
        registry.Register(new DeveloperMovementModeCommand("godmode", DeveloperTools.DeveloperMovementMode.GodMode, "god"));
        registry.Register(new DeveloperMovementModeCommand("noclip", DeveloperTools.DeveloperMovementMode.NoClip));
        registry.Register(new DeveloperMovementModeCommand("fly", DeveloperTools.DeveloperMovementMode.Fly));
        registry.Register(new SpeedCommand());
        registry.Register(new ChunkCommand());
        registry.Register(new SpawnRateCommand());
        registry.Register(new DebugWorldCommand());
        registry.Register(new PerformanceCommand());
        registry.Register(new EventDiagnosticsCommand());
        registry.Register(new HelpCommand(registry));
        return registry;
    }

    public void Register(IConsoleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pendingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ValidateName(command.Name, pendingNames);
        for (var index = 0; index < command.Aliases.Count; index++)
        {
            ValidateName(command.Aliases[index], pendingNames);
        }

        _byName.Add(command.Name, command);
        for (var index = 0; index < command.Aliases.Count; index++)
        {
            _byName.Add(command.Aliases[index], command);
        }

        _commands.Add(command);
    }

    public bool TryGet(string name, out IConsoleCommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _byName.TryGetValue(name, out command!);
    }

    public CommandSpecification GetSpecification(IConsoleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command is TypedConsoleCommand typed
            ? typed.Specification
            : new CommandSpecification(command.Name, command.Description, aliases: command.Aliases);
    }

    private void ValidateName(string name, ISet<string> pendingNames)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Command names and aliases must not be empty.", nameof(name));
        }

        if (!pendingNames.Add(name))
        {
            throw new InvalidOperationException($"Command '{name}' declares a duplicate name or alias.");
        }

        if (_byName.ContainsKey(name))
        {
            throw new InvalidOperationException($"A command named '{name}' is already registered.");
        }
    }
}
