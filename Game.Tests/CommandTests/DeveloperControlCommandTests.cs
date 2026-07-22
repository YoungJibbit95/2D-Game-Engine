using Game.Core.Commands;
using Game.Core.DeveloperTools;
using Game.Core.Events;
using Xunit;

namespace Game.Tests.CommandTests;

public sealed class DeveloperControlCommandTests
{
    [Fact]
    public void Dispatcher_PublishesTypedIntentThroughAuthoritativeEventBus()
    {
        var events = new GameEventBus();
        DeveloperCommandIntentRequestedEvent? published = null;
        using var subscription = events.Subscribe<DeveloperCommandIntentRequestedEvent>(
            value => published = value);
        var dispatcher = new CommandDispatcher(CommandRegistry.CreateDefault());

        var result = dispatcher.Execute(
            "/weather snow 0.65",
            new CommandContext { Events = events });

        Assert.Equal(CommandResultKind.Request, result.Kind);
        var intent = Assert.IsType<WeatherOverrideRequestIntent>(result.Intent);
        Assert.Equal(WeatherOverrideRequestKind.Set, intent.Kind);
        Assert.Equal("snow", intent.WeatherId);
        Assert.Equal(0.65f, intent.Intensity);
        Assert.NotNull(published);
        Assert.Equal("weather", published.CommandName);
        Assert.Same(result.Intent, published.Intent);
    }

    [Theory]
    [InlineData("/save full", typeof(DeveloperSaveRequestIntent))]
    [InlineData("/render shadows off", typeof(SetRenderingFeatureIntent))]
    [InlineData("/lighting rays on", typeof(SetLightingDebugViewIntent))]
    [InlineData("/rule mana-cost 0", typeof(GameRuleRequestIntent))]
    [InlineData("/mana set 75", typeof(PlayerResourceRequestIntent))]
    [InlineData("/projectile magic_bolt", typeof(SpawnProjectileRequestIntent))]
    public void DeveloperCommands_ReturnSafeTypedRequests(string command, Type intentType)
    {
        var result = new CommandDispatcher(CommandRegistry.CreateDefault())
            .Execute(command, new CommandContext());

        Assert.Equal(CommandResultKind.Request, result.Kind);
        Assert.NotNull(result.Intent);
        Assert.IsType(intentType, result.Intent);
    }

    [Fact]
    public void RuleList_IsImmediateReadOnlyResult()
    {
        var result = new CommandDispatcher(CommandRegistry.CreateDefault())
            .Execute("/rule list", new CommandContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(CommandResultKind.Success, result.Kind);
        Assert.Contains("enemy-spawning", result.Message);
        Assert.Null(result.Intent);
    }
}
