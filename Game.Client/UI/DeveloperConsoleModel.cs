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

[Flags]
public enum DeveloperConsoleOutputFilter
{
    None = 0,
    Command = 1,
    Information = 2,
    Success = 4,
    Request = 8,
    Error = 16,
    All = Command | Information | Success | Request | Error
}

public enum DeveloperConsoleFocus
{
    CommandInput,
    PaletteSearch
}

public readonly record struct DeveloperConsoleLine(string Text, DeveloperConsoleSeverity Severity);

public sealed class DeveloperConsoleModel
{
    private const int InputLimit = 256;
    private const int SearchLimit = 64;
    private const int LineCapacity = 192;
    private const int SuggestionLimit = 10;
    private static readonly CommandCategory[] CategoryValues =
    {
        CommandCategory.All,
        CommandCategory.General,
        CommandCategory.Player,
        CommandCategory.Inventory,
        CommandCategory.Entities,
        CommandCategory.Combat,
        CommandCategory.World,
        CommandCategory.Environment,
        CommandCategory.Movement,
        CommandCategory.Rendering,
        CommandCategory.Diagnostics,
        CommandCategory.Persistence
    };

    private readonly List<DeveloperConsoleLine> _lines = new(LineCapacity);
    private readonly int[] _filteredLineIndices = new int[LineCapacity];
    private IReadOnlyList<CommandSuggestion> _suggestions = Array.Empty<CommandSuggestion>();
    private IReadOnlyList<DeveloperCommandCatalogEntry> _catalog = Array.Empty<DeveloperCommandCatalogEntry>();
    private int[] _commandIndices = Array.Empty<int>();
    private string[] _helpLines = Array.Empty<string>();
    private string[] _argumentHints = Array.Empty<string>();
    private IReadOnlyList<string> _examples = Array.Empty<string>();
    private string _input = string.Empty;
    private string _suggestionInput = "\0";
    private string _searchQuery = string.Empty;
    private string _searchSnapshot = "\0";
    private int _selectedSuggestion;
    private int _selectedCommand;
    private int _commandCount;
    private int _commandScroll;
    private int _visibleCommandCapacity = 8;
    private int _historyScroll;
    private int _filteredLineCount;
    private int _visibleHistoryCapacity = 8;
    private bool _preferPaletteDetails;

    public bool IsOpen { get; private set; }
    public bool IsCompactViewport { get; private set; }
    public bool IsHelpExpanded { get; private set; }
    public DeveloperConsoleFocus Focus { get; private set; } = DeveloperConsoleFocus.CommandInput;
    public string Input => _input;
    public string SearchQuery => _searchQuery;
    public IReadOnlyList<DeveloperConsoleLine> Lines => _lines;
    public IReadOnlyList<CommandSuggestion> Suggestions => _suggestions;
    public IReadOnlyList<string> HelpLines => _helpLines;
    public IReadOnlyList<string> ArgumentHints => _argumentHints;
    public IReadOnlyList<string> Examples => _examples;
    public IReadOnlyList<CommandCategory> Categories => CategoryValues;
    public CommandCategory SelectedCategory { get; private set; } = CommandCategory.All;
    public DeveloperConsoleOutputFilter OutputFilter { get; private set; } = DeveloperConsoleOutputFilter.All;
    public string Signature { get; private set; } = string.Empty;
    public string Description { get; private set; } = "Select a command or type to autocomplete.";
    public string DispatchLabel { get; private set; } = string.Empty;
    public int SelectedSuggestionIndex => _selectedSuggestion;
    public int SelectedCommandIndex => _selectedCommand;
    public int FilteredCommandCount => _commandCount;
    public int HistoryScrollOffset => _historyScroll;
    public int CommandScrollOffset => _commandScroll;
    public int VisibleSuggestionCount => Math.Min(_suggestions.Count, IsCompactViewport ? 3 : 6);
    public int FirstVisibleSuggestionIndex => Math.Clamp(
        _selectedSuggestion - VisibleSuggestionCount + 1,
        0,
        Math.Max(0, _suggestions.Count - VisibleSuggestionCount));
    public int VisibleHistoryLineCount => Math.Min(_visibleHistoryCapacity, _filteredLineCount);
    public int VisibleCommandCount => Math.Min(
        _visibleCommandCapacity,
        Math.Max(0, _commandCount - _commandScroll));
    public bool HasOlderHistory => _historyScroll < MaximumHistoryScroll;
    public bool HasNewerHistory => _historyScroll > 0;
    public bool HasMoreCommandsAbove => _commandScroll > 0;
    public bool HasMoreCommandsBelow => _commandScroll + VisibleCommandCount < _commandCount;
    public DeveloperCommandCatalogEntry? SelectedCommand =>
        _commandCount == 0 ? null : _catalog[_commandIndices[_selectedCommand]];

