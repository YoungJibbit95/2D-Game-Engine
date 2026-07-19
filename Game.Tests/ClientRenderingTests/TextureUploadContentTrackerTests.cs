using Game.Client.Rendering.Lighting;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class TextureUploadContentTrackerTests
{
    [Fact]
    public void TrackerOnlyAcceptsChangedCommittedContent()
    {
        var tracker = new TextureUploadContentTracker();

        Assert.True(tracker.IsChanged(42));
        tracker.Commit(42);
        Assert.False(tracker.IsChanged(42));
        Assert.True(tracker.IsChanged(43));

        tracker.Reset();
        Assert.True(tracker.IsChanged(42));
        Assert.False(tracker.HasCommittedContent);
    }

    [Fact]
    public void HashIsStableAndSensitiveToOrderAndLength()
    {
        Color[] first = [Color.Red, Color.Green, Color.Blue];
        Color[] same = [Color.Red, Color.Green, Color.Blue];
        Color[] reordered = [Color.Blue, Color.Green, Color.Red];
        Color[] shorter = [Color.Red, Color.Green];

        var expected = LightingTextureContentHash.Compute(first);
        Assert.Equal(expected, LightingTextureContentHash.Compute(same));
        Assert.NotEqual(expected, LightingTextureContentHash.Compute(reordered));
        Assert.NotEqual(expected, LightingTextureContentHash.Compute(shorter));
    }

    [Fact]
    public void EmptyHashIsDeterministic()
    {
        Assert.Equal(
            LightingTextureContentHash.Compute(ReadOnlySpan<Color>.Empty),
            LightingTextureContentHash.Compute(ReadOnlySpan<Color>.Empty));
    }

    [Fact]
    public void TrackingAndHashing_DoNotAllocateInSteadyState()
    {
        var pixels = new Color[256];
        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = new Color(
                (byte)index,
                (byte)(index * 3),
                (byte)(index * 7),
                byte.MaxValue);
        }

        var tracker = new TextureUploadContentTracker();
        var hash = LightingTextureContentHash.Compute(pixels);
        tracker.Commit(hash);
        for (var index = 0; index < 32; index++)
        {
            Assert.False(tracker.IsChanged(LightingTextureContentHash.Compute(pixels)));
        }

        ulong checksum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 10_000; index++)
        {
            var current = LightingTextureContentHash.Compute(pixels);
            checksum ^= current;
            if (tracker.IsChanged(current))
            {
                tracker.Commit(current);
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0UL, checksum);
        Assert.Equal(0, allocated);
    }
}
