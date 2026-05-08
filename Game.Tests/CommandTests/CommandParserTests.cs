using Game.Core.Commands;
using Xunit;

namespace Game.Tests.CommandTests;

public sealed class CommandParserTests
{
    [Fact]
    public void TryParse_SplitsCommandNameAndArguments()
    {
        var parsed = new CommandParser().TryParse("/give gel 25", out var command);

        Assert.True(parsed);
        Assert.Equal("give", command.Name);
        Assert.Equal(new[] { "gel", "25" }, command.Arguments);
    }

    [Fact]
    public void TryParse_KeepsQuotedArgumentsTogether()
    {
        var parsed = new CommandParser().TryParse("/give \"dirt block\" 10", out var command);

        Assert.True(parsed);
        Assert.Equal("give", command.Name);
        Assert.Equal(new[] { "dirt block", "10" }, command.Arguments);
    }

    [Fact]
    public void TryParse_ReturnsFalseForEmptyInput()
    {
        var parsed = new CommandParser().TryParse(" / ", out _);

        Assert.False(parsed);
    }
}
