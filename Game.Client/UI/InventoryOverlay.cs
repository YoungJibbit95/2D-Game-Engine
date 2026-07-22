using Game.Client.Input;
using Game.Client.Rendering;
using Game.Core.Equipment;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Settings;
using Game.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Game.Tests")]

namespace Game.Client.UI;

public sealed class InventoryOverlay
{
    private const int MaximumSlotSize = 42;
    private const int MinimumSlotSize = 28;
    private const int SlotGap = 5;
    private const int HotbarColumns = 10;
    private const int MainColumns = 10;

    private static readonly FilterOption[] Filters =
    {
        new("ALL", Array.Empty<ItemCategory>()),
        new("BLOCKS", new[] { ItemCategory.Block }),
        new("WEAPONS", new[] { ItemCategory.Weapon }),
        new("TOOLS", new[] { ItemCategory.Tool }),
        new("SUPPLIES", new[] { ItemCategory.Consumable, ItemCategory.Ammo }),
        new("EQUIP", new[] { ItemCategory.Equipment }),
        new("MATERIAL", new[] { ItemCategory.Material, ItemCategory.Farming }),
        new("OTHER", new[] { ItemCategory.Quest, ItemCategory.Miscellaneous })
    };

    private static readonly EquipmentSlotDisplay[] EquipmentSlots =
    {
        new(EquipmentSlotType.Head, "HEAD"),
        new(EquipmentSlotType.Body, "BODY"),
        new(EquipmentSlotType.Legs, "LEGS"),
        new(EquipmentSlotType.Accessory1, "ACC 1"),
        new(EquipmentSlotType.Accessory2, "ACC 2"),
        new(EquipmentSlotType.Accessory3, "ACC 3")
    };

    private readonly CursorItemState _cursor = new();
    private readonly HitZoneBuffer<SlotTarget> _slotHitZones = new(PlayerInventory.HotbarSlotCount + PlayerInventory.MainSlotCount);
    private readonly HitZoneBuffer<EquipmentSlotType> _equipmentHitZones = new(6);
    private readonly HitZoneBuffer<ControlAction> _controlHitZones = new(3);
    private readonly HitZoneBuffer<int> _filterHitZones = new(Filters.Length);
    private readonly InventorySectionQueryCache _hotbarView = new("HOTBAR");
    private readonly InventorySectionQueryCache _mainView = new("PACK");
    private readonly InventoryStatisticsCache _statisticsCache = new();
    private readonly InventoryTooltipCache _tooltipCache = new();

    private InventoryInteractionService? _interactions;
    private InventoryQueryService? _queryService;
    private PlayerInventory? _inventory;
    private IItemDefinitionProvider? _items;
    private EquipmentLoadout? _equipment;
    private HitZone<SlotTarget>? _hoveredSlot;
    private HitZone<EquipmentSlotType>? _hoveredEquipment;
    private InventorySortMode _sortMode = InventorySortMode.ItemType;
    private int _activeFilterIndex;
    private int _slotSize = MaximumSlotSize;
    private int _focusedFlatSlotIndex;
    private bool _keyboardSlotFocus;
    private Point _mousePosition;
    private string? _status;

    public bool IsOpen { get; private set; }

    public bool IsHoldingItem => _cursor.IsHoldingItem;

    public void Toggle(PlayerInventory inventory, IItemDefinitionProvider items)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(items);

        EnsureServices(inventory, items);
        if (IsOpen)
        {
            TryClose();
            return;
        }

