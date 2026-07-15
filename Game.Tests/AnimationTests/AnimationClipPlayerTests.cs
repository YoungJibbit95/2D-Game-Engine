using Game.Core.Animation;
using Xunit;

namespace Game.Tests.AnimationTests;

public sealed class AnimationClipPlayerTests
{
    [Fact]
    public void Sample_ChangesFramesExactlyAtVariableDurationEdges()
    {
        var player = new AnimationClipPlayer(AnimationTestFactory.CreateClip(
            "edges",
            frameDurations: new[] { 2, 1, 3 }));

        Assert.Equal(0, Assert.Single(player.Sample().Tracks).SpriteFrameIndex);
        player.AdvanceFixedTick();
        Assert.Equal(0, Assert.Single(player.Sample().Tracks).SpriteFrameIndex);
        player.AdvanceFixedTick();
        Assert.Equal(1, Assert.Single(player.Sample().Tracks).SpriteFrameIndex);
        player.AdvanceFixedTick();
        Assert.Equal(2, Assert.Single(player.Sample().Tracks).SpriteFrameIndex);
    }

    [Fact]
    public void Loop_WrapsFromLastTimelineTickToZero()
    {
        var player = new AnimationClipPlayer(AnimationTestFactory.CreateClip("loop"));

        player.AdvanceFixedTick();
        player.AdvanceFixedTick();
        Assert.Equal(2, player.TimelineTick);

        player.AdvanceFixedTick();

        Assert.Equal(0, player.TimelineTick);
        Assert.False(player.IsComplete);
    }

    [Fact]
    public void Once_StopsOnLastFrameWithoutRepeatingTerminalEvents()
    {
        var player = new AnimationClipPlayer(AnimationTestFactory.CreateClip(
            "once",
            AnimationLoopMode.Once,
            new[] { 1, 1 },
            new[] { new AnimationEventMarker("finish-frame", 1) }));
        player.Events.ConsumeAll();

        player.AdvanceFixedTick();
        Assert.Equal("finish-frame", Assert.Single(player.Events.ConsumeAll()).EventId);
        player.AdvanceFixedTick();
        player.AdvanceFixedTick();

        Assert.True(player.IsComplete);
        Assert.Equal(1, player.TimelineTick);
        Assert.Empty(player.Events.ConsumeAll());
    }

    [Fact]
    public void PingPong_VisitsBothEdgesOncePerDirectionChange()
    {
        var player = new AnimationClipPlayer(AnimationTestFactory.CreateClip(
            "ping",
            AnimationLoopMode.PingPong));
        var ticks = new List<int> { player.TimelineTick };

        for (var index = 0; index < 6; index++)
        {
            player.AdvanceFixedTick();
            ticks.Add(player.TimelineTick);
        }

        Assert.Equal(new[] { 0, 1, 2, 1, 0, 1, 2 }, ticks);
    }

    [Fact]
    public void Events_FireExactlyOnceForEveryEnteredTimelineTickAcrossWraps()
    {
        var player = new AnimationClipPlayer(AnimationTestFactory.CreateClip(
            "events",
            events: new[]
            {
                new AnimationEventMarker("start", 0),
                new AnimationEventMarker("impact", 2, "pickaxe")
            }));

        var initial = Assert.Single(player.Events.ConsumeAll());
        Assert.Equal("start", initial.EventId);

        for (var index = 0; index < 5; index++)
        {
            player.AdvanceFixedTick();
        }

        var events = player.Events.ConsumeAll();
        Assert.Equal(new[] { "impact", "start", "impact" }, events.Select(item => item.EventId));
        Assert.Equal(new long[] { 1, 2, 3 }, events.Select(item => item.Sequence));
    }

    [Fact]
    public void PlaybackRate_UsesRationalAccumulatorWithoutFractionalDrift()
    {
        var halfSpeed = new AnimationClipPlayer(AnimationTestFactory.CreateClip("half"));
        var doubleSpeed = new AnimationClipPlayer(AnimationTestFactory.CreateClip("double"));

        for (var index = 0; index < 5; index++)
        {
            halfSpeed.AdvanceFixedTick(new AnimationPlaybackRate(1, 2));
            doubleSpeed.AdvanceFixedTick(new AnimationPlaybackRate(2, 1));
        }

        Assert.Equal(2, halfSpeed.TimelineTick);
        Assert.Equal(1, doubleSpeed.TimelineTick);
        Assert.Equal(5, halfSpeed.ElapsedFixedTicks);
        Assert.Equal(5, doubleSpeed.ElapsedFixedTicks);
    }

    [Fact]
    public void PlaybackRate_PreservesExactRemainderWhenDenominatorChanges()
    {
        var player = new AnimationClipPlayer(AnimationTestFactory.CreateClip(
            "changing-rate",
            frameDurations: new[] { 1, 1, 1, 1 }));

        player.AdvanceFixedTick(new AnimationPlaybackRate(1, 2));
        player.AdvanceFixedTick(new AnimationPlaybackRate(1, 3));
        player.AdvanceFixedTick(new AnimationPlaybackRate(1, 6));

        Assert.Equal(1, player.TimelineTick);
    }

    [Fact]
    public void Clip_RejectsTracksWithDifferentFixedTickDurations()
    {
        var body = new AnimationTrack("body", "body", "body", new[] { new AnimationFrame(0, 2) });
        var eyes = new AnimationTrack("eyes", "eyes", "eyes", new[] { new AnimationFrame(0, 3) });

        var exception = Assert.Throws<ArgumentException>(() =>
            new AnimationClip("invalid", AnimationLoopMode.Loop, new[] { body, eyes }));

        Assert.Contains("requires 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sample_UsesOneSharedTimelineForAllTracksAndSockets()
    {
        var body = new AnimationTrack(
            "body",
            "body",
            "body",
            new[]
            {
                new AnimationFrame(0, 1),
                new AnimationFrame(1, 2, sockets: new[] { new AttachmentSocketPose("hand", new(3, 4)) })
            });
        var eyes = new AnimationTrack(
            "eyes",
            "eyes",
            "eyes",
            new[] { new AnimationFrame(5, 2), new AnimationFrame(6, 1) });
        var player = new AnimationClipPlayer(new AnimationClip(
            "multi",
            AnimationLoopMode.Loop,
            new[] { body, eyes }));

        player.AdvanceFixedTick();
        var sample = player.Sample();

        Assert.Equal(1, sample.TimelineTick);
        Assert.True(sample.TryGetTrack("body", out var bodySample));
        Assert.True(sample.TryGetTrack("eyes", out var eyesSample));
        Assert.Equal(1, bodySample.SpriteFrameIndex);
        Assert.Equal(5, eyesSample.SpriteFrameIndex);
        Assert.Equal("hand", Assert.Single(bodySample.Sockets).Id);
    }
}
