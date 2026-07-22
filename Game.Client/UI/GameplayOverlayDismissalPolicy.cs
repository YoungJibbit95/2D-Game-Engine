namespace Game.Client.UI;

public enum GameplayOverlayKind
{
    None,
    Inventory,
    Crafting,
    CharacterEditor
}

internal static class GameplayOverlayDismissalPolicy
{
    public static GameplayOverlayKind ResolveTopmost(
        bool characterEditorOpen,
        bool craftingOpen,
        bool inventoryOpen)
    {
        if (characterEditorOpen)
        {
            return GameplayOverlayKind.CharacterEditor;
        }

        if (craftingOpen)
        {
            return GameplayOverlayKind.Crafting;
        }

        return inventoryOpen
            ? GameplayOverlayKind.Inventory
            : GameplayOverlayKind.None;
    }
}
