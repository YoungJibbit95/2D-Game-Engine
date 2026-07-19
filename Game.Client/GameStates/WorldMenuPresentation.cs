using System.Globalization;
using Game.Client.Rendering;
using Game.Client.UI;
using Microsoft.Xna.Framework;

namespace Game.Client.GameStates;

internal readonly record struct WorldSelectLayout(
    Rectangle Panel,
    Rectangle List,
    Rectangle CreateButton,
    Rectangle RefreshButton,
    Rectangle BackButton,
    int ContentInset,
    int RowHeight,
    int RowGap,
    int VisibleRowCount,
    bool StackedMetadata,
    bool ShowCreatedDate,
    bool Compact)
{
    public Rectangle GetRowBounds(int visibleIndex, bool reserveScrollRail)
    {
        var rightInset = reserveScrollRail ? 18 : 10;
        return new Rectangle(
            List.X + 10,
            List.Y + 8 + visibleIndex * (RowHeight + RowGap),
            Math.Max(1, List.Width - 10 - rightInset),
            RowHeight);
    }
}

internal readonly record struct CreateWorldLayout(
    Rectangle Panel,
    Rectangle NameField,
    Rectangle SeedField,
    Rectangle CreateButton,
    Rectangle RandomButton,
    Rectangle BackButton,
    Rectangle ProfileCard,
    int ContentInset,
    bool StackedFieldLabels,
    bool Compact);

internal static class WorldMenuLayoutPlanner
{
    public static WorldSelectLayout PlanWorldSelect(Rectangle viewport, int entryCount, int offsetY = 0)
    {
        var panel = CreatePanel(viewport, maxWidth: 760, maxHeight: 520, offsetY);
        var compact = panel.Width < 620 || panel.Height < 400;
        var inset = panel.Width < 360 ? 10 : compact ? 14 : 22;
        var preferredHeader = panel.Height < 300 ? 54 : compact ? 68 : 82;
        var headerHeight = Math.Min(preferredHeader, Math.Max(1, panel.Height / 3));
        var preferredFooter = panel.Height < 300 ? 48 : compact ? 58 : 76;
        var footerHeight = Math.Min(preferredFooter, Math.Max(1, (panel.Height - headerHeight) / 2));
        var list = new Rectangle(
            panel.X + inset,
            panel.Y + headerHeight,
            Math.Max(1, panel.Width - inset * 2),
            Math.Max(1, panel.Height - headerHeight - footerHeight));

        var buttonHeight = Math.Min(compact ? 32 : 36, Math.Max(1, footerHeight - inset));
        var buttonY = panel.Bottom - inset - buttonHeight;
        var gap = list.Width < 260 ? 4 : compact ? 6 : 10;
        var distributableWidth = Math.Max(3, list.Width - gap * 2);
        var createWidth = Math.Max(1, distributableWidth * 44 / 100);
        var refreshWidth = Math.Max(1, distributableWidth * 30 / 100);
        var backWidth = Math.Max(1, distributableWidth - createWidth - refreshWidth);
        var create = new Rectangle(list.X, buttonY, createWidth, buttonHeight);
        var refresh = new Rectangle(create.Right + gap, buttonY, refreshWidth, buttonHeight);
        var back = new Rectangle(refresh.Right + gap, buttonY, backWidth, buttonHeight);

        var listInnerHeight = Math.Max(1, list.Height - 16);
        var preferredRowHeight = compact ? 48 : 44;
        var rowHeight = Math.Min(preferredRowHeight, listInnerHeight);
        var rowGap = compact ? 4 : 6;
        var visibleRows = 1 + Math.Max(0, listInnerHeight - rowHeight) / Math.Max(1, rowHeight + rowGap);
        if (entryCount > 0)
        {
            visibleRows = Math.Min(entryCount, visibleRows);
        }

        var stackedMetadata = list.Width < 620;
        return new WorldSelectLayout(
            panel,
            list,
            create,
            refresh,
            back,
            inset,
            rowHeight,
            rowGap,
            Math.Max(1, visibleRows),
            stackedMetadata,
            ShowCreatedDate: list.Width >= 350,
            compact);
    }

