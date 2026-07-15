using Game.Core.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public interface ITextureResourceFactory
{
    ITextureResource LoadFromFile(string canonicalPath);

    ITextureResource CreatePlaceholder(SpriteAssetDefinition asset);
}

internal sealed class MonoGameTextureResourceFactory : ITextureResourceFactory
{
    private readonly GraphicsDevice _graphicsDevice;

    public MonoGameTextureResourceFactory(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public ITextureResource LoadFromFile(string canonicalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);
        using var stream = File.OpenRead(canonicalPath);
        return new TextureResource(Texture2D.FromStream(_graphicsDevice, stream), isPlaceholder: false);
    }

    public ITextureResource CreatePlaceholder(SpriteAssetDefinition asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var width = Math.Clamp(asset.Width, 1, 256);
        var height = Math.Clamp(asset.Height, 1, 256);
        var texture = new Texture2D(_graphicsDevice, width, height);
        var data = new Color[width * height];
        var primary = PlaceholderColor(asset.Category);
        var secondary = new Color(
            (byte)Math.Clamp(primary.R + 46, 0, 255),
            (byte)Math.Clamp(primary.G + 46, 0, 255),
            (byte)Math.Clamp(primary.B + 46, 0, 255),
            (byte)255);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                var diagonal = x == y || x == width - y - 1;
                var checker = ((x / 4) + (y / 4)) % 2 == 0;
                data[y * width + x] = border || diagonal
                    ? Color.Magenta
                    : checker ? primary : secondary;
            }
        }

        texture.SetData(data);
        return new TextureResource(texture, isPlaceholder: true);
    }

    private static Color PlaceholderColor(SpriteAssetCategory category)
    {
        return category switch
        {
            SpriteAssetCategory.Tile => new Color(92, 94, 104),
            SpriteAssetCategory.Wall => new Color(75, 66, 84),
            SpriteAssetCategory.Item => new Color(219, 173, 88),
            SpriteAssetCategory.Tool => new Color(173, 118, 69),
            SpriteAssetCategory.Weapon => new Color(184, 82, 72),
            SpriteAssetCategory.Entity => new Color(94, 166, 103),
            SpriteAssetCategory.Projectile => new Color(224, 205, 126),
            SpriteAssetCategory.Particle => new Color(190, 190, 180),
            SpriteAssetCategory.Effect => new Color(120, 178, 224),
            SpriteAssetCategory.Background => new Color(76, 124, 166),
            SpriteAssetCategory.WorldObject => new Color(142, 91, 54),
            _ => new Color(128, 112, 176)
        };
    }
}
