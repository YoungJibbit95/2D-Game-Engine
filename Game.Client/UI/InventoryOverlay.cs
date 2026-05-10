using Game.Client.Input;
using Game.Client.Rendering;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.UI;

public sealed class InventoryOverlay
{
    private const int SlotSize = 42;
    private const int SlotGap = 5;
    private const int HotbarColumns = 10;
    private const int MainColumns = 10;

    private readonly CursorItemState _cursor = new();
    private readonly List<SlotHitZone> _slotHitZones = new();

    private InventoryInteractionService? _interactions;
    private PlayerInventory? _inventory;
    private IItemDefinitionProvider? _items;
    private SlotHitZone? _hoveredSlot;
    private string? _status;

    public bool IsOpen { get; private set; }

    public bool IsHoldingItem => _cursor.IsHoldingItem;

    public void Toggle(PlayerInventory inventory, IItemDefinitionProvider items)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(items);

        EnsureServices(inventory, items);
        IsOpen = !IsOpen;
        _status = IsOpen ? "INVENTORY" : null;
    }

    public void Close()
    {
        IsOpen = false;
        _status = null;
    }

    public bool Update(InputManager input, PlayerInventory inventory, IItemDefinitionProvider items, GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(settings);

        if (input.IsBindingPressed(settings.Input.KeyBindings.OpenInventory))
        {
            Toggle(inventory, items);
            return true;
        }

        if (!IsOpen)
        {
            return false;
        }

        EnsureServices(inventory, items);
        _hoveredSlot = _slotHitZones.LastOrDefault(zone => zone.Bounds.Contains(input.MousePosition));

        if (input.IsKeyPressed(Keys.Escape))
        {
            Close();
            return true;
        }

        if (_hoveredSlot is { } hovered)
        {
            if (input.IsLeftMousePressed)
            {
                HandleSlotClick(hovered, InventoryClickButton.Left, input);
            }
            else if (input.IsRightMousePressed)
            {
                HandleSlotClick(hovered, InventoryClickButton.Right, input);
            }
        }

        return true;
    }

    public void Draw(RenderContext context, PlayerInventory inventory, IItemDefinitionProvider items, GameSettings settings)
    {
        if (!IsOpen)
        {
            return;
        }

        EnsureServices(inventory, items);
        var palette = UiTheme.Resolve(settings);
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, UiTheme.WithAlpha(palette.Backdrop, settings.Ui.MenuBackdropOpacity * 0.85f));

        var panelWidth = Math.Min(620, context.ViewportBounds.Width - 48);
        var panelHeight = Math.Min(388, context.ViewportBounds.Height - 72);
        var panel = new Rectangle(
            context.ViewportBounds.Width / 2 - panelWidth / 2,
            Math.Max(32, context.ViewportBounds.Height / 2 - panelHeight / 2),
            panelWidth,
            panelHeight);

        UiTheme.DrawPanel(context, panel, palette, settings.Ui.PanelOpacity);
        context.DebugText.Draw(new Vector2(panel.X + 20, panel.Y + 18), "INVENTORY", palette.Accent, 3);
        if (settings.Ui.ShowControlHints)
        {
            context.DebugText.Draw(new Vector2(panel.X + 22, panel.Y + 52), "LEFT PICK PLACE MERGE   RIGHT SPLIT ONE   SHIFT QUICK MOVE   I ESC CLOSE", palette.TextMuted, 1);
        }

        _slotHitZones.Clear();
        var hotbarStart = new Point(panel.X + 22, panel.Y + 78);
        DrawSection(context, palette, "HOTBAR", inventory.Hotbar, SlotSection.Hotbar, hotbarStart, HotbarColumns);

        var mainStart = new Point(panel.X + 22, panel.Y + 150);
        DrawSection(context, palette, "PACK", inventory.Main, SlotSection.Main, mainStart, MainColumns);

        DrawTooltip(context, palette, panel, settings);
        DrawCursorStack(context, palette, context.ViewportBounds);

        if (!string.IsNullOrWhiteSpace(_status))
        {
            context.DebugText.Draw(new Vector2(panel.X + 20, panel.Bottom - 28), _status, palette.Warning, 1);
        }
    }

    private void EnsureServices(PlayerInventory inventory, IItemDefinitionProvider items)
    {
        if (!ReferenceEquals(_inventory, inventory) || !ReferenceEquals(_items, items) || _interactions is null)
        {
            _inventory = inventory;
            _items = items;
            _interactions = new InventoryInteractionService(items);
        }
    }

    private void HandleSlotClick(SlotHitZone zone, InventoryClickButton button, InputManager input)
    {
        if (_inventory is null || _interactions is null)
        {
            return;
        }

        if (button == InventoryClickButton.Left &&
            !_cursor.IsHoldingItem &&
            (input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift)))
        {
            var moved = zone.Section == SlotSection.Hotbar
                ? _inventory.QuickMoveToMain(zone.Index)
                : _inventory.QuickMoveToHotbar(zone.Index);
            _status = moved ? "QUICK MOVED" : "NO SPACE";
            return;
        }

        var result = _interactions.Click(zone.Slot, _cursor, button);
        _status = result.Error ?? (result.Changed ? "STACK UPDATED" : "NO CHANGE");
    }

    private void DrawSection(
        RenderContext context,
        UiPalette palette,
        string title,
        Inventory inventory,
        SlotSection section,
        Point start,
        int columns)
    {
        context.DebugText.Draw(new Vector2(start.X, start.Y - 18), title, palette.TextMuted, 1);

        for (var index = 0; index < inventory.Slots.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var bounds = new Rectangle(
                start.X + column * (SlotSize + SlotGap),
                start.Y + row * (SlotSize + SlotGap),
                SlotSize,
                SlotSize);
            var zone = new SlotHitZone(bounds, inventory.Slots[index], section, index);
            _slotHitZones.Add(zone);
            DrawSlot(context, palette, zone, section == SlotSection.Hotbar && _inventory?.SelectedHotbarSlot == index);
        }
    }

    private void DrawSlot(RenderContext context, UiPalette palette, SlotHitZone zone, bool selected)
    {
        var hovered = zone.Bounds.Contains(Mouse.GetState().Position);
        UiTheme.DrawButton(context, zone.Bounds, palette, selected, hovered);

        if (zone.Slot.IsEmpty)
        {
            return;
        }

        var item = _items?.GetById(zone.Slot.Stack.ItemId);
        var label = Abbreviate(item?.DisplayName ?? zone.Slot.Stack.ItemId);
        context.DebugText.Draw(new Vector2(zone.Bounds.X + 5, zone.Bounds.Y + 6), label, palette.Text, 1);

        if (zone.Slot.Stack.Count > 1)
        {
            var count = zone.Slot.Stack.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            context.DebugText.Draw(new Vector2(zone.Bounds.Right - Math.Min(28, count.Length * 6 + 4), zone.Bounds.Bottom - 13), count, palette.Warning, 1);
        }
    }

    private void DrawTooltip(RenderContext context, UiPalette palette, Rectangle panel, GameSettings settings)
    {
        if (_hoveredSlot is not { Slot.IsEmpty: false } hovered || _items is null)
        {
            return;
        }

        var item = _items.GetById(hovered.Slot.Stack.ItemId);
        var width = 260;
        var height = 132;
        var x = Math.Min(panel.Right - width - 16, hovered.Bounds.Right + 14);
        var y = Math.Min(panel.Bottom - height - 16, hovered.Bounds.Y);
        var bounds = new Rectangle(x, y, width, height);
        UiTheme.DrawPanel(context, bounds, palette, Math.Min(1f, settings.Ui.PanelOpacity + 0.05f));
        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 10), item.DisplayName, palette.Text, 2);
        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 34), $"TYPE {item.Type}", palette.TextMuted, 1);
        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 48), $"STACK {hovered.Slot.Stack.Count}/{item.MaxStack}", palette.TextMuted, 1);

        var textY = bounds.Y + 62;
        foreach (var line in BuildTooltipLines(item).Take(4))
        {
            context.DebugText.Draw(new Vector2(bounds.X + 10, textY), line, palette.Warning, 1);
            textY += 14;
        }
    }

    private void DrawCursorStack(RenderContext context, UiPalette palette, Rectangle viewport)
    {
        if (!_cursor.IsHoldingItem)
        {
            return;
        }

        var mouse = Mouse.GetState().Position;
        var bounds = new Rectangle(
            Math.Clamp(mouse.X + 10, 0, viewport.Width - SlotSize),
            Math.Clamp(mouse.Y + 10, 0, viewport.Height - SlotSize),
            SlotSize,
            SlotSize);
        UiTheme.DrawButton(context, bounds, palette, selected: true, hovered: false);
        var item = _items?.GetById(_cursor.HeldStack.ItemId);
        context.DebugText.Draw(new Vector2(bounds.X + 5, bounds.Y + 6), Abbreviate(item?.DisplayName ?? _cursor.HeldStack.ItemId), palette.Text, 1);
        if (_cursor.HeldStack.Count > 1)
        {
            context.DebugText.Draw(new Vector2(bounds.X + 5, bounds.Bottom - 13), _cursor.HeldStack.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), palette.Warning, 1);
        }
    }

    private static IEnumerable<string> BuildTooltipLines(ItemDefinition item)
    {
        var parts = new List<string>();
        if (item.Damage > 0)
        {
            parts.Add($"DMG {item.Damage}");
        }

        if (item.ToolPower > 0)
        {
            parts.Add($"POWER {item.ToolPower}");
        }

        if (item.Defense > 0)
        {
            parts.Add($"DEF {item.Defense}");
        }

        if (parts.Count > 0)
        {
            yield return string.Join("  ", parts);
        }

        var modifiers = new List<string>();
        if (item.MaxHealthBonus != 0)
        {
            modifiers.Add($"HEALTH {item.MaxHealthBonus}");
        }

        if (Math.Abs(item.MovementSpeedBonus) > 0.001f)
        {
            modifiers.Add($"MOVE {item.MovementSpeedBonus:0.##}");
        }

        if (Math.Abs(item.MeleeDamageBonus) > 0.001f)
        {
            modifiers.Add($"MELEE {item.MeleeDamageBonus:0.##}");
        }

        if (Math.Abs(item.RangedDamageBonus) > 0.001f)
        {
            modifiers.Add($"RANGED {item.RangedDamageBonus:0.##}");
        }

        if (Math.Abs(item.MiningSpeedBonus) > 0.001f)
        {
            modifiers.Add($"MINING {item.MiningSpeedBonus:0.##}");
        }

        if (modifiers.Count > 0)
        {
            yield return string.Join("  ", modifiers.Take(3));
        }

        if (item.OnHitEffects.Count > 0)
        {
            yield return $"EFFECT {string.Join(" ", item.OnHitEffects.Take(2).Select(effect => effect.EffectId.ToUpperInvariant()))}";
        }

        if (item.Tags.Count > 0)
        {
            yield return $"TAGS {string.Join(" ", item.Tags.Take(3).Select(tag => tag.ToUpperInvariant()))}";
        }
    }

    private static string Abbreviate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "?";
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length > 1)
        {
            return string.Concat(words.Select(word => char.ToUpperInvariant(word[0]))).Substring(0, Math.Min(4, words.Length));
        }

        return text.Length <= 4
            ? text.ToUpperInvariant()
            : text[..4].ToUpperInvariant();
    }

    private enum SlotSection
    {
        Hotbar,
        Main
    }

    private sealed record SlotHitZone(Rectangle Bounds, InventorySlot Slot, SlotSection Section, int Index);
}