    public static CreateWorldLayout PlanCreateWorld(Rectangle viewport, int offsetY = 0)
    {
        var panel = CreatePanel(viewport, maxWidth: 660, maxHeight: 440, offsetY);
        var compact = panel.Width < 540 || panel.Height < 390;
        var inset = panel.Width < 360 ? 10 : compact ? 14 : 24;
        var headerHeight = panel.Height < 280 ? 52 : compact ? 68 : 86;
        headerHeight = Math.Min(headerHeight, Math.Max(1, panel.Height / 3));

        var fieldGap = compact ? 8 : 12;
        var fieldHeight = compact ? 38 : 44;
        var availableBelowHeader = Math.Max(1, panel.Height - headerHeight - inset);
        if (fieldHeight * 2 + fieldGap + 44 > availableBelowHeader)
        {
            fieldHeight = Math.Max(24, (availableBelowHeader - fieldGap - 40) / 2);
        }

        var contentWidth = Math.Max(1, panel.Width - inset * 2);
        var nameField = new Rectangle(panel.X + inset, panel.Y + headerHeight, contentWidth, fieldHeight);
        var seedField = new Rectangle(panel.X + inset, nameField.Bottom + fieldGap, contentWidth, fieldHeight);
        var buttonGapY = compact ? 8 : 14;
        var buttonHeight = compact ? 32 : 36;
        if (seedField.Bottom + buttonGapY + buttonHeight > panel.Bottom - inset)
        {
            buttonHeight = Math.Max(24, panel.Bottom - inset - seedField.Bottom - buttonGapY);
        }

        var buttonY = seedField.Bottom + buttonGapY;
        var buttonGapX = contentWidth < 260 ? 4 : compact ? 6 : 8;
        var distributableWidth = Math.Max(3, contentWidth - buttonGapX * 2);
        var createWidth = Math.Max(1, distributableWidth * 40 / 100);
        var randomWidth = Math.Max(1, distributableWidth * 36 / 100);
        var backWidth = Math.Max(1, distributableWidth - createWidth - randomWidth);
        var create = new Rectangle(nameField.X, buttonY, createWidth, buttonHeight);
        var random = new Rectangle(create.Right + buttonGapX, buttonY, randomWidth, buttonHeight);
        var back = new Rectangle(random.Right + buttonGapX, buttonY, backWidth, buttonHeight);

        var profileBottom = panel.Bottom - inset;
        var profileTop = Math.Min(profileBottom, create.Bottom + (compact ? 6 : 12));
        var profile = new Rectangle(
            nameField.X,
            profileTop,
            contentWidth,
            Math.Max(1, profileBottom - profileTop));

        return new CreateWorldLayout(
            panel,
            nameField,
            seedField,
            create,
            random,
            back,
            profile,
            inset,
            StackedFieldLabels: panel.Width < 440,
            compact);
    }

    private static Rectangle CreatePanel(Rectangle viewport, int maxWidth, int maxHeight, int offsetY)
    {
        var width = Math.Max(1, viewport.Width);
        var height = Math.Max(1, viewport.Height);
        var margin = width < 480 || height < 320 ? 8 : 16;
        var panelWidth = Math.Max(1, Math.Min(maxWidth, width - Math.Min(width - 1, margin * 2)));
        var panelHeight = Math.Max(1, Math.Min(maxHeight, height - Math.Min(height - 1, margin * 2)));
        return new Rectangle(
            viewport.X + (width - panelWidth) / 2,
            viewport.Y + (height - panelHeight) / 2 + offsetY,
            panelWidth,
            panelHeight);
    }
}

internal sealed class WorldCreationForm
{
    public const int WorldNameIndex = 0;
    public const int SeedIndex = 1;
    public const int CreateIndex = 2;
    public const int RandomSeedIndex = 3;
    public const int BackIndex = 4;
    public const int OptionCount = 5;

    private bool _replaceNameOnInput = true;
    private bool _replaceSeedOnInput = true;

    public WorldCreationForm(int initialSeed)
    {
        WorldName = "New World";
        SeedText = initialSeed.ToString(CultureInfo.InvariantCulture);
        RefreshDisplayText();
    }

    public string WorldName { get; private set; }

    public string SeedText { get; private set; }

    public string WorldNameDisplay { get; private set; } = string.Empty;

    public string SeedDisplay { get; private set; } = string.Empty;

    public int SelectedIndex { get; private set; }

    public bool IsEditingField => SelectedIndex is WorldNameIndex or SeedIndex;

    public bool AcceptsLetterNavigation => !IsEditingField;

