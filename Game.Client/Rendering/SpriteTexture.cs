using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public sealed class SpriteTexture
{
    private readonly ITextureResource _resource;

    internal SpriteTexture(string spriteId, ITextureResource resource, Rectangle sourceRectangle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        SpriteId = spriteId;
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));
        SourceRectangle = sourceRectangle;
    }

    public string SpriteId { get; }

    public Texture2D Texture => _resource.Texture;

    public Rectangle SourceRectangle { get; }

    public bool IsPlaceholder => _resource.IsPlaceholder;

    internal ITextureResource Resource => _resource;
}
