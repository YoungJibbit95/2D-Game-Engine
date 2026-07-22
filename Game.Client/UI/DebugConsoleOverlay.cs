using Game.Client.DeveloperTools;
using Game.Client.Input;
using Game.Client.Rendering;
using Game.Core.Commands;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.UI;

public sealed class DebugConsoleOverlay
{
    private const int MaximumCommandRows = 14;
    private const int MaximumSuggestionRows = 6;
    private static readonly DeveloperConsoleOutputFilter[] OutputFilters =
    {
        DeveloperConsoleOutputFilter.All,
        DeveloperConsoleOutputFilter.Command,
        DeveloperConsoleOutputFilter.Information,
        DeveloperConsoleOutputFilter.Success,
        DeveloperConsoleOutputFilter.Request,
        DeveloperConsoleOutputFilter.Error
    };
    private static readonly string[] OutputFilterLabels =
    {
        "ALL", "CMD", "INFO", "OK", "REQ", "ERR"
    };

    private static readonly string[] CategoryLabels =
    {
        "ALL", "GENERAL", "PLAYER", "INVENTORY", "ENTITIES", "COMBAT",
        "WORLD", "ENVIRONMENT", "MOVEMENT", "RENDERING", "DIAGNOSTICS", "PERSISTENCE"
    };

    private readonly DeveloperConsoleModel _state;
    private readonly Rectangle[] _categoryBounds = new Rectangle[12];
    private readonly Rectangle[] _commandBounds = new Rectangle[MaximumCommandRows];
    private readonly Rectangle[] _suggestionBounds = new Rectangle[MaximumSuggestionRows];
    private readonly Rectangle[] _filterBounds = new Rectangle[OutputFilters.Length];
    private readonly ConsoleTextClipCache _textCache = new(160);
    private Rectangle _panelBounds;
    private Rectangle _searchBounds;
    private Rectangle _commandListBounds;
    private Rectangle _historyBounds;
    private Rectangle _inputBounds;
    private Rectangle _insertButtonBounds;
    private Rectangle _runButtonBounds;
    private Rectangle _detailInsertButtonBounds;
    private Rectangle _detailRunButtonBounds;
    private Rectangle _clearButtonBounds;
    private Rectangle _helpButtonBounds;
    private Rectangle _closeButtonBounds;
    private int _drawnCategoryCount;
    private int _drawnCommandCount;
    private int _drawnSuggestionCount;
    private string _lastSearchText = "\0";
    private string _searchDisplayText = "SEARCH COMMANDS...";
    private string _lastInputText = "\0";
    private string _inputDisplayText = "> ENTER COMMAND...";
    private GamePadState _previousGamePad;

    public DebugConsoleOverlay(DeveloperConsoleModel? state = null)
    {
        _state = state ?? new DeveloperConsoleModel();
    }

    public bool IsOpen => _state.IsOpen;

    public DeveloperConsoleModel State => _state;

    public bool Update(InputManager input, Func<string, CommandResult> executeCommand, string toggleBinding = "F10")
    {
        ArgumentNullException.ThrowIfNull(executeCommand);
        return UpdateCore(input, executeCommand, null, null, toggleBinding);
    }

