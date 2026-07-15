using Game.Core.Assets;

namespace Game.Client.Rendering.TextureGroups;

public static class TextureResourceGroups
{
    internal static readonly TextureResourceGroup[] Ordered =
    [
        TextureResourceGroup.Ui,
        TextureResourceGroup.World,
        TextureResourceGroup.Entities,
        TextureResourceGroup.Backgrounds,
        TextureResourceGroup.Effects
    ];

    public static TextureResourceGroup FromCategory(SpriteAssetCategory category)
    {
        return category switch
        {
            SpriteAssetCategory.Ui => TextureResourceGroup.Ui,
            SpriteAssetCategory.Tile or
            SpriteAssetCategory.Wall or
            SpriteAssetCategory.WorldObject => TextureResourceGroup.World,
            SpriteAssetCategory.Item or
            SpriteAssetCategory.Tool or
            SpriteAssetCategory.Weapon or
            SpriteAssetCategory.Entity or
            SpriteAssetCategory.Crop => TextureResourceGroup.Entities,
            SpriteAssetCategory.Background => TextureResourceGroup.Backgrounds,
            SpriteAssetCategory.Projectile or
            SpriteAssetCategory.Particle or
            SpriteAssetCategory.Effect => TextureResourceGroup.Effects,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown sprite asset category.")
        };
    }
}