        IsOpen = true;
        _focusedFlatSlotIndex = Math.Clamp(inventory.SelectedHotbarSlot, 0, PlayerInventory.HotbarSlotCount - 1);
        _keyboardSlotFocus = false;
        _status = "INVENTORY READY";
    }

    public void Close()
    {
        TryClose();
    }

    public bool Update(
        InputManager input,
        PlayerInventory inventory,
        IItemDefinitionProvider items,
        EquipmentLoadout equipment,
        GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(settings);
        _mousePosition = input.MousePosition;

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
        _hoveredSlot = _slotHitZones.FindTopmost(_mousePosition);
        _hoveredEquipment = _equipmentHitZones.FindTopmost(_mousePosition);

        if (input.IsKeyPressed(Keys.Escape))
        {
            Close();
            return true;
        }

        if (TryMoveSlotFocus(input, inventory))
        {
            return true;
        }

        if (_keyboardSlotFocus && (input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.Space)))
        {
            var focused = GetFocusedTarget(inventory);
            if (focused is { } target && IsSlotInteractive(target))
            {
                HandleSlotClick(target, InventoryClickButton.Left, input);
            }

            return true;
        }

        if (input.IsKeyPressed(Keys.Tab))
        {
            _activeFilterIndex = (_activeFilterIndex + 1) % Filters.Length;
            _status = $"FILTER {Filters[_activeFilterIndex].Label}";
            return true;
        }

        if (input.IsLeftMousePressed)
        {
            _keyboardSlotFocus = false;
            var control = _controlHitZones.FindTopmost(_mousePosition);
            if (control is { } hoveredControl)
            {
                HandleControl(hoveredControl.Value);
                return true;
            }

            var filter = _filterHitZones.FindTopmost(_mousePosition);
            if (filter is { } hoveredFilter)
            {
                _activeFilterIndex = hoveredFilter.Value;
                _status = $"FILTER {Filters[hoveredFilter.Value].Label}";
                return true;
            }
        }

        if (_hoveredEquipment is { } hoveredEquipment)
        {
            if (input.IsLeftMousePressed)
            {
                HandleEquipmentClick(hoveredEquipment.Value);
            }
        }
        else if (_hoveredSlot is { } hovered)
        {
            if (input.IsLeftMousePressed)
            {
                FocusSlot(hovered.Value);
                HandleSlotClick(hovered.Value, InventoryClickButton.Left, input);
            }
            else if (input.IsRightMousePressed)
            {
                FocusSlot(hovered.Value);
                HandleSlotClick(hovered.Value, InventoryClickButton.Right, input);
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
        var layout = PixelInventoryLayoutPlanner.Resolve(context.ViewportBounds);
        var panel = layout.Panel;
        UiTheme.DrawBackdrop(context, palette, settings.Ui.MenuBackdropOpacity * 0.85f, settings);
        UiTheme.DrawPanel(context, panel, palette, settings.Ui.PanelOpacity);
        UiTheme.DrawHeader(context, layout.Header, palette, settings: settings);

        var inventoryStats = _statisticsCache.Get(_queryService, inventory);
        var headerIconSize = layout.Density == PixelUiDensity.Compact ? 22 : 34;
        var headerIconBounds = new Rectangle(
            layout.Header.X + 12,
            layout.Header.Y + Math.Max(2, (layout.Header.Height - headerIconSize) / 2),
            headerIconSize,
            headerIconSize);
        var drewHeaderIcon = ItemIconRenderer.TryDrawSprite(context, textures, "ui/inventory_tab", headerIconBounds);
        var titleScale = layout.Density == PixelUiDensity.Compact ? 1 : 2;
        context.DebugText.Draw(
            new Vector2(
                layout.Header.X + (drewHeaderIcon ? headerIconSize + 20 : 14),
                layout.Header.Y + Math.Max(4, (layout.Header.Height - 7 * titleScale) / 2)),
            "INVENTORY",
            palette.Accent,
            titleScale);
        if (inventoryStats is not null && panel.Width >= 860)
        {
            context.DebugText.Draw(
                new Vector2(layout.Header.X + 190, layout.Header.Y + Math.Max(5, (layout.Header.Height - 7) / 2)),
                _statisticsCache.Summary,
                palette.TextMuted,
                1);
        }

        DrawControls(context, palette, layout.Toolbar);
        DrawFilters(context, palette, layout.Filters);

        context.SpriteBatch.Draw(context.Pixel, layout.PackSurface, UiTheme.WithAlpha(palette.Surface, 0.48f));
        UiTheme.DrawBorder(context, layout.PackSurface, UiTheme.WithAlpha(palette.SurfaceHover, 0.72f), 1);
        _slotSize = layout.SlotSize;
        _slotHitZones.Clear();
        DrawSection(context, palette, inventory.Hotbar, SlotSection.Hotbar, layout.HotbarOrigin, layout.Columns, layout.SlotGap, textures, _hotbarView, applyFilter: false);
        DrawSection(context, palette, inventory.Main, SlotSection.Main, layout.MainOrigin, layout.Columns, layout.SlotGap, textures, _mainView, applyFilter: true);

        if (layout.ShowEquipment)
        {
            context.SpriteBatch.Draw(context.Pixel, layout.EquipmentSurface, UiTheme.WithAlpha(palette.Surface, 0.48f));
            UiTheme.DrawBorder(context, layout.EquipmentSurface, UiTheme.WithAlpha(palette.SurfaceHover, 0.72f), 1);
            DrawEquipmentAndStats(
                context,
                palette,
                new Rectangle(
                    layout.EquipmentSurface.X + 12,
                    layout.EquipmentSurface.Y + 12,
                    Math.Max(0, layout.EquipmentSurface.Width - 24),
                    Math.Max(0, layout.EquipmentSurface.Height - 24)),
                equipment,
                items,
                textures,
                stats,
                mana,
                maxMana);
        }
        else
        {
            _equipmentHitZones.Clear();
        }

        DrawTooltip(context, palette, panel, settings);
        DrawCursorStack(context, palette, context.ViewportBounds, textures);
        if (!string.IsNullOrWhiteSpace(_status))
        {
            context.DebugText.Draw(
                new Vector2(layout.StatusBar.X, layout.StatusBar.Y + Math.Max(1, (layout.StatusBar.Height - 7) / 2)),
                AbbreviateLabel(_status, Math.Max(3, layout.StatusBar.Width / 8)),
                palette.Warning,
                1);
        }
    }

    private void EnsureServices(PlayerInventory inventory, IItemDefinitionProvider items, EquipmentLoadout? equipment = null)
    {
        var itemProviderChanged = !ReferenceEquals(_items, items);
        if (!ReferenceEquals(_inventory, inventory) || !ReferenceEquals(_items, items) || _interactions is null || _queryService is null)
        {
            _inventory = inventory;
            _items = items;
            _interactions = new InventoryInteractionService(items);
            _queryService = new InventoryQueryService(items);
            _statisticsCache.Clear();
            if (itemProviderChanged)
            {
                _tooltipCache.Clear();
            }
        }

        if (equipment is not null)
        {
            _equipment = equipment;
        }
    }

    private bool TryMoveSlotFocus(InputManager input, PlayerInventory inventory)
    {
        UiGridDirection? direction = null;
        if (input.IsKeyPressed(Keys.Left) || input.IsKeyPressed(Keys.A))
        {
            direction = UiGridDirection.Left;
        }
        else if (input.IsKeyPressed(Keys.Right) || input.IsKeyPressed(Keys.D))
        {
            direction = UiGridDirection.Right;
        }
        else if (input.IsKeyPressed(Keys.Up) || input.IsKeyPressed(Keys.W))
        {
            direction = UiGridDirection.Up;
        }
        else if (input.IsKeyPressed(Keys.Down) || input.IsKeyPressed(Keys.S))
        {
            direction = UiGridDirection.Down;
        }

        if (direction is null)
        {
            return false;
        }

        var itemCount = inventory.Hotbar.Slots.Count + inventory.Main.Slots.Count;
        _focusedFlatSlotIndex = UiGridNavigator.Move(
            _focusedFlatSlotIndex,
            itemCount,
            MainColumns,
            direction.Value,
            wrap: true);
        _keyboardSlotFocus = true;
        var focused = GetFocusedTarget(inventory);
        _status = focused is { Slot.IsEmpty: false } && _items is not null
            ? _items.GetById(focused.Value.Slot.Stack.ItemId).DisplayName.ToUpperInvariant()
            : "EMPTY SLOT";
        return true;
    }

    private SlotTarget? GetFocusedTarget(PlayerInventory inventory)
    {
        if (!_keyboardSlotFocus)
        {
            return null;
        }

        if (_focusedFlatSlotIndex < inventory.Hotbar.Slots.Count)
        {
            var index = Math.Clamp(_focusedFlatSlotIndex, 0, inventory.Hotbar.Slots.Count - 1);
            return new SlotTarget(inventory.Hotbar, inventory.Hotbar.Slots[index], SlotSection.Hotbar, index);
        }

        var mainIndex = _focusedFlatSlotIndex - inventory.Hotbar.Slots.Count;
        if (mainIndex < 0 || mainIndex >= inventory.Main.Slots.Count)
        {
            return null;
        }

        return new SlotTarget(inventory.Main, inventory.Main.Slots[mainIndex], SlotSection.Main, mainIndex);
    }

    private bool IsFocused(SlotSection section, int index)
    {
        if (!_keyboardSlotFocus)
        {
            return false;
        }

        var flatIndex = section == SlotSection.Hotbar ? index : PlayerInventory.HotbarSlotCount + index;
        return flatIndex == _focusedFlatSlotIndex;
    }

    private void FocusSlot(SlotTarget target)
    {
        _focusedFlatSlotIndex = target.Section == SlotSection.Hotbar
            ? target.Index
            : PlayerInventory.HotbarSlotCount + target.Index;
    }

    private bool IsSlotInteractive(SlotTarget target)
    {
        if (target.Section == SlotSection.Hotbar || target.Slot.IsEmpty)
        {
            return true;
        }

        var categories = Filters[_activeFilterIndex].Categories;
        if (categories.Count == 0 || _items is null)
        {
            return true;
        }

        var category = _items.GetById(target.Slot.Stack.ItemId).ResolvedCategory;
        for (var index = 0; index < categories.Count; index++)
        {
            if (categories[index] == category)
            {
                return true;
            }
        }

        return false;
    }

    private HitZone<SlotTarget>? FocusedHitZone()
    {
        if (!_keyboardSlotFocus)
        {
            return null;
        }

        if (_slotHitZones.TryGet(_focusedFlatSlotIndex, out var zone))
        {
            return zone;
        }

        return null;
    }

    private void HandleControl(ControlAction action)
    {
        if (_inventory is null)
        {
            return;
        }

        switch (action)
        {
            case ControlAction.Mode:
                _sortMode = (InventorySortMode)(((int)_sortMode + 1) % Enum.GetValues<InventorySortMode>().Length);
                _status = $"ORDER {_sortMode.ToString().ToUpperInvariant()}";
                break;
            case ControlAction.Sort:
                {
                    var result = _inventory.SortMain(_sortMode);
                    _status = result.Changed ? $"PACK SORTED: {result.ChangedSlots} SLOTS" : "PACK ALREADY SORTED";
                    break;
                }
            case ControlAction.Compact:
                {
                    var result = _inventory.CompactMain();
                    _status = result.Changed ? $"COMPACTED: {result.FreedSlots} SLOTS FREED" : "PACK ALREADY COMPACT";
                    break;
                }
        }
    }

    private void HandleSlotClick(SlotTarget zone, InventoryClickButton button, InputManager input)
    {
        if (_inventory is null || _interactions is null || _items is null)
        {
            return;
        }

        var altDown = input.IsKeyDown(Keys.LeftAlt) || input.IsKeyDown(Keys.RightAlt);
        if (button == InventoryClickButton.Left && !_cursor.IsHoldingItem && altDown && !zone.Slot.IsEmpty)
        {
            var favorite = !zone.Slot.IsFavorite;
            var changed = zone.Inventory.SetFavorite(zone.Index, favorite);
            _status = changed
                ? favorite ? "FAVORITE SET" : "FAVORITE CLEARED"
                : "ITEM CANNOT BE FAVORITED";
            return;
        }

        var controlDown = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
        if (button == InventoryClickButton.Left && !_cursor.IsHoldingItem && controlDown && !zone.Slot.IsEmpty)
        {
            var result = zone.Inventory.TrashSlot(zone.Index);
            _status = FormatTransaction(result, "TRASHED");
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

            var result = zone.Section == SlotSection.Hotbar
                ? _inventory.QuickMoveToMainTransaction(zone.Index)
                : _inventory.QuickMoveToHotbarTransaction(zone.Index);
            _status = FormatTransaction(result, "QUICK MOVED");
            return;
        }

        var interaction = _interactions.Click(zone.Slot, _cursor, button);
        _status = interaction.Error ?? (interaction.Changed ? "STACK UPDATED" : "NO CHANGE");
    }

    private bool TryQuickEquip(SlotTarget zone)
    {
        if (_equipment is null || _items is null || zone.Slot.IsEmpty || zone.Slot.IsFavorite)
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

    private void HandleEquipmentClick(EquipmentSlotType slot)
    {
        if (_equipment is null || _items is null)
        {
            return;
        }

        var equipped = _equipment.GetStack(slot);
        if (!_cursor.IsHoldingItem)
        {
            if (equipped.IsEmpty)
            {
                _status = "EMPTY EQUIP SLOT";
                return;
            }

            _cursor.Set(_equipment.Unequip(slot));
            _status = "UNEQUIPPED";
            return;
        }

        if (!equipped.IsEmpty && _cursor.HeldStack.Count > 1 && _inventory?.CanAddItem(equipped) != true)
        {
            _status = "NO SPACE FOR REPLACED ITEM";
            return;
        }

        var result = _equipment.TryEquip(_cursor.HeldStack, _items, slot);
        if (!result.Success)
        {
            _status = result.Error;
            return;
        }

        var cursorFavorite = _cursor.IsFavorite;
        _cursor.Set(_cursor.HeldStack.WithCount(_cursor.HeldStack.Count - 1), cursorFavorite);
        if (!result.ReplacedStack.IsEmpty)
        {
            if (_cursor.IsHoldingItem)
            {
                var returned = _inventory!.AddTransaction(result.ReplacedStack);
                if (!returned.Completed)
                {
                    throw new InvalidOperationException("Equipment replacement preflight and inventory transaction diverged.");
                }
            }
            else
            {
                _cursor.Set(result.ReplacedStack);
            }
        }

        _status = "EQUIPPED";
    }

    private bool TryClose()
    {
        if (_cursor.IsHoldingItem)
        {
            if (_inventory is null)
            {
                _status = "INVENTORY UNAVAILABLE";
                return false;
            }

            var returned = _inventory.AddSlotStateTransaction(
                new InventorySlotState(_cursor.HeldStack, _cursor.IsFavorite));
            if (!returned.Completed)
            {
                _status = "NO SPACE FOR CURSOR ITEM";
                return false;
            }

            _cursor.Clear();
        }

        IsOpen = false;
        _status = null;
        return true;
    }

    private void DrawControls(RenderContext context, UiPalette palette, Rectangle bounds)
    {
        _controlHitZones.Clear();
        var gap = Math.Min(4, Math.Max(1, bounds.Width / 80));
        var compactWidth = Math.Clamp(bounds.Width * 30 / 100, 48, 92);
        var sortWidth = Math.Clamp(bounds.Width * 22 / 100, 42, 66);
        var modeWidth = Math.Max(1, bounds.Width - compactWidth - sortWidth - gap * 2);
        var mode = new Rectangle(bounds.X, bounds.Y, modeWidth, bounds.Height);
        var sort = new Rectangle(mode.Right + gap, bounds.Y, sortWidth, bounds.Height);
        var compact = new Rectangle(sort.Right + gap, bounds.Y, compactWidth, bounds.Height);

        DrawControl(context, palette, mode, GetSortModeLabel(_sortMode), ControlAction.Mode);
        DrawControl(context, palette, sort, "SORT", ControlAction.Sort);
        DrawControl(context, palette, compact, "COMPACT", ControlAction.Compact);
    }

    private void DrawControl(RenderContext context, UiPalette palette, Rectangle bounds, string label, ControlAction action)
    {
        UiTheme.DrawButton(context, bounds, palette, selected: false, hovered: bounds.Contains(_mousePosition));
        context.DebugText.Draw(
            new Vector2(bounds.X + 6, bounds.Y + Math.Max(2, (bounds.Height - 7) / 2)),
            AbbreviateLabel(label, Math.Max(3, (bounds.Width - 10) / 8)),
            palette.Text,
            1);
        _controlHitZones.Add(bounds, action);
    }

    private void DrawFilters(RenderContext context, UiPalette palette, Rectangle bounds)
    {
        _filterHitZones.Clear();
        var gap = bounds.Width < 520 ? 2 : 4;
        var width = Math.Max(1, (bounds.Width - gap * (Filters.Length - 1)) / Filters.Length);

        for (var index = 0; index < Filters.Length; index++)
        {
            var tabWidth = index == Filters.Length - 1
                ? Math.Max(1, bounds.Right - (bounds.X + index * (width + gap)))
                : width;
            var tab = new Rectangle(bounds.X + index * (width + gap), bounds.Y, tabWidth, bounds.Height);
            var selected = index == _activeFilterIndex;
            UiTheme.DrawButton(context, tab, palette, selected, tab.Contains(_mousePosition));
            context.DebugText.Draw(
                new Vector2(tab.X + 4, tab.Y + Math.Max(2, (tab.Height - 7) / 2)),
                AbbreviateLabel(Filters[index].Label, Math.Max(2, (tab.Width - 7) / 8)),
                selected ? palette.Warning : palette.TextMuted,
                1);
            _filterHitZones.Add(tab, index);
        }
    }

    private void DrawSection(
        RenderContext context,
        UiPalette palette,
        Inventory inventory,
        SlotSection section,
        Point start,
        int columns,
        int slotGap,
        ClientTextureRegistry? textures,
        InventorySectionQueryCache view,
        bool applyFilter)
    {
        var categories = Filters[_activeFilterIndex].Categories;
        view.Refresh(inventory, _items!, categories, _activeFilterIndex, applyFilter);
        context.DebugText.Draw(new Vector2(start.X, start.Y - 18), view.Summary, palette.TextMuted, 1);

        for (var index = 0; index < inventory.Slots.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var bounds = new Rectangle(
                start.X + column * (_slotSize + slotGap),
                start.Y + row * (_slotSize + slotGap),
                _slotSize,
                _slotSize);
            var slot = inventory.Slots[index];
            var matchesFilter = view.IsInteractive(index);
            var target = new SlotTarget(inventory, slot, section, index);
            _slotHitZones.Add(bounds, target, matchesFilter);
            DrawSlot(
                context,
                palette,
                bounds,
                target,
                matchesFilter,
                (section == SlotSection.Hotbar && _inventory?.SelectedHotbarSlot == index) || IsFocused(section, index),
                textures,
                matchesFilter ? 1f : 0.24f);
        }
    }

    private void DrawSlot(
        RenderContext context,
        UiPalette palette,
        Rectangle bounds,
        SlotTarget target,
        bool isInteractive,
        bool selected,
        ClientTextureRegistry? textures,
        float opacity)
    {
        var hovered = isInteractive && bounds.Contains(_mousePosition);
        UiTheme.DrawSlot(context, bounds, palette, selected, hovered, opacity);
        if (IsFocused(target.Section, target.Index))
        {
            UiTheme.DrawFocusFrame(context, bounds, palette, 6);
        }

        if (target.Slot.IsEmpty || !isInteractive || _items is null)
        {
            return;
        }

        ItemIconRenderer.DrawItemStack(
            context,
            textures,
            _items,
            target.Slot.Stack,
            bounds,
            palette,
            opacity: opacity,
            isFavorite: target.Slot.IsFavorite);
    }

    private void DrawTooltip(RenderContext context, UiPalette palette, Rectangle panel, GameSettings settings)
    {
        var tooltipTarget = _hoveredSlot ?? FocusedHitZone();
        if (tooltipTarget is not { Value.Slot.IsEmpty: false } hovered || _items is null)
        {
            return;
        }

        var slot = hovered.Value.Slot;
        var item = _items.GetById(slot.Stack.ItemId);
        var tooltip = _tooltipCache.Get(item);
        var height = 96 + tooltip.DescriptionLines.Length * 14 + tooltip.DetailLines.Length * 14;
        var width = Math.Min(320, panel.Width - 32);
        var x = Math.Min(panel.Right - width - 14, hovered.Bounds.Right + 12);
        var y = Math.Clamp(hovered.Bounds.Y, panel.Y + 104, Math.Max(panel.Y + 104, panel.Bottom - height - 14));
        var bounds = new Rectangle(x, y, width, height);
        UiTheme.DrawPanel(context, bounds, palette, Math.Min(1f, settings.Ui.PanelOpacity + 0.05f));

        var rarityColor = ItemIconRenderer.GetRarityColor(item.Rarity);
        context.DebugText.Draw(new Vector2(bounds.X + 12, bounds.Y + 10), item.DisplayName, rarityColor, 2);
        context.DebugText.Draw(
            new Vector2(bounds.X + 12, bounds.Y + 34),
            tooltip.Classification,
            palette.TextMuted,
            1);
        context.DebugText.Draw(
            new Vector2(bounds.X + 12, bounds.Y + 49),
            tooltip.GetStackLine(slot.Stack.Count, slot.IsFavorite),
            slot.IsFavorite ? palette.Warning : palette.TextMuted,
            1);

        var textY = bounds.Y + 68;
        foreach (var line in tooltip.DescriptionLines)
        {
            context.DebugText.Draw(new Vector2(bounds.X + 12, textY), line, palette.Text, 1);
            textY += 14;
        }

        if (tooltip.DescriptionLines.Length > 0 && tooltip.DetailLines.Length > 0)
        {
            textY += 4;
        }

        foreach (var line in tooltip.DetailLines)
        {
            context.DebugText.Draw(new Vector2(bounds.X + 12, textY), line, palette.Warning, 1);
            textY += 14;
        }

        if (!item.CanTrash)
        {
            context.DebugText.Draw(new Vector2(bounds.X + 12, bounds.Bottom - 18), "TRASH PROTECTED", palette.Danger, 1);
        }
    }

    private void DrawEquipmentAndStats(
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
        context.DebugText.Draw(new Vector2(bounds.X, bounds.Y), "EQUIPMENT", palette.Text, 2);

        _equipmentHitZones.Clear();
        for (var index = 0; index < EquipmentSlots.Length; index++)
        {
            var row = index / 2;
            var column = index % 2;
            var slotBounds = new Rectangle(bounds.X + column * 108, bounds.Y + 34 + row * 48, 42, 42);
            var hovered = slotBounds.Contains(_mousePosition);
            UiTheme.DrawSlot(context, slotBounds, palette, selected: false, hovered, opacity: 0.78f);
            var slot = EquipmentSlots[index];
            ItemIconRenderer.DrawItemStack(context, textures, items, equipment.GetStack(slot.Slot), slotBounds, palette);
            context.DebugText.Draw(new Vector2(slotBounds.Right + 5, slotBounds.Y + 14), slot.Label, palette.TextMuted, 1);
            _equipmentHitZones.Add(slotBounds, slot.Slot);
        }

        var statsY = bounds.Y + 190;
        if (bounds.Height < 330)
        {
            return;
        }

        context.DebugText.Draw(new Vector2(bounds.X, statsY), "STATS", palette.Accent, 2);
        statsY += 26;
        DrawStatLine(context, palette, bounds.X, ref statsY, "HEALTH", stats.MaxHealth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        DrawStatLine(context, palette, bounds.X, ref statsY, "MANA", $"{mana}/{Math.Max(maxMana, stats.MaxMana)}");
        DrawStatLine(context, palette, bounds.X, ref statsY, "DEFENSE", stats.Defense.ToString(System.Globalization.CultureInfo.InvariantCulture));
        DrawStatLine(context, palette, bounds.X, ref statsY, "MELEE", FormatMultiplier(stats.MeleeDamageMultiplier));
        DrawStatLine(context, palette, bounds.X, ref statsY, "RANGED", FormatMultiplier(stats.RangedDamageMultiplier));
        DrawStatLine(context, palette, bounds.X, ref statsY, "MAGIC", FormatMultiplier(stats.MagicDamageMultiplier));
        DrawStatLine(context, palette, bounds.X, ref statsY, "MINING", FormatMultiplier(stats.MiningSpeedMultiplier));
        DrawStatLine(context, palette, bounds.X, ref statsY, "MANA COST", FormatMultiplier(stats.ManaCostMultiplier));
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

    private static string AbbreviateLabel(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(1, maxLength - 1)] + ".";
    }

    private void DrawCursorStack(RenderContext context, UiPalette palette, Rectangle viewport, ClientTextureRegistry? textures)
    {
        if (!_cursor.IsHoldingItem)
        {
            return;
        }

        var bounds = new Rectangle(
            Math.Clamp(_mousePosition.X + 10, 0, Math.Max(0, viewport.Width - _slotSize)),
            Math.Clamp(_mousePosition.Y + 10, 0, Math.Max(0, viewport.Height - _slotSize)),
            _slotSize,
            _slotSize);
        UiTheme.DrawButton(context, bounds, palette, selected: true, hovered: false);
        if (_items is not null)
        {
            ItemIconRenderer.DrawItemStack(
                context,
                textures,
                _items,
                _cursor.HeldStack,
                bounds,
                palette,
                isFavorite: _cursor.IsFavorite);
        }
    }

    private static string FormatTransaction(InventoryTransactionResult result, string success)
    {
        if (result.Completed)
        {
            return result.Changed ? $"{success}: {result.Moved}" : "NO CHANGE";
        }

        return result.Error ?? $"{result.Status.ToString().ToUpperInvariant()}: {result.Remaining} REMAIN";
    }

    private static string GetSortModeLabel(InventorySortMode mode)
    {
        return mode switch
        {
            InventorySortMode.ItemType => "BY ITEMTYPE",
            InventorySortMode.Name => "BY NAME",
            InventorySortMode.Rarity => "BY RARITY",
            InventorySortMode.Value => "BY VALUE",
            _ => "BY ITEMTYPE"
        };
    }

    private enum SlotSection
    {
        Hotbar,
        Main
    }

    private enum ControlAction
    {
        Mode,
        Sort,
        Compact
    }

    private sealed record FilterOption(string Label, IReadOnlyList<ItemCategory> Categories);

    private readonly record struct EquipmentSlotDisplay(EquipmentSlotType Slot, string Label);

    private readonly record struct SlotTarget(
        Inventory Inventory,
        InventorySlot Slot,
        SlotSection Section,
        int Index);
}

internal readonly record struct HitZone<T>(Rectangle Bounds, T Value, bool IsInteractive);

internal sealed class HitZoneBuffer<T>
{
    private HitZone<T>[] _zones;
    private int _count;

    public HitZoneBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        _zones = new HitZone<T>[capacity];
    }

    public int Count => _count;

    public int Capacity => _zones.Length;

    public void Add(Rectangle bounds, T value, bool isInteractive = true)
    {
        if (_count == _zones.Length)
        {
            Array.Resize(ref _zones, Math.Max(4, _zones.Length * 2));
        }

        _zones[_count++] = new HitZone<T>(bounds, value, isInteractive);
    }

    public void Clear()
    {
        _count = 0;
    }

    public HitZone<T>? FindTopmost(Point point)
    {
        for (var index = _count - 1; index >= 0; index--)
        {
            ref readonly var zone = ref _zones[index];
            if (zone.IsInteractive && zone.Bounds.Contains(point))
            {
                return zone;
            }
        }

        return null;
    }

    public bool TryGet(int index, out HitZone<T> zone)
    {
        if ((uint)index >= (uint)_count)
        {
            zone = default;
            return false;
        }

        zone = _zones[index];
        return true;
    }
}

internal sealed class InventorySectionQueryCache
{
    private readonly string _title;
    private Inventory? _inventory;
    private IItemDefinitionProvider? _items;
    private bool[] _interactive = Array.Empty<bool>();
    private long _version = -1;
    private int _filterKey = int.MinValue;

    public InventorySectionQueryCache(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        _title = title;
        Summary = title;
    }

    public string Summary { get; private set; }

    public int RefreshCount { get; private set; }

    public void Refresh(
        Inventory inventory,
        IItemDefinitionProvider items,
        IReadOnlyList<ItemCategory> categories,
        int filterKey,
        bool applyFilter)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(categories);

        var effectiveFilterKey = applyFilter ? filterKey : -1;
        if (ReferenceEquals(_inventory, inventory) &&
            ReferenceEquals(_items, items) &&
            _version == inventory.Version &&
            _filterKey == effectiveFilterKey)
        {
            return;
        }

        if (_interactive.Length < inventory.Slots.Count)
        {
            Array.Resize(ref _interactive, inventory.Slots.Count);
        }

        var usedSlots = 0;
        for (var index = 0; index < inventory.Slots.Count; index++)
        {
            var slot = inventory.Slots[index];
            if (slot.IsEmpty)
            {
                _interactive[index] = true;
                continue;
            }

            usedSlots++;
            _interactive[index] = !applyFilter || categories.Count == 0 || MatchesCategory(items, slot, categories);
        }

        _inventory = inventory;
        _items = items;
        _version = inventory.Version;
        _filterKey = effectiveFilterKey;
        Summary = $"{_title}  {usedSlots}/{inventory.Slots.Count}";
        RefreshCount++;
    }

    public bool IsInteractive(int slotIndex)
    {
        if ((uint)slotIndex >= (uint)(_inventory?.Slots.Count ?? 0))
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        return _interactive[slotIndex];
    }

    private static bool MatchesCategory(
        IItemDefinitionProvider items,
        InventorySlot slot,
        IReadOnlyList<ItemCategory> categories)
    {
        var category = items.GetById(slot.Stack.ItemId).ResolvedCategory;
        for (var index = 0; index < categories.Count; index++)
        {
            if (categories[index] == category)
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class InventoryStatisticsCache
{
    private InventoryQueryService? _service;
    private PlayerInventory? _inventory;
    private InventoryStatistics? _value;
    private long _hotbarVersion = -1;
    private long _mainVersion = -1;

    public string Summary { get; private set; } = string.Empty;

    public int RefreshCount { get; private set; }

    public InventoryStatistics? Get(InventoryQueryService? service, PlayerInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (service is null)
        {
            return null;
        }

        if (ReferenceEquals(_service, service) &&
            ReferenceEquals(_inventory, inventory) &&
            _hotbarVersion == inventory.Hotbar.Version &&
            _mainVersion == inventory.Main.Version)
        {
            return _value;
        }

        _service = service;
        _inventory = inventory;
        _hotbarVersion = inventory.Hotbar.Version;
        _mainVersion = inventory.Main.Version;
        _value = service.GetStatistics(inventory);
        Summary = $"USED {_value.UsedSlots}   FREE {_value.AvailableSlots}   ITEMS {_value.TotalItems}   VALUE {_value.TotalValue}";
        RefreshCount++;
        return _value;
    }

    public void Clear()
    {
        _service = null;
        _inventory = null;
        _value = null;
        _hotbarVersion = -1;
        _mainVersion = -1;
        Summary = string.Empty;
    }
}

internal sealed class InventoryTooltipCache
{
    private readonly Dictionary<string, InventoryTooltipContent> _entries = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _entries.Count;

    public int BuildCount { get; private set; }

    public InventoryTooltipContent Get(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_entries.TryGetValue(item.Id, out var cached))
        {
            return cached;
        }

        cached = InventoryTooltipContent.Create(item);
        _entries.Add(item.Id, cached);
        BuildCount++;
        return cached;
    }

    public void Clear()
    {
        _entries.Clear();
    }
}

internal sealed class InventoryTooltipContent
{
    private readonly int _maxStack;
    private int _cachedStackCount = -1;
    private bool _cachedFavorite;
    private string _cachedStackLine = string.Empty;

    private InventoryTooltipContent(
        int maxStack,
        string classification,
        string[] descriptionLines,
        string[] detailLines)
    {
        _maxStack = maxStack;
        Classification = classification;
        DescriptionLines = descriptionLines;
        DetailLines = detailLines;
    }

    public string Classification { get; }

    public string[] DescriptionLines { get; }

    public string[] DetailLines { get; }

    public string GetStackLine(int count, bool favorite)
    {
        if (_cachedStackCount != count || _cachedFavorite != favorite)
        {
            _cachedStackCount = count;
            _cachedFavorite = favorite;
            _cachedStackLine = $"STACK {count}/{_maxStack}{(favorite ? "  FAVORITE" : string.Empty)}";
        }

        return _cachedStackLine;
    }

    public static InventoryTooltipContent Create(ItemDefinition item)
    {
        var classification = $"{item.Rarity.ToString().ToUpperInvariant()}  {item.ResolvedCategory.ToString().ToUpperInvariant()}  VALUE {item.Value}";
        return new InventoryTooltipContent(
            item.MaxStack,
            classification,
            WrapText(item.Description, 42, 3),
            BuildDetailLines(item, 5));
    }

    private static string[] WrapText(string text, int maxCharacters, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text) || maxLines <= 0)
        {
            return Array.Empty<string>();
        }

        var lines = new List<string>(maxLines);
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var line = string.Empty;
        foreach (var word in words)
        {
            if (line.Length == 0)
            {
                line = word;
                continue;
            }

            if (line.Length + word.Length + 1 <= maxCharacters)
            {
                line += $" {word}";
                continue;
            }

            lines.Add(line);
            if (lines.Count == maxLines)
            {
                return lines.ToArray();
            }

            line = word;
        }

        if (line.Length > 0 && lines.Count < maxLines)
        {
            lines.Add(line);
        }

        return lines.ToArray();
    }

    private static string[] BuildDetailLines(ItemDefinition item, int maxLines)
    {
        var lines = new List<string>(maxLines);
        var primary = new List<string>(4);
        AddIfPositive(primary, "DMG", item.Damage);
        AddIfPositive(primary, "POWER", item.ToolPower);
        AddIfPositive(primary, "DEF", item.Defense);
        AddIfPositive(primary, "HEALS", item.HealthRestore);
        AddJoinedLine(lines, primary, maxLines);

        var modifiers = new List<string>(9);
        AddIfNonZero(modifiers, "HEALTH", item.MaxHealthBonus);
        AddIfSignificant(modifiers, "MOVE", item.MovementSpeedBonus);
        AddIfSignificant(modifiers, "MELEE", item.MeleeDamageBonus);
        AddIfSignificant(modifiers, "RANGED", item.RangedDamageBonus);
        AddIfSignificant(modifiers, "MINING", item.MiningSpeedBonus);
        AddIfNonZero(modifiers, "MANA", item.MaxManaBonus);
        AddIfSignificant(modifiers, "MAGIC", item.MagicDamageBonus);
        if (Math.Abs(item.ManaCostReduction) > 0.001f)
        {
            modifiers.Add($"MANA COST -{item.ManaCostReduction:0.##}");
        }

        AddIfSignificant(modifiers, "MANA REGEN", item.ManaRegenBonus);
        for (var start = 0; start < modifiers.Count && lines.Count < maxLines; start += 3)
        {
            lines.Add(string.Join("  ", modifiers.GetRange(start, Math.Min(3, modifiers.Count - start))));
        }

        if (lines.Count < maxLines && (item.ManaCost > 0 || item.ManaRestore > 0))
        {
            lines.Add(item.ManaCost > 0 ? $"MANA COST {item.ManaCost}" : $"RESTORES {item.ManaRestore} MANA");
        }

        if (lines.Count < maxLines && item.OnHitEffects.Count > 0)
        {
            var effects = new string[Math.Min(2, item.OnHitEffects.Count)];
            for (var index = 0; index < effects.Length; index++)
            {
                effects[index] = item.OnHitEffects[index].EffectId.ToUpperInvariant();
            }

            lines.Add($"EFFECT {string.Join(" ", effects)}");
        }

        if (lines.Count < maxLines && item.Tags.Count > 0)
        {
            var tags = new string[Math.Min(3, item.Tags.Count)];
            for (var index = 0; index < tags.Length; index++)
            {
                tags[index] = item.Tags[index].ToUpperInvariant();
            }

            lines.Add($"TAGS {string.Join(" ", tags)}");
        }

        return lines.ToArray();
    }

    private static void AddIfPositive(List<string> values, string label, int value)
    {
        if (value > 0)
        {
            values.Add($"{label} {value}");
        }
    }

    private static void AddIfNonZero(List<string> values, string label, int value)
    {
        if (value != 0)
        {
            values.Add($"{label} {value}");
        }
    }

    private static void AddIfSignificant(List<string> values, string label, float value)
    {
        if (Math.Abs(value) > 0.001f)
        {
            values.Add($"{label} {value:0.##}");
        }
    }

    private static void AddJoinedLine(List<string> lines, List<string> values, int maxLines)
    {
        if (values.Count > 0 && lines.Count < maxLines)
        {
            lines.Add(string.Join("  ", values));
        }
    }
}
