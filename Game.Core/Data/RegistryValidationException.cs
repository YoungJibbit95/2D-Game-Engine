namespace Game.Core.Data;

public sealed class RegistryValidationException : Exception
{
    public RegistryValidationException(string message)
        : base(message)
    {
    }
}