    public bool IsSeedValid => int.TryParse(SeedText, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    public bool IsOptionEnabled(int index)
    {
        return index is >= 0 and < OptionCount && (index != CreateIndex || IsSeedValid);
    }

    public void Focus(int index, bool selectContents = false)
    {
        SelectedIndex = Math.Clamp(index, 0, OptionCount - 1);
        if (!selectContents)
        {
            return;
        }

        if (SelectedIndex == WorldNameIndex)
        {
            _replaceNameOnInput = true;
        }
        else if (SelectedIndex == SeedIndex)
        {
            _replaceSeedOnInput = true;
        }
    }

    public void MoveSelection(int direction)
    {
        var next = SelectedIndex;
        for (var attempt = 0; attempt < OptionCount; attempt++)
        {
            next = ((next + direction) % OptionCount + OptionCount) % OptionCount;
            if (IsOptionEnabled(next))
            {
                Focus(next);
                return;
            }
        }
    }

    public bool Append(char character)
    {
        if (SelectedIndex == WorldNameIndex)
        {
            return AppendWorldName(character);
        }

        if (SelectedIndex == SeedIndex)
        {
            return AppendSeed(character);
        }

        return false;
    }

    public bool Backspace()
    {
        if (SelectedIndex == WorldNameIndex)
        {
            if (_replaceNameOnInput)
            {
                WorldName = string.Empty;
                _replaceNameOnInput = false;
            }
            else if (WorldName.Length > 0)
            {
                WorldName = WorldName[..^1];
            }
            else
            {
                return false;
            }

            RefreshDisplayText();
            return true;
        }

        if (SelectedIndex != SeedIndex)
        {
            return false;
        }

        if (_replaceSeedOnInput)
        {
            SeedText = string.Empty;
            _replaceSeedOnInput = false;
        }
        else if (SeedText.Length > 0)
        {
            SeedText = SeedText[..^1];
        }
        else
        {
            return false;
        }

        RefreshDisplayText();
        return true;
    }

    public void SetRandomSeed(int seed)
    {
        SeedText = Math.Max(1, seed).ToString(CultureInfo.InvariantCulture);
        _replaceSeedOnInput = true;
        RefreshDisplayText();
    }

    public bool TryGetWorld(out string name, out int seed)
    {
        name = string.IsNullOrWhiteSpace(WorldName) ? "New World" : WorldName.Trim();
        return int.TryParse(SeedText, NumberStyles.None, CultureInfo.InvariantCulture, out seed);
    }

    private bool AppendWorldName(char character)
    {
        if (char.IsControl(character) || !IsAllowedNameCharacter(character))
        {
            return false;
        }

        if (_replaceNameOnInput)
        {
            if (character == ' ')
            {
                return false;
            }

            WorldName = character.ToString();
            _replaceNameOnInput = false;
        }
        else
        {
            if (WorldName.Length >= 28)
            {
                return false;
            }

            WorldName += character;
        }

        RefreshDisplayText();
        return true;
    }

    private bool AppendSeed(char character)
    {
        if (!char.IsDigit(character))
        {
            return false;
        }

        if (_replaceSeedOnInput)
        {
            SeedText = character.ToString();
            _replaceSeedOnInput = false;
        }
        else
        {
            if (SeedText.Length >= 10)
            {
                return false;
            }

            SeedText += character;
        }

        RefreshDisplayText();
        return true;
    }

    private void RefreshDisplayText()
    {
        WorldNameDisplay = WorldMenuText.Normalize(WorldName);
        SeedDisplay = SeedText.Length == 0 ? "ENTER A SEED" : SeedText;
    }

    private static bool IsAllowedNameCharacter(char character)
    {
        return char.IsLetterOrDigit(character) || character is ' ' or '_' or '-';
    }
}

internal static class WorldMenuText
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        Span<char> normalized = value.Length <= 128 ? stackalloc char[value.Length] : new char[value.Length];
        for (var index = 0; index < value.Length; index++)
        {
            var character = char.ToUpperInvariant(value[index]);
            normalized[index] = char.IsLetterOrDigit(character) || character is ' ' or '.' or ':'
                ? character
                : ' ';
        }

        return new string(normalized);
    }

    public static string Ellipsize(string value, int maxLength)
    {
        var normalized = Normalize(value);
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return maxLength <= 1 ? "." : normalized[..(maxLength - 1)].TrimEnd() + ".";
    }
}

internal static class WorldMenuPresentation
{
    public static void DrawBackdrop(RenderContext context, UiPalette palette)
    {
        var viewport = context.ViewportBounds;
        context.SpriteBatch.Draw(context.Pixel, viewport, palette.Backdrop);

        var skyTop = new Rectangle(viewport.X, viewport.Y, viewport.Width, Math.Max(1, viewport.Height / 3));
        var skyMiddle = new Rectangle(viewport.X, skyTop.Bottom, viewport.Width, Math.Max(1, viewport.Height / 3));
        var skyBottom = new Rectangle(viewport.X, skyMiddle.Bottom, viewport.Width, Math.Max(1, viewport.Bottom - skyMiddle.Bottom));
        context.SpriteBatch.Draw(context.Pixel, skyTop, UiTheme.WithAlpha(palette.AccentSoft, 0.16f));
        context.SpriteBatch.Draw(context.Pixel, skyMiddle, UiTheme.WithAlpha(palette.SurfaceHover, 0.22f));
        context.SpriteBatch.Draw(context.Pixel, skyBottom, UiTheme.WithAlpha(palette.AccentSoft, 0.08f));

        var horizonY = viewport.Y + viewport.Height * 72 / 100;
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(viewport.X, horizonY, viewport.Width, Math.Max(1, viewport.Bottom - horizonY)),
            UiTheme.WithAlpha(palette.Surface, 0.72f));
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(viewport.X, horizonY, viewport.Width, Math.Max(2, viewport.Height / 120)),
            UiTheme.WithAlpha(palette.Accent, 0.68f));
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(viewport.X, viewport.Bottom - Math.Max(18, viewport.Height / 9), viewport.Width, Math.Max(18, viewport.Height / 9)),
            UiTheme.WithAlpha(palette.Backdrop, 0.78f));
    }
}
