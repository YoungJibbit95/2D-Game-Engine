namespace Game.Core.Commands;

public sealed record CommandResult(bool IsSuccess, string Message)
{
    public static CommandResult Success(string message)
    {
        return new CommandResult(true, message);
    }

    public static CommandResult Failure(string message)
    {
        return new CommandResult(false, message);
    }
}