    private int MaximumHistoryScroll => Math.Max(0, _filteredLineCount - _visibleHistoryCapacity);

    public void Toggle()
    {
        IsOpen = !IsOpen;
        if (IsOpen)
        {
            _historyScroll = 0;
            Focus = DeveloperConsoleFocus.CommandInput;
        }
    }

    public void Open()
    {
        IsOpen = true;
        _historyScroll = 0;
        Focus = DeveloperConsoleFocus.CommandInput;
    }

    public void Close() => IsOpen = false;

    public void ConfigureViewport(int width, int height)
    {
        IsCompactViewport = width < 900 || height < 600;
        SetVisibleHistoryLineCapacity(IsCompactViewport ? 5 : 9);
    }

    public void SetFocus(DeveloperConsoleFocus focus) => Focus = focus;

    public void SetVisibleHistoryLineCapacity(int capacity)
    {
        _visibleHistoryCapacity = Math.Max(1, capacity);
        ClampHistoryScroll();
    }
    public void SetVisibleCommandCapacity(int capacity)
    {
        _visibleCommandCapacity = Math.Max(1, capacity);
        EnsureSelectedCommandVisible();
    }

    public DeveloperConsoleLine GetVisibleHistoryLine(int visibleIndex)
    {
        if ((uint)visibleIndex >= (uint)VisibleHistoryLineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleIndex));
        }

        var start = Math.Max(0, _filteredLineCount - _visibleHistoryCapacity - _historyScroll);
        return _lines[_filteredLineIndices[start + visibleIndex]];
    }

    public DeveloperCommandCatalogEntry GetVisibleCommand(int visibleIndex)
    {
        if ((uint)visibleIndex >= (uint)VisibleCommandCount)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleIndex));
        }

        return _catalog[_commandIndices[_commandScroll + visibleIndex]];
    }

    public bool IsVisibleCommandSelected(int visibleIndex) =>
        _commandScroll + visibleIndex == _selectedCommand;

    public void AppendCharacter(char character)
    {
        if (!IsOpen || char.IsControl(character))
        {
            return;
        }

        if (Focus == DeveloperConsoleFocus.PaletteSearch)
        {
            if (_searchQuery.Length < SearchLimit)
            {
                _searchQuery += character;
                _searchSnapshot = "\0";
            }

            return;
        }

        if (_input.Length < InputLimit)
        {
            _input += character;
            _suggestionInput = "\0";
            _preferPaletteDetails = false;
        }
    }

    public void Backspace()
    {
        if (Focus == DeveloperConsoleFocus.PaletteSearch)
        {
            if (_searchQuery.Length > 0)
            {
                _searchQuery = _searchQuery[..^1];
                _searchSnapshot = "\0";
            }

            return;
        }

        if (_input.Length > 0)
        {
            _input = _input[..^1];
            _suggestionInput = "\0";
            _preferPaletteDetails = false;
        }
    }

    public void ClearSearch(DeveloperConsoleAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _searchQuery = string.Empty;
        _searchSnapshot = "\0";
        RefreshPalette(adapter);
        RefreshDetails(adapter);
    }

    public void ClearInput(DeveloperConsoleAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _input = string.Empty;
        _suggestions = Array.Empty<CommandSuggestion>();
        _suggestionInput = "\0";
        _preferPaletteDetails = true;
        RefreshDetails(adapter);
    }

    public void RefreshSuggestions(
        DeveloperConsoleAdapter adapter,
        CommandContext context,
        bool force = false)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(context);
        EnsureCatalog(adapter);
        if (!string.Equals(_searchQuery, _searchSnapshot, StringComparison.Ordinal))
        {
            RefreshPalette(adapter);
        }

        if (!force && string.Equals(_input, _suggestionInput, StringComparison.Ordinal))
        {
            return;
        }

        _suggestionInput = _input;
        _suggestions = adapter.Suggest(_input, context, SuggestionLimit);
        _selectedSuggestion = 0;
        RefreshDetails(adapter);
    }

    public void MoveSuggestionSelection(int delta, DeveloperConsoleAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        if (_suggestions.Count == 0 || delta == 0)
        {
            return;
        }

        _selectedSuggestion = Wrap(_selectedSuggestion + delta, _suggestions.Count);
        _preferPaletteDetails = false;
        RefreshDetails(adapter);
    }

    public void SelectSuggestion(
        int index,
        DeveloperConsoleAdapter adapter,
        CommandContext context,
        bool apply)
    {
        if ((uint)index >= (uint)_suggestions.Count)
        {
            return;
        }

        _selectedSuggestion = index;
        _preferPaletteDetails = false;
        RefreshDetails(adapter);
        if (apply)
        {
            ApplySelectedSuggestion(adapter, context);
        }
    }

    public void ApplySelectedSuggestion(DeveloperConsoleAdapter adapter, CommandContext context)
    {
        if (_suggestions.Count == 0)
        {
            return;
        }

        _input = adapter.ApplySuggestion(_input, _suggestions[_selectedSuggestion]);
        Focus = DeveloperConsoleFocus.CommandInput;
        _suggestionInput = "\0";
        _preferPaletteDetails = false;
        RefreshSuggestions(adapter, context, true);
    }

    public void SetCategory(CommandCategory category, DeveloperConsoleAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        if (Array.IndexOf(CategoryValues, category) < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown command category.");
        }

        if (SelectedCategory == category)
        {
            return;
        }

        SelectedCategory = category;
        _searchSnapshot = "\0";
        RefreshPalette(adapter);
        _preferPaletteDetails = true;
        RefreshDetails(adapter);
    }

    public void MoveCategory(int delta, DeveloperConsoleAdapter adapter)
    {
        var current = 0;
        for (var index = 0; index < CategoryValues.Length; index++)
        {
            if (CategoryValues[index] == SelectedCategory)
            {
                current = index;
                break;
            }
        }

        SetCategory(CategoryValues[Wrap(current + delta, CategoryValues.Length)], adapter);
    }

    public void MoveCommandSelection(int delta, DeveloperConsoleAdapter adapter)
    {
        if (_commandCount == 0 || delta == 0)
        {
            return;
        }

        _selectedCommand = Wrap(_selectedCommand + delta, _commandCount);
        _preferPaletteDetails = true;
        EnsureSelectedCommandVisible();
        RefreshDetails(adapter);
    }

    public void SelectVisibleCommand(int index, DeveloperConsoleAdapter adapter)
    {
        if ((uint)index >= (uint)VisibleCommandCount)
        {
            return;
        }

        _selectedCommand = _commandScroll + index;
        _preferPaletteDetails = true;
        RefreshDetails(adapter);
    }

    public void InsertSelectedCommand(DeveloperConsoleAdapter adapter, CommandContext context)
    {
        if (SelectedCommand is not { } entry)
        {
            return;
        }

        _input = entry.InsertText;
        Focus = DeveloperConsoleFocus.CommandInput;
        _suggestionInput = "\0";
        _preferPaletteDetails = false;
        RefreshSuggestions(adapter, context, true);
    }

    public void ScrollCommands(int delta)
    {
        if (_commandCount <= _visibleCommandCapacity || delta == 0)
        {
            return;
        }

        _commandScroll = Math.Clamp(
            _commandScroll + delta,
            0,
            Math.Max(0, _commandCount - _visibleCommandCapacity));
        _selectedCommand = Math.Clamp(
            _selectedCommand,
            _commandScroll,
            _commandScroll + VisibleCommandCount - 1);
    }

    public void RecallPrevious(DeveloperConsoleAdapter adapter, CommandContext context)
    {
        var previous = adapter.PreviousHistory();
        if (previous is null)
        {
            return;
        }

        _input = previous;
        Focus = DeveloperConsoleFocus.CommandInput;
        _suggestionInput = "\0";
        _preferPaletteDetails = false;
        RefreshSuggestions(adapter, context, true);
    }

    public void RecallNext(DeveloperConsoleAdapter adapter, CommandContext context)
    {
        _input = adapter.NextHistory() ?? string.Empty;
        Focus = DeveloperConsoleFocus.CommandInput;
        _suggestionInput = "\0";
        _preferPaletteDetails = false;
        RefreshSuggestions(adapter, context, true);
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
        CommandResult result;
        try
        {
            result = executeCommand(commandText) ??
                     CommandResult.Failure("null_command_result", "Command execution returned no result.");
        }
        catch (Exception ex)
        {
            result = CommandResult.Failure(
                "console_execution_exception",
                $"Command execution failed with {ex.GetType().Name}: {ex.Message}");
        }
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
        _selectedSuggestion = 0;
        _preferPaletteDetails = true;
        return result;
    }

    public void AddInformation(string text) =>
        AddLine(text, DeveloperConsoleSeverity.Information);

    public void ClearOutput()
    {
        _lines.Clear();
        _filteredLineCount = 0;
        _historyScroll = 0;
    }

    public void SetOutputFilter(DeveloperConsoleOutputFilter filter)
    {
        OutputFilter = filter;
        RebuildFilteredLines();
    }

    public void ToggleOutputFilter(DeveloperConsoleOutputFilter filter)
    {
        if (filter == DeveloperConsoleOutputFilter.All)
        {
            SetOutputFilter(DeveloperConsoleOutputFilter.All);
            return;
        }

        OutputFilter ^= filter;
        RebuildFilteredLines();
    }

    public bool IncludesSeverity(DeveloperConsoleSeverity severity) =>
        (OutputFilter & FilterFor(severity)) != 0;

    public void ScrollHistory(int delta)
    {
        _historyScroll = Math.Clamp(_historyScroll + delta, 0, MaximumHistoryScroll);
    }

    public void ToggleHelp() => IsHelpExpanded = !IsHelpExpanded;

    private void EnsureCatalog(DeveloperConsoleAdapter adapter)
    {
        if (ReferenceEquals(_catalog, adapter.Catalog))
        {
            return;
        }

        _catalog = adapter.Catalog;
        _commandIndices = new int[_catalog.Count];
        _searchSnapshot = "\0";
        RefreshPalette(adapter);
    }

    private void RefreshPalette(DeveloperConsoleAdapter adapter)
    {
        if (_commandIndices.Length == 0)
        {
            _commandCount = 0;
            _searchSnapshot = _searchQuery;
            return;
        }

        var previous = SelectedCommand?.Name;
        _commandCount = adapter.SearchCatalog(_searchQuery, SelectedCategory, _commandIndices);
        _selectedCommand = 0;
        if (previous is not null)
        {
            for (var index = 0; index < _commandCount; index++)
            {
                if (_catalog[_commandIndices[index]].Name.Equals(previous, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedCommand = index;
                    break;
                }
            }
        }

        _searchSnapshot = _searchQuery;
        EnsureSelectedCommandVisible();
    }

    private void RefreshDetails(DeveloperConsoleAdapter adapter)
    {
        ConsoleCommandDetails? details = null;
        if (!_preferPaletteDetails && _input.Length > 0)
        {
            var suggestion = _suggestions.Count == 0 ? null : _suggestions[_selectedSuggestion];
            details = adapter.Describe(_input, suggestion);
        }

        if (details is null && SelectedCommand is { } entry)
        {
            details = adapter.Describe(entry);
        }

        if (details is null)
        {
            Signature = string.Empty;
            Description = "Select a command or type to autocomplete.";
            DispatchLabel = string.Empty;
            _helpLines = Array.Empty<string>();
            _argumentHints = Array.Empty<string>();
            _examples = Array.Empty<string>();
            return;
        }

        Signature = details.Signature;
        Description = details.Description;
        DispatchLabel = details.DispatchLabel;
        _helpLines = SplitLines(details.HelpText);
        _examples = details.Examples;
        _argumentHints = new string[details.Arguments.Count];
        for (var index = 0; index < details.Arguments.Count; index++)
        {
            var argument = details.Arguments[index];
            var marker = argument.IsRequired ? $"<{argument.Name}>" : $"[{argument.Name}]";
            _argumentHints[index] = $"{marker}  {argument.Type}  {argument.Description}";
        }
    }

    private void AddLine(string text, DeveloperConsoleSeverity severity)
    {
        if (_lines.Count == LineCapacity)
        {
            _lines.RemoveAt(0);
        }

        _lines.Add(new DeveloperConsoleLine(text, severity));
        _historyScroll = 0;
        RebuildFilteredLines();
    }

    private void RebuildFilteredLines()
    {
        _filteredLineCount = 0;
        for (var index = 0; index < _lines.Count; index++)
        {
            if (IncludesSeverity(_lines[index].Severity))
            {
                _filteredLineIndices[_filteredLineCount++] = index;
            }
        }

        ClampHistoryScroll();
    }
    private void EnsureSelectedCommandVisible()
    {
        if (_commandCount == 0)
        {
            _selectedCommand = 0;
            _commandScroll = 0;
            return;
        }

        _selectedCommand = Math.Clamp(_selectedCommand, 0, _commandCount - 1);
        if (_selectedCommand < _commandScroll)
        {
            _commandScroll = _selectedCommand;
        }
        else if (_selectedCommand >= _commandScroll + _visibleCommandCapacity)
        {
            _commandScroll = _selectedCommand - _visibleCommandCapacity + 1;
        }

        _commandScroll = Math.Clamp(
            _commandScroll,
            0,
            Math.Max(0, _commandCount - _visibleCommandCapacity));
    }

    private void ClampHistoryScroll() =>
        _historyScroll = Math.Clamp(_historyScroll, 0, MaximumHistoryScroll);

    private static DeveloperConsoleOutputFilter FilterFor(DeveloperConsoleSeverity severity) =>
        severity switch
        {
            DeveloperConsoleSeverity.Command => DeveloperConsoleOutputFilter.Command,
            DeveloperConsoleSeverity.Information => DeveloperConsoleOutputFilter.Information,
            DeveloperConsoleSeverity.Success => DeveloperConsoleOutputFilter.Success,
            DeveloperConsoleSeverity.Request => DeveloperConsoleOutputFilter.Request,
            DeveloperConsoleSeverity.Error => DeveloperConsoleOutputFilter.Error,
            _ => DeveloperConsoleOutputFilter.None
        };

    private static int Wrap(int value, int count)
    {
        var wrapped = value % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }

    private static string[] SplitLines(string value) =>
        value.Length == 0
            ? Array.Empty<string>()
            : value.Replace("\r", string.Empty, StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
