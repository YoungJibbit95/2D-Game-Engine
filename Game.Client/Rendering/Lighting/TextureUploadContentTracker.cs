using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Lighting;

public struct TextureUploadContentTracker
{
    private ulong _committedHash;

    public bool HasCommittedContent { get; private set; }

    public bool IsChanged(ulong contentHash)
    {
        return !HasCommittedContent || _committedHash != contentHash;
    }

    public void Commit(ulong contentHash)
    {
        _committedHash = contentHash;
        HasCommittedContent = true;
    }

    public void Reset()
    {
        _committedHash = 0;
        HasCommittedContent = false;
    }
}

public static class LightingTextureContentHash
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Compute(ReadOnlySpan<Color> pixels)
    {
        var hash = OffsetBasis;
        for (var index = 0; index < pixels.Length; index++)
        {
            hash ^= pixels[index].PackedValue;
            hash *= Prime;
        }

        hash ^= (uint)pixels.Length;
        hash *= Prime;
        return hash;
    }
}
