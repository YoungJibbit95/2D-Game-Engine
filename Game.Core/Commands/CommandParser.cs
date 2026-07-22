using System.Text;

namespace Game.Core.Commands;

public sealed class CommandParser
{
    public bool TryParse(string? input, out ParsedCommand parsedCommand)
    {
        return TryParse(input, out parsedCommand, out _);
    }

    public bool TryParse(string? input, out ParsedCommand parsedCommand, out string? error)
    {
        parsedCommand = new ParsedCommand(string.Empty, Array.Empty<string>());
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var commandText = input.Trim();
        if (commandText.StartsWith("/", StringComparison.Ordinal))
        {
            commandText = commandText[1..].TrimStart();
        }

        if (commandText.Length == 0)
        {
            return false;
        }

        if (!TryTokenize(commandText, out var tokens))
        {
            error = "Unterminated quoted argument.";
            return false;
        }

        if (tokens.Count == 0)
        {
            return false;
        }

        parsedCommand = new ParsedCommand(tokens[0], tokens.Skip(1).ToArray());
        return true;
    }

    private static bool TryTokenize(string text, out List<string> tokens)
    {
        tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character == '"' && (i == 0 || text[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                FlushToken(tokens, current);
                continue;
            }

            if (character == '\\' && inQuotes && i + 1 < text.Length && text[i + 1] == '"')
            {
                current.Append('"');
                i++;
                continue;
            }

            current.Append(character);
        }

        if (inQuotes)
        {
            return false;
        }

        FlushToken(tokens, current);
        return true;
    }

    private static void FlushToken(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        tokens.Add(current.ToString());
        current.Clear();
    }
}
