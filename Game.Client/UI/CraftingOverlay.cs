using Game.Client.Input;
using Game.Client.Rendering;
using Game.Core;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Settings;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.UI;

public sealed class CraftingOverlay
{
    private const int VisibleRecipeRows = 10;
    private const int MaxSearchLength = 48;
    private const int MaxRequestedQuantity = 999;
    private static readonly string[] CategoryFilters = ["ALL", "BLOCKS", "TOOLS", "WEAPONS", "MAGIC", "FURNITURE"];
    private static readonly RecipeVisibilityMode[] VisibilityModes =
    [
        RecipeVisibilityMode.Known,
        RecipeVisibilityMode.Craftable,
        RecipeVisibilityMode.All
    ];

    private readonly CraftingSystem _crafting;
    private readonly CraftingStationLocator _stations;
    private readonly RecipeSearchService _recipeSearch;
    private readonly HitZoneBuffer<int> _recipeHitZones = new(32);
    private readonly HitZoneBuffer<int> _categoryHitZones = new(CategoryFilters.Length);
    private readonly HitZoneBuffer<RecipeVisibilityMode> _visibilityHitZones = new(VisibilityModes.Length);
    private readonly UiGamepadNavigator _gamepad = new();
    private Rectangle _searchBounds;
    private Rectangle _decreaseQuantityBounds;
    private Rectangle _increaseQuantityBounds;
    private Rectangle _maximumQuantityBounds;
    private Rectangle _craftButtonBounds;
    private Rectangle _pinButtonBounds;
    private IReadOnlyList<CraftingQueryResult> _lastResults = Array.Empty<CraftingQueryResult>();
    private CraftingContext? _lastContext;
    private CraftingBatchPlan? _selectedPlan;
    private int _selectedIndex;
    private int _scroll;
    private int _visibleRecipeRows = VisibleRecipeRows;
    private int _categoryIndex;
    private int _requestedQuantity = 1;
    private CraftingFocusArea _focusArea = CraftingFocusArea.Recipes;
    private string? _selectedRecipeId;
    private string? _status;
    private bool _hasRefreshKey;
    private long _lastHotbarVersion;
    private long _lastMainVersion;
    private long _lastTrackingVersion;
    private TilePos _lastActorTile;
    private int _lastRadiusTiles;
    private int _lastCategoryIndex;
    private RecipeVisibilityMode _lastVisibilityMode;
    private string _lastSearchQuery = string.Empty;
    private Point _mousePosition;

    public CraftingOverlay()
        : this(new CraftingSystem(), new CraftingStationLocator(), new RecipeSearchService(), new RecipeTrackingState())
    {
    }

    public CraftingOverlay(
        CraftingSystem crafting,
        CraftingStationLocator stations,
        RecipeSearchService recipeSearch,
        RecipeTrackingState trackingState)
    {
        _crafting = crafting ?? throw new ArgumentNullException(nameof(crafting));
        _stations = stations ?? throw new ArgumentNullException(nameof(stations));
        _recipeSearch = recipeSearch ?? throw new ArgumentNullException(nameof(recipeSearch));
        TrackingState = trackingState ?? throw new ArgumentNullException(nameof(trackingState));
    }

    public event Action<CraftingBatchResult>? CraftingResolved;

    public bool IsOpen { get; private set; }

    public bool IsSearchFocused { get; private set; }

    public string SearchQuery { get; private set; } = string.Empty;

    public RecipeVisibilityMode VisibilityMode { get; private set; } = RecipeVisibilityMode.Known;

    public int RequestedQuantity => _requestedQuantity;

    public RecipeTrackingState TrackingState { get; }

