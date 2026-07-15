using Game.Core.DeveloperTools;

namespace Game.Core.Commands;

public sealed record CommandResult(bool IsSuccess, string Message)
{
    public CommandResultKind Kind { get; init; } = IsSuccess
        ? CommandResultKind.Success
        : CommandResultKind.Failure;

    public string Code { get; init; } = IsSuccess ? "ok" : "error";

    public IDeveloperCommandIntent? Intent { get; init; }

    public static CommandResult Success(string message)
    {
        return new CommandResult(true, message);
    }

    public static CommandResult Success(string code, string message)
    {
        return new CommandResult(true, message) { Code = code };
    }

    public static CommandResult Failure(string message)
    {
        return new CommandResult(false, message);
    }

    public static CommandResult Failure(string code, string message)
    {
        return new CommandResult(false, message) { Code = code };
    }

    public static CommandResult Request(string code, string message, IDeveloperCommandIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return new CommandResult(true, message)
        {
            Kind = CommandResultKind.Request,
            Code = code,
            Intent = intent
        };
    }
}
