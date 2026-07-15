using Game.Core.Commands;
using Xunit;

namespace Game.Tests.CommandTests;

public sealed class TypedCommandTests
{
    [Fact]
    public void Validate_ReturnsStableIssueForMissingRequiredArgument()
    {
        var specification = CreateSpecification();

        var result = new CommandArgumentValidator().Validate(specification, Array.Empty<string>());

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("missing_argument", issue.Code);
        Assert.Equal("count", issue.ArgumentName);
        Assert.Contains(specification.Usage, issue.Message);
    }

    [Theory]
    [InlineData("zero", "invalid_argument")]
    [InlineData("0", "invalid_argument")]
    [InlineData("11", "invalid_argument")]
    public void Validate_RejectsWrongIntegerTypeAndRange(string value, string expectedCode)
    {
        var result = new CommandArgumentValidator().Validate(CreateSpecification(), new[] { value });

        Assert.False(result.IsValid);
        Assert.Equal(expectedCode, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Validate_ProducesTypedInvariantArguments()
    {
        var specification = new CommandSpecification(
            "number",
            "Parses a number.",
            new[] { new CommandArgumentSpecification("value", CommandArgumentType.Number) });

        var result = new CommandArgumentValidator().Validate(specification, new[] { "1.25" });

        Assert.True(result.IsValid);
        Assert.Equal(1.25, result.Arguments!.GetDouble("value"));
    }

    [Fact]
    public void Specification_RejectsRequiredArgumentAfterOptionalArgument()
    {
        Assert.Throws<ArgumentException>(() => new CommandSpecification(
            "invalid",
            "Invalid schema.",
            new[]
            {
                new CommandArgumentSpecification("optional", CommandArgumentType.Text, false),
                new CommandArgumentSpecification("required", CommandArgumentType.Text)
            }));
    }

    [Fact]
    public void Dispatcher_KeepsLegacyConsoleCommandsCompatible()
    {
        var registry = new CommandRegistry();
        registry.Register(new LegacyCommand());

        var result = new CommandDispatcher(registry).Execute("/legacy one two", new CommandContext());

        Assert.True(result.IsSuccess);
        Assert.Equal("one,two", result.Message);
    }

    private static CommandSpecification CreateSpecification()
    {
        return new CommandSpecification(
            "amount",
            "Validates an amount.",
            new[]
            {
                new CommandArgumentSpecification(
                    "count",
                    CommandArgumentType.Integer,
                    minimum: 1,
                    maximum: 10)
            });
    }

    private sealed class LegacyCommand : IConsoleCommand
    {
        public string Name => "legacy";

        public string Description => "Legacy command.";

        public IReadOnlyList<string> Aliases => Array.Empty<string>();

        public CommandResult Execute(CommandContext context, IReadOnlyList<string> arguments)
        {
            return CommandResult.Success(string.Join(',', arguments));
        }
    }
}