    public CraftingBatchResult? LastCraftResult { get; private set; }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        IsSearchFocused = false;
        _focusArea = CraftingFocusArea.Recipes;
        _status = IsOpen ? "READY" : null;
        _gamepad.Reset();
    }

    public void Close()
    {
        IsOpen = false;
        IsSearchFocused = false;
        _focusArea = CraftingFocusArea.Recipes;
        _status = null;
        _gamepad.Reset();
    }

    public void SetSearchQuery(string? query)
    {
        var normalized = new string((query ?? string.Empty).Where(character => !char.IsControl(character)).ToArray());
        SearchQuery = normalized.Length <= MaxSearchLength ? normalized : normalized[..MaxSearchLength];
        ResetSelection();
    }

    public void SetSearchFocused(bool focused)
    {
        IsSearchFocused = IsOpen && focused;
    }

    public void SetVisibilityMode(RecipeVisibilityMode visibility)
    {
        if (!VisibilityModes.Contains(visibility))
        {
            throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unsupported recipe visibility mode.");
        }

        VisibilityMode = visibility;
        ResetSelection();
    }

    public void OnTextInput(char character)
    {
        if (!IsOpen || !IsSearchFocused || char.IsControl(character) || SearchQuery.Length >= MaxSearchLength)
        {
            return;
        }

        SearchQuery += character;
        ResetSelection();
    }

    public bool Update(
        InputManager input,
        PlayerInventory inventory,
        GameContentDatabase content,
        World world,
        PlayerEntity player,
        GameSettings settings,
        double deltaSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(settings);
        _mousePosition = input.MousePosition;

        if (input.IsBindingPressed(settings.Input.KeyBindings.OpenCrafting))
        {
            Toggle();
            if (IsOpen)
            {
                RefreshResults(inventory, content, world, player, settings);
            }

            return true;
        }

        if (!IsOpen)
        {
            return false;
        }

        _gamepad.Update(deltaSeconds);
        if (IsSearchFocused && _gamepad.CancelPressed)
        {
            IsSearchFocused = false;
            _focusArea = CraftingFocusArea.Search;
            return true;
        }

        if (input.IsKeyPressed(Keys.Escape) || _gamepad.CancelPressed)
        {
            Close();
            return true;
        }

        if (IsSearchFocused)
        {
            UpdateFocusedSearch(input);
            RefreshResults(inventory, content, world, player, settings);
            UpdateMouse(input);
            return true;
        }

        if (input.IsBindingPressed(settings.Input.KeyBindings.OpenInventory))
        {
            Close();
            return true;
        }

        RefreshResults(inventory, content, world, player, settings);
        UpdateFocus(input);
        UpdateMouse(input);

        return true;
    }

    public void Draw(RenderContext context, GameContentDatabase content, ClientTextureRegistry? textures, GameSettings settings)
    {
        if (!IsOpen)
        {
            return;
        }

        var palette = UiTheme.Resolve(settings);
        var layout = PixelCraftingLayoutPlanner.Resolve(context.ViewportBounds);
        UiTheme.DrawBackdrop(context, palette, settings.Ui.MenuBackdropOpacity * 0.9f, settings);
        UiTheme.DrawPanel(context, layout.Panel, palette, settings.Ui.PanelOpacity);
        UiTheme.DrawHeader(context, layout.Header, palette, settings: settings);
        DrawHeader(context, palette, layout, textures);
        DrawCategoryTabs(context, palette, layout.Categories);
        DrawRecipeList(context, palette, layout, content.Items, textures);
        DrawRecipeDetails(context, palette, layout, content.Items, textures);

        if (!string.IsNullOrWhiteSpace(_status))
        {
            context.DebugText.Draw(
                new Vector2(layout.StatusBar.X, layout.StatusBar.Y + Math.Max(1, (layout.StatusBar.Height - 7) / 2)),
                AbbreviateText(_status, Math.Max(3, layout.StatusBar.Width / 8)),
                LastCraftResult?.IsSuccess == false ? palette.Danger : palette.Warning,
                1);
        }

        if (settings.Ui.ShowControlHints && layout.StatusBar.Width >= 220)
        {
            var hint = layout.Density == PixelUiDensity.Compact
                ? "TAB MOVE  ENTER USE  ESC CLOSE"
                : "TAB/SHOULDERS MOVE   ARROWS ADJUST   ENTER/A USE   ESC/B CLOSE";
            var shown = AbbreviateText(hint, Math.Max(4, (layout.StatusBar.Width - 96) / 6));
            context.DebugText.Draw(
                new Vector2(
                    Math.Max(layout.StatusBar.X, layout.StatusBar.Right - shown.Length * 6),
                    layout.StatusBar.Y + Math.Max(1, (layout.StatusBar.Height - 7) / 2)),
                shown,
                palette.TextMuted,
                1);
        }
    }

    private void DrawHeader(
        RenderContext context,
        UiPalette palette,
        in PixelCraftingLayout layout,
        ClientTextureRegistry? textures)
    {
        var title = layout.Title;
        var iconSize = layout.Density == PixelUiDensity.Compact ? 22 : 32;
        var iconBounds = new Rectangle(
            title.X,
            title.Y + Math.Max(0, (title.Height - iconSize) / 2),
            Math.Min(iconSize, title.Width),
            Math.Min(iconSize, title.Height));
        var drewIcon = ItemIconRenderer.TryDrawSprite(context, textures, "ui/crafting_hammer", iconBounds);
        context.DebugText.Draw(
            new Vector2(
                title.X + (drewIcon ? iconBounds.Width + 5 : 2),
                title.Y + Math.Max(2, (title.Height - 7) / 2)),
            layout.Density == PixelUiDensity.Compact ? "CRAFT" : "CRAFTING",
            palette.Accent,
            1);

        _searchBounds = layout.Search;
        UiTheme.DrawButton(
            context,
            _searchBounds,
            palette,
            selected: IsSearchFocused || _focusArea == CraftingFocusArea.Search,
            hovered: _searchBounds.Contains(_mousePosition));
        var searchText = SearchQuery.Length == 0
            ? "SEARCH"
            : AbbreviateText(SearchQuery, Math.Max(3, (_searchBounds.Width - 18) / 8));
        var searchColor = SearchQuery.Length == 0 ? palette.TextMuted : palette.Text;
        context.DebugText.Draw(
            new Vector2(_searchBounds.X + 7, _searchBounds.Y + Math.Max(2, (_searchBounds.Height - 7) / 2)),
            searchText + (IsSearchFocused ? "_" : string.Empty),
            searchColor,
            1);

        _visibilityHitZones.Clear();
        var visibility = layout.Visibility;
        var gap = layout.Density == PixelUiDensity.Compact ? 2 : 3;
        var segmentWidth = Math.Max(1, (visibility.Width - gap * (VisibilityModes.Length - 1)) / VisibilityModes.Length);
        for (var index = 0; index < VisibilityModes.Length; index++)
        {
            var x = visibility.X + index * (segmentWidth + gap);
            var width = index == VisibilityModes.Length - 1 ? Math.Max(1, visibility.Right - x) : segmentWidth;
            var bounds = new Rectangle(x, visibility.Y, width, visibility.Height);
            var mode = VisibilityModes[index];
            var selected = mode == VisibilityMode;
            UiTheme.DrawButton(context, bounds, palette, selected, bounds.Contains(_mousePosition));
            context.DebugText.Draw(
                new Vector2(bounds.X + 4, bounds.Y + Math.Max(2, (bounds.Height - 7) / 2)),
                AbbreviateText(VisibilityLabel(mode), Math.Max(2, (bounds.Width - 7) / 8)),
                selected ? palette.Text : palette.TextMuted,
                1);
            _visibilityHitZones.Add(bounds, mode);
        }

        if (_focusArea == CraftingFocusArea.Visibility)
        {
            UiTheme.DrawFocusFrame(context, new Rectangle(visibility.X - 2, visibility.Y - 2, visibility.Width + 4, visibility.Height + 4), palette);
        }
    }

    private void RefreshResults(
        PlayerInventory inventory,
        GameContentDatabase content,
        World world,
        PlayerEntity player,
        GameSettings settings)
    {
        var actorTile = CoordinateUtils.WorldToTile(player.Body.Center.X, player.Body.Center.Y);
        var radiusTiles = Math.Max(2, (int)MathF.Ceiling(settings.Gameplay.InteractionReachPixels / GameConstants.TileSize));
        if (_hasRefreshKey &&
            _lastHotbarVersion == inventory.Hotbar.Version &&
            _lastMainVersion == inventory.Main.Version &&
            _lastTrackingVersion == TrackingState.Version &&
            _lastActorTile == actorTile &&
            _lastRadiusTiles == radiusTiles &&
            _lastCategoryIndex == _categoryIndex &&
            _lastVisibilityMode == VisibilityMode &&
            string.Equals(_lastSearchQuery, SearchQuery, StringComparison.Ordinal))
        {
            return;
        }

        _hasRefreshKey = true;
        _lastHotbarVersion = inventory.Hotbar.Version;
        _lastMainVersion = inventory.Main.Version;
        _lastTrackingVersion = TrackingState.Version;
        _lastActorTile = actorTile;
        _lastRadiusTiles = radiusTiles;
        _lastCategoryIndex = _categoryIndex;
        _lastVisibilityMode = VisibilityMode;
        _lastSearchQuery = SearchQuery;
        _lastContext = _stations.CreateContext(inventory, world, content.Tiles, actorTile, radiusTiles);
        var previousId = SelectedResult()?.Recipe.Id ?? _selectedRecipeId;
        var query = new RecipeSearchQuery
        {
            SearchText = SearchQuery,
            Visibility = VisibilityMode,
            Sort = string.IsNullOrWhiteSpace(SearchQuery) ? RecipeSortMode.CraftableFirst : RecipeSortMode.Relevance,
            PinnedFirst = true
        };

        _lastResults = _recipeSearch
            .Search(_crafting.QueryRecipes(_lastContext, content.Recipes), query, content.Items, TrackingState)
            .Where(result => MatchesCategory(result, content.Items))
            .ToArray();

        if (_lastResults.Count == 0)
        {
            _selectedIndex = 0;
            _selectedRecipeId = null;
            _selectedPlan = null;
            _scroll = 0;
            return;
        }

        var preservedIndex = previousId is null
            ? -1
            : FindRecipeIndex(previousId);
        _selectedIndex = preservedIndex >= 0
            ? preservedIndex
            : Math.Clamp(_selectedIndex, 0, _lastResults.Count - 1);
        _selectedRecipeId = _lastResults[_selectedIndex].Recipe.Id;
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _lastResults.Count - _visibleRecipeRows));
        EnsureSelectionVisible();
        _selectedPlan = _crafting.PlanCraft(_lastContext, _lastResults[_selectedIndex].Recipe, _requestedQuantity);
    }

    private void UpdateFocusedSearch(InputManager input)
    {
        if (input.IsKeyPressed(Keys.Enter) || _gamepad.ConfirmPressed)
        {
            IsSearchFocused = false;
            return;
        }

        if (input.IsKeyPressed(Keys.Back) && SearchQuery.Length > 0)
        {
            SearchQuery = SearchQuery[..^1];
            ResetSelection();
        }
        else if (input.IsKeyPressed(Keys.Delete) && SearchQuery.Length > 0)
        {
            SetSearchQuery(string.Empty);
        }
    }

    private void UpdateFocus(InputManager input)
    {
        var shiftDown = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);
        var tabPressed = input.IsKeyPressed(Keys.Tab);
        var navigationInput = new CraftingNavigationInput(
            Up: input.IsKeyPressed(Keys.Up) || input.IsKeyPressed(Keys.W) || input.ScrollDelta > 0 || _gamepad.UpPressed,
            Down: input.IsKeyPressed(Keys.Down) || input.IsKeyPressed(Keys.S) || input.ScrollDelta < 0 || _gamepad.DownPressed,
            Left: input.IsKeyPressed(Keys.Left) || input.IsKeyPressed(Keys.A) || input.IsKeyPressed(Keys.OemMinus) || _gamepad.LeftPressed,
            Right: input.IsKeyPressed(Keys.Right) || input.IsKeyPressed(Keys.D) || input.IsKeyPressed(Keys.OemPlus) || _gamepad.RightPressed,
            Confirm: input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.Space) || _gamepad.ConfirmPressed,
            Cancel: false,
            PreviousFocus: tabPressed && shiftDown || _gamepad.PreviousTabPressed,
            NextFocus: tabPressed && !shiftDown || _gamepad.NextTabPressed);
        var intent = CraftingNavigationPlanner.Resolve(_focusArea, navigationInput);
        if (intent.Focus != _focusArea)
        {
            _focusArea = intent.Focus;
            IsSearchFocused = false;
            return;
        }

        if (intent.RecipeDelta != 0 && _lastResults.Count > 0)
        {
            SelectIndex(Math.Clamp(_selectedIndex + intent.RecipeDelta, 0, _lastResults.Count - 1));
            EnsureSelectionVisible();
            return;
        }

        if (intent.ValueDelta != 0)
        {
            AdjustFocusedValue(intent.ValueDelta);
            return;
        }

        if (intent.Activate)
        {
            ActivateFocusedArea();
        }
    }

    private void AdjustFocusedValue(int direction)
    {
        if (_focusArea == CraftingFocusArea.Visibility)
        {
            var index = Array.IndexOf(VisibilityModes, VisibilityMode);
            SetVisibilityMode(VisibilityModes[(index + direction + VisibilityModes.Length) % VisibilityModes.Length]);
            _status = $"SHOW {VisibilityLabel(VisibilityMode)}";
        }
        else if (_focusArea == CraftingFocusArea.Category)
        {
            _categoryIndex = (_categoryIndex + direction + CategoryFilters.Length) % CategoryFilters.Length;
            ResetSelection();
            _status = $"CATEGORY {CategoryFilters[_categoryIndex]}";
        }
        else if (_focusArea == CraftingFocusArea.Quantity)
        {
            AdjustQuantity(direction);
        }
    }

    private void ActivateFocusedArea()
    {
        switch (_focusArea)
        {
            case CraftingFocusArea.Search:
                IsSearchFocused = true;
                break;
            case CraftingFocusArea.Visibility:
                AdjustFocusedValue(1);
                break;
            case CraftingFocusArea.Category:
                AdjustFocusedValue(1);
                break;
            case CraftingFocusArea.Recipes:
            case CraftingFocusArea.Craft:
                CraftSelected();
                break;
            case CraftingFocusArea.Quantity:
                SetMaximumQuantity();
                break;
            case CraftingFocusArea.Pin:
                ToggleSelectedPin();
                break;
        }
    }

    private void UpdateMouse(InputManager input)
    {
        if (!input.IsLeftMousePressed)
        {
            return;
        }

        if (_searchBounds.Contains(input.MousePosition))
        {
            _focusArea = CraftingFocusArea.Search;
            IsSearchFocused = true;
            return;
        }

        IsSearchFocused = false;
        var visibility = _visibilityHitZones.FindTopmost(input.MousePosition);
        if (visibility is { } visibilityZone)
        {
            _focusArea = CraftingFocusArea.Visibility;
            SetVisibilityMode(visibilityZone.Value);
            _status = $"SHOW {VisibilityLabel(visibilityZone.Value)}";
            return;
        }

        var category = _categoryHitZones.FindTopmost(input.MousePosition);
        if (category is { } categoryZone)
        {
            _focusArea = CraftingFocusArea.Category;
            _categoryIndex = categoryZone.Value;
            ResetSelection();
            _status = $"CATEGORY {CategoryFilters[_categoryIndex]}";
            return;
        }

        var recipe = _recipeHitZones.FindTopmost(input.MousePosition);
        if (recipe is { } recipeZone)
        {
            _focusArea = CraftingFocusArea.Recipes;
            SelectIndex(recipeZone.Value);
            return;
        }

        if (_decreaseQuantityBounds.Contains(input.MousePosition))
        {
            _focusArea = CraftingFocusArea.Quantity;
            AdjustQuantity(-1);
        }
        else if (_increaseQuantityBounds.Contains(input.MousePosition))
        {
            _focusArea = CraftingFocusArea.Quantity;
            AdjustQuantity(1);
        }
        else if (_maximumQuantityBounds.Contains(input.MousePosition))
        {
            _focusArea = CraftingFocusArea.Quantity;
            SetMaximumQuantity();
        }
        else if (_pinButtonBounds.Contains(input.MousePosition))
        {
            _focusArea = CraftingFocusArea.Pin;
            ToggleSelectedPin();
        }
        else if (_craftButtonBounds.Contains(input.MousePosition))
        {
            _focusArea = CraftingFocusArea.Craft;
            CraftSelected();
        }
    }

    private void DrawCategoryTabs(RenderContext context, UiPalette palette, Rectangle bounds)
    {
        _categoryHitZones.Clear();
        var gap = bounds.Width < 520 ? 2 : 5;
        var tabWidth = Math.Max(1, (bounds.Width - gap * (CategoryFilters.Length - 1)) / CategoryFilters.Length);
        for (var index = 0; index < CategoryFilters.Length; index++)
        {
            var tab = new Rectangle(bounds.X + index * (tabWidth + gap), bounds.Y, tabWidth, bounds.Height);
            var selected = index == _categoryIndex;
            UiTheme.DrawButton(context, tab, palette, selected, tab.Contains(_mousePosition));
            context.DebugText.Draw(new Vector2(tab.X + 7, tab.Y + 9), AbbreviateText(CategoryFilters[index], Math.Max(4, tabWidth / 8)), selected ? palette.Text : palette.TextMuted, 1);
            _categoryHitZones.Add(tab, index);
        }

        if (_focusArea == CraftingFocusArea.Category)
        {
            UiTheme.DrawFocusFrame(context, new Rectangle(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4), palette);
        }
    }

    private void DrawRecipeList(
        RenderContext context,
        UiPalette palette,
        in PixelCraftingLayout layout,
        IItemDefinitionProvider items,
        ClientTextureRegistry? textures)
    {
        var bounds = layout.RecipeList;
        context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(palette.Surface, 0.78f));
        UiTheme.DrawBorder(context, bounds, UiTheme.WithAlpha(palette.SurfaceHover, 0.9f), 1);
        context.DebugText.Draw(
            new Vector2(layout.RecipeHeader.X + 6, layout.RecipeHeader.Y + Math.Max(2, (layout.RecipeHeader.Height - 7) / 2)),
            layout.Density == PixelUiDensity.Compact ? "RECIPES" : "RECIPE LIST",
            palette.Text,
            1);
        var positionLabel = _lastResults.Count == 0
            ? "0"
            : $"{_selectedIndex + 1}/{_lastResults.Count}";
        context.DebugText.Draw(
            new Vector2(
                Math.Max(layout.RecipeHeader.X + 52, layout.RecipeHeader.Right - positionLabel.Length * 6 - 5),
                layout.RecipeHeader.Y + Math.Max(2, (layout.RecipeHeader.Height - 7) / 2)),
            positionLabel,
            palette.TextMuted,
            1);

        _recipeHitZones.Clear();
        if (_lastResults.Count == 0)
        {
            var emptyText = layout.RecipeRows.Width >= 104 ? "NO MATCHING RECIPES" : "NO RECIPES";
            context.DebugText.Draw(
                new Vector2(layout.RecipeRows.X + 4, layout.RecipeRows.Y + 7),
                AbbreviateText(emptyText, Math.Max(3, layout.RecipeRows.Width / 6)),
                palette.TextMuted,
                1);
            if (layout.RecipeRows.Height >= 30)
            {
                context.DebugText.Draw(
                    new Vector2(layout.RecipeRows.X + 4, layout.RecipeRows.Y + 22),
                    AbbreviateText("CHANGE FILTER OR SEARCH", Math.Max(3, layout.RecipeRows.Width / 6)),
                    palette.Warning,
                    1);
            }

            return;
        }

        var rowHeight = layout.Density switch
        {
            PixelUiDensity.Compact => 24,
            PixelUiDensity.Expanded => 34,
            _ => 30
        };
        _visibleRecipeRows = Math.Max(1, layout.RecipeRows.Height / rowHeight);
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _lastResults.Count - _visibleRecipeRows));
        EnsureSelectionVisible();
        var end = Math.Min(_lastResults.Count, _scroll + _visibleRecipeRows);
        for (var index = _scroll; index < end; index++)
        {
            var row = index - _scroll;
            var result = _lastResults[index];
            var rowBounds = new Rectangle(
                layout.RecipeRows.X,
                layout.RecipeRows.Y + row * rowHeight,
                layout.RecipeRows.Width,
                Math.Max(1, rowHeight - 3));
            var selected = index == _selectedIndex;
            UiTheme.DrawButton(context, rowBounds, palette, selected, rowBounds.Contains(_mousePosition), result.IsKnown);
            if (selected && _focusArea == CraftingFocusArea.Recipes)
            {
                UiTheme.DrawFocusFrame(context, rowBounds, palette);
            }

            var item = items.GetById(result.Recipe.Result.ItemId);
            var iconSize = Math.Max(10, Math.Min(20, rowBounds.Height - 4));
            var iconBounds = new Rectangle(rowBounds.X + 3, rowBounds.Y + 2, iconSize, iconSize);
            var availability = CraftingAvailabilityPresenter.Resolve(result);
            ItemIconRenderer.TryDrawSprite(
                context,
                textures,
                item.TexturePath,
                iconBounds,
                availability.CanCraft ? 1f : 0.48f);
            var pinned = TrackingState.IsPinned(result.Recipe.Id);
            if (pinned)
            {
                context.DebugText.Draw(
                    new Vector2(iconBounds.Right + 2, rowBounds.Y + Math.Max(2, (rowBounds.Height - 7) / 2)),
                    "*",
                    palette.Warning,
                    1);
            }

            var nameX = iconBounds.Right + (pinned ? 10 : 4);
            var availabilityLabel = availability.CanCraft && result.MaxCraftable > 0
                ? $"x{result.MaxCraftable}"
                : availability.CompactLabel;
            var stateWidth = availabilityLabel.Length * 6 + 6;
            var maximumNameCharacters = Math.Max(2, (rowBounds.Right - nameX - stateWidth) / 6);
            context.DebugText.Draw(
                new Vector2(nameX, rowBounds.Y + Math.Max(2, (rowBounds.Height - 7) / 2)),
                AbbreviateText(item.DisplayName, maximumNameCharacters),
                availability.CanCraft ? palette.Text : palette.TextMuted,
                1);
            context.DebugText.Draw(
                new Vector2(rowBounds.Right - stateWidth + 3, rowBounds.Y + Math.Max(2, (rowBounds.Height - 7) / 2)),
                availabilityLabel,
                AvailabilityColor(palette, availability),
                1);
            _recipeHitZones.Add(rowBounds, index);
        }
    }

    private void DrawRecipeDetails(
        RenderContext context,
        UiPalette palette,
        in PixelCraftingLayout layout,
        IItemDefinitionProvider items,
        ClientTextureRegistry? textures)
    {
        var bounds = layout.Details;
        context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(palette.Surface, 0.78f));
        UiTheme.DrawBorder(context, bounds, UiTheme.WithAlpha(palette.SurfaceHover, 0.9f), 1);
        var result = SelectedResult();
        if (result is null || _selectedPlan is null)
        {
            context.DebugText.Draw(
                new Vector2(layout.DetailsHeader.X + 4, layout.DetailsHeader.Y + 5),
                layout.CompactDetails ? "SELECT RECIPE" : "SELECT A RECIPE TO VIEW DETAILS",
                palette.TextMuted,
                1);
            if (layout.IngredientList.Height >= 20)
            {
                context.DebugText.Draw(
                    new Vector2(layout.IngredientList.X + 3, layout.IngredientList.Y + 4),
                    "MATERIALS AND ACTIONS APPEAR HERE",
                    palette.TextMuted,
                    1);
            }

            ClearActionBounds();
            return;
        }

        var recipe = result.Recipe;
        var plan = _selectedPlan;
        var item = items.GetById(recipe.Result.ItemId);
        var availability = CraftingAvailabilityPresenter.Resolve(plan);
        var availabilityColor = AvailabilityColor(palette, availability);
        var header = layout.DetailsHeader;
        var resultIconSize = layout.CompactDetails ? 28 : 46;
        resultIconSize = Math.Min(resultIconSize, Math.Max(0, header.Height - 8));
        var resultIcon = new Rectangle(header.X, header.Y + 4, resultIconSize, resultIconSize);
        if (!resultIcon.IsEmpty)
        {
            UiTheme.DrawSlot(context, resultIcon, palette, selected: true, hovered: false);
            ItemIconRenderer.DrawItemStack(context, textures, items, recipe.Result, resultIcon, palette);
        }

        var pinWidth = Math.Min(layout.CompactDetails ? 46 : 68, Math.Max(0, header.Width / 3));
        var pinHeight = Math.Min(layout.CompactDetails ? 20 : 26, header.Height);
        _pinButtonBounds = new Rectangle(header.Right - pinWidth, header.Y + 3, pinWidth, pinHeight);
        var pinned = TrackingState.IsPinned(recipe.Id);
        UiTheme.DrawButton(context, _pinButtonBounds, palette, pinned, _pinButtonBounds.Contains(_mousePosition));
        if (_focusArea == CraftingFocusArea.Pin)
        {
            UiTheme.DrawFocusFrame(context, _pinButtonBounds, palette);
        }
        var pinLabel = pinned ? (layout.CompactDetails ? "ON" : "PINNED") : "PIN";
        context.DebugText.Draw(
            new Vector2(_pinButtonBounds.X + 5, _pinButtonBounds.Y + Math.Max(2, (_pinButtonBounds.Height - 7) / 2)),
            pinLabel,
            pinned ? palette.Text : palette.TextMuted,
            1);

        var nameX = resultIcon.Right + 7;
        var nameRight = Math.Max(nameX, _pinButtonBounds.X - 4);
        var nameScale = layout.CompactDetails ? 1 : 2;
        context.DebugText.Draw(
            new Vector2(nameX, header.Y + 5),
            AbbreviateText(item.DisplayName, Math.Max(2, (nameRight - nameX) / (6 * nameScale))),
            palette.Text,
            nameScale);
        var stateY = header.Y + (layout.CompactDetails ? 21 : 35);
        context.DebugText.Draw(
            new Vector2(nameX, stateY),
            AbbreviateText(availability.Label, Math.Max(3, (header.Right - nameX) / 6)),
            availabilityColor,
            1);
        if (header.Height >= (layout.CompactDetails ? 44 : 60))
        {
            context.DebugText.Draw(
                new Vector2(nameX, stateY + 14),
                $"MAX {plan.MaxCraftable}   OUTPUT {recipe.Result.Count} EACH",
                palette.TextMuted,
                1);
        }

        var ingredientBounds = layout.IngredientList;
        if (ingredientBounds.IsEmpty)
        {
            DrawQuantityControls(context, palette, layout.ActionBar, plan);
            return;
        }

        var y = ingredientBounds.Y;
        var stationLabel = recipe.Station is null ? "HAND CRAFT" : recipe.Station.ToUpperInvariant();
        context.DebugText.Draw(
            new Vector2(ingredientBounds.X + 2, y + 2),
            AbbreviateText(
                result.HasStation ? $"STATION  {stationLabel}" : $"MISSING STATION  {stationLabel}",
                Math.Max(3, ingredientBounds.Width / 6)),
            result.HasStation ? palette.TextMuted : palette.Danger,
            1);
        y += layout.CompactDetails ? 15 : 18;
        if (y + 7 < ingredientBounds.Bottom)
        {
            context.DebugText.Draw(
                new Vector2(ingredientBounds.X + 2, y),
                plan.Ingredients.Count == 0 ? "NO MATERIALS REQUIRED" : "MATERIALS",
                palette.Text,
                1);
            y += layout.CompactDetails ? 13 : 17;
        }

        if (availability.State == CraftingAvailabilityState.Locked)
        {
            if (y + 7 < ingredientBounds.Bottom)
            {
                context.DebugText.Draw(
                    new Vector2(ingredientBounds.X + 2, y),
                    AbbreviateText("DISCOVER THIS RECIPE TO VIEW REQUIREMENTS", Math.Max(3, ingredientBounds.Width / 6)),
                    palette.Warning,
                    1);
            }

            DrawQuantityControls(context, palette, layout.ActionBar, plan);
            return;
        }

        var ingredientRowHeight = layout.CompactDetails ? 19 : 27;
        var maxIngredientRows = Math.Max(0, (ingredientBounds.Bottom - y) / ingredientRowHeight);
        var visibleIngredientRows = Math.Min(plan.Ingredients.Count, maxIngredientRows);
        for (var ingredientIndex = 0; ingredientIndex < visibleIngredientRows; ingredientIndex++)
        {
            var ingredient = plan.Ingredients[ingredientIndex];
            var ingredientItem = items.GetById(ingredient.ItemId);
            var enough = ingredient.Available >= ingredient.DesiredTotal;
            var rowBounds = new Rectangle(
                ingredientBounds.X,
                y,
                ingredientBounds.Width,
                Math.Max(1, ingredientRowHeight - 3));
            context.SpriteBatch.Draw(context.Pixel, rowBounds, UiTheme.WithAlpha(enough ? palette.SurfaceRaised : palette.Danger, enough ? 0.55f : 0.16f));
            var iconSize = Math.Max(8, Math.Min(19, rowBounds.Height - 4));
            ItemIconRenderer.TryDrawSprite(
                context,
                textures,
                ingredientItem.TexturePath,
                new Rectangle(rowBounds.X + 2, rowBounds.Y + 2, iconSize, iconSize),
                enough ? 0.95f : 0.5f);
            var itemNameX = rowBounds.X + iconSize + 6;
            var quantityLabel = enough
                ? $"{ingredient.Available}/{ingredient.DesiredTotal}"
                : $"{ingredient.Available}/{ingredient.DesiredTotal}  MISS {ingredient.MissingForDesired}";
            var quantityWidth = quantityLabel.Length * 6 + 4;
            context.DebugText.Draw(
                new Vector2(itemNameX, rowBounds.Y + Math.Max(2, (rowBounds.Height - 7) / 2)),
                AbbreviateText(ingredientItem.DisplayName, Math.Max(2, (rowBounds.Right - itemNameX - quantityWidth) / 6)),
                enough ? palette.TextMuted : palette.Danger,
                1);
            context.DebugText.Draw(
                new Vector2(Math.Max(itemNameX, rowBounds.Right - quantityWidth), rowBounds.Y + Math.Max(2, (rowBounds.Height - 7) / 2)),
                quantityLabel,
                enough ? palette.Text : palette.Danger,
                1);
            y += ingredientRowHeight;
        }

        if (plan.Ingredients.Count > maxIngredientRows)
        {
            context.DebugText.Draw(
                new Vector2(ingredientBounds.X + 2, Math.Min(y, ingredientBounds.Bottom - 7)),
                $"+{plan.Ingredients.Count - maxIngredientRows} MORE",
                palette.TextMuted,
                1);
        }

        DrawQuantityControls(context, palette, layout.ActionBar, plan);
    }

    private void DrawQuantityControls(RenderContext context, UiPalette palette, Rectangle bounds, CraftingBatchPlan plan)
    {
        var controls = PixelCraftingActionLayoutPlanner.Resolve(bounds);
        _decreaseQuantityBounds = controls.Decrease;
        var quantityBounds = controls.Quantity;
        _increaseQuantityBounds = controls.Increase;
        _maximumQuantityBounds = controls.Maximum;
        _craftButtonBounds = controls.Craft;
        if (controls.IsEmpty)
        {
            return;
        }

        UiTheme.DrawButton(context, _decreaseQuantityBounds, palette, selected: false, hovered: _decreaseQuantityBounds.Contains(_mousePosition), enabled: _requestedQuantity > 1);
        UiTheme.DrawButton(context, quantityBounds, palette, selected: true, hovered: false);
        UiTheme.DrawButton(context, _increaseQuantityBounds, palette, selected: false, hovered: _increaseQuantityBounds.Contains(_mousePosition), enabled: _requestedQuantity < MaxRequestedQuantity);
        UiTheme.DrawButton(context, _maximumQuantityBounds, palette, selected: _requestedQuantity == Math.Max(1, plan.MaxCraftable), hovered: _maximumQuantityBounds.Contains(_mousePosition));
        UiTheme.DrawButton(context, _craftButtonBounds, palette, selected: plan.CanCraft, hovered: _craftButtonBounds.Contains(_mousePosition), enabled: plan.CanCraft);

        if (_focusArea == CraftingFocusArea.Quantity)
        {
            UiTheme.DrawFocusFrame(
                context,
                new Rectangle(
                    _decreaseQuantityBounds.X - 2,
                    bounds.Y - 2,
                    _maximumQuantityBounds.Right - _decreaseQuantityBounds.X + 4,
                    bounds.Height + 4),
                palette);
        }

        if (_focusArea == CraftingFocusArea.Craft)
        {
            UiTheme.DrawFocusFrame(context, _craftButtonBounds, palette);
        }

        var textY = bounds.Y + Math.Max(2, (bounds.Height - 7) / 2);
        context.DebugText.Draw(new Vector2(_decreaseQuantityBounds.Center.X - 3, textY), "-", _requestedQuantity > 1 ? palette.Text : palette.TextMuted, 1);
        var quantityLabel = _requestedQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        context.DebugText.Draw(new Vector2(quantityBounds.Center.X - quantityLabel.Length * 3, textY), quantityLabel, palette.Text, 1);
        context.DebugText.Draw(new Vector2(_increaseQuantityBounds.Center.X - 3, textY), "+", _requestedQuantity < MaxRequestedQuantity ? palette.Text : palette.TextMuted, 1);
        context.DebugText.Draw(
            new Vector2(_maximumQuantityBounds.Center.X - 9, textY),
            _maximumQuantityBounds.Width >= 22 ? "MAX" : "M",
            palette.TextMuted,
            1);
        var craftLabel = plan.ActualQuantity > 1 && _craftButtonBounds.Width >= 72
            ? $"CRAFT {plan.ActualQuantity}"
            : "CRAFT";
        context.DebugText.Draw(
            new Vector2(_craftButtonBounds.Center.X - craftLabel.Length * 3, textY),
            craftLabel,
            plan.CanCraft ? palette.Text : palette.TextMuted,
            1);
    }

    private void SelectIndex(int index)
    {
        if (_lastResults.Count == 0)
        {
            return;
        }

        var clamped = Math.Clamp(index, 0, _lastResults.Count - 1);
        var nextId = _lastResults[clamped].Recipe.Id;
        if (!string.Equals(nextId, _selectedRecipeId, StringComparison.OrdinalIgnoreCase))
        {
            _requestedQuantity = 1;
        }

        _selectedIndex = clamped;
        _selectedRecipeId = nextId;
        RefreshSelectedPlan();
        EnsureSelectionVisible();
    }

    private void AdjustQuantity(int delta)
    {
        _requestedQuantity = Math.Clamp(_requestedQuantity + delta, 1, MaxRequestedQuantity);
        RefreshSelectedPlan();
    }

    private void SetMaximumQuantity()
    {
        var result = SelectedResult();
        if (_lastContext is null || result is null)
        {
            return;
        }

        var maximum = _crafting.PlanCraftMaximum(_lastContext, result.Recipe);
        _requestedQuantity = Math.Clamp(Math.Max(1, maximum.MaxCraftable), 1, MaxRequestedQuantity);
        RefreshSelectedPlan();
        _status = maximum.MaxCraftable > 0 ? $"MAXIMUM {maximum.MaxCraftable}" : PlanStatus(maximum);
    }

    private void ToggleSelectedPin()
    {
        var result = SelectedResult();
        if (result is null)
        {
            return;
        }

        var pinned = TrackingState.TogglePin(result.Recipe.Id);
        _selectedRecipeId = result.Recipe.Id;
        _status = pinned ? "RECIPE PINNED" : "RECIPE UNPINNED";
    }

    private void CraftSelected()
    {
        var result = SelectedResult();
        if (_lastContext is null || result is null)
        {
            _status = "NO RECIPE";
            return;
        }

        LastCraftResult = _crafting.CraftMany(_lastContext, result.Recipe, _requestedQuantity);
        _status = LastCraftResult.IsSuccess
            ? $"CRAFTED {LastCraftResult.CraftedQuantity}  OUTPUT {LastCraftResult.Output.Count}"
            : FailureLabel(LastCraftResult.Plan.PrimaryFailureReason);
        CraftingResolved?.Invoke(LastCraftResult);
        RefreshSelectedPlan();
    }

    private void RefreshSelectedPlan()
    {
        var result = SelectedResult();
        _selectedPlan = _lastContext is null || result is null
            ? null
            : _crafting.PlanCraft(_lastContext, result.Recipe, _requestedQuantity);
    }

    private CraftingQueryResult? SelectedResult()
    {
        return _selectedIndex >= 0 && _selectedIndex < _lastResults.Count
            ? _lastResults[_selectedIndex]
            : null;
    }

    private int FindRecipeIndex(string recipeId)
    {
        for (var index = 0; index < _lastResults.Count; index++)
        {
            if (string.Equals(_lastResults[index].Recipe.Id, recipeId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private void ResetSelection()
    {
        _selectedIndex = 0;
        _selectedRecipeId = null;
        _scroll = 0;
        _requestedQuantity = 1;
        _selectedPlan = null;
    }

    private void EnsureSelectionVisible()
    {
        if (_selectedIndex < _scroll)
        {
            _scroll = _selectedIndex;
        }
        else if (_selectedIndex >= _scroll + _visibleRecipeRows)
        {
            _scroll = _selectedIndex - _visibleRecipeRows + 1;
        }
    }

    private void ClearActionBounds()
    {
        _decreaseQuantityBounds = Rectangle.Empty;
        _increaseQuantityBounds = Rectangle.Empty;
        _maximumQuantityBounds = Rectangle.Empty;
        _craftButtonBounds = Rectangle.Empty;
        _pinButtonBounds = Rectangle.Empty;
    }

    private bool MatchesCategory(CraftingQueryResult result, IItemDefinitionProvider items)
    {
        var filter = CategoryFilters[Math.Clamp(_categoryIndex, 0, CategoryFilters.Length - 1)];
        if (filter == "ALL")
        {
            return true;
        }

        var recipe = result.Recipe;
        var category = recipe.Category.ToUpperInvariant();
        if (category.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var item = items.GetById(recipe.Result.ItemId);
        return filter switch
        {
            "BLOCKS" => item.Type == ItemType.PlaceableTile || item.HasTag("block") || item.HasTag("placeable"),
            "TOOLS" => item.Type is ItemType.ToolPickaxe or ItemType.ToolAxe or ItemType.ToolHoe or ItemType.ToolWateringCan,
            "WEAPONS" => item.Type is ItemType.WeaponMelee or ItemType.WeaponRanged or ItemType.WeaponMagic || item.HasTag("weapon"),
            "MAGIC" => item.Type == ItemType.WeaponMagic || item.HasTag("magic") || item.ManaCost > 0 || item.MaxManaBonus != 0,
            "FURNITURE" => item.HasTag("furniture") || item.HasTag("station") || category.Contains("FURNITURE", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static string PlanStatus(CraftingBatchPlan plan)
    {
        return plan.CanCraft && plan.FulfillsDesiredQuantity
            ? "READY"
            : plan.CanCraft
                ? $"PARTIAL  {FailureLabel(plan.PrimaryFailureReason)}"
                : FailureLabel(plan.PrimaryFailureReason);
    }

    private static Color AvailabilityColor(
        UiPalette palette,
        in CraftingAvailabilityPresentation availability)
    {
        return availability.State switch
        {
            CraftingAvailabilityState.Craftable => palette.Accent,
            CraftingAvailabilityState.Partial => palette.Warning,
            CraftingAvailabilityState.Empty => palette.TextMuted,
            CraftingAvailabilityState.Locked => palette.TextMuted,
            _ => palette.Danger
        };
    }

    private static string FailureLabel(CraftingQueryResult result)
    {
        var reason = result.FailureReasons.FirstOrDefault();
        if (reason == CraftingFailureReason.None)
        {
            return result.CanCraft ? "READY" : "BLOCKED";
        }

        return FailureLabel(reason);
    }

    private static string FailureLabel(CraftingFailureReason reason)
    {
        return reason switch
        {
            CraftingFailureReason.InvalidQuantity => "INVALID QUANTITY",
            CraftingFailureReason.UnknownRecipe => "UNKNOWN RECIPE",
            CraftingFailureReason.MissingStation => "MISSING STATION",
            CraftingFailureReason.MissingIngredients => "MISSING ITEMS",
            CraftingFailureReason.InsufficientOutputCapacity => "NO OUTPUT SPACE",
            CraftingFailureReason.RequestedQuantityUnavailable => "QUANTITY LIMITED",
            CraftingFailureReason.InventoryChanged => "INVENTORY CHANGED",
            _ => "BLOCKED"
        };
    }

    private static string VisibilityLabel(RecipeVisibilityMode mode)
    {
        return mode switch
        {
            RecipeVisibilityMode.Known => "KNOWN",
            RecipeVisibilityMode.Craftable => "READY",
            RecipeVisibilityMode.All => "ALL",
            _ => mode.ToString().ToUpperInvariant()
        };
    }

    private static string AbbreviateText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(1, maxLength)];
    }

}
