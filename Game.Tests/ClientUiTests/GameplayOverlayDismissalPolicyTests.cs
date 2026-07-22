using Game.Client.UI;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class GameplayOverlayDismissalPolicyTests
{
    [Fact]
    public void ResolveTopmost_ReturnsNoneWhenNoGameplayOverlayIsOpen()
    {
        var result = GameplayOverlayDismissalPolicy.ResolveTopmost(
            characterEditorOpen: false,
            craftingOpen: false,
            inventoryOpen: false);

        Assert.Equal(GameplayOverlayKind.None, result);
    }

    [Theory]
    [InlineData(false, false, true, (int)GameplayOverlayKind.Inventory)]
    [InlineData(false, true, false, (int)GameplayOverlayKind.Crafting)]
    [InlineData(false, true, true, (int)GameplayOverlayKind.Crafting)]
    [InlineData(true, false, false, (int)GameplayOverlayKind.CharacterEditor)]
    [InlineData(true, true, true, (int)GameplayOverlayKind.CharacterEditor)]
    public void ResolveTopmost_MatchesVisualOverlayOrder(
        bool characterEditorOpen,
        bool craftingOpen,
        bool inventoryOpen,
        int expected)
    {
        var result = GameplayOverlayDismissalPolicy.ResolveTopmost(
            characterEditorOpen,
            craftingOpen,
            inventoryOpen);

        Assert.Equal((GameplayOverlayKind)expected, result);
    }
}
