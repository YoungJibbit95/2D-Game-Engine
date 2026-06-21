using Game.Core.Animations;
using Game.Core.Data;
using Xunit;

namespace Game.Tests.AnimationTests;

public sealed class SpriteAnimationTests
{
    [Fact]
    public void Loader_ExpandsCompactSpriteSheetDefinition()
    {
        const string json = """
        {
          "animations": [
            {
              "id": "player.walk",
              "displayName": "Player Walk",
              "sprite": "entities/player/base_actions",
              "frameStart": 2,
              "frameCount": 4,
              "frameDuration": 0.08,
              "loopMode": "Loop",
              "tags": ["player", "movement"]
            }
          ]
        }
        """;

        var clip = Assert.Single(new SpriteAnimationJsonLoader().LoadClipsFromJson(json));

        Assert.Equal("player.walk", clip.Id);
        Assert.Equal(SpriteAnimationLoopMode.Loop, clip.LoopMode);
        Assert.Equal(4, clip.Frames.Count);
        Assert.Equal("entities/player/base_actions", clip.Frames[0].SpriteId);
        Assert.Equal(2, clip.Frames[0].FrameIndex);
        Assert.Equal(5, clip.Frames[3].FrameIndex);
        Assert.True(clip.HasTag("movement"));
    }

    [Fact]
    public void Registry_RejectsInvalidClips()
    {
        var clip = new SpriteAnimationClip
        {
            Id = "broken",
            Frames = new[]
            {
                new SpriteAnimationFrame
                {
                    SpriteId = "entities/player/base_actions",
                    FrameIndex = 0,
                    DurationSeconds = 0
                }
            }
        };

        Assert.Throws<RegistryValidationException>(() => SpriteAnimationRegistry.Create(new[] { clip }));
    }

    [Fact]
    public void Animator_LoopsClipFrames()
    {
        var clip = CreateClip(SpriteAnimationLoopMode.Loop, 0.1f, 3);
        var animator = new SpriteAnimator();

        animator.Play(clip);
        animator.Update(0.11f);
        Assert.Equal(1, animator.FrameIndex);
        animator.Update(0.2f);
        Assert.Equal(0, animator.FrameIndex);
        Assert.True(animator.IsPlaying);
    }

    [Fact]
    public void Animator_CompletesOnceClipOnLastFrame()
    {
        var clip = CreateClip(SpriteAnimationLoopMode.Once, 0.1f, 2);
        var animator = new SpriteAnimator();

        animator.Play(clip);
        animator.Update(0.25f);

        Assert.Equal(1, animator.FrameIndex);
        Assert.True(animator.IsComplete);
        Assert.False(animator.IsPlaying);
    }

    [Fact]
    public void Animator_PingPongsBetweenEdges()
    {
        var clip = CreateClip(SpriteAnimationLoopMode.PingPong, 0.1f, 3);
        var animator = new SpriteAnimator();

        animator.Play(clip);
        animator.Update(0.1f);
        Assert.Equal(1, animator.FrameIndex);
        animator.Update(0.1f);
        Assert.Equal(2, animator.FrameIndex);
        animator.Update(0.1f);
        Assert.Equal(1, animator.FrameIndex);
    }

    private static SpriteAnimationClip CreateClip(SpriteAnimationLoopMode loopMode, float duration, int frameCount)
    {
        return new SpriteAnimationClip
        {
            Id = $"clip.{loopMode}",
            LoopMode = loopMode,
            Frames = Enumerable.Range(0, frameCount)
                .Select(frame => new SpriteAnimationFrame
                {
                    SpriteId = "entities/player/base_actions",
                    FrameIndex = frame,
                    DurationSeconds = duration
                })
                .ToArray()
        };
    }
}
