using Game.Client.DeveloperTools;
using Game.Client.UI;
using Game.Core.Commands;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class DeveloperConsolePaletteTests
{
    [Fact]
    public void Palette_SearchesCategoriesAndInsertsSelectedCommand()
    {
        var adapter = new DeveloperConsoleAdapter();
        var context = new CommandContext();
        var model = new DeveloperConsoleModel();
        model.Open();
        model.RefreshSuggestions(adapter, context, true);
        model.SetCategory(CommandCategory.Rendering, adapter);
        model.SetFocus(DeveloperConsoleFocus.PaletteSearch);
        Enter(model, "ligting");
        model.RefreshSuggestions(adapter, context);

        Assert.True(model.FilteredCommandCount > 0);
        Assert.Equal("lighting", model.SelectedCommand!.Name);
        Assert.All(
            Enumerable.Range(0, model.VisibleCommandCount),
            index => Assert.Equal(CommandCategory.Rendering, model.GetVisibleCommand(index).Category));

        model.InsertSelectedCommand(adapter, context);

        Assert.Equal(DeveloperConsoleFocus.CommandInput, model.Focus);
        Assert.Equal("/lighting ", model.Input);
        Assert.NotEmpty(model.ArgumentHints);
        Assert.NotEmpty(model.Examples);
    }

    [Fact]
    public void OutputFilters_UseCachedVisibleIndicesAndClearDeterministically()
    {
        var model = new DeveloperConsoleModel();
        model.Open();

        Enter(model, "/ok");
        model.Submit(_ => CommandResult.Success("done"));
        Enter(model, "/bad");
        model.Submit(_ => CommandResult.Failure("broken"));
        Enter(model, "/request");
        model.Submit(_ => CommandResult.Request(
            "request",
            "queued",
            new TestIntent()));

        model.SetVisibleHistoryLineCapacity(20);
        model.SetOutputFilter(DeveloperConsoleOutputFilter.Error);

        Assert.Equal(1, model.VisibleHistoryLineCount);
        Assert.Equal(DeveloperConsoleSeverity.Error, model.GetVisibleHistoryLine(0).Severity);

        model.ToggleOutputFilter(DeveloperConsoleOutputFilter.Request);
        Assert.Equal(2, model.VisibleHistoryLineCount);

        model.ClearOutput();
        Assert.Empty(model.Lines);
        Assert.Equal(0, model.VisibleHistoryLineCount);
    }

    private static void Enter(DeveloperConsoleModel model, string value)
    {
        foreach (var character in value)
        {
            model.AppendCharacter(character);
        }
    }

    private sealed record TestIntent : Game.Core.DeveloperTools.IDeveloperCommandIntent;
}
