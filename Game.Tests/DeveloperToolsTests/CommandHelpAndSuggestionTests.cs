using Game.Core.Commands;
using Game.Core.DeveloperTools;
using Game.Core.Entities;
using Game.Core.Physics;
using System.Numerics;
using Xunit;

namespace Game.Tests.DeveloperToolsTests;

public sealed class CommandHelpAndSuggestionTests
{
    [Fact]
    public void Help_ListsRegisteredCommandsAndDetailedTypedSchema()
    {
        var registry = CommandRegistry.CreateDefault();
        var help = new CommandHelpService(registry);

        var overview = help.BuildOverview();
        var found = help.TryBuildCommandHelp("item", out var detail);

        Assert.Contains("/give", overview);
        Assert.Contains("/performance", overview);
        Assert.True(found);
        Assert.Contains("/give <itemId> [count]", detail);
        Assert.Contains("Aliases: /item", detail);
        Assert.Contains("Required itemId", detail);
    }

    [Fact]
    public void HelpCommand_ReturnsUnknownCommandFailureWithStableCode()
    {
        var result = new CommandDispatcher(CommandRegistry.CreateDefault())
            .Execute("/help absent", new CommandContext());

        Assert.False(result.IsSuccess);
        Assert.Equal("unknown_command", result.Code);
    }

    [Fact]
    public void Suggestions_CompleteCommandsAndAliases()
    {
        var suggestions = new CommandSuggestionService(CommandRegistry.CreateDefault())
            .GetSuggestions("/sp", new CommandContext());

        Assert.Contains(suggestions, suggestion => suggestion.ReplacementText == "spawn");
        Assert.Contains(suggestions, suggestion => suggestion.ReplacementText == "spawnrate");
        Assert.Contains(suggestions, suggestion => suggestion.ReplacementText == "spawn-rate" &&
                                                   suggestion.Kind == CommandSuggestionKind.Alias);
    }

    [Fact]
    public void Suggestions_UseItemAndEntityRegistriesForArguments()
    {
        var context = new CommandContext { Content = CommandTests.CommandTestContent.Create() };
        var suggestions = new CommandSuggestionService(CommandRegistry.CreateDefault());

        var items = suggestions.GetSuggestions("/give g", context);
        var entities = suggestions.GetSuggestions("/spawn s", context);

        Assert.Contains(items, suggestion => suggestion.ReplacementText == "gel");
        Assert.Contains(entities, suggestion => suggestion.ReplacementText == "slime");
    }

    [Fact]
    public void Suggestions_UseLoadedEntityInstanceIdsAndBooleanValues()
    {
        var content = CommandTests.CommandTestContent.Create();
        var entities = new EntityManager();
        entities.Add(new EntityFactory(new TileCollisionResolver())
            .CreateEnemy(content.Entities.GetById("slime"), new Vector2(2, 3)));
        var context = new CommandContext { EntityManager = entities };
        var service = new CommandSuggestionService(CommandRegistry.CreateDefault());

        var instances = service.GetSuggestions("/despawn #", context);
        var toggles = service.GetSuggestions("/fly ", context);

        Assert.Contains(instances, suggestion => suggestion.ReplacementText == "#1");
        Assert.Equal(new[] { "off", "on", "toggle" }, toggles.Select(suggestion => suggestion.ReplacementText));
    }
}
