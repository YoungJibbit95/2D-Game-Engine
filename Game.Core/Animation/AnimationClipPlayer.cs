namespace Game.Core.Animation;

public sealed class AnimationClipPlayer
{
    private readonly AnimationTrackSample[] _trackSamples;
    private long _rateRemainderNumerator;
    private long _rateRemainderDenominator = 1;
    private int _timelineDirection = 1;

    public AnimationClipPlayer(AnimationClip clip)
    {
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));
        _trackSamples = new AnimationTrackSample[clip.Tracks.Count];
        Events = new AnimationEventCursor();
        Clip.AppendEventsAtTick(0, Events);
    }

    public AnimationClip Clip { get; }

    public AnimationEventCursor Events { get; }

    public int TimelineTick { get; private set; }

    public bool IsComplete { get; private set; }

    public long ElapsedFixedTicks { get; private set; }

    public void Restart()
    {
        TimelineTick = 0;
        ElapsedFixedTicks = 0;
        _rateRemainderNumerator = 0;
        _rateRemainderDenominator = 1;
        _timelineDirection = 1;
        IsComplete = false;
        Events.Clear();
        Clip.AppendEventsAtTick(0, Events);
    }

    public void AdvanceFixedTick(AnimationPlaybackRate? playbackRate = null)
    {
        ElapsedFixedTicks++;
        if (IsComplete)
        {
            return;
        }

        var rate = playbackRate ?? AnimationPlaybackRate.Normal;
        var advanceCount = AccumulatePlaybackRate(rate);
        for (var index = 0L; index < advanceCount && !IsComplete; index++)
        {
            AdvanceTimelineTick();
        }
    }

    public AnimationClipSample Sample()
    {
        for (var index = 0; index < Clip.Tracks.Count; index++)
        {
            var track = Clip.Tracks[index];
            var frame = track.GetFrameAtTick(TimelineTick);
            _trackSamples[index] = new AnimationTrackSample(
                track.Id,
                track.TargetLayerId,
                frame.SpriteIdOverride ?? track.SpriteId,
                frame.SpriteFrameIndex,
                frame.Transform,
                frame.Tint,
                frame.Visible,
                frame.Sockets);
        }

        return new AnimationClipSample
        {
            ClipId = Clip.Id,
            TimelineTick = TimelineTick,
            IsComplete = IsComplete,
            Tracks = (AnimationTrackSample[])_trackSamples.Clone()
        };
    }

    private void AdvanceTimelineTick()
    {
        switch (Clip.LoopMode)
        {
            case AnimationLoopMode.Once:
                AdvanceOnce();
                break;
            case AnimationLoopMode.PingPong:
                AdvancePingPong();
                break;
            default:
                TimelineTick = (TimelineTick + 1) % Clip.DurationTicks;
                Clip.AppendEventsAtTick(TimelineTick, Events);
                break;
        }
    }

    private long AccumulatePlaybackRate(AnimationPlaybackRate rate)
    {
        var divisor = GreatestCommonDivisor(_rateRemainderDenominator, rate.Denominator);
        var commonDenominator = checked((_rateRemainderDenominator / divisor) * rate.Denominator);
        var accumulatedNumerator = checked(
            (_rateRemainderNumerator * (commonDenominator / _rateRemainderDenominator)) +
            ((long)rate.Numerator * (commonDenominator / rate.Denominator)));
        var advanceCount = accumulatedNumerator / commonDenominator;
        _rateRemainderNumerator = accumulatedNumerator % commonDenominator;

        if (_rateRemainderNumerator == 0)
        {
            _rateRemainderDenominator = 1;
            return advanceCount;
        }

        var remainderDivisor = GreatestCommonDivisor(_rateRemainderNumerator, commonDenominator);
        _rateRemainderNumerator /= remainderDivisor;
        _rateRemainderDenominator = commonDenominator / remainderDivisor;
        return advanceCount;
    }

    private static long GreatestCommonDivisor(long left, long right)
    {
        while (right != 0)
        {
            var remainder = left % right;
            left = right;
            right = remainder;
        }

        return Math.Max(1, left);
    }

    private void AdvanceOnce()
    {
        if (TimelineTick >= Clip.DurationTicks - 1)
        {
            IsComplete = true;
            return;
        }

        TimelineTick++;
        Clip.AppendEventsAtTick(TimelineTick, Events);
    }

    private void AdvancePingPong()
    {
        if (Clip.DurationTicks == 1)
        {
            Clip.AppendEventsAtTick(0, Events);
            return;
        }

        var nextTick = TimelineTick + _timelineDirection;
        if (nextTick >= Clip.DurationTicks)
        {
            _timelineDirection = -1;
            nextTick = Clip.DurationTicks - 2;
        }
        else if (nextTick < 0)
        {
            _timelineDirection = 1;
            nextTick = 1;
        }

        TimelineTick = nextTick;
        Clip.AppendEventsAtTick(TimelineTick, Events);
    }
}
