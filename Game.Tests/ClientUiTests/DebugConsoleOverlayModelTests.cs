using Game.Client.DeveloperTools;
using Game.Client.UI;
using Game.Core.Commands;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class DebugConsoleOverlayModelTests
{
    [Fact]
    public void OpenedCompactConsole_TracksCompletionHelpHistoryScrollingAndSeverity()
    {
        var adapter = new DeveloperConsoleAdapter();
        var context = new CommandContext();
        var model = new DeveloperConsoleModel();

        model.Toggle();
        model.ConfigureViewport(640, 360);
        Enter(model, "/sp");
        model.RefreshSuggestions(adapter, context);

        Assert.True(model.IsOpen);
        Assert.True(model.IsCompactViewport);
        Assert.InRange(model.VisibleSuggestionCount, 2, 3);
        Assert.Equal("spawn", model.Suggestions[model.SelectedSuggestionIndex].DisplayText);
        Assert.Equal("/spawn <entityId> [x] [y]", model.Signature);

        model.MoveSuggestionSelection(1, adapter);
        Assert.Equal("spawn-rate", model.Suggestions[model.SelectedSuggestionIndex].DisplayText);
        Assert.Contains("spawn", model.Description, StringComparison.OrdinalIgnoreCase);
        model.ApplySelectedSuggestion(adapter, context);
        Assert.Equal("/spawn-rate ", model.Input);

        model.Submit(_ => CommandResult.Request("spawn_rate", "Spawn rate update requested.", new TestIntent()));
        Assert.Equal(DeveloperConsoleSeverity.Request, model.Lines[^1].Severity);
        Assert.Contains("REQ [spawn_rate]", model.Lines[^1].Text, StringComparison.Ordinal);

        for (var index = 0; index < 8; index++)
        {
            Enter(model, $"/missing-{index}");
            model.Submit(CommandResult.Failure);
        }

        model.SetVisibleHistoryLineCapacity(4);
        model.ScrollHistory(3);
        Assert.Equal(3, model.HistoryScrollOffset);
        Assert.True(model.HasNewerHistory);
        Assert.True(model.HasOlderHistory);
        Assert.Equal(4, model.VisibleHistoryLineCount);
        Assert.Equal(DeveloperConsoleSeverity.Error, model.Lines[^1].Severity);
    }

    [Fact]
    public void CompletionSelection_WrapsAndHistoryRecallRemainsSeparate()
    {
        var adapter = new DeveloperConsoleAdapter();
        var context = new CommandContext();
        var model = new DeveloperConsoleModel();
        model.Toggle();
        model.ConfigureViewport(640, 360);
        Enter(model, "/");
        model.RefreshSuggestions(adapter, context);

        model.MoveSuggestionSelection(-1, adapter);
        Assert.Equal(model.Suggestions.Count - 1, model.SelectedSuggestionIndex);
        Assert.True(model.FirstVisibleSuggestionIndex > 0);

        model.Submit(command => adapter.Execute(command, context));
        model.RecallPrevious(adapter, context);
        Assert.Equal("/", model.Input);
    }

    private static void Enter(DeveloperConsoleModel model, string text)
    {
        foreach (var character in text)
        {
            model.AppendCharacter(character);
        }
    }

    private sealed record TestIntent : Game.Core.DeveloperTools.IDeveloperCommandIntent;
}
