using Game.Core.DeveloperTools;

namespace Game.Core.Commands;

internal static class DeveloperCommandParsing
{
    public static DeveloperToggle ParseToggle(string? value)
    {
        if (value is null || value.Equals("toggle", StringComparison.OrdinalIgnoreCase))
        {
            return DeveloperToggle.Toggle;
        }

        return value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.Ordinal)
            ? DeveloperToggle.On
            : DeveloperToggle.Off;
    }
}
