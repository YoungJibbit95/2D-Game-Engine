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
    private const int RowHeight = 29;
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
    private readonly List<RecipeHitZone> _recipeHitZones = new();
    private readonly List<CategoryHitZone> _categoryHitZones = new();
    private readonly List<VisibilityHitZone> _visibilityHitZones = new();
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
    }

    public void Close()
    {
        IsOpen = false;
        IsSearchFocused = false;
        _focusArea = CraftingFocusArea.Recipes;
        _status = null;
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
        GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(settings);

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

        if (IsSearchFocused)
        {
            UpdateFocusedSearch(input);
            RefreshResults(inventory, content, world, player, settings);
            UpdateMouse(input);
            return true;
        }

        if (input.IsKeyPressed(Keys.Escape) || input.IsBindingPressed(settings.Input.KeyBindings.OpenInventory))
        {
            Close();
            return true;
        }

        RefreshResults(inventory, content, world, player, settings);
        UpdateFocus(input);
        UpdateMouse(input);

        if ((input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.Space)) && _focusArea == CraftingFocusArea.Recipes)
        {
            CraftSelected();
        }

        return true;
    }

    public void Draw(RenderContext context, GameContentDatabase content, ClientTextureRegistry? textures, GameSettings settings)
    {
        if (!IsOpen)
        {
            return;
        }

        var palette = UiTheme.Resolve(settings);
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, UiTheme.WithAlpha(palette.Backdrop, settings.Ui.MenuBackdropOpacity * 0.9f));

        var panelWidth = Math.Min(900, Math.Max(320, context.ViewportBounds.Width - 32));
        var panelHeight = Math.Min(540, Math.Max(300, context.ViewportBounds.Height - 32));
        var panel = new Rectangle(
            context.ViewportBounds.Width / 2 - panelWidth / 2,
            Math.Max(16, context.ViewportBounds.Height / 2 - panelHeight / 2),
            panelWidth,
            panelHeight);

        UiTheme.DrawPanel(context, panel, palette, settings.Ui.PanelOpacity);
        DrawHeader(context, palette, panel, textures);

        var categoryBounds = new Rectangle(panel.X + 16, panel.Y + 58, panel.Width - 32, 28);
        DrawCategoryTabs(context, palette, categoryBounds);

        var contentTop = categoryBounds.Bottom + 10;
        var contentBottom = panel.Bottom - 42;
        var listWidth = Math.Clamp((int)MathF.Round(panel.Width * 0.40f), 270, 356);
        var listBounds = new Rectangle(panel.X + 16, contentTop, listWidth, contentBottom - contentTop);
        var detailBounds = new Rectangle(listBounds.Right + 12, contentTop, panel.Right - listBounds.Right - 28, listBounds.Height);
        DrawRecipeList(context, palette, listBounds, content.Items, textures);
        DrawRecipeDetails(context, palette, detailBounds, content.Items, textures);

        if (!string.IsNullOrWhiteSpace(_status))
        {
            context.DebugText.Draw(
                new Vector2(panel.X + 18, panel.Bottom - 25),
                AbbreviateText(_status, Math.Max(24, panel.Width / 8)),
                LastCraftResult?.IsSuccess == false ? palette.Danger : palette.Warning,
                1);
        }
    }

    private void DrawHeader(
        RenderContext context,
        UiPalette palette,
        Rectangle panel,
        ClientTextureRegistry? textures)
    {
        var iconBounds = new Rectangle(panel.X + 16, panel.Y + 10, 32, 32);
        var drewIcon = ItemIconRenderer.TryDrawSprite(context, textures, "ui/crafting_hammer", iconBounds);
        context.DebugText.Draw(
            new Vector2(panel.X + (drewIcon ? 54 : 18), panel.Y + (drewIcon ? 21 : 18)),
            drewIcon ? "CRAFT" : "CRAFTING",
            palette.Accent,
            2);

        var visibilityWidth = Math.Min(230, Math.Max(168, panel.Width / 4));
        var searchX = panel.X + 144;
        var searchWidth = Math.Max(120, panel.Right - visibilityWidth - searchX - 30);
        _searchBounds = new Rectangle(searchX, panel.Y + 12, searchWidth, 34);
        UiTheme.DrawButton(context, _searchBounds, palette, selected: IsSearchFocused || _focusArea == CraftingFocusArea.Search, hovered: _searchBounds.Contains(Mouse.GetState().Position));
        var searchText = SearchQuery.Length == 0 ? "SEARCH RECIPES" : AbbreviateText(SearchQuery, Math.Max(10, searchWidth / 8));
        var searchColor = SearchQuery.Length == 0 ? palette.TextMuted : palette.Text;
        context.DebugText.Draw(new Vector2(_searchBounds.X + 10, _searchBounds.Y + 11), searchText + (IsSearchFocused ? "_" : string.Empty), searchColor, 1);

        _visibilityHitZones.Clear();
        var visibilityX = panel.Right - visibilityWidth - 16;
        var segmentWidth = visibilityWidth / VisibilityModes.Length;
        for (var index = 0; index < VisibilityModes.Length; index++)
        {
            var bounds = new Rectangle(visibilityX + index * segmentWidth, panel.Y + 12, segmentWidth - 3, 34);
            var mode = VisibilityModes[index];
            var selected = mode == VisibilityMode;
            UiTheme.DrawButton(context, bounds, palette, selected, bounds.Contains(Mouse.GetState().Position));
            context.DebugText.Draw(new Vector2(bounds.X + 8, bounds.Y + 11), VisibilityLabel(mode), selected ? palette.Text : palette.TextMuted, 1);
            _visibilityHitZones.Add(new VisibilityHitZone(bounds, mode));
        }

        if (_focusArea == CraftingFocusArea.Visibility)
        {
            UiTheme.DrawFocusFrame(context, new Rectangle(visibilityX - 2, panel.Y + 10, visibilityWidth + 2, 38), palette);
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
        if (input.IsKeyPressed(Keys.Escape) || input.IsKeyPressed(Keys.Enter))
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
        if (input.IsKeyPressed(Keys.Tab))
        {
            var direction = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift) ? -1 : 1;
            var count = Enum.GetValues<CraftingFocusArea>().Length;
            _focusArea = (CraftingFocusArea)(((int)_focusArea + direction + count) % count);
            IsSearchFocused = false;
            return;
        }

        var activate = input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.Space);
        if (_focusArea == CraftingFocusArea.Search && activate)
        {
            IsSearchFocused = true;
            return;
        }

        if (_focusArea == CraftingFocusArea.Visibility)
        {
            var direction = HorizontalDirection(input);
            if (direction != 0)
            {
                var index = Array.IndexOf(VisibilityModes, VisibilityMode);
                SetVisibilityMode(VisibilityModes[(index + direction + VisibilityModes.Length) % VisibilityModes.Length]);
            }

            return;
        }

        if (_focusArea == CraftingFocusArea.Category)
        {
            var direction = HorizontalDirection(input);
            if (direction != 0)
            {
                _categoryIndex = (_categoryIndex + direction + CategoryFilters.Length) % CategoryFilters.Length;
                ResetSelection();
                _status = $"CATEGORY {CategoryFilters[_categoryIndex]}";
            }

            return;
        }

        if (_focusArea == CraftingFocusArea.Quantity)
        {
            var direction = HorizontalDirection(input);
            if (direction != 0)
            {
                AdjustQuantity(direction);
            }
            else if (activate)
            {
                SetMaximumQuantity();
            }

            return;
        }

        if (_focusArea == CraftingFocusArea.Pin && activate)
        {
            ToggleSelectedPin();
            return;
        }

        if (_focusArea == CraftingFocusArea.Craft && activate)
        {
            CraftSelected();
            return;
        }

        if (_focusArea != CraftingFocusArea.Recipes || _lastResults.Count == 0)
        {
            return;
        }

        if (input.IsKeyPressed(Keys.Down) || input.IsKeyPressed(Keys.S) || input.ScrollDelta < 0)
        {
            SelectIndex(Math.Min(_lastResults.Count - 1, _selectedIndex + 1));
        }
        else if (input.IsKeyPressed(Keys.Up) || input.IsKeyPressed(Keys.W) || input.ScrollDelta > 0)
        {
            SelectIndex(Math.Max(0, _selectedIndex - 1));
        }

        EnsureSelectionVisible();
    }

    private static int HorizontalDirection(InputManager input)
    {
        if (input.IsKeyPressed(Keys.Left) || input.IsKeyPressed(Keys.A) || input.IsKeyPressed(Keys.OemMinus))
        {
            return -1;
        }

        return input.IsKeyPressed(Keys.Right) || input.IsKeyPressed(Keys.D) || input.IsKeyPressed(Keys.OemPlus)
            ? 1
            : 0;
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
        var visibility = _visibilityHitZones.LastOrDefault(zone => zone.Bounds.Contains(input.MousePosition));
        if (visibility is not null)
        {
            _focusArea = CraftingFocusArea.Visibility;
            SetVisibilityMode(visibility.Mode);
            _status = $"SHOW {VisibilityLabel(visibility.Mode)}";
            return;
        }

        var category = _categoryHitZones.LastOrDefault(zone => zone.Bounds.Contains(input.MousePosition));
        if (category is not null)
        {
            _focusArea = CraftingFocusArea.Category;
            _categoryIndex = category.Index;
            ResetSelection();
            _status = $"CATEGORY {CategoryFilters[_categoryIndex]}";
            return;
        }

        var recipe = _recipeHitZones.LastOrDefault(zone => zone.Bounds.Contains(input.MousePosition));
        if (recipe is not null)
        {
            _focusArea = CraftingFocusArea.Recipes;
            SelectIndex(recipe.Index);
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
        var gap = 5;
        var tabWidth = Math.Max(52, (bounds.Width - gap * (CategoryFilters.Length - 1)) / CategoryFilters.Length);
        for (var index = 0; index < CategoryFilters.Length; index++)
        {
            var tab = new Rectangle(bounds.X + index * (tabWidth + gap), bounds.Y, tabWidth, bounds.Height);
            var selected = index == _categoryIndex;
            UiTheme.DrawButton(context, tab, palette, selected, tab.Contains(Mouse.GetState().Position));
            context.DebugText.Draw(new Vector2(tab.X + 7, tab.Y + 9), AbbreviateText(CategoryFilters[index], Math.Max(4, tabWidth / 8)), selected ? palette.Text : palette.TextMuted, 1);
            _categoryHitZones.Add(new CategoryHitZone(tab, index));
        }

        if (_focusArea == CraftingFocusArea.Category)
        {
            UiTheme.DrawFocusFrame(context, new Rectangle(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4), palette);
        }
    }

    private void DrawRecipeList(
        RenderContext context,
        UiPalette palette,
        Rectangle bounds,
        IItemDefinitionProvider items,
        ClientTextureRegistry? textures)
    {
        context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(palette.Surface, 0.78f));
        UiTheme.DrawBorder(context, bounds, UiTheme.WithAlpha(palette.SurfaceHover, 0.9f), 1);
        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 9), $"RECIPES  {_lastResults.Count}", palette.Text, 1);

        _recipeHitZones.Clear();
        if (_lastResults.Count == 0)
        {
            context.DebugText.Draw(new Vector2(bounds.X + 12, bounds.Y + 42), "NO MATCHING RECIPES", palette.TextMuted, 1);
            return;
        }

        var startY = bounds.Y + 31;
        _visibleRecipeRows = Math.Max(1, (bounds.Height - 50) / RowHeight);
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _lastResults.Count - _visibleRecipeRows));
        EnsureSelectionVisible();
        var end = Math.Min(_lastResults.Count, _scroll + _visibleRecipeRows);
        for (var index = _scroll; index < end; index++)
        {
            var row = index - _scroll;
            var result = _lastResults[index];
            var rowBounds = new Rectangle(bounds.X + 6, startY + row * RowHeight, bounds.Width - 12, RowHeight - 3);
            var selected = index == _selectedIndex;
            UiTheme.DrawButton(context, rowBounds, palette, selected, rowBounds.Contains(Mouse.GetState().Position), result.IsKnown);
            if (selected && _focusArea == CraftingFocusArea.Recipes)
            {
                UiTheme.DrawFocusFrame(context, rowBounds, palette);
            }

            var item = items.GetById(result.Recipe.Result.ItemId);
            var iconBounds = new Rectangle(rowBounds.X + 4, rowBounds.Y + 3, 20, 20);
            ItemIconRenderer.TryDrawSprite(context, textures, item.TexturePath, iconBounds, result.CanCraft ? 1f : 0.45f);
            var pinned = TrackingState.IsPinned(result.Recipe.Id);
            var namePrefix = pinned ? "PIN " : string.Empty;
            context.DebugText.Draw(
                new Vector2(rowBounds.X + 29, rowBounds.Y + 7),
                namePrefix + AbbreviateText(item.DisplayName, Math.Max(10, (bounds.Width - 124) / 8)),
                result.CanCraft ? palette.Text : palette.TextMuted,
                1);
            context.DebugText.Draw(
                new Vector2(rowBounds.Right - 66, rowBounds.Y + 7),
                result.MaxCraftable > 0 ? $"MAX {result.MaxCraftable}" : FailureLabel(result),
                result.CanCraft ? palette.Warning : palette.Danger,
                1);
            _recipeHitZones.Add(new RecipeHitZone(rowBounds, index));
        }

        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Bottom - 17), $"{_selectedIndex + 1}/{_lastResults.Count}", palette.TextMuted, 1);
    }

    private void DrawRecipeDetails(
        RenderContext context,
        UiPalette palette,
        Rectangle bounds,
        IItemDefinitionProvider items,
        ClientTextureRegistry? textures)
    {
        context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(palette.Surface, 0.78f));
        UiTheme.DrawBorder(context, bounds, UiTheme.WithAlpha(palette.SurfaceHover, 0.9f), 1);
        var result = SelectedResult();
        if (result is null || _selectedPlan is null)
        {
            context.DebugText.Draw(new Vector2(bounds.X + 12, bounds.Y + 12), "SELECT A RECIPE", palette.TextMuted, 1);
            ClearActionBounds();
            return;
        }

        var recipe = result.Recipe;
        var plan = _selectedPlan;
        var item = items.GetById(recipe.Result.ItemId);
        var resultIcon = new Rectangle(bounds.X + 12, bounds.Y + 12, 46, 46);
        UiTheme.DrawSlot(context, resultIcon, palette, selected: true, hovered: false);
        ItemIconRenderer.DrawItemStack(context, textures, items, recipe.Result, resultIcon, palette);
        context.DebugText.Draw(new Vector2(bounds.X + 68, bounds.Y + 11), AbbreviateText(item.DisplayName, Math.Max(14, (bounds.Width - 160) / 8)), palette.Text, 2);
        context.DebugText.Draw(new Vector2(bounds.X + 68, bounds.Y + 36), $"{recipe.Result.Count} PER CRAFT  MAX {plan.MaxCraftable}", palette.Warning, 1);

        _pinButtonBounds = new Rectangle(bounds.Right - 78, bounds.Y + 12, 66, 28);
        var pinned = TrackingState.IsPinned(recipe.Id);
        UiTheme.DrawButton(context, _pinButtonBounds, palette, pinned, _pinButtonBounds.Contains(Mouse.GetState().Position));
        if (_focusArea == CraftingFocusArea.Pin)
        {
            UiTheme.DrawFocusFrame(context, _pinButtonBounds, palette);
        }
        context.DebugText.Draw(new Vector2(_pinButtonBounds.X + 10, _pinButtonBounds.Y + 9), pinned ? "UNPIN" : "PIN", pinned ? palette.Text : palette.TextMuted, 1);

        var y = bounds.Y + 70;
        var stationLabel = recipe.Station is null ? "HAND CRAFT" : recipe.Station.ToUpperInvariant();
        context.DebugText.Draw(new Vector2(bounds.X + 12, y), $"STATION  {stationLabel}", result.HasStation ? palette.Text : palette.Danger, 1);
        y += 17;
        var nearby = _lastContext?.AvailableStations.Count > 0
            ? string.Join("  ", _lastContext.AvailableStations.OrderBy(value => value).Take(3)).ToUpperInvariant()
            : "NONE";
        context.DebugText.Draw(new Vector2(bounds.X + 12, y), "NEARBY  " + AbbreviateText(nearby, Math.Max(16, bounds.Width / 8 - 10)), palette.TextMuted, 1);

        y += 25;
        context.DebugText.Draw(new Vector2(bounds.X + 12, y), $"INGREDIENTS  PLAN {plan.ActualQuantity}/{plan.DesiredQuantity}", palette.Text, 1);
        y += 18;
        var maxIngredientRows = Math.Max(1, (bounds.Bottom - 126 - y) / 29);
        foreach (var ingredient in plan.Ingredients.Take(maxIngredientRows))
        {
            var ingredientItem = items.GetById(ingredient.ItemId);
            var enough = ingredient.Available >= ingredient.DesiredTotal;
            var rowBounds = new Rectangle(bounds.X + 12, y, bounds.Width - 24, 25);
            context.SpriteBatch.Draw(context.Pixel, rowBounds, UiTheme.WithAlpha(enough ? palette.SurfaceRaised : palette.Danger, enough ? 0.55f : 0.16f));
            ItemIconRenderer.TryDrawSprite(context, textures, ingredientItem.TexturePath, new Rectangle(rowBounds.X + 3, rowBounds.Y + 3, 19, 19), enough ? 0.95f : 0.5f);
            var ingredientText = $"{AbbreviateText(ingredientItem.DisplayName, Math.Max(10, (bounds.Width - 190) / 8))}  {ingredient.Available}/{ingredient.DesiredTotal}  ({ingredient.PerCraft} EACH)";
            context.DebugText.Draw(new Vector2(rowBounds.X + 28, rowBounds.Y + 7), ingredientText, enough ? palette.TextMuted : palette.Danger, 1);
            y += 29;
        }

        if (plan.Ingredients.Count > maxIngredientRows)
        {
            context.DebugText.Draw(new Vector2(bounds.X + 14, y), $"+{plan.Ingredients.Count - maxIngredientRows} MORE", palette.TextMuted, 1);
        }

        var summaryY = bounds.Bottom - 88;
        var outputText = $"OUTPUT {plan.OutputCapacity.ActualItemCount}  CAP {plan.OutputCapacity.MaxItemCount}";
        context.DebugText.Draw(new Vector2(bounds.X + 12, summaryY), outputText, plan.CanCraft ? palette.Warning : palette.Danger, 1);
        context.DebugText.Draw(new Vector2(bounds.X + 12, summaryY + 16), PlanStatus(plan), plan.CanCraft ? palette.TextMuted : palette.Danger, 1);

        DrawQuantityControls(context, palette, bounds, plan);
    }

    private void DrawQuantityControls(RenderContext context, UiPalette palette, Rectangle bounds, CraftingBatchPlan plan)
    {
        var y = bounds.Bottom - 38;
        _decreaseQuantityBounds = new Rectangle(bounds.X + 12, y, 30, 28);
        var quantityBounds = new Rectangle(_decreaseQuantityBounds.Right + 4, y, 48, 28);
        _increaseQuantityBounds = new Rectangle(quantityBounds.Right + 4, y, 30, 28);
        _maximumQuantityBounds = new Rectangle(_increaseQuantityBounds.Right + 6, y, 48, 28);
        _craftButtonBounds = new Rectangle(_maximumQuantityBounds.Right + 8, y, Math.Max(78, bounds.Right - _maximumQuantityBounds.Right - 20), 28);

        UiTheme.DrawButton(context, _decreaseQuantityBounds, palette, selected: false, hovered: _decreaseQuantityBounds.Contains(Mouse.GetState().Position), enabled: _requestedQuantity > 1);
        UiTheme.DrawButton(context, quantityBounds, palette, selected: true, hovered: false);
        UiTheme.DrawButton(context, _increaseQuantityBounds, palette, selected: false, hovered: _increaseQuantityBounds.Contains(Mouse.GetState().Position), enabled: _requestedQuantity < MaxRequestedQuantity);
        UiTheme.DrawButton(context, _maximumQuantityBounds, palette, selected: _requestedQuantity == Math.Max(1, plan.MaxCraftable), hovered: _maximumQuantityBounds.Contains(Mouse.GetState().Position));
        UiTheme.DrawButton(context, _craftButtonBounds, palette, selected: plan.CanCraft, hovered: _craftButtonBounds.Contains(Mouse.GetState().Position), enabled: plan.CanCraft);

        if (_focusArea == CraftingFocusArea.Quantity)
        {
            UiTheme.DrawFocusFrame(context, new Rectangle(_decreaseQuantityBounds.X - 2, y - 2, _maximumQuantityBounds.Right - _decreaseQuantityBounds.X + 4, 32), palette);
        }

        if (_focusArea == CraftingFocusArea.Craft)
        {
            UiTheme.DrawFocusFrame(context, _craftButtonBounds, palette);
        }

        context.DebugText.Draw(new Vector2(_decreaseQuantityBounds.X + 11, y + 9), "-", _requestedQuantity > 1 ? palette.Text : palette.TextMuted, 1);
        context.DebugText.Draw(new Vector2(quantityBounds.X + 8, y + 9), _requestedQuantity.ToString(), palette.Text, 1);
        context.DebugText.Draw(new Vector2(_increaseQuantityBounds.X + 10, y + 9), "+", _requestedQuantity < MaxRequestedQuantity ? palette.Text : palette.TextMuted, 1);
        context.DebugText.Draw(new Vector2(_maximumQuantityBounds.X + 9, y + 9), "MAX", palette.TextMuted, 1);
        context.DebugText.Draw(new Vector2(_craftButtonBounds.X + 10, y + 9), plan.ActualQuantity > 1 ? $"CRAFT {plan.ActualQuantity}" : "CRAFT", plan.CanCraft ? palette.Text : palette.TextMuted, 1);
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

    private sealed record RecipeHitZone(Rectangle Bounds, int Index);

    private sealed record CategoryHitZone(Rectangle Bounds, int Index);

    private sealed record VisibilityHitZone(Rectangle Bounds, RecipeVisibilityMode Mode);

    private enum CraftingFocusArea
    {
        Search,
        Visibility,
        Category,
        Recipes,
        Quantity,
        Pin,
        Craft
    }
}
