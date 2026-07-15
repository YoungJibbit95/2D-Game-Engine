using Game.Client.DeveloperTools;
using Game.Core.Commands;
using Game.Core.DeveloperTools;

namespace Game.Client.UI;

public enum DeveloperConsoleSeverity
{
    Command,
    Information,
    Success,
    Request,
    Error
}

public readonly record struct DeveloperConsoleLine(string Text, DeveloperConsoleSeverity Severity);

public sealed class DeveloperConsoleModel
{
    private const int InputLimit = 160;
    private const int LineCapacity = 96;
    private const int SuggestionLimit = 6;
    private readonly List<DeveloperConsoleLine> _lines = new(LineCapacity);
    private IReadOnlyList<CommandSuggestion> _suggestions = Array.Empty<CommandSuggestion>();
    private string[] _helpLines = Array.Empty<string>();
    private string _input = string.Empty;
    private string _suggestionInput = string.Empty;
    private int _selectedSuggestionIndex;
    private int _historyScrollOffset;
    private int _visibleHistoryLineCapacity = 8;

    public bool IsOpen { get; private set; }

    public bool IsCompactViewport { get; private set; }

    public bool IsHelpExpanded { get; private set; }

    public string Input => _input;

    public IReadOnlyList<DeveloperConsoleLine> Lines => _lines;

    public IReadOnlyList<CommandSuggestion> Suggestions => _suggestions;

    public IReadOnlyList<string> HelpLines => _helpLines;

    public string Signature { get; private set; } = string.Empty;

    public string Description { get; private set; } = "Type /help to list commands.";

    public int SelectedSuggestionIndex => _selectedSuggestionIndex;

    public int HistoryScrollOffset => _historyScrollOffset;

    public int VisibleSuggestionCount => Math.Min(_suggestions.Count, IsCompactViewport ? 3 : 5);

    public int FirstVisibleSuggestionIndex => Math.Clamp(
        _selectedSuggestionIndex - VisibleSuggestionCount + 1,
        0,
        Math.Max(0, _suggestions.Count - VisibleSuggestionCount));

    public int VisibleHistoryLineCount => Math.Min(_visibleHistoryLineCapacity, _lines.Count);

    public bool HasOlderHistory => _historyScrollOffset < MaximumHistoryScrollOffset;

    public bool HasNewerHistory => _historyScrollOffset > 0;

    private int MaximumHistoryScrollOffset => Math.Max(0, _lines.Count - _visibleHistoryLineCapacity);

    public void Toggle()
    {
        IsOpen = !IsOpen;
        if (IsOpen)
        {
            _historyScrollOffset = 0;
        }
    }

    public void Open()
    {
        IsOpen = true;
        _historyScrollOffset = 0;
    }

    public void Close()
    {
        IsOpen = false;
    }

    public void ConfigureViewport(int width, int height)
    {
        IsCompactViewport = width < 720 || height < 480;
        SetVisibleHistoryLineCapacity(IsCompactViewport ? 5 : 10);
    }

    public void SetVisibleHistoryLineCapacity(int capacity)
    {
        _visibleHistoryLineCapacity = Math.Max(1, capacity);
        ClampHistoryScroll();
    }

