using Game.Client.DeveloperTools;
using Game.Client.Input;
using Game.Client.Rendering;
using Game.Core.Commands;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.UI;

public sealed class DebugConsoleOverlay
{
    private readonly DeveloperConsoleModel _state;
    private Rectangle _helpButtonBounds;
    private Rectangle _closeButtonBounds;

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

    public void OnTextInput(char character)
    {
        _state.AppendCharacter(character);
    }

    public void Draw(RenderContext context)
    {
        if (!_state.IsOpen)
        {
            return;
        }

        _state.ConfigureViewport(context.ViewportBounds.Width, context.ViewportBounds.Height);
        var palette = UiTheme.Resolve();
        var compact = _state.IsCompactViewport;
        var margin = compact ? 8 : 18;
        var availableWidth = Math.Max(160, context.ViewportBounds.Width - margin * 2);
        var availableHeight = Math.Max(112, context.ViewportBounds.Height - margin * 2);
        var width = Math.Min(compact ? 720 : 900, availableWidth);
        var height = Math.Min(compact ? 292 : 410, availableHeight);
        var bounds = new Rectangle(
            context.ViewportBounds.X + (context.ViewportBounds.Width - width) / 2,
            context.ViewportBounds.Y + margin,
            width,
            height);

        UiTheme.DrawPanel(context, bounds, palette, 0.98f);
        DrawHeader(context, bounds, palette, compact);

        var footerHeight = 20;
        var helpHeight = _state.IsHelpExpanded ? (compact ? 42 : 58) : 22;
        var suggestionHeight = _state.VisibleSuggestionCount * 22;
        var inputHeight = 30;
        var historyTop = bounds.Y + 38;
        var historyHeight = Math.Max(26, bounds.Height - 38 - inputHeight - suggestionHeight - helpHeight - footerHeight - 10);
        _state.SetVisibleHistoryLineCapacity(Math.Max(1, historyHeight / 16));

        DrawHistory(context, new Rectangle(bounds.X + 10, historyTop, bounds.Width - 20, historyHeight), palette);
        var inputBounds = new Rectangle(bounds.X + 9, historyTop + historyHeight + 2, bounds.Width - 18, inputHeight);
        DrawInput(context, inputBounds, palette);
        var suggestionY = inputBounds.Bottom + 2;
        DrawSuggestions(context, new Rectangle(inputBounds.X, suggestionY, inputBounds.Width, suggestionHeight), palette, compact);
        var helpY = suggestionY + suggestionHeight + 2;
        DrawHelp(context, new Rectangle(inputBounds.X, helpY, inputBounds.Width, helpHeight), palette, compact);
        DrawFooter(context, new Rectangle(bounds.X + 10, bounds.Bottom - footerHeight, bounds.Width - 20, footerHeight), palette, compact);
        DrawControlTooltip(context, bounds, palette);
    }

