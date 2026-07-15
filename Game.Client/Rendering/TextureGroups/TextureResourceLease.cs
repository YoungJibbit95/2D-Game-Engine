namespace Game.Client.Rendering.TextureGroups;

public sealed class TextureResourceLease : IDisposable
{
    private ClientTextureRegistry? _owner;
    private readonly ITextureResource _resource;

    internal TextureResourceLease(
        ClientTextureRegistry owner,
        ITextureResource resource,
        SpriteTexture sprite)
    {
        _owner = owner;
        _resource = resource;
        Sprite = sprite;
    }

    public SpriteTexture Sprite { get; }

    public void Dispose()
    {
        var owner = Interlocked.Exchange(ref _owner, null);
        owner?.ReleaseLease(_resource);
    }
}
