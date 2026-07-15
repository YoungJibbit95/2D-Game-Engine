using Game.Core.Runtime;

namespace Game.Client.Rendering.Entities;

public enum EntityVisualState
{
    Idle,
    Walk,
    Run,
    Jump,
    Fly,
    Attack,
    Hurt,
    Death
}

public enum EntityVisualPlayback
{
    Loop,
    PingPong,
    Clamp
}

[Flags]
public enum EntityVisualMotionStyle
{
    None = 0,
    Bob = 1 << 0,
    SquashStretch = 1 << 1,
    WingFlap = 1 << 2,
    RotateToVelocity = 1 << 3,
    Spin = 1 << 4
}

public readonly record struct EntityVisualAnimationRange(
    int StartFrame,
    int FrameCount,
    int TicksPerFrame,
    EntityVisualPlayback Playback = EntityVisualPlayback.Loop)
{
    public int ResolveFrame(long elapsedTicks, int phaseOffset = 0)
    {
        if (FrameCount <= 1 || TicksPerFrame <= 0)
        {
            return Math.Max(0, StartFrame);
        }

        var step = Math.Max(0, elapsedTicks) / TicksPerFrame + phaseOffset;
        var localFrame = Playback switch
        {
            EntityVisualPlayback.Clamp => (int)Math.Min(FrameCount - 1L, step),
            EntityVisualPlayback.PingPong => ResolvePingPong(step, FrameCount),
            _ => (int)(step % FrameCount)
        };
        return StartFrame + localFrame;
    }

    private static int ResolvePingPong(long step, int frameCount)
    {
        var cycleLength = (frameCount - 1) * 2;
        var cycleFrame = (int)(step % cycleLength);
        return cycleFrame < frameCount ? cycleFrame : cycleLength - cycleFrame;
    }
}

public sealed record EntityVisualProfile
{
    public required string Id { get; init; }

    public required string SpriteId { get; init; }

    public string? EliteSpriteId { get; init; }

    public EntityFrameKind Kind { get; init; } = EntityFrameKind.Enemy;

    public EntityVisualMotionStyle MotionStyle { get; init; } =
        EntityVisualMotionStyle.Bob | EntityVisualMotionStyle.SquashStretch;

    public bool IsFlying { get; init; }

    public bool IsElite { get; init; }

    public bool CastsShadow { get; init; } = true;

    public float DisplayScale { get; init; } = 1f;

    public float WalkSpeedThreshold { get; init; } = 4f;

    public float RunSpeedThreshold { get; init; } = 72f;

    public float AirborneSpeedThreshold { get; init; } = 18f;

    public float BobAmplitude { get; init; } = 1.25f;

    public float ShadowWidthFactor { get; init; } = 0.75f;

    public float ShadowOpacity { get; init; } = 0.34f;

    public int HurtLockTicks { get; init; } = 6;

    public int AttackLockTicks { get; init; } = 12;

    public EntityVisualAnimationRange Idle { get; init; } = new(0, 1, 12);

    public EntityVisualAnimationRange Walk { get; init; } = new(0, 1, 8);

    public EntityVisualAnimationRange Run { get; init; } = new(0, 1, 5);

    public EntityVisualAnimationRange Jump { get; init; } = new(0, 1, 8);

    public EntityVisualAnimationRange Fly { get; init; } = new(0, 1, 4);

    public EntityVisualAnimationRange Attack { get; init; } = new(0, 1, 3, EntityVisualPlayback.Clamp);

    public EntityVisualAnimationRange Hurt { get; init; } = new(0, 1, 3, EntityVisualPlayback.Clamp);

    public EntityVisualAnimationRange Death { get; init; } = new(0, 1, 4, EntityVisualPlayback.Clamp);

    public EntityVisualAnimationRange GetAnimation(EntityVisualState state)
    {
        return state switch
        {
            EntityVisualState.Walk => Walk,
            EntityVisualState.Run => Run,
            EntityVisualState.Jump => Jump,
            EntityVisualState.Fly => Fly,
            EntityVisualState.Attack => Attack,
            EntityVisualState.Hurt => Hurt,
            EntityVisualState.Death => Death,
            _ => Idle
        };
    }
}

public sealed class EntityVisualProfileBuilder
{
    private readonly string _id;
    private readonly string _spriteId;
    private EntityFrameKind _kind = EntityFrameKind.Enemy;
    private EntityVisualMotionStyle _motionStyle =
        EntityVisualMotionStyle.Bob | EntityVisualMotionStyle.SquashStretch;
    private string? _eliteSpriteId;
    private bool _isFlying;
    private bool _isElite;
    private bool _castsShadow = true;
    private float _displayScale = 1f;
    private float _walkThreshold = 4f;
    private float _runThreshold = 72f;
    private float _airborneThreshold = 18f;
    private float _bobAmplitude = 1.25f;
    private float _shadowWidthFactor = 0.75f;
    private float _shadowOpacity = 0.34f;
    private int _hurtLockTicks = 6;
    private int _attackLockTicks = 12;
    private readonly EntityVisualAnimationRange[] _animations =
    {
        new(0, 1, 12),
        new(0, 1, 8),
        new(0, 1, 5),
        new(0, 1, 8),
        new(0, 1, 4),
        new(0, 1, 3, EntityVisualPlayback.Clamp),
        new(0, 1, 3, EntityVisualPlayback.Clamp),
        new(0, 1, 4, EntityVisualPlayback.Clamp)
    };

