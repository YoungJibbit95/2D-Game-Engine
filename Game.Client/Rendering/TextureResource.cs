using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public interface ITextureResource : IDisposable
{
    Texture2D Texture { get; }

    int Width { get; }

    int Height { get; }

    long DecodedByteCount { get; }

    bool IsPlaceholder { get; }
}

internal sealed class TextureResource : ITextureResource
{
    private Texture2D? _texture;

    public TextureResource(Texture2D texture, bool isPlaceholder)
    {
        _texture = texture ?? throw new ArgumentNullException(nameof(texture));
        Width = texture.Width;
        Height = texture.Height;
        IsPlaceholder = isPlaceholder;
    }

    public Texture2D Texture => _texture ?? throw new ObjectDisposedException(nameof(TextureResource));

    public int Width { get; }

    public int Height { get; }

    public long DecodedByteCount => Width * (long)Height * 4L;

    public bool IsPlaceholder { get; }

    public void Dispose()
    {
        _texture?.Dispose();
        _texture = null;
    }
}
