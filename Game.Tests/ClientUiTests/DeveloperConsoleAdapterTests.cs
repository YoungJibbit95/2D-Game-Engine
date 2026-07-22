using Game.Client.DeveloperTools;
using Game.Core.Commands;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class DeveloperConsoleAdapterTests
{
    [Fact]
    public void SuggestAndApply_CompletesCommandWithoutDroppingSlash()
    {
        var adapter = new DeveloperConsoleAdapter();
        var context = new CommandContext();
        var suggestion = Assert.Single(adapter.Suggest("/gi", context), item => item.DisplayText == "give");

        var completed = adapter.ApplySuggestion("/gi", suggestion);

        Assert.Equal("/give ", completed);
        Assert.Contains("/give", adapter.Help("give"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_RecordsNavigableHistoryAndTypedFailure()
    {
        var adapter = new DeveloperConsoleAdapter();

        var result = adapter.Execute("/does-not-exist", new CommandContext());

        Assert.False(result.IsSuccess);
        Assert.Equal("/does-not-exist", adapter.PreviousHistory());
        Assert.Null(adapter.NextHistory());
        Assert.Single(adapter.History);
    }
}