    private bool UpdateCore(
        InputManager input,
        Func<string, CommandResult> executeCommand,
        DeveloperConsoleAdapter? adapter,
        CommandContext? context,
        string toggleBinding)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.IsBindingPressed(toggleBinding))
        {
            _state.Toggle();
            if (_state.IsOpen && adapter is not null && context is not null)
            {
                _state.RefreshSuggestions(adapter, context, force: true);
            }

            return true;
        }

        if (!_state.IsOpen)
        {
            return false;
        }

        if (input.IsKeyPressed(Keys.Escape) ||
            (input.IsLeftMousePressed && _closeButtonBounds.Contains(input.MousePosition)))
        {
            _state.Close();
            return true;
        }

        if (input.IsKeyPressed(Keys.F1) ||
            (input.IsLeftMousePressed && _helpButtonBounds.Contains(input.MousePosition)))
        {
            _state.ToggleHelp();
        }

        if (input.IsKeyPressed(Keys.Back))
        {
            _state.Backspace();
        }

        if (input.ScrollDelta > 0 || input.IsKeyPressed(Keys.PageUp))
        {
            _state.ScrollHistory(Math.Max(1, _state.VisibleHistoryLineCount - 1));
        }
        else if (input.ScrollDelta < 0 || input.IsKeyPressed(Keys.PageDown))
        {
            _state.ScrollHistory(-Math.Max(1, _state.VisibleHistoryLineCount - 1));
        }

        if (adapter is not null && context is not null)
        {
            _state.RefreshSuggestions(adapter, context);
            var controlDown = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
            if (input.IsKeyPressed(Keys.Up))
            {
                if (controlDown || _state.Suggestions.Count == 0)
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
                if (controlDown || _state.Suggestions.Count == 0)
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
                _state.ApplySelectedSuggestion(adapter, context);
            }
        }

        if (input.IsKeyPressed(Keys.Enter))
        {
            _state.Submit(executeCommand);
            if (adapter is not null && context is not null)
            {
                _state.RefreshSuggestions(adapter, context, force: true);
            }
        }

        return true;
    }

    private void DrawHeader(RenderContext context, Rectangle bounds, UiPalette palette, bool compact)
    {
        var header = new Rectangle(bounds.X + 1, bounds.Y + 2, bounds.Width - 2, 32);
        UiTheme.DrawHeader(context, header, palette);
        context.DebugText.Draw(new Vector2(header.X + 10, header.Y + 9), compact ? "CONSOLE" : "DEVELOPER CONSOLE", palette.Text, compact ? 1 : 2);
        _closeButtonBounds = new Rectangle(header.Right - 27, header.Y + 5, 21, 20);
        _helpButtonBounds = new Rectangle(_closeButtonBounds.X - 25, header.Y + 5, 21, 20);
        var mouse = Mouse.GetState().Position;
        UiTheme.DrawButton(context, _helpButtonBounds, palette, _state.IsHelpExpanded, _helpButtonBounds.Contains(mouse));
        UiTheme.DrawButton(context, _closeButtonBounds, palette, selected: false, _closeButtonBounds.Contains(mouse));
        context.DebugText.Draw(new Vector2(_helpButtonBounds.X + 8, _helpButtonBounds.Y + 7), "?", palette.Warning, 1);
        context.DebugText.Draw(new Vector2(_closeButtonBounds.X + 8, _closeButtonBounds.Y + 7), "X", palette.TextMuted, 1);
    }

    private void DrawHistory(RenderContext context, Rectangle bounds, UiPalette palette)
    {
        context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(palette.Backdrop, 0.72f));
        var y = bounds.Y + 3;
        for (var index = 0; index < _state.VisibleHistoryLineCount; index++)
        {
            var line = _state.GetVisibleHistoryLine(index);
            context.DebugText.Draw(
                new Vector2(bounds.X + 5, y),
                TrimLine(line.Text, CharacterCapacity(bounds.Width - 10)),
                SeverityColor(line.Severity, palette),
                1);
            y += 16;
        }

        if (_state.HasOlderHistory)
        {
            context.DebugText.Draw(new Vector2(bounds.Right - 54, bounds.Y + 3), "MORE ^", palette.Warning, 1);
        }
    }

    private void DrawInput(RenderContext context, Rectangle bounds, UiPalette palette)
    {
        UiTheme.DrawButton(context, bounds, palette, selected: true, hovered: false);
        context.DebugText.Draw(
            new Vector2(bounds.X + 8, bounds.Y + 10),
            TrimLine("> " + _state.Input + "_", CharacterCapacity(bounds.Width - 16)),
            palette.Text,
            1);
    }

    private void DrawSuggestions(RenderContext context, Rectangle bounds, UiPalette palette, bool compact)
    {
        for (var index = 0; index < _state.VisibleSuggestionCount; index++)
        {
            var suggestionIndex = _state.FirstVisibleSuggestionIndex + index;
            var suggestion = _state.Suggestions[suggestionIndex];
            var row = new Rectangle(bounds.X, bounds.Y + index * 22, bounds.Width, 20);
            var selected = suggestionIndex == _state.SelectedSuggestionIndex;
            UiTheme.DrawButton(context, row, palette, selected, hovered: false);
            context.DebugText.Draw(new Vector2(row.X + 8, row.Y + 7), selected ? ">" : " ", selected ? palette.Warning : palette.TextMuted, 1);
            var labelWidth = compact ? Math.Min(150, row.Width / 2) : Math.Min(230, row.Width / 3);
            context.DebugText.Draw(
                new Vector2(row.X + 20, row.Y + 7),
                TrimLine(suggestion.DisplayText, CharacterCapacity(labelWidth - 24)),
                selected ? palette.Text : palette.Accent,
                1);
            context.DebugText.Draw(
                new Vector2(row.X + labelWidth, row.Y + 7),
                TrimLine(suggestion.Description, CharacterCapacity(row.Width - labelWidth - 8)),
                palette.TextMuted,
                1);
        }
    }

    private void DrawHelp(RenderContext context, Rectangle bounds, UiPalette palette, bool compact)
    {
        if (bounds.Height <= 0)
        {
            return;
        }

        context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(palette.Surface, 0.78f));
        var signature = _state.Signature.Length == 0 ? "F1 HELP  /HELP COMMANDS" : _state.Signature;
        context.DebugText.Draw(new Vector2(bounds.X + 7, bounds.Y + 6), TrimLine(signature, CharacterCapacity(bounds.Width - 14)), palette.Warning, 1);
        if (!_state.IsHelpExpanded)
        {
            return;
        }

        var maxLines = compact ? 1 : 2;
        var lineCount = Math.Min(maxLines, _state.HelpLines.Count);
        for (var index = 0; index < lineCount; index++)
        {
            context.DebugText.Draw(
                new Vector2(bounds.X + 7, bounds.Y + 20 + index * 14),
                TrimLine(_state.HelpLines[index], CharacterCapacity(bounds.Width - 14)),
                palette.TextMuted,
                1);
        }
    }

    private void DrawFooter(RenderContext context, Rectangle bounds, UiPalette palette, bool compact)
    {
        var hint = compact
            ? "TAB COMPLETE  UP/DOWN SELECT  PGUP/PGDN SCROLL"
            : "TAB COMPLETE   UP/DOWN SELECT   CTRL+UP/DOWN HISTORY   WHEEL/PGUP/PGDN SCROLL";
        context.DebugText.Draw(new Vector2(bounds.X, bounds.Y + 6), TrimLine(hint, CharacterCapacity(bounds.Width)), palette.TextMuted, 1);
    }

    private void DrawControlTooltip(RenderContext context, Rectangle panel, UiPalette palette)
    {
        var mouse = Mouse.GetState().Position;
        var text = _helpButtonBounds.Contains(mouse)
            ? "HELP (F1)"
            : _closeButtonBounds.Contains(mouse)
                ? "CLOSE (ESC)"
                : string.Empty;
        if (text.Length == 0)
        {
            return;
        }

        var width = text.Length * 6 + 12;
        var x = Math.Clamp(mouse.X - width, panel.X + 4, panel.Right - width - 4);
        var bounds = new Rectangle(x, _helpButtonBounds.Bottom + 4, width, 20);
        context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(palette.Backdrop, 0.98f));
        UiTheme.DrawBorder(context, bounds, palette.AccentSoft, 1);
        context.DebugText.Draw(new Vector2(bounds.X + 6, bounds.Y + 7), text, palette.Text, 1);
    }

    private static int CharacterCapacity(int pixelWidth)
    {
        return Math.Max(1, pixelWidth / 6);
    }

    private static string TrimLine(string value, int maximum)
    {
        if (maximum <= 1)
        {
            return value.Length == 0 ? string.Empty : ">";
        }

        return value.Length <= maximum ? value : value[..(maximum - 1)] + ">";
    }

    private static Color SeverityColor(DeveloperConsoleSeverity severity, UiPalette palette)
    {
        return severity switch
        {
            DeveloperConsoleSeverity.Command => palette.Text,
            DeveloperConsoleSeverity.Information => palette.TextMuted,
            DeveloperConsoleSeverity.Success => new Color(116, 214, 143),
            DeveloperConsoleSeverity.Request => palette.Accent,
            DeveloperConsoleSeverity.Error => palette.Danger,
            _ => palette.TextMuted
        };
    }
}
