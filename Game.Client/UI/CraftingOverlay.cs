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
    private const int RowHeight = 28;
    private const int VisibleRecipeRows = 11;

    private readonly CraftingSystem _crafting = new();
    private readonly CraftingStationLocator _stations = new();
    private readonly List<RecipeHitZone> _recipeHitZones = new();
    private Rectangle _craftButtonBounds;
    private IReadOnlyList<CraftingQueryResult> _lastResults = Array.Empty<CraftingQueryResult>();
    private CraftingContext? _lastContext;
    private int _selectedIndex;
    private int _scroll;
    private string? _status;

    public bool IsOpen { get; private set; }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        _status = IsOpen ? "CRAFTING" : null;
    }

    public void Close()
    {
        IsOpen = false;
        _status = null;
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
            RefreshResults(inventory, content, world, player, settings);
            return true;
        }

        if (!IsOpen)
        {
            return false;
        }

        if (input.IsKeyPressed(Keys.Escape) || input.IsBindingPressed(settings.Input.KeyBindings.OpenInventory))
        {
            Close();
            return true;
        }

        RefreshResults(inventory, content, world, player, settings);
        UpdateSelection(input);
        UpdateMouse(input);

        if (input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.Space))
        {
            CraftSelected(input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift));
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

        var panelWidth = Math.Min(760, context.ViewportBounds.Width - 48);
        var panelHeight = Math.Min(476, context.ViewportBounds.Height - 72);
        var panel = new Rectangle(
            context.ViewportBounds.Width / 2 - panelWidth / 2,
            Math.Max(28, context.ViewportBounds.Height / 2 - panelHeight / 2),
            panelWidth,
            panelHeight);

        UiTheme.DrawPanel(context, panel, palette, settings.Ui.PanelOpacity);
        context.DebugText.Draw(new Vector2(panel.X + 20, panel.Y + 18), "CRAFTING", palette.Accent, 3);
        if (settings.Ui.ShowControlHints)
        {
            context.DebugText.Draw(new Vector2(panel.X + 22, panel.Y + 52), "UP DOWN SELECT   ENTER CRAFT   SHIFT ENTER REPEAT   C ESC CLOSE", palette.TextMuted, 1);
        }

        var listBounds = new Rectangle(panel.X + 20, panel.Y + 80, 314, panel.Height - 122);
        var detailBounds = new Rectangle(listBounds.Right + 18, listBounds.Y, panel.Right - listBounds.Right - 38, listBounds.Height);
        DrawRecipeList(context, palette, listBounds, content.Items, textures);
        DrawRecipeDetails(context, palette, detailBounds, content.Items, textures);

        if (!string.IsNullOrWhiteSpace(_status))
        {
            context.DebugText.Draw(new Vector2(panel.X + 20, panel.Bottom - 28), _status, palette.Warning, 1);
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
        _lastContext = _stations.CreateContext(inventory, world, content.Tiles, actorTile, radiusTiles);
        _lastResults = _crafting.QueryRecipes(_lastContext, content.Recipes)
            .Where(result => result.IsKnown)
            .ToArray();
        _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, _lastResults.Count - 1));
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _lastResults.Count - VisibleRecipeRows));
    }

    private void UpdateSelection(InputManager input)
    {
        if (_lastResults.Count == 0)
        {
            _selectedIndex = 0;
            _scroll = 0;
            return;
        }

        if (input.IsKeyPressed(Keys.Down) || input.IsKeyPressed(Keys.S))
        {
            _selectedIndex = Math.Min(_lastResults.Count - 1, _selectedIndex + 1);
        }
        else if (input.IsKeyPressed(Keys.Up) || input.IsKeyPressed(Keys.W))
        {
            _selectedIndex = Math.Max(0, _selectedIndex - 1);
        }

        if (input.ScrollDelta < 0)
        {
            _selectedIndex = Math.Min(_lastResults.Count - 1, _selectedIndex + 1);
        }
        else if (input.ScrollDelta > 0)
        {
            _selectedIndex = Math.Max(0, _selectedIndex - 1);
        }

        if (_selectedIndex < _scroll)
        {
            _scroll = _selectedIndex;
        }
        else if (_selectedIndex >= _scroll + VisibleRecipeRows)
        {
            _scroll = _selectedIndex - VisibleRecipeRows + 1;
        }
    }

    private void UpdateMouse(InputManager input)
    {
        var hoveredRecipe = _recipeHitZones.LastOrDefault(zone => zone.Bounds.Contains(input.MousePosition));
        if (hoveredRecipe is not null)
        {
            _selectedIndex = hoveredRecipe.Index;
            if (input.IsLeftMousePressed)
            {
                CraftSelected(repeat: input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift));
            }
        }

        if (_craftButtonBounds.Contains(input.MousePosition) && input.IsLeftMousePressed)
        {
            CraftSelected(repeat: input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift));
        }
    }

    private void CraftSelected(bool repeat)
    {
        if (_lastContext is null || _lastResults.Count == 0 || _selectedIndex >= _lastResults.Count)
        {
            _status = "NO RECIPE";
            return;
        }

        var recipe = _lastResults[_selectedIndex].Recipe;
        var crafted = 0;
        var maxAttempts = repeat ? 10 : 1;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (!_crafting.Craft(_lastContext, recipe))
            {
                break;
            }

            crafted++;
        }

        _status = crafted > 0
            ? $"CRAFTED {crafted}X {recipe.Result.ItemId.ToUpperInvariant()}"
            : BuildBlockedReason(_lastResults[_selectedIndex]);
    }

    private void DrawRecipeList(RenderContext context, UiPalette palette, Rectangle bounds, IItemDefinitionProvider items, ClientTextureRegistry? textures)
    {
        UiTheme.DrawPanel(context, bounds, palette, 0.72f, raised: false);
        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 10), "RECIPES", palette.Text, 2);

        _recipeHitZones.Clear();
        if (_lastResults.Count == 0)
        {
            context.DebugText.Draw(new Vector2(bounds.X + 12, bounds.Y + 44), "NO KNOWN RECIPES", palette.TextMuted, 1);
            return;
        }

        var startY = bounds.Y + 38;
        var end = Math.Min(_lastResults.Count, _scroll + VisibleRecipeRows);
        for (var index = _scroll; index < end; index++)
        {
            var row = index - _scroll;
            var result = _lastResults[index];
            var rowBounds = new Rectangle(bounds.X + 8, startY + row * RowHeight, bounds.Width - 16, RowHeight - 3);
            var selected = index == _selectedIndex;
            var hovered = rowBounds.Contains(Microsoft.Xna.Framework.Input.Mouse.GetState().Position);
            UiTheme.DrawButton(context, rowBounds, palette, selected, hovered, result.IsKnown);

            var item = items.GetById(result.Recipe.Result.ItemId);
            var countSuffix = result.Recipe.Result.Count > 1
                ? $" X{result.Recipe.Result.Count}"
                : string.Empty;
            var iconBounds = new Rectangle(rowBounds.X + 4, rowBounds.Y + 3, 20, 20);
            ItemIconRenderer.TryDrawSprite(context, textures, item.TexturePath, iconBounds, result.CanCraft ? 1f : 0.42f);
            context.DebugText.Draw(new Vector2(rowBounds.X + 30, rowBounds.Y + 7), AbbreviateText(item.DisplayName, 18) + countSuffix, result.CanCraft ? palette.Text : palette.TextMuted, 1);
            context.DebugText.Draw(new Vector2(rowBounds.Right - 62, rowBounds.Y + 7), result.CanCraft ? "READY" : "BLOCK", result.CanCraft ? palette.Warning : palette.Danger, 1);
            _recipeHitZones.Add(new RecipeHitZone(rowBounds, index));
        }

        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Bottom - 18), $"{_selectedIndex + 1}/{_lastResults.Count}", palette.TextMuted, 1);
    }

    private void DrawRecipeDetails(RenderContext context, UiPalette palette, Rectangle bounds, IItemDefinitionProvider items, ClientTextureRegistry? textures)
    {
        UiTheme.DrawPanel(context, bounds, palette, 0.72f, raised: false);
        if (_lastResults.Count == 0 || _selectedIndex >= _lastResults.Count)
        {
            return;
        }

        var result = _lastResults[_selectedIndex];
        var recipe = result.Recipe;
        var item = items.GetById(recipe.Result.ItemId);
        var resultIcon = new Rectangle(bounds.X + 12, bounds.Y + 12, 52, 52);
        UiTheme.DrawButton(context, resultIcon, palette, selected: true, hovered: false);
        ItemIconRenderer.DrawItemStack(context, textures, items, recipe.Result, resultIcon, palette);
        context.DebugText.Draw(new Vector2(bounds.X + 76, bounds.Y + 12), AbbreviateText(item.DisplayName, 26), palette.Text, 2);
        context.DebugText.Draw(new Vector2(bounds.X + 76, bounds.Y + 38), $"CATEGORY {recipe.Category.ToUpperInvariant()}", palette.TextMuted, 1);
        context.DebugText.Draw(new Vector2(bounds.X + 76, bounds.Y + 54), $"RESULT {recipe.Result.Count}X {recipe.Result.ItemId.ToUpperInvariant()}", palette.Warning, 1);

        var y = bounds.Y + 82;
        context.DebugText.Draw(new Vector2(bounds.X + 12, y), "INGREDIENTS", palette.Text, 1);
        y += 18;
        foreach (var ingredient in recipe.Ingredients)
        {
            var have = _lastContext?.Inventory.CountItem(ingredient.ItemId) ?? 0;
            var ingredientItem = items.GetById(ingredient.ItemId);
            var color = have >= ingredient.Count ? palette.TextMuted : palette.Danger;
            var rowBounds = new Rectangle(bounds.X + 16, y - 4, bounds.Width - 32, 28);
            context.SpriteBatch.Draw(context.Pixel, rowBounds, UiTheme.WithAlpha(have >= ingredient.Count ? palette.Surface : palette.Danger, have >= ingredient.Count ? 0.38f : 0.16f));
            ItemIconRenderer.TryDrawSprite(context, textures, ingredientItem.TexturePath, new Rectangle(rowBounds.X + 4, rowBounds.Y + 4, 20, 20), have >= ingredient.Count ? 0.9f : 0.45f);
            context.DebugText.Draw(new Vector2(rowBounds.X + 32, rowBounds.Y + 8), $"{AbbreviateText(ingredientItem.DisplayName, 22)} {have}/{ingredient.Count}", color, 1);
            y += 30;
        }

        y += 8;
        var stationText = recipe.Station is null ? "NONE" : recipe.Station.ToUpperInvariant();
        context.DebugText.Draw(new Vector2(bounds.X + 12, y), $"STATION {stationText}", result.HasStation ? palette.TextMuted : palette.Danger, 1);
        y += 16;
        context.DebugText.Draw(new Vector2(bounds.X + 12, y), BuildBlockedReason(result), result.CanCraft ? palette.Warning : palette.Danger, 1);

        _craftButtonBounds = new Rectangle(bounds.X + 12, bounds.Bottom - 48, 160, 30);
        UiTheme.DrawButton(context, _craftButtonBounds, palette, selected: result.CanCraft, hovered: _craftButtonBounds.Contains(Microsoft.Xna.Framework.Input.Mouse.GetState().Position), enabled: result.CanCraft);
        context.DebugText.Draw(new Vector2(_craftButtonBounds.X + 14, _craftButtonBounds.Y + 9), "CRAFT", result.CanCraft ? palette.Text : palette.TextMuted, 1);

        if (_lastContext?.AvailableStations.Count > 0)
        {
            context.DebugText.Draw(new Vector2(bounds.X + 190, bounds.Bottom - 38), "NEAR " + string.Join(" ", _lastContext.AvailableStations.Take(2)).ToUpperInvariant(), palette.TextMuted, 1);
        }
    }

    private static string BuildBlockedReason(CraftingQueryResult result)
    {
        if (!result.IsKnown)
        {
            return "UNKNOWN";
        }

        if (!result.HasStation)
        {
            return "MISSING STATION";
        }

        if (!result.HasIngredients)
        {
            return "MISSING ITEMS";
        }

        return result.CanCraft ? "CRAFTABLE" : "NO SPACE";
    }

    private static string AbbreviateText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private sealed record RecipeHitZone(Rectangle Bounds, int Index);
}