    public bool Update(
        InputManager input,
        DeveloperConsoleAdapter adapter,
        CommandContext context,
        string toggleBinding = "F10")
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(context);
        return UpdateCore(input, command => adapter.Execute(command, context), adapter, context, toggleBinding);
    }

    public void OnTextInput(char character) => _state.AppendCharacter(character);

    public void Draw(RenderContext context)
    {
        if (!_state.IsOpen)
        {
            return;
        }

        _state.ConfigureViewport(context.ViewportBounds.Width, context.ViewportBounds.Height);
        var palette = UiTheme.Resolve();
        var compact = _state.IsCompactViewport;
        var margin = compact ? 8 : 16;
        var width = Math.Min(compact ? 920 : 1180, context.ViewportBounds.Width - margin * 2);
        var height = Math.Min(compact ? 620 : 760, context.ViewportBounds.Height - margin * 2);
        width = Math.Max(480, width);
        height = Math.Max(360, height);
        _panelBounds = new Rectangle(
            context.ViewportBounds.X + (context.ViewportBounds.Width - width) / 2,
            context.ViewportBounds.Y + (context.ViewportBounds.Height - height) / 2,
            width,
            height);

        UiTheme.DrawRoundedRectangle(context, _panelBounds, UiTheme.WithAlpha(palette.Backdrop, 0.98f), 8);
        UiTheme.DrawRoundedBorder(context, _panelBounds, palette.AccentSoft, 8, 1);
        DrawHeader(context, palette);

        var inner = new Rectangle(_panelBounds.X + 10, _panelBounds.Y + 48, _panelBounds.Width - 20, _panelBounds.Height - 58);
        var inputHeight = 34;
        var footerHeight = 18;
        var outputHeight = compact ? 128 : 174;
        var outputTop = inner.Bottom - outputHeight - inputHeight - footerHeight - 8;
        var workspace = new Rectangle(inner.X, inner.Y, inner.Width, Math.Max(120, outputTop - inner.Y - 6));
        DrawWorkspace(context, workspace, palette, compact);

        var filters = new Rectangle(inner.X, outputTop, inner.Width, 24);
        DrawOutputFilters(context, filters, palette);
        _historyBounds = new Rectangle(inner.X, filters.Bottom + 2, inner.Width, outputHeight - filters.Height - 2);
        DrawHistory(context, _historyBounds, palette);

        _inputBounds = new Rectangle(inner.X, _historyBounds.Bottom + 4, inner.Width - 174, inputHeight);
        _insertButtonBounds = new Rectangle(_inputBounds.Right + 6, _inputBounds.Y, 78, inputHeight);
        _runButtonBounds = new Rectangle(_insertButtonBounds.Right + 6, _inputBounds.Y, 84, inputHeight);
        DrawInput(context, palette);
        DrawSuggestions(context, palette);
        DrawFooter(context, new Rectangle(inner.X, _inputBounds.Bottom + 2, inner.Width, footerHeight), palette);
    }

    private bool UpdateCore(
        InputManager input,
        Func<string, CommandResult> executeCommand,
        DeveloperConsoleAdapter? adapter,
        CommandContext? context,
        string toggleBinding)
    {
        ArgumentNullException.ThrowIfNull(input);
        var gamePad = GamePad.GetState(PlayerIndex.One);
        var previousGamePad = _previousGamePad;
        _previousGamePad = gamePad;
        var gamePadChord = gamePad.IsButtonDown(Buttons.Start) &&
                           IsPressed(gamePad, previousGamePad, Buttons.Y);

        if (input.IsBindingPressed(toggleBinding) || gamePadChord)
        {
            _state.Toggle();
            if (_state.IsOpen && adapter is not null && context is not null)
            {
                _state.RefreshSuggestions(adapter, context, true);
            }

            return true;
        }

        if (!_state.IsOpen)
        {
            return false;
        }

        if (adapter is not null && context is not null)
        {
            _state.RefreshSuggestions(adapter, context);
        }

        if (HandleDismiss(input, gamePad, previousGamePad, adapter))
        {
            return true;
        }

        if (input.IsKeyPressed(Keys.F1) || IsPressed(gamePad, previousGamePad, Buttons.Y) ||
            input.IsLeftMousePressed && _helpButtonBounds.Contains(input.MousePosition))
        {
            _state.ToggleHelp();
        }

        if (input.IsKeyPressed(Keys.Back))
        {
            _state.Backspace();
        }

        if (adapter is not null && context is not null)
        {
            HandleMouse(input, adapter, context, executeCommand);
            HandleKeyboard(input, adapter, context, executeCommand);
            HandleGamePad(gamePad, previousGamePad, adapter, context, executeCommand);
        }
        else if (input.IsKeyPressed(Keys.Enter))
        {
            _state.Submit(executeCommand);
        }

        return true;
    }

    private bool HandleDismiss(
        InputManager input,
        GamePadState gamePad,
        GamePadState previousGamePad,
        DeveloperConsoleAdapter? adapter)
    {
        var escape = input.IsKeyPressed(Keys.Escape);
        var closeClick = input.IsLeftMousePressed && _closeButtonBounds.Contains(input.MousePosition);
        var gamePadBack = IsPressed(gamePad, previousGamePad, Buttons.B);
        if (!escape && !closeClick && !gamePadBack)
        {
            return false;
        }

        if (escape && _state.Focus == DeveloperConsoleFocus.PaletteSearch &&
            _state.SearchQuery.Length > 0 && adapter is not null)
        {
            _state.ClearSearch(adapter);
            return true;
        }

        _state.Close();
        return true;
    }

    private void HandleKeyboard(
        InputManager input,
        DeveloperConsoleAdapter adapter,
        CommandContext context,
        Func<string, CommandResult> executeCommand)
    {
        var control = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
        if (control && input.IsKeyPressed(Keys.F))
        {
            _state.SetFocus(DeveloperConsoleFocus.PaletteSearch);
        }

        if (control && input.IsKeyPressed(Keys.L))
        {
            _state.ClearInput(adapter);
        }

        if (input.IsKeyPressed(Keys.PageUp))
        {
            _state.ScrollHistory(Math.Max(1, _state.VisibleHistoryLineCount - 1));
        }
        else if (input.IsKeyPressed(Keys.PageDown))
        {
            _state.ScrollHistory(-Math.Max(1, _state.VisibleHistoryLineCount - 1));
        }

        if (_state.Focus == DeveloperConsoleFocus.PaletteSearch)
        {
            if (input.IsKeyPressed(Keys.Up))
            {
                _state.MoveCommandSelection(-1, adapter);
            }
            else if (input.IsKeyPressed(Keys.Down))
            {
                _state.MoveCommandSelection(1, adapter);
            }

            if (input.IsKeyPressed(Keys.Left))
            {
                _state.MoveCategory(-1, adapter);
            }
            else if (input.IsKeyPressed(Keys.Right))
            {
                _state.MoveCategory(1, adapter);
            }

            if (input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.Tab))
            {
                _state.InsertSelectedCommand(adapter, context);
            }

            return;
        }

        if (input.IsKeyPressed(Keys.Up))
        {
            if (control || _state.Suggestions.Count == 0)
            {
                _state.RecallPrevious(adapter, context);
            }
            else
            {
                _state.MoveSuggestionSelection(-1, adapter);
            }
        }
        else if (input.IsKeyPressed(Keys.Down))
        {
            if (control || _state.Suggestions.Count == 0)
            {
                _state.RecallNext(adapter, context);
            }
            else
            {
                _state.MoveSuggestionSelection(1, adapter);
            }
        }

        if (input.IsKeyPressed(Keys.Tab))
        {
            if (_state.Suggestions.Count > 0)
            {
                _state.ApplySelectedSuggestion(adapter, context);
            }
            else
            {
                _state.InsertSelectedCommand(adapter, context);
            }
        }

        if (input.IsKeyPressed(Keys.Enter))
        {
            SubmitAndRefresh(executeCommand, adapter, context);
        }
    }

    private void HandleGamePad(
        GamePadState current,
        GamePadState previous,
        DeveloperConsoleAdapter adapter,
        CommandContext context,
        Func<string, CommandResult> executeCommand)
    {
        if (IsPressed(current, previous, Buttons.LeftShoulder))
        {
            _state.MoveCategory(-1, adapter);
        }
        else if (IsPressed(current, previous, Buttons.RightShoulder))
        {
            _state.MoveCategory(1, adapter);
        }

        if (IsPressed(current, previous, Buttons.DPadUp))
        {
            if (_state.Focus == DeveloperConsoleFocus.CommandInput && _state.Suggestions.Count > 0)
            {
                _state.MoveSuggestionSelection(-1, adapter);
            }
            else
            {
                _state.MoveCommandSelection(-1, adapter);
            }
        }
        else if (IsPressed(current, previous, Buttons.DPadDown))
        {
            if (_state.Focus == DeveloperConsoleFocus.CommandInput && _state.Suggestions.Count > 0)
            {
                _state.MoveSuggestionSelection(1, adapter);
            }
            else
            {
                _state.MoveCommandSelection(1, adapter);
            }
        }

        if (IsPressed(current, previous, Buttons.A))
        {
            if (_state.Focus == DeveloperConsoleFocus.CommandInput && _state.Suggestions.Count > 0)
            {
                _state.ApplySelectedSuggestion(adapter, context);
            }
            else
            {
                _state.InsertSelectedCommand(adapter, context);
            }
        }

        if (IsPressed(current, previous, Buttons.X))
        {
            RunCurrentOrSelected(executeCommand, adapter, context);
        }

        if (IsPressed(current, previous, Buttons.LeftStick))
        {
            _state.SetFocus(_state.Focus == DeveloperConsoleFocus.CommandInput
                ? DeveloperConsoleFocus.PaletteSearch
                : DeveloperConsoleFocus.CommandInput);
        }
    }

    private void HandleMouse(
        InputManager input,
        DeveloperConsoleAdapter adapter,
        CommandContext context,
        Func<string, CommandResult> executeCommand)
    {
        var mouse = input.MousePosition;
        if (input.ScrollDelta != 0)
        {
            var direction = input.ScrollDelta > 0 ? -1 : 1;
            if (_commandListBounds.Contains(mouse))
            {
                _state.ScrollCommands(direction * 2);
            }
            else if (_historyBounds.Contains(mouse))
            {
                _state.ScrollHistory(-direction * Math.Max(1, _state.VisibleHistoryLineCount / 2));
            }
        }

        if (!input.IsLeftMousePressed && !input.IsRightMousePressed)
        {
            return;
        }

        if (_searchBounds.Contains(mouse))
        {
            _state.SetFocus(DeveloperConsoleFocus.PaletteSearch);
            return;
        }

        if (_inputBounds.Contains(mouse))
        {
            _state.SetFocus(DeveloperConsoleFocus.CommandInput);
            return;
        }

        if (_insertButtonBounds.Contains(mouse) || _detailInsertButtonBounds.Contains(mouse))
        {
            _state.InsertSelectedCommand(adapter, context);
            return;
        }

        if (_runButtonBounds.Contains(mouse) || _detailRunButtonBounds.Contains(mouse))
        {
            RunCurrentOrSelected(executeCommand, adapter, context);
            return;
        }

        if (_clearButtonBounds.Contains(mouse))
        {
            _state.ClearOutput();
            return;
        }

        for (var index = 0; index < _drawnCategoryCount; index++)
        {
            if (_categoryBounds[index].Contains(mouse))
            {
                _state.SetCategory(_state.Categories[index], adapter);
                return;
            }
        }
        for (var index = 0; index < _drawnCommandCount; index++)
        {
            if (!_commandBounds[index].Contains(mouse))
            {
                continue;
            }

            _state.SelectVisibleCommand(index, adapter);
            if (input.IsRightMousePressed)
            {
                _state.InsertSelectedCommand(adapter, context);
            }

            return;
        }

        for (var index = 0; index < _drawnSuggestionCount; index++)
        {
            if (_suggestionBounds[index].Contains(mouse))
            {
                _state.SelectSuggestion(
                    _state.FirstVisibleSuggestionIndex + index,
                    adapter,
                    context,
                    apply: true);
                return;
            }
        }

        for (var index = 0; index < OutputFilters.Length; index++)
        {
            if (_filterBounds[index].Contains(mouse))
            {
                _state.ToggleOutputFilter(OutputFilters[index]);
                return;
            }
        }
    }

    private void RunCurrentOrSelected(
        Func<string, CommandResult> executeCommand,
        DeveloperConsoleAdapter adapter,
        CommandContext context)
    {
        if (_state.Input.Trim().Length == 0)
        {
            var selected = _state.SelectedCommand;
            _state.InsertSelectedCommand(adapter, context);
            if (selected is null || selected.RequiredArgumentCount > 0)
            {
                return;
            }
        }

        SubmitAndRefresh(executeCommand, adapter, context);
    }

    private void SubmitAndRefresh(
        Func<string, CommandResult> executeCommand,
        DeveloperConsoleAdapter adapter,
        CommandContext context)
    {
        _state.Submit(executeCommand);
        _state.RefreshSuggestions(adapter, context, true);
    }

    private void DrawHeader(RenderContext context, UiPalette palette)
    {
        var header = new Rectangle(_panelBounds.X + 2, _panelBounds.Y + 2, _panelBounds.Width - 4, 38);
        UiTheme.DrawRoundedRectangle(context, header, UiTheme.WithAlpha(palette.Surface, 0.98f), 7);
        context.DebugText.Draw(new Vector2(header.X + 12, header.Y + 11), "DEVELOPER WORKBENCH", palette.Text, 2);
        context.DebugText.Draw(new Vector2(header.X + 250, header.Y + 13), "COMMAND PALETTE  /  LIVE CONSOLE", palette.TextMuted, 1);

        _closeButtonBounds = new Rectangle(header.Right - 32, header.Y + 7, 24, 24);
        _helpButtonBounds = new Rectangle(_closeButtonBounds.X - 30, header.Y + 7, 24, 24);
        var mouse = Mouse.GetState().Position;
        UiTheme.DrawButton(context, _helpButtonBounds, palette, _state.IsHelpExpanded, _helpButtonBounds.Contains(mouse));
        UiTheme.DrawButton(context, _closeButtonBounds, palette, false, _closeButtonBounds.Contains(mouse));
        context.DebugText.Draw(new Vector2(_helpButtonBounds.X + 8, _helpButtonBounds.Y + 8), "?", palette.Warning, 1);
        context.DebugText.Draw(new Vector2(_closeButtonBounds.X + 8, _closeButtonBounds.Y + 8), "X", palette.Text, 1);
    }

    private void DrawWorkspace(RenderContext context, Rectangle bounds, UiPalette palette, bool compact)
    {
        var categoryWidth = compact ? 104 : 132;
        var commandWidth = compact ? Math.Min(278, bounds.Width / 2) : Math.Min(340, bounds.Width / 3);
        var categories = new Rectangle(bounds.X, bounds.Y, categoryWidth, bounds.Height);
        var commands = new Rectangle(categories.Right + 6, bounds.Y, commandWidth, bounds.Height);
        var details = new Rectangle(commands.Right + 6, bounds.Y, bounds.Right - commands.Right - 6, bounds.Height);

        DrawCategories(context, categories, palette);
        DrawCommandPalette(context, commands, palette);
        DrawCommandDetails(context, details, palette, compact);
    }

    private void DrawCategories(RenderContext context, Rectangle bounds, UiPalette palette)
    {
        UiTheme.DrawRoundedRectangle(context, bounds, UiTheme.WithAlpha(palette.Surface, 0.78f), 6);
        context.DebugText.Draw(new Vector2(bounds.X + 8, bounds.Y + 8), "CATEGORIES", palette.TextMuted, 1);
        _drawnCategoryCount = Math.Min(_categoryBounds.Length, _state.Categories.Count);
        var y = bounds.Y + 26;
        for (var index = 0; index < _drawnCategoryCount; index++)
        {
            var row = new Rectangle(bounds.X + 5, y, bounds.Width - 10, 24);
            _categoryBounds[index] = row;
            var selected = _state.Categories[index] == _state.SelectedCategory;
            var hovered = row.Contains(Mouse.GetState().Position);
            UiTheme.DrawButton(context, row, palette, selected, hovered);
            context.DebugText.Draw(new Vector2(row.X + 8, row.Y + 8), _textCache.Clip(CategoryLabels[index], CharacterCapacity(row.Width - 16)), selected ? palette.Warning : palette.TextMuted, 1);
            y += 27;
        }
    }

    private void DrawCommandPalette(RenderContext context, Rectangle bounds, UiPalette palette)
    {
        UiTheme.DrawRoundedRectangle(context, bounds, UiTheme.WithAlpha(palette.Surface, 0.78f), 6);
        _searchBounds = new Rectangle(bounds.X + 6, bounds.Y + 6, bounds.Width - 12, 30);
        var searchFocused = _state.Focus == DeveloperConsoleFocus.PaletteSearch;
        UiTheme.DrawButton(context, _searchBounds, palette, searchFocused, _searchBounds.Contains(Mouse.GetState().Position));
        if (!string.Equals(_lastSearchText, _state.SearchQuery, StringComparison.Ordinal))
        {
            _lastSearchText = _state.SearchQuery;
            _searchDisplayText = _state.SearchQuery.Length == 0 ? "SEARCH COMMANDS..." : _state.SearchQuery + "_";
        }

        context.DebugText.Draw(new Vector2(_searchBounds.X + 9, _searchBounds.Y + 10), _textCache.Clip(_searchDisplayText, CharacterCapacity(_searchBounds.Width - 18)), searchFocused ? palette.Text : palette.TextMuted, 1);

        _commandListBounds = new Rectangle(bounds.X + 5, _searchBounds.Bottom + 5, bounds.Width - 10, bounds.Bottom - _searchBounds.Bottom - 10);
        var rowHeight = 31;
        _state.SetVisibleCommandCapacity(Math.Min(MaximumCommandRows, Math.Max(1, _commandListBounds.Height / rowHeight)));
        _drawnCommandCount = Math.Min(MaximumCommandRows, _state.VisibleCommandCount);
        for (var index = 0; index < _drawnCommandCount; index++)
        {
            var entry = _state.GetVisibleCommand(index);
            var row = new Rectangle(_commandListBounds.X, _commandListBounds.Y + index * rowHeight, _commandListBounds.Width, rowHeight - 2);
            _commandBounds[index] = row;
            var selected = _state.IsVisibleCommandSelected(index);
            UiTheme.DrawButton(context, row, palette, selected, row.Contains(Mouse.GetState().Position));
            context.DebugText.Draw(new Vector2(row.X + 8, row.Y + 7), _textCache.Clip(entry.InsertText, CharacterCapacity(row.Width - 16)), selected ? palette.Text : palette.Accent, 1);
            context.DebugText.Draw(new Vector2(row.X + 8, row.Y + 18), _textCache.Clip(entry.Description, CharacterCapacity(row.Width - 16)), palette.TextMuted, 1);
        }

        if (_state.HasMoreCommandsAbove)
        {
            context.DebugText.Draw(new Vector2(bounds.Right - 34, _commandListBounds.Y), "UP", palette.Warning, 1);
        }

        if (_state.HasMoreCommandsBelow)
        {
            context.DebugText.Draw(new Vector2(bounds.Right - 46, bounds.Bottom - 12), "MORE", palette.Warning, 1);
        }
    }

    private void DrawCommandDetails(RenderContext context, Rectangle bounds, UiPalette palette, bool compact)
    {
        UiTheme.DrawRoundedRectangle(context, bounds, UiTheme.WithAlpha(palette.Surface, 0.78f), 6);
        if (_state.IsHelpExpanded)
        {
            _detailInsertButtonBounds = Rectangle.Empty;
            _detailRunButtonBounds = Rectangle.Empty;
            context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 10), "COMMAND HELP  /  F1 TO CLOSE", palette.Warning, 1);
            var helpY = bounds.Y + 32;
            var maximumHelpLines = Math.Max(1, (bounds.Height - 42) / 16);
            if (_state.HelpLines.Count == 0)
            {
                context.DebugText.Draw(
                    new Vector2(bounds.X + 12, helpY),
                    "Select a command to inspect its schema and examples.",
                    palette.TextMuted,
                    1);
                return;
            }

            for (var index = 0; index < Math.Min(maximumHelpLines, _state.HelpLines.Count); index++)
            {
                context.DebugText.Draw(
                    new Vector2(bounds.X + 12, helpY),
                    _textCache.Clip(_state.HelpLines[index], CharacterCapacity(bounds.Width - 24)),
                    index == 0 ? palette.Accent : palette.TextMuted,
                    1);
                helpY += 16;
            }

            return;
        }
        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 10), "COMMAND DETAILS", palette.TextMuted, 1);
        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 30), _textCache.Clip(_state.Signature, CharacterCapacity(bounds.Width - 20)), palette.Warning, 1);
        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 49), _textCache.Clip(_state.Description, CharacterCapacity(bounds.Width - 20)), palette.Text, 1);

        context.DebugText.Draw(
            new Vector2(bounds.X + 10, bounds.Y + 67),
            _textCache.Clip(_state.DispatchLabel, CharacterCapacity(bounds.Width - 20)),
            _state.DispatchLabel.StartsWith("REQUEST", StringComparison.Ordinal) ? palette.Accent : palette.TextMuted,
            1);

        var y = bounds.Y + 88;
        var maxArguments = compact ? 3 : 6;
        for (var index = 0; index < Math.Min(maxArguments, _state.ArgumentHints.Count); index++)
        {
            context.DebugText.Draw(new Vector2(bounds.X + 12, y), _textCache.Clip(_state.ArgumentHints[index], CharacterCapacity(bounds.Width - 24)), palette.TextMuted, 1);
            y += 17;
        }

        if (_state.Examples.Count > 0)
        {
            y += 5;
            context.DebugText.Draw(new Vector2(bounds.X + 10, y), "EXAMPLES", palette.Accent, 1);
            y += 17;
            var maximum = compact ? 1 : 3;
            for (var index = 0; index < Math.Min(maximum, _state.Examples.Count); index++)
            {
                context.DebugText.Draw(new Vector2(bounds.X + 12, y), _textCache.Clip(_state.Examples[index], CharacterCapacity(bounds.Width - 24)), palette.TextMuted, 1);
                y += 17;
            }
        }

        _detailInsertButtonBounds = new Rectangle(bounds.X + 10, bounds.Bottom - 36, Math.Min(112, (bounds.Width - 26) / 2), 28);
        _detailRunButtonBounds = new Rectangle(_detailInsertButtonBounds.Right + 6, bounds.Bottom - 36, Math.Min(112, bounds.Right - _detailInsertButtonBounds.Right - 16), 28);
        UiTheme.DrawButton(context, _detailInsertButtonBounds, palette, false, _detailInsertButtonBounds.Contains(Mouse.GetState().Position));
        UiTheme.DrawButton(context, _detailRunButtonBounds, palette, true, _detailRunButtonBounds.Contains(Mouse.GetState().Position));
        context.DebugText.Draw(new Vector2(_detailInsertButtonBounds.X + 18, _detailInsertButtonBounds.Y + 9), "INSERT", palette.Text, 1);
        context.DebugText.Draw(new Vector2(_detailRunButtonBounds.X + 24, _detailRunButtonBounds.Y + 9), "RUN", palette.Warning, 1);
    }

    private void DrawOutputFilters(RenderContext context, Rectangle bounds, UiPalette palette)
    {
        context.DebugText.Draw(new Vector2(bounds.X + 4, bounds.Y + 8), "OUTPUT", palette.TextMuted, 1);
        var x = bounds.X + 62;
        for (var index = 0; index < OutputFilters.Length; index++)
        {
            var width = index == 0 ? 42 : 48;
            var button = new Rectangle(x, bounds.Y + 1, width, 22);
            _filterBounds[index] = button;
            var active = OutputFilters[index] == DeveloperConsoleOutputFilter.All
                ? _state.OutputFilter == DeveloperConsoleOutputFilter.All
                : (_state.OutputFilter & OutputFilters[index]) != 0;
            UiTheme.DrawButton(context, button, palette, active, button.Contains(Mouse.GetState().Position));
            context.DebugText.Draw(new Vector2(button.X + 7, button.Y + 8), OutputFilterLabels[index], active ? palette.Text : palette.TextMuted, 1);
            x += width + 4;
        }

        _clearButtonBounds = new Rectangle(bounds.Right - 68, bounds.Y + 1, 66, 22);
        UiTheme.DrawButton(context, _clearButtonBounds, palette, false, _clearButtonBounds.Contains(Mouse.GetState().Position));
        context.DebugText.Draw(new Vector2(_clearButtonBounds.X + 12, _clearButtonBounds.Y + 8), "CLEAR", palette.TextMuted, 1);
    }

    private void DrawHistory(RenderContext context, Rectangle bounds, UiPalette palette)
    {
        UiTheme.DrawRoundedRectangle(context, bounds, UiTheme.WithAlpha(palette.Backdrop, 0.78f), 5);
        _state.SetVisibleHistoryLineCapacity(Math.Max(1, (bounds.Height - 8) / 16));
        var y = bounds.Y + 5;
        for (var index = 0; index < _state.VisibleHistoryLineCount; index++)
        {
            var line = _state.GetVisibleHistoryLine(index);
            context.DebugText.Draw(
                new Vector2(bounds.X + 7, y),
                _textCache.Clip(line.Text, CharacterCapacity(bounds.Width - 14)),
                SeverityColor(line.Severity, palette),
                1);
            y += 16;
        }

        if (_state.HasOlderHistory)
        {
            context.DebugText.Draw(new Vector2(bounds.Right - 56, bounds.Y + 5), "MORE UP", palette.Warning, 1);
        }
    }

    private void DrawInput(RenderContext context, UiPalette palette)
    {
        var focused = _state.Focus == DeveloperConsoleFocus.CommandInput;
        UiTheme.DrawButton(context, _inputBounds, palette, focused, _inputBounds.Contains(Mouse.GetState().Position));
        if (!string.Equals(_lastInputText, _state.Input, StringComparison.Ordinal))
        {
            _lastInputText = _state.Input;
            _inputDisplayText = _state.Input.Length == 0 ? "> ENTER COMMAND..." : "> " + _state.Input + "_";
        }

        context.DebugText.Draw(new Vector2(_inputBounds.X + 9, _inputBounds.Y + 12), _textCache.Clip(_inputDisplayText, CharacterCapacity(_inputBounds.Width - 18)), focused ? palette.Text : palette.TextMuted, 1);

        UiTheme.DrawButton(context, _insertButtonBounds, palette, false, _insertButtonBounds.Contains(Mouse.GetState().Position));
        UiTheme.DrawButton(context, _runButtonBounds, palette, true, _runButtonBounds.Contains(Mouse.GetState().Position));
        context.DebugText.Draw(new Vector2(_insertButtonBounds.X + 17, _insertButtonBounds.Y + 12), "INSERT", palette.Text, 1);
        context.DebugText.Draw(new Vector2(_runButtonBounds.X + 27, _runButtonBounds.Y + 12), "RUN", palette.Warning, 1);
    }

    private void DrawSuggestions(RenderContext context, UiPalette palette)
    {
        _drawnSuggestionCount = Math.Min(MaximumSuggestionRows, _state.VisibleSuggestionCount);
        if (_drawnSuggestionCount == 0 || _state.Focus != DeveloperConsoleFocus.CommandInput || _state.Input.Length == 0)
        {
            return;
        }

        var rowHeight = 24;
        var popup = new Rectangle(
            _inputBounds.X,
            _inputBounds.Y - _drawnSuggestionCount * rowHeight - 3,
            _inputBounds.Width,
            _drawnSuggestionCount * rowHeight);
        UiTheme.DrawRoundedRectangle(context, popup, UiTheme.WithAlpha(palette.Backdrop, 0.98f), 5);
        UiTheme.DrawRoundedBorder(context, popup, palette.AccentSoft, 5, 1);
        for (var index = 0; index < _drawnSuggestionCount; index++)
        {
            var suggestionIndex = _state.FirstVisibleSuggestionIndex + index;
            var suggestion = _state.Suggestions[suggestionIndex];
            var row = new Rectangle(popup.X + 2, popup.Y + index * rowHeight + 1, popup.Width - 4, rowHeight - 2);
            _suggestionBounds[index] = row;
            var selected = suggestionIndex == _state.SelectedSuggestionIndex;
            UiTheme.DrawButton(context, row, palette, selected, row.Contains(Mouse.GetState().Position));
            context.DebugText.Draw(new Vector2(row.X + 8, row.Y + 8), _textCache.Clip(suggestion.DisplayText, CharacterCapacity(Math.Min(220, row.Width / 3))), selected ? palette.Warning : palette.Accent, 1);
            context.DebugText.Draw(new Vector2(row.X + Math.Min(230, row.Width / 3), row.Y + 8), _textCache.Clip(suggestion.Description, CharacterCapacity(row.Width - Math.Min(238, row.Width / 3))), palette.TextMuted, 1);
        }
    }

    private void DrawFooter(RenderContext context, Rectangle bounds, UiPalette palette)
    {
        var hint = _state.IsCompactViewport
            ? "CTRL+F SEARCH  TAB COMPLETE  ENTER RUN  F1 HELP"
            : "MOUSE: SELECT / INSERT / RUN   KEYBOARD: CTRL+F SEARCH, TAB COMPLETE, CTRL+UP HISTORY   GAMEPAD: LB/RB CATEGORY, A INSERT, X RUN";
        context.DebugText.Draw(new Vector2(bounds.X + 2, bounds.Y + 6), _textCache.Clip(hint, CharacterCapacity(bounds.Width - 4)), palette.TextMuted, 1);
    }

    private static bool IsPressed(GamePadState current, GamePadState previous, Buttons button) =>
        current.IsButtonDown(button) && previous.IsButtonUp(button);

    private static int CharacterCapacity(int pixelWidth) => Math.Max(1, pixelWidth / 6);

    private static Color SeverityColor(DeveloperConsoleSeverity severity, UiPalette palette) =>
        severity switch
        {
            DeveloperConsoleSeverity.Command => palette.Text,
            DeveloperConsoleSeverity.Information => palette.TextMuted,
            DeveloperConsoleSeverity.Success => new Color(116, 214, 143),
            DeveloperConsoleSeverity.Request => palette.Accent,
            DeveloperConsoleSeverity.Error => palette.Danger,
            _ => palette.TextMuted
        };

    private sealed class ConsoleTextClipCache
    {
        private readonly string?[] _sources;
        private readonly string?[] _results;
        private readonly int[] _lengths;
        private int _cursor;

        public ConsoleTextClipCache(int capacity)
        {
            _sources = new string[capacity];
            _results = new string[capacity];
            _lengths = new int[capacity];
        }

        public string Clip(string value, int maximum)
        {
            if (value.Length <= maximum)
            {
                return value;
            }

            for (var index = 0; index < _sources.Length; index++)
            {
                if (_lengths[index] == maximum && ReferenceEquals(_sources[index], value))
                {
                    return _results[index]!;
                }
            }

            var result = maximum <= 1 ? ">" : value[..(maximum - 1)] + ">";
            _sources[_cursor] = value;
            _results[_cursor] = result;
            _lengths[_cursor] = maximum;
            _cursor = (_cursor + 1) % _sources.Length;
            return result;
        }
    }
}
