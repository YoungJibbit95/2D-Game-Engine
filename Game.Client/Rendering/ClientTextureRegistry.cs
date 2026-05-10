using Game.Core.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public sealed class ClientTextureRegistry : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly string _contentRoot;
    private readonly SpriteAssetRegistry _assets;
    private readonly Dictionary<string, SpriteTexture> _textures = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public ClientTextureRegistry(GraphicsDevice graphicsDevice, string contentRoot, SpriteAssetRegistry assets)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        _contentRoot = contentRoot;
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
    }

    public SpriteTexture Get(string spriteId, int frameIndex = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        ThrowIfDisposed();

        var cacheKey = $"{spriteId}#{frameIndex}";
        if (_textures.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var asset = _assets.GetById(spriteId);
        var texturePath = Path.Combine(_contentRoot, NormalizePath(asset.Path));
        var source = ResolveSourceRectangle(asset, frameIndex);
        var texture = File.Exists(texturePath)
            ? LoadTexture(texturePath)
            : CreatePlaceholderTexture(asset);
        var sprite = new SpriteTexture(asset.Id, texture, source, !File.Exists(texturePath));
        _textures.Add(cacheKey, sprite);
        return sprite;
    }

    public bool TryGetRealTexture(string spriteId, out SpriteTexture sprite, int frameIndex = 0)
    {
        sprite = Get(spriteId, frameIndex);
        return !sprite.IsPlaceholder;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var texture in _textures.Values.Select(value => value.Texture).Distinct())
        {
            texture.Dispose();
        }

        _textures.Clear();
        _disposed = true;
    }

    private Texture2D LoadTexture(string path)
    {
        using var stream = File.OpenRead(path);
        return Texture2D.FromStream(_graphicsDevice, stream);
    }

    private Texture2D CreatePlaceholderTexture(SpriteAssetDefinition asset)
    {
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
        return texture;
    }

    private static Rectangle ResolveSourceRectangle(SpriteAssetDefinition asset, int frameIndex)
    {
        if (asset.Frames.Count == 0)
        {
            return new Rectangle(0, 0, asset.Width, asset.Height);
        }

        var frame = asset.Frames[Math.Clamp(frameIndex, 0, asset.Frames.Count - 1)];
        return new Rectangle(frame.X, frame.Y, frame.Width, frame.Height);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
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

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClientTextureRegistry));
        }
    }
}
