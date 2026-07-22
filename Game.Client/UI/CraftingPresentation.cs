using Game.Core.Crafting;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public enum CraftingAvailabilityState
{
    Empty,
    Craftable,
    Partial,
    Locked,
    MissingStation,
    MissingMaterials,
    NoOutputSpace,
    Blocked
}

public readonly record struct CraftingAvailabilityPresentation(
    CraftingAvailabilityState State,
    string Label,
    string CompactLabel,
    bool CanCraft)
{
    public static CraftingAvailabilityPresentation Empty { get; } =
        new(CraftingAvailabilityState.Empty, "NO RECIPE SELECTED", "EMPTY", false);
}

public static class CraftingAvailabilityPresenter
{
    public static CraftingAvailabilityPresentation Resolve(CraftingQueryResult? result)
    {
        if (result is null)
        {
            return CraftingAvailabilityPresentation.Empty;
        }

        if (!result.IsKnown)
        {
            return new(CraftingAvailabilityState.Locked, "LOCKED - DISCOVER THIS RECIPE", "LOCKED", false);
        }

        if (!result.HasStation)
        {
            return new(CraftingAvailabilityState.MissingStation, "MISSING REQUIRED STATION", "STATION", false);
        }

        if (!result.HasIngredients)
        {
            return new(CraftingAvailabilityState.MissingMaterials, "MISSING MATERIALS", "MISSING", false);
        }

        if (result.CanCraft)
        {
            return new(CraftingAvailabilityState.Craftable, "READY TO CRAFT", "READY", true);
        }

        return FromFailure(result.FailureReasons.Count == 0
            ? CraftingFailureReason.None
            : result.FailureReasons[0]);
    }

    public static CraftingAvailabilityPresentation Resolve(CraftingBatchPlan? plan)
    {
        if (plan is null)
        {
            return CraftingAvailabilityPresentation.Empty;
        }

        if (plan.CanCraft)
        {
            return plan.FulfillsDesiredQuantity
                ? new(CraftingAvailabilityState.Craftable, "READY TO CRAFT", "READY", true)
                : new(CraftingAvailabilityState.Partial, "PARTIAL QUANTITY AVAILABLE", "PARTIAL", true);
        }

        return FromFailure(plan.PrimaryFailureReason);
    }

    private static CraftingAvailabilityPresentation FromFailure(CraftingFailureReason reason)
    {
        return reason switch
        {
            CraftingFailureReason.UnknownRecipe =>
                new(CraftingAvailabilityState.Locked, "LOCKED - DISCOVER THIS RECIPE", "LOCKED", false),
            CraftingFailureReason.MissingStation =>
                new(CraftingAvailabilityState.MissingStation, "MISSING REQUIRED STATION", "STATION", false),
            CraftingFailureReason.MissingIngredients =>
                new(CraftingAvailabilityState.MissingMaterials, "MISSING MATERIALS", "MISSING", false),
            CraftingFailureReason.InsufficientOutputCapacity =>
                new(CraftingAvailabilityState.NoOutputSpace, "NO INVENTORY SPACE", "NO SPACE", false),
            CraftingFailureReason.RequestedQuantityUnavailable =>
                new(CraftingAvailabilityState.Partial, "QUANTITY LIMITED", "LIMITED", false),
            _ => new(CraftingAvailabilityState.Blocked, "CRAFTING BLOCKED", "BLOCKED", false)
        };
    }
}

public readonly record struct PixelCraftingActionLayout(
    Rectangle Decrease,
    Rectangle Quantity,
    Rectangle Increase,
    Rectangle Maximum,
    Rectangle Craft)
{
    public bool IsEmpty => Craft.IsEmpty;
}

public static class PixelCraftingActionLayoutPlanner
{
    public static PixelCraftingActionLayout Resolve(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return default;
        }

        var gap = bounds.Width < 120 ? 1 : bounds.Width < 260 ? 2 : 4;
        var available = Math.Max(0, bounds.Width - gap * 4);
        if (available < 5)
        {
            return default;
        }

        var craftWidth = Math.Clamp(bounds.Width * 38 / 100, 1, Math.Max(1, available - 4));
        var controlsWidth = available - craftWidth;
        var decreaseWidth = Math.Max(1, controlsWidth * 20 / 100);
        var quantityWidth = Math.Max(1, controlsWidth * 30 / 100);
        var increaseWidth = Math.Max(1, controlsWidth * 20 / 100);
        var maximumWidth = Math.Max(1, controlsWidth - decreaseWidth - quantityWidth - increaseWidth);
        var x = bounds.X;
        var decrease = new Rectangle(x, bounds.Y, decreaseWidth, bounds.Height);
        x = decrease.Right + gap;
        var quantity = new Rectangle(x, bounds.Y, quantityWidth, bounds.Height);
        x = quantity.Right + gap;
        var increase = new Rectangle(x, bounds.Y, increaseWidth, bounds.Height);
        x = increase.Right + gap;
        var maximum = new Rectangle(x, bounds.Y, maximumWidth, bounds.Height);
        x = maximum.Right + gap;
        var craft = new Rectangle(x, bounds.Y, Math.Max(0, bounds.Right - x), bounds.Height);
        return new PixelCraftingActionLayout(decrease, quantity, increase, maximum, craft);
    }
}

internal enum CraftingFocusArea
{
    Search,
    Visibility,
    Category,
    Recipes,
    Quantity,
    Pin,
    Craft
}

internal readonly record struct CraftingNavigationInput(
    bool Up,
    bool Down,
    bool Left,
    bool Right,
    bool Confirm,
    bool Cancel,
    bool PreviousFocus,
    bool NextFocus);

internal readonly record struct CraftingNavigationIntent(
    CraftingFocusArea Focus,
    int RecipeDelta,
    int ValueDelta,
    bool Activate,
    bool Cancel);

internal static class CraftingNavigationPlanner
{
    private const int FocusCount = 7;

    public static CraftingNavigationIntent Resolve(
        CraftingFocusArea current,
        in CraftingNavigationInput input)
    {
        if (input.Cancel)
        {
            return new CraftingNavigationIntent(current, 0, 0, false, true);
        }

        if (input.PreviousFocus || input.NextFocus)
        {
            var direction = input.PreviousFocus ? -1 : 1;
            return new CraftingNavigationIntent(MoveFocus(current, direction), 0, 0, false, false);
        }

        if (current == CraftingFocusArea.Recipes && (input.Up || input.Down))
        {
            return new CraftingNavigationIntent(current, input.Up ? -1 : 1, 0, false, false);
        }

        if (current != CraftingFocusArea.Recipes && (input.Up || input.Down))
        {
            return new CraftingNavigationIntent(
                MoveFocus(current, input.Up ? -1 : 1),
                0,
                0,
                false,
                false);
        }

        var valueDelta = input.Left ? -1 : input.Right ? 1 : 0;
        return new CraftingNavigationIntent(current, 0, valueDelta, input.Confirm, false);
    }

    private static CraftingFocusArea MoveFocus(CraftingFocusArea current, int direction)
    {
        var index = ((int)current + Math.Sign(direction) + FocusCount) % FocusCount;
        return (CraftingFocusArea)index;
    }
}