    public EntityVisualProfileBuilder(string id, string spriteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        _id = id;
        _spriteId = spriteId;
    }

    public EntityVisualProfileBuilder ForKind(EntityFrameKind kind)
    {
        _kind = kind;
        return this;
    }

    public EntityVisualProfileBuilder WithMotion(EntityVisualMotionStyle motionStyle)
    {
        _motionStyle = motionStyle;
        return this;
    }

    public EntityVisualProfileBuilder Flying(bool flying = true)
    {
        _isFlying = flying;
        return this;
    }

    public EntityVisualProfileBuilder Elite(string eliteSpriteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eliteSpriteId);
        _isElite = true;
        _eliteSpriteId = eliteSpriteId;
        return this;
    }

    public EntityVisualProfileBuilder WithoutShadow()
    {
        _castsShadow = false;
        return this;
    }

    public EntityVisualProfileBuilder WithScale(float displayScale)
    {
        _displayScale = RequirePositiveFinite(displayScale, nameof(displayScale));
        return this;
    }

    public EntityVisualProfileBuilder WithSpeedThresholds(float walk, float run, float airborne)
    {
        _walkThreshold = RequireNonNegativeFinite(walk, nameof(walk));
        _runThreshold = RequireNonNegativeFinite(run, nameof(run));
        _airborneThreshold = RequireNonNegativeFinite(airborne, nameof(airborne));
        if (_runThreshold < _walkThreshold)
        {
            throw new ArgumentException("Run threshold must be greater than or equal to walk threshold.");
        }

        return this;
    }

    public EntityVisualProfileBuilder WithBob(float amplitude)
    {
        _bobAmplitude = RequireNonNegativeFinite(amplitude, nameof(amplitude));
        return this;
    }

    public EntityVisualProfileBuilder WithShadow(float widthFactor, float opacity)
    {
        _shadowWidthFactor = RequirePositiveFinite(widthFactor, nameof(widthFactor));
        _shadowOpacity = Math.Clamp(RequireNonNegativeFinite(opacity, nameof(opacity)), 0f, 1f);
        return this;
    }

    public EntityVisualProfileBuilder WithStateLocks(int hurtTicks, int attackTicks)
    {
        _hurtLockTicks = Math.Max(0, hurtTicks);
        _attackLockTicks = Math.Max(0, attackTicks);
        return this;
    }

    public EntityVisualProfileBuilder WithAnimation(EntityVisualState state, EntityVisualAnimationRange animation)
    {
        if (animation.StartFrame < 0 || animation.FrameCount <= 0 || animation.TicksPerFrame <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(animation));
        }

        _animations[(int)state] = animation;
        return this;
    }

    public EntityVisualProfile Build()
    {
        return new EntityVisualProfile
        {
            Id = _id,
            SpriteId = _spriteId,
            EliteSpriteId = _eliteSpriteId,
            Kind = _kind,
            MotionStyle = _motionStyle,
            IsFlying = _isFlying,
            IsElite = _isElite,
            CastsShadow = _castsShadow,
            DisplayScale = _displayScale,
            WalkSpeedThreshold = _walkThreshold,
            RunSpeedThreshold = _runThreshold,
            AirborneSpeedThreshold = _airborneThreshold,
            BobAmplitude = _bobAmplitude,
            ShadowWidthFactor = _shadowWidthFactor,
            ShadowOpacity = _shadowOpacity,
            HurtLockTicks = _hurtLockTicks,
            AttackLockTicks = _attackLockTicks,
            Idle = _animations[(int)EntityVisualState.Idle],
            Walk = _animations[(int)EntityVisualState.Walk],
            Run = _animations[(int)EntityVisualState.Run],
            Jump = _animations[(int)EntityVisualState.Jump],
            Fly = _animations[(int)EntityVisualState.Fly],
            Attack = _animations[(int)EntityVisualState.Attack],
            Hurt = _animations[(int)EntityVisualState.Hurt],
            Death = _animations[(int)EntityVisualState.Death]
        };
    }

    private static float RequirePositiveFinite(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            throw new ArgumentOutOfRangeException(name);
        }

        return value;
    }

    private static float RequireNonNegativeFinite(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(name);
        }

        return value;
    }
}
