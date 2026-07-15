using Game.Core.Commands;
using Game.Core.DeveloperTools;
using Xunit;

namespace Game.Tests.DeveloperToolsTests;

public sealed class CommandHistoryTests
{
    [Fact]
    public void Record_EnforcesCapacityAndPreservesMonotonicSequence()
    {
        var now = new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);
        var history = new CommandHistory(capacity: 2, clock: () => now);

        history.Record("/one", CommandResult.Success("one"));
        history.Record("/two", CommandResult.Failure("two"));
        history.Record("/three", CommandResult.Success("three"));

        Assert.Equal(new[] { "/two", "/three" }, history.Entries.Select(entry => entry.Input));
        Assert.Equal(new long[] { 2, 3 }, history.Entries.Select(entry => entry.Sequence));
        Assert.All(history.Entries, entry => Assert.Equal(now, entry.ExecutedAtUtc));
    }

    [Fact]
    public void Navigation_MovesBackwardForwardAndReturnsToDraftPosition()
    {
        var history = new CommandHistory();
        history.Record("/one", CommandResult.Success("one"));
        history.Record("/two", CommandResult.Success("two"));

        Assert.Equal("/two", history.Previous()!.Input);
        Assert.Equal("/one", history.Previous()!.Input);
        Assert.Equal("/one", history.Previous()!.Input);
        Assert.Equal("/two", history.Next()!.Input);
        Assert.Null(history.Next());
    }

    [Fact]
    public void Dispatcher_RecordsSuccessAndFailureResults()
    {
        var history = new CommandHistory();
        var dispatcher = new CommandDispatcher(CommandRegistry.CreateDefault(), history: history);

        dispatcher.Execute("/position", new CommandContext());
        dispatcher.Execute("/missing", new CommandContext());

        Assert.Equal(2, history.Entries.Count);
        Assert.False(history.Entries[0].Result.IsSuccess);
        Assert.Equal("/missing", history.Entries[1].Input);
    }
}