    public DeveloperConsoleLine GetVisibleHistoryLine(int visibleIndex)
    {
        if (visibleIndex < 0 || visibleIndex >= VisibleHistoryLineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleIndex));
        }

        var start = Math.Max(0, _lines.Count - _visibleHistoryLineCapacity - _historyScrollOffset);
        return _lines[start + visibleIndex];
    }

    public void AppendCharacter(char character)
    {
        if (!IsOpen || char.IsControl(character) || _input.Length >= InputLimit)
        {
            return;
        }

        _input += character;
        MarkSuggestionsDirty();
    }

    public void Backspace()
    {
        if (_input.Length == 0)
        {
            return;
        }

        _input = _input[..^1];
        MarkSuggestionsDirty();
    }

    public void RefreshSuggestions(DeveloperConsoleAdapter adapter, CommandContext context, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(context);
        if (!force && string.Equals(_input, _suggestionInput, StringComparison.Ordinal))
        {
            return;
        }

        _suggestionInput = _input;
        _suggestions = adapter.Suggest(_input, context, SuggestionLimit);
        _selectedSuggestionIndex = 0;
        RefreshHelp(adapter);
    }

    public void MoveSuggestionSelection(int delta, DeveloperConsoleAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        if (_suggestions.Count == 0 || delta == 0)
        {
            return;
        }

        _selectedSuggestionIndex = WrapIndex(_selectedSuggestionIndex + delta, _suggestions.Count);
        RefreshHelp(adapter);
    }

    public void ApplySelectedSuggestion(DeveloperConsoleAdapter adapter, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(context);
        if (_suggestions.Count == 0)
        {
            return;
        }

        _input = adapter.ApplySuggestion(_input, _suggestions[_selectedSuggestionIndex]);
        MarkSuggestionsDirty();
        RefreshSuggestions(adapter, context, force: true);
    }

    public void RecallPrevious(DeveloperConsoleAdapter adapter, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(context);
        var previous = adapter.PreviousHistory();
        if (previous is null)
        {
            return;
        }

        _input = previous;
        MarkSuggestionsDirty();
        RefreshSuggestions(adapter, context, force: true);
    }

    public void RecallNext(DeveloperConsoleAdapter adapter, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(context);
        _input = adapter.NextHistory() ?? string.Empty;
        MarkSuggestionsDirty();
        RefreshSuggestions(adapter, context, force: true);
    }

    public CommandResult? Submit(Func<string, CommandResult> executeCommand)
    {
        ArgumentNullException.ThrowIfNull(executeCommand);
        var commandText = _input.Trim();
        if (commandText.Length == 0)
        {
            return null;
        }

        AddLine($"> {commandText}", DeveloperConsoleSeverity.Command);
        var result = executeCommand(commandText);
        var severity = result.Kind switch
        {
            CommandResultKind.Request => DeveloperConsoleSeverity.Request,
            CommandResultKind.Failure => DeveloperConsoleSeverity.Error,
            _ => DeveloperConsoleSeverity.Success
        };
        var prefix = result.Kind switch
        {
            CommandResultKind.Request => "REQ",
            CommandResultKind.Failure => "ERR",
            _ => "OK"
        };
        AddLine($"{prefix} [{result.Code}]: {result.Message}", severity);
        _input = string.Empty;
        _suggestions = Array.Empty<CommandSuggestion>();
        _suggestionInput = string.Empty;
        _selectedSuggestionIndex = 0;
        Signature = string.Empty;
        Description = "Type /help to list commands.";
        _helpLines = Array.Empty<string>();
        return result;
    }

    public void ScrollHistory(int lineDelta)
    {
        _historyScrollOffset = Math.Clamp(
            _historyScrollOffset + lineDelta,
            0,
            MaximumHistoryScrollOffset);
    }

    public void ToggleHelp()
    {
        IsHelpExpanded = !IsHelpExpanded;
    }

    private void AddLine(string text, DeveloperConsoleSeverity severity)
    {
        if (_lines.Count == LineCapacity)
        {
            _lines.RemoveAt(0);
        }

        _lines.Add(new DeveloperConsoleLine(text, severity));
        _historyScrollOffset = 0;
    }

    private void RefreshHelp(DeveloperConsoleAdapter adapter)
    {
        var selected = _suggestions.Count == 0 ? null : _suggestions[_selectedSuggestionIndex];
        var details = adapter.Describe(_input, selected);
        if (details is null)
        {
            Signature = string.Empty;
            Description = selected?.Description ?? "Type /help to list commands.";
            _helpLines = Array.Empty<string>();
            return;
        }

        Signature = details.Signature;
        Description = details.Description;
        _helpLines = SplitLines(details.HelpText);
    }

    private void MarkSuggestionsDirty()
    {
        _suggestionInput = "\0";
    }

    private void ClampHistoryScroll()
    {
        _historyScrollOffset = Math.Clamp(_historyScrollOffset, 0, MaximumHistoryScrollOffset);
    }

    private static int WrapIndex(int value, int count)
    {
        var wrapped = value % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }

    private static string[] SplitLines(string value)
    {
        return value.Length == 0
            ? Array.Empty<string>()
            : value.Replace("\r", string.Empty, StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
