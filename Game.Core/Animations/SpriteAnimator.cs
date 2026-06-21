namespace Game.Core.Animations;

public sealed class SpriteAnimator
{
    private SpriteAnimationClip? _clip;
    private int _frameIndex;
    private int _direction = 1;
    private float _elapsed;

    public SpriteAnimationClip? Clip => _clip;

    public int FrameIndex => _frameIndex;

    public bool IsPlaying { get; private set; }

    public bool IsComplete { get; private set; }

    public SpriteAnimationFrame? CurrentFrame => _clip is null || _clip.Frames.Count == 0
        ? null
        : _clip.GetFrame(_frameIndex);

    public void Play(SpriteAnimationClip clip, bool restartIfSame = false)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (!restartIfSame && ReferenceEquals(_clip, clip) && IsPlaying)
        {
            return;
        }

        _clip = clip;
        _frameIndex = 0;
        _direction = 1;
        _elapsed = 0f;
        IsPlaying = true;
        IsComplete = false;
    }

    public void Stop()
    {
        IsPlaying = false;
    }

    public void Update(float deltaSeconds)
    {
        if (!IsPlaying || IsComplete || _clip is null || _clip.Frames.Count == 0 || deltaSeconds <= 0)
        {
            return;
        }

        _elapsed += deltaSeconds;
        while (_clip is not null && _elapsed >= _clip.GetFrame(_frameIndex).DurationSeconds && IsPlaying && !IsComplete)
        {
            _elapsed -= _clip.GetFrame(_frameIndex).DurationSeconds;
            AdvanceFrame();
        }
    }

    private void AdvanceFrame()
    {
        if (_clip is null)
        {
            return;
        }

        var count = _clip.Frames.Count;
        if (count <= 1)
        {
            if (_clip.LoopMode == SpriteAnimationLoopMode.Once)
            {
                IsPlaying = false;
                IsComplete = true;
            }

            return;
        }

        switch (_clip.LoopMode)
        {
            case SpriteAnimationLoopMode.Once:
                if (_frameIndex >= count - 1)
                {
                    IsPlaying = false;
                    IsComplete = true;
                    return;
                }

                _frameIndex++;
                break;
            case SpriteAnimationLoopMode.PingPong:
                var next = _frameIndex + _direction;
                if (next >= count)
                {
                    _direction = -1;
                    next = count - 2;
                }
                else if (next < 0)
                {
                    _direction = 1;
                    next = 1;
                }

                _frameIndex = Math.Clamp(next, 0, count - 1);
                break;
            default:
                _frameIndex = (_frameIndex + 1) % count;
                break;
        }
    }
}
