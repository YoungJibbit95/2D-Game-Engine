using Game.Core.Commands;
using Game.Core.DeveloperTools;
using Xunit;

namespace Game.Tests.DeveloperToolsTests;

public sealed class DeveloperIntentContractTests
{
    private static readonly (string Name, string Input, Type IntentType)[] RequestCases =
    {
        ("tp", "/tp 1 2", typeof(TeleportPlayerIntent)),
        ("godmode", "/godmode on", typeof(SetDeveloperMovementModeIntent)),
        ("noclip", "/noclip off", typeof(SetDeveloperMovementModeIntent)),
        ("fly", "/fly toggle", typeof(SetDeveloperMovementModeIntent)),
        ("speed", "/speed 2", typeof(SetDeveloperSpeedIntent)),
        ("chunk", "/chunk reload 0 0 on", typeof(ReloadChunkIntent)),
        ("spawnrate", "/spawnrate 2", typeof(SetSpawnRateIntent)),
        ("debug", "/debug collisions on", typeof(SetDebugViewIntent)),
        ("performance", "/performance", typeof(PerformanceRequestIntent)),
        ("event", "/event", typeof(EventDiagnosticsRequestIntent)),
        ("weather", "/weather status", typeof(WeatherOverrideRequestIntent)),
        ("biome", "/biome status", typeof(BiomeOverrideRequestIntent)),
        ("save", "/save", typeof(DeveloperSaveRequestIntent)),
        ("render", "/render lighting on", typeof(SetRenderingFeatureIntent)),
        ("lighting", "/lighting rays on", typeof(SetLightingDebugViewIntent)),
        ("rule", "/rule enemy-spawning 2", typeof(GameRuleRequestIntent)),
        ("health", "/health fill", typeof(PlayerResourceRequestIntent)),
        ("mana", "/mana fill", typeof(PlayerResourceRequestIntent)),
        ("projectile", "/projectile magic_bolt", typeof(SpawnProjectileRequestIntent))
    };

    [Fact]
    public void DefaultRegistry_DeclaresAndProducesEveryConcreteIntentType()
    {
        var registry = CommandRegistry.CreateDefault();
        var dispatcher = new CommandDispatcher(registry);

        foreach (var requestCase in RequestCases)
        {
            Assert.True(registry.TryGet(requestCase.Name, out var command));
            var specification = registry.GetSpecification(command);
            Assert.Equal(requestCase.IntentType, specification.RequestIntentType);

            var result = dispatcher.Execute(requestCase.Input, new CommandContext());

            Assert.Equal(CommandResultKind.Request, result.Kind);
            Assert.IsType(requestCase.IntentType, result.Intent);
        }

        var declaredTypes = registry.Commands
            .Select(registry.GetSpecification)
            .Where(specification => specification.RequestIntentType is not null)
            .Select(specification => specification.RequestIntentType!)
            .Distinct()
            .OrderBy(type => type.FullName)
            .ToArray();
        var concreteTypes = typeof(IDeveloperCommandIntent).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract &&
                           !type.IsInterface &&
                           typeof(IDeveloperCommandIntent).IsAssignableFrom(type))
            .OrderBy(type => type.FullName)
            .ToArray();

        Assert.Equal(concreteTypes, declaredTypes);
        Assert.All(
            registry.Commands.Where(command => registry.GetSpecification(command).RequestIntentType is not null),
            command => Assert.Contains(RequestCases, requestCase => requestCase.Name == command.Name));
    }

    [Fact]
    public void TypedCommand_RejectsARequestThatViolatesItsDeclaredIntentContract()
    {
        var result = new MismatchedIntentCommand().Execute(new CommandContext(), Array.Empty<string>());

        Assert.False(result.IsSuccess);
        Assert.Equal("request_intent_mismatch", result.Code);
        Assert.Null(result.Intent);
    }

    private sealed class MismatchedIntentCommand : TypedConsoleCommand
    {
        public MismatchedIntentCommand()
            : base(new CommandSpecification(
                "mismatch",
                "Test command.",
                requestIntentType: typeof(SetDeveloperSpeedIntent)))
        {
        }

        protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
        {
            return CommandResult.Request(
                "bad_request",
                "Wrong intent.",
                new SetSpawnRateIntent(1f, Reset: false));
        }
    }
}
