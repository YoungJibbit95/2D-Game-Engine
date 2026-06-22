using Game.Client.Input;
using Game.Client.Rendering;
using Game.Core.Equipment;
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
    private readonly List<EquipmentHitZone> _equipmentHitZones = new();

    private InventoryInteractionService? _interactions;
    private PlayerInventory? _inventory;
    private IItemDefinitionProvider? _items;
    private EquipmentLoadout? _equipment;
    private SlotHitZone? _hoveredSlot;
    private EquipmentHitZone? _hoveredEquipment;
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

    public bool Update(InputManager input, PlayerInventory inventory, IItemDefinitionProvider items, EquipmentLoadout equipment, GameSettings settings)
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

        EnsureServices(inventory, items, equipment);
        _hoveredSlot = _slotHitZones.LastOrDefault(zone => zone.Bounds.Contains(input.MousePosition));
        _hoveredEquipment = _equipmentHitZones.LastOrDefault(zone => zone.Bounds.Contains(input.MousePosition));

        if (input.IsKeyPressed(Keys.Escape))
        {
            Close();
            return true;
        }

        if (_hoveredEquipment is { } hoveredEquipment)
        {
            if (input.IsLeftMousePressed)
            {
                HandleEquipmentClick(hoveredEquipment);
            }
        }
        else if (_hoveredSlot is { } hovered)
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

    public void Draw(
        RenderContext context,
        PlayerInventory inventory,
        IItemDefinitionProvider items,
        ClientTextureRegistry? textures,
        GameSettings settings,
        EquipmentLoadout equipment,
        PlayerStatBlock stats,
        int mana,
        int maxMana)
    {
        if (!IsOpen)
        {
            return;
        }

        EnsureServices(inventory, items, equipment);
        var palette = UiTheme.Resolve(settings);
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, UiTheme.WithAlpha(palette.Backdrop, settings.Ui.MenuBackdropOpacity * 0.85f));

        var panelWidth = Math.Min(900, context.ViewportBounds.Width - 48);
        var panelHeight = Math.Min(460, context.ViewportBounds.Height - 72);
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
        var hotbarStart = new Point(panel.X + 22, panel.Y + 82);
        DrawSection(context, palette, "HOTBAR", inventory.Hotbar, SlotSection.Hotbar, hotbarStart, HotbarColumns, textures);

        var mainStart = new Point(panel.X + 22, panel.Y + 154);
        DrawSection(context, palette, "PACK", inventory.Main, SlotSection.Main, mainStart, MainColumns, textures);

        var sidePanel = new Rectangle(panel.Right - 260, panel.Y + 82, 238, panel.Height - 126);
        DrawStatsPanel(context, palette, sidePanel, equipment, items, textures, stats, mana, maxMana);

        DrawTooltip(context, palette, panel, settings);
        DrawCursorStack(context, palette, context.ViewportBounds, textures);

        if (!string.IsNullOrWhiteSpace(_status))
        {
            context.DebugText.Draw(new Vector2(panel.X + 20, panel.Bottom - 28), _status, palette.Warning, 1);
        }
    }

    private void EnsureServices(PlayerInventory inventory, IItemDefinitionProvider items, EquipmentLoadout? equipment = null)
    {
        if (!ReferenceEquals(_inventory, inventory) || !ReferenceEquals(_items, items) || _interactions is null)
        {
            _inventory = inventory;
            _items = items;
            _interactions = new InventoryInteractionService(items);
        }

        if (equipment is not null)
        {
            _equipment = equipment;
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
            if (TryQuickEquip(zone))
            {
                return;
            }

            var moved = zone.Section == SlotSection.Hotbar
                ? _inventory.QuickMoveToMain(zone.Index)
                : _inventory.QuickMoveToHotbar(zone.Index);
            _status = moved ? "QUICK MOVED" : "NO SPACE";
            return;
        }

        var result = _interactions.Click(zone.Slot, _cursor, button);
        _status = result.Error ?? (result.Changed ? "STACK UPDATED" : "NO CHANGE");
    }

    private bool TryQuickEquip(SlotHitZone zone)
    {
        if (_equipment is null || _items is null || zone.Slot.IsEmpty)
        {
            return false;
        }

        var definition = _items.GetById(zone.Slot.Stack.ItemId);
        foreach (var slot in _equipment.Slots.Keys)
        {
            if (!_equipment.GetStack(slot).IsEmpty || !_equipment.CanEquip(definition, slot))
            {
                continue;
            }

            var result = _equipment.TryEquip(zone.Slot.Stack, _items, slot);
            if (!result.Success)
            {
                _status = result.Error;
                return true;
            }

            zone.Slot.SetStack(zone.Slot.Stack.WithCount(zone.Slot.Stack.Count - 1));
            _status = $"EQUIPPED {definition.DisplayName.ToUpperInvariant()}";
            return true;
        }

        if (definition.Type is ItemType.Armor or ItemType.Accessory)
        {
            _status = "NO EQUIP SLOT";
            return true;
        }

        return false;
    }

    private void HandleEquipmentClick(EquipmentHitZone zone)
    {
        if (_equipment is null || _items is null)
        {
            return;
        }

        var equipped = _equipment.GetStack(zone.Slot);
        if (!_cursor.IsHoldingItem)
        {
            if (equipped.IsEmpty)
            {
                _status = "EMPTY EQUIP SLOT";
                return;
            }

            _cursor.Set(_equipment.Unequip(zone.Slot));
            _status = "UNEQUIPPED";
            return;
        }

        var result = _equipment.TryEquip(_cursor.HeldStack, _items, zone.Slot);
        if (!result.Success)
        {
            _status = result.Error;
            return;
        }

        _cursor.Set(_cursor.HeldStack.WithCount(_cursor.HeldStack.Count - 1));
        if (!result.ReplacedStack.IsEmpty)
        {
            if (_cursor.IsHoldingItem)
            {
                _inventory?.AddItem(result.ReplacedStack);
            }
            else
            {
                _cursor.Set(result.ReplacedStack);
            }
        }

        _status = "EQUIPPED";
    }

    private void DrawSection(
        RenderContext context,
        UiPalette palette,
        string title,
        Inventory inventory,
        SlotSection section,
        Point start,
        int columns,
        ClientTextureRegistry? textures)
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
            DrawSlot(context, palette, zone, section == SlotSection.Hotbar && _inventory?.SelectedHotbarSlot == index, textures);
        }
    }

    private void DrawSlot(RenderContext context, UiPalette palette, SlotHitZone zone, bool selected, ClientTextureRegistry? textures)
    {
        var hovered = zone.Bounds.Contains(Mouse.GetState().Position);
        UiTheme.DrawSlot(context, zone.Bounds, palette, selected, hovered);

        if (zone.Slot.IsEmpty)
        {
            return;
        }

        if (_items is not null)
        {
            ItemIconRenderer.DrawItemStack(context, textures, _items, zone.Slot.Stack, zone.Bounds, palette);
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
        var height = 158;
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

    private void DrawStatsPanel(
        RenderContext context,
        UiPalette palette,
        Rectangle bounds,
        EquipmentLoadout equipment,
        IItemDefinitionProvider items,
        ClientTextureRegistry? textures,
        PlayerStatBlock stats,
        int mana,
        int maxMana)
    {
        UiTheme.DrawPanel(context, bounds, palette, 0.68f, raised: false);
        context.DebugText.Draw(new Vector2(bounds.X + 12, bounds.Y + 12), "EQUIPMENT", palette.Text, 2);

        _equipmentHitZones.Clear();
        var slots = new[]
        {
            (EquipmentSlotType.Head, "HEAD"),
            (EquipmentSlotType.Body, "BODY"),
            (EquipmentSlotType.Legs, "LEGS"),
            (EquipmentSlotType.Accessory1, "ACC 1"),
            (EquipmentSlotType.Accessory2, "ACC 2"),
            (EquipmentSlotType.Accessory3, "ACC 3")
        };
        for (var i = 0; i < slots.Length; i++)
        {
            var row = i / 2;
            var column = i % 2;
            var slot = new Rectangle(bounds.X + 12 + column * 108, bounds.Y + 42 + row * 48, 42, 42);
            var hovered = slot.Contains(Mouse.GetState().Position);
            UiTheme.DrawSlot(context, slot, palette, selected: false, hovered, opacity: 0.78f);
            ItemIconRenderer.DrawItemStack(context, textures, items, equipment.GetStack(slots[i].Item1), slot, palette);
            context.DebugText.Draw(new Vector2(slot.Right + 5, slot.Y + 14), slots[i].Item2, palette.TextMuted, 1);
            _equipmentHitZones.Add(new EquipmentHitZone(slot, slots[i].Item1));
        }

        var statsY = bounds.Y + 198;
        context.DebugText.Draw(new Vector2(bounds.X + 12, statsY), "STATS", palette.Accent, 2);
        statsY += 26;
        DrawStatLine(context, palette, bounds.X + 12, ref statsY, "HEALTH", stats.MaxHealth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        DrawStatLine(context, palette, bounds.X + 12, ref statsY, "MANA", $"{mana}/{Math.Max(maxMana, stats.MaxMana)}");
        DrawStatLine(context, palette, bounds.X + 12, ref statsY, "DEFENSE", stats.Defense.ToString(System.Globalization.CultureInfo.InvariantCulture));
        DrawStatLine(context, palette, bounds.X + 12, ref statsY, "MELEE", FormatMultiplier(stats.MeleeDamageMultiplier));
        DrawStatLine(context, palette, bounds.X + 12, ref statsY, "RANGED", FormatMultiplier(stats.RangedDamageMultiplier));
        DrawStatLine(context, palette, bounds.X + 12, ref statsY, "MAGIC", FormatMultiplier(stats.MagicDamageMultiplier));
        DrawStatLine(context, palette, bounds.X + 12, ref statsY, "MINING", FormatMultiplier(stats.MiningSpeedMultiplier));
        DrawStatLine(context, palette, bounds.X + 12, ref statsY, "MANA COST", FormatMultiplier(stats.ManaCostMultiplier));
    }

    private static void DrawStatLine(RenderContext context, UiPalette palette, int x, ref int y, string label, string value)
    {
        context.DebugText.Draw(new Vector2(x, y), label, palette.TextMuted, 1);
        context.DebugText.Draw(new Vector2(x + 110, y), value, palette.Warning, 1);
        y += 15;
    }

    private static string FormatMultiplier(float value)
    {
        return $"{value * 100f:0}%";
    }

    private void DrawCursorStack(RenderContext context, UiPalette palette, Rectangle viewport, ClientTextureRegistry? textures)
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
        if (_items is not null)
        {
            ItemIconRenderer.DrawItemStack(context, textures, _items, _cursor.HeldStack, bounds, palette);
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

        if (item.MaxManaBonus != 0)
        {
            modifiers.Add($"MANA {item.MaxManaBonus}");
        }

        if (Math.Abs(item.MagicDamageBonus) > 0.001f)
        {
            modifiers.Add($"MAGIC {item.MagicDamageBonus:0.##}");
        }

        if (Math.Abs(item.ManaCostReduction) > 0.001f)
        {
            modifiers.Add($"MANA COST -{item.ManaCostReduction:0.##}");
        }

        if (Math.Abs(item.ManaRegenBonus) > 0.001f)
        {
            modifiers.Add($"MANA REGEN {item.ManaRegenBonus:0.##}");
        }

        if (modifiers.Count > 0)
        {
            foreach (var chunk in modifiers.Chunk(3))
            {
                yield return string.Join("  ", chunk);
            }
        }

        if (item.ManaCost > 0 || item.ManaRestore > 0)
        {
            yield return item.ManaCost > 0
                ? $"MANA COST {item.ManaCost}"
                : $"RESTORES {item.ManaRestore} MANA";
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

    private enum SlotSection
    {
        Hotbar,
        Main
    }

    private sealed record SlotHitZone(Rectangle Bounds, InventorySlot Slot, SlotSection Section, int Index);

    private sealed record EquipmentHitZone(Rectangle Bounds, EquipmentSlotType Slot);
}
