using Game.Core.UI.Animation;
using Xunit;

namespace Game.Tests.UITests;

public sealed class UiAnimationTests
{
    [Fact]
    public void Track_EvaluatesKeyframesWithCurve()
    {
        var track = new UiAnimationTrack(
            UiAnimationProperty.Opacity,
            new[]
            {
                new UiAnimationKeyframe(0f, 0f),
                new UiAnimationKeyframe(1f, 1f, UiAnimationCurve.Linear)
            });

        Assert.Equal(0f, track.Evaluate(-1f));
        Assert.Equal(0.5f, track.Evaluate(0.5f), precision: 4);
        Assert.Equal(1f, track.Evaluate(2f));
    }

    [Fact]
    public void Player_ClampsNonLoopingClipAtEnd()
    {
        var player = new UiAnimationPlayer();
        player.Play(UiAnimationClip.FadeIn(1f));

        player.Update(2f);

        Assert.False(player.IsPlaying);
        Assert.Equal(1f, player.TimeSeconds);
        Assert.Equal(1f, player.GetValue(UiAnimationProperty.Opacity), precision: 4);
    }

    [Fact]
    public void Player_LoopsClipTime()
    {
        var clip = new UiAnimationClip(
            "loop",
            new[]
            {
                new UiAnimationTrack(
                    UiAnimationProperty.Custom0,
                    new[]
                    {
                        new UiAnimationKeyframe(0f, 0f),
                        new UiAnimationKeyframe(1f, 10f, UiAnimationCurve.Linear)
                    })
            },
            loop: true);
        var player = new UiAnimationPlayer();

        player.Play(clip);
        player.Update(1.25f);

        Assert.True(player.IsPlaying);
        Assert.Equal(0.25f, player.TimeSeconds, precision: 4);
        Assert.Equal(2.5f, player.GetValue(UiAnimationProperty.Custom0), precision: 4);
    }
}
