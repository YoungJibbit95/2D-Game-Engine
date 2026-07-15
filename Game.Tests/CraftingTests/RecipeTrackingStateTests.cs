using Game.Core.Crafting;
using Xunit;

namespace Game.Tests.CraftingTests;

public sealed class RecipeTrackingStateTests
{
    [Fact]
    public void Pinning_IsCaseInsensitiveAndDoesNotDuplicateIds()
    {
        var tracking = new RecipeTrackingState();

        Assert.True(tracking.Pin("Iron_Sword"));
        Assert.False(tracking.Pin("iron_sword"));
        Assert.True(tracking.IsPinned("IRON_SWORD"));
        Assert.Single(tracking.PinnedRecipeIds);
    }

    [Fact]
    public void TogglePin_ReturnsTheNewStateAndRaisesChanges()
    {
        var tracking = new RecipeTrackingState();
        var changes = new List<RecipeTrackingChange>();
        tracking.Changed += changes.Add;

        Assert.True(tracking.TogglePin("campfire"));
        Assert.False(tracking.TogglePin("CAMPFIRE"));

        Assert.Equal(2, changes.Count);
        Assert.True(changes[0].IsPinned);
        Assert.False(changes[1].IsPinned);
        Assert.Empty(tracking.PinnedRecipeIds);
    }

    [Fact]
    public void Clear_UnpinsEveryTrackedRecipe()
    {
        var tracking = new RecipeTrackingState(["wand", "sword"]);

        tracking.Clear();

        Assert.Empty(tracking.PinnedRecipeIds);
    }
}
