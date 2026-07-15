using Game.Core.Characters;

namespace Game.Core.Animation;

public sealed record AnimationStateMachineProfile
{
    public AnimationStateMachineProfile(string id, LayeredAnimationStateMachineDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public string Id { get; }

    public LayeredAnimationStateMachineDefinition Definition { get; }
}

public sealed record CharacterRigSpriteBinding
{
    public CharacterRigSpriteBinding(
        CharacterAppearanceSlot slot,
        string spriteId,
        bool visibleByDefault = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        Slot = slot;
        SpriteId = spriteId;
        VisibleByDefault = visibleByDefault;
    }

    public CharacterAppearanceSlot Slot { get; }

    public string SpriteId { get; }

    public bool VisibleByDefault { get; }
}

public sealed class CharacterRigSpriteBindings
{
    private readonly Dictionary<CharacterAppearanceSlot, CharacterRigSpriteBinding> _bySlot;
    private readonly CharacterRigSpriteBinding[] _ordered;

    public CharacterRigSpriteBindings(IEnumerable<CharacterRigSpriteBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        _bySlot = new Dictionary<CharacterAppearanceSlot, CharacterRigSpriteBinding>();
        foreach (var binding in bindings.OrderBy(binding => binding.Slot))
        {
            if (!_bySlot.TryAdd(binding.Slot, binding))
            {
                throw new ArgumentException(
                    $"Duplicate character sprite binding for slot '{binding.Slot}'.",
                    nameof(bindings));
            }
        }

        if (!_bySlot.ContainsKey(CharacterAppearanceSlot.Body))
        {
            throw new ArgumentException("Character sprite bindings require a body sprite.", nameof(bindings));
        }

        _ordered = _bySlot.Values.OrderBy(binding => binding.Slot).ToArray();
    }

    public IReadOnlyList<CharacterRigSpriteBinding> Bindings => _ordered;

    public bool TryGetBinding(CharacterAppearanceSlot slot, out CharacterRigSpriteBinding binding)
    {
        return _bySlot.TryGetValue(slot, out binding!);
    }

    public CharacterAppearanceProfile CreateAppearance(
        IReadOnlyDictionary<CharacterAppearanceSlot, AnimationColor>? tints = null,
        IReadOnlyDictionary<CharacterAppearanceSlot, bool>? visibility = null)
    {
        var body = CreatePart(CharacterAppearanceSlot.Body, tints, visibility)
            ?? throw new InvalidOperationException("Character body binding cannot be hidden or missing.");
        if (!body.Visible)
        {
            throw new InvalidOperationException("Character body binding cannot be hidden or missing.");
        }

        return new CharacterAppearanceProfile(
            body,
            eyes: CreatePart(CharacterAppearanceSlot.Eyes, tints, visibility),
            hair: CreatePart(CharacterAppearanceSlot.Hair, tints, visibility),
            clothing: CreatePart(CharacterAppearanceSlot.Clothing, tints, visibility),
            armor: CreatePart(CharacterAppearanceSlot.Armor, tints, visibility),
            hands: CreatePart(CharacterAppearanceSlot.Hands, tints, visibility),
            shield: CreatePart(CharacterAppearanceSlot.Shield, tints, visibility),
            tool: CreatePart(CharacterAppearanceSlot.Tool, tints, visibility),
            accessory: CreatePart(CharacterAppearanceSlot.Accessory, tints, visibility));
    }

    private CharacterPartAppearance? CreatePart(
        CharacterAppearanceSlot slot,
        IReadOnlyDictionary<CharacterAppearanceSlot, AnimationColor>? tints,
        IReadOnlyDictionary<CharacterAppearanceSlot, bool>? visibility)
    {
        if (!_bySlot.TryGetValue(slot, out var binding))
        {
            return null;
        }

        var visible = visibility is not null && visibility.TryGetValue(slot, out var overrideVisibility)
            ? overrideVisibility
            : binding.VisibleByDefault;
        if (!visible)
        {
            return CharacterPartAppearance.Hidden;
        }

        var tint = tints is not null && tints.TryGetValue(slot, out var overrideTint)
            ? overrideTint
            : AnimationColor.White;
        return new CharacterPartAppearance(binding.SpriteId, tint);
    }
}

public sealed record CharacterAnimationActionBinding
{
    public CharacterAnimationActionBinding(
        CharacterAnimationState action,
        string layerId,
        string stateId,
        bool bypassActionLock = false,
        bool restartIfActive = true,
        int? blendTicksOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        if (blendTicksOverride < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blendTicksOverride));
        }

        Action = action;
        LayerId = layerId;
        StateId = stateId;
        Request = new AnimationStateRequest(bypassActionLock, restartIfActive, blendTicksOverride);
    }

    public CharacterAnimationState Action { get; }

    public string LayerId { get; }

    public string StateId { get; }

    public AnimationStateRequest Request { get; }
}

public sealed class CharacterAnimationProfile
{
    private readonly Dictionary<CharacterAnimationState, CharacterAnimationActionBinding> _actions;
    private readonly CharacterAnimationActionBinding[] _orderedActions;

    public CharacterAnimationProfile(
        string id,
        CharacterRigProfile rig,
        AnimationStateMachineProfile stateMachine,
        CharacterRigSpriteBindings spriteBindings,
        IEnumerable<CharacterAnimationActionBinding>? actions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        Rig = rig ?? throw new ArgumentNullException(nameof(rig));
        StateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        SpriteBindings = spriteBindings ?? throw new ArgumentNullException(nameof(spriteBindings));
        _actions = new Dictionary<CharacterAnimationState, CharacterAnimationActionBinding>();

        foreach (var action in (actions ?? Array.Empty<CharacterAnimationActionBinding>())
                     .OrderBy(action => action.Action))
        {
            if (!_actions.TryAdd(action.Action, action))
            {
                throw new ArgumentException(
                    $"Duplicate animation action binding '{action.Action}' in profile '{id}'.",
                    nameof(actions));
            }

            var layer = StateMachine.Definition.Layers.FirstOrDefault(layer =>
                string.Equals(layer.Id, action.LayerId, StringComparison.OrdinalIgnoreCase));
            if (layer is null || !layer.TryGetState(action.StateId, out _))
            {
                throw new ArgumentException(
                    $"Action '{action.Action}' references missing state '{action.LayerId}/{action.StateId}' in profile '{id}'.",
                    nameof(actions));
            }
        }

        _orderedActions = _actions.Values.OrderBy(action => action.Action).ToArray();
    }

    public string Id { get; }

    public CharacterRigProfile Rig { get; }

    public AnimationStateMachineProfile StateMachine { get; }

    public CharacterRigSpriteBindings SpriteBindings { get; }

    public IReadOnlyList<CharacterAnimationActionBinding> Actions => _orderedActions;

    public LayeredAnimationStateMachine CreateStateMachine()
    {
        return new LayeredAnimationStateMachine(StateMachine.Definition);
    }

    public bool TryResolveAction(
        CharacterAnimationState action,
        out CharacterAnimationActionBinding binding)
    {
        return _actions.TryGetValue(action, out binding!);
    }

    public AnimationStateRequestResult RequestAction(
        LayeredAnimationStateMachine machine,
        CharacterAnimationState action)
    {
        ArgumentNullException.ThrowIfNull(machine);
        return _actions.TryGetValue(action, out var binding)
            ? machine.RequestState(binding.LayerId, binding.StateId, binding.Request)
            : AnimationStateRequestResult.StateNotFound;
    }
}

public enum AnimationEntityKind
{
    Entity,
    Enemy,
    Projectile,
    DroppedItem
}

public enum AnimationEntityVisualState
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

public enum AnimationEntityPlayback
{
    Loop,
    PingPong,
    Clamp
}

[Flags]
public enum AnimationEntityMotionStyle
{
    None = 0,
    Bob = 1 << 0,
    SquashStretch = 1 << 1,
    WingFlap = 1 << 2,
    RotateToVelocity = 1 << 3,
    Spin = 1 << 4
}

public readonly record struct AnimationEntityFrameRange
{
    public AnimationEntityFrameRange(
        int startFrame,
        int frameCount,
        int ticksPerFrame,
        AnimationEntityPlayback playback = AnimationEntityPlayback.Loop)
    {
        if (startFrame < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startFrame));
        }

        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        }

        if (ticksPerFrame <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksPerFrame));
        }

        StartFrame = startFrame;
        FrameCount = frameCount;
        TicksPerFrame = ticksPerFrame;
        Playback = playback;
        _ = checked(startFrame + frameCount);
    }

    public int StartFrame { get; }

    public int FrameCount { get; }

    public int TicksPerFrame { get; }

    public AnimationEntityPlayback Playback { get; }

    public int EndFrameExclusive => checked(StartFrame + FrameCount);

    public int ResolveFrame(long elapsedFixedTicks, int phaseOffset = 0)
    {
        if (FrameCount == 1)
        {
            return StartFrame;
        }

        var step = Math.Max(0, elapsedFixedTicks) / TicksPerFrame + phaseOffset;
        var localFrame = Playback switch
        {
            AnimationEntityPlayback.Clamp => (int)Math.Min(FrameCount - 1L, Math.Max(0, step)),
            AnimationEntityPlayback.PingPong => ResolvePingPong(Math.Max(0, step), FrameCount),
            _ => (int)(((step % FrameCount) + FrameCount) % FrameCount)
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

public sealed class EntityAnimationProfile
{
    private readonly Dictionary<AnimationEntityVisualState, AnimationEntityFrameRange> _animations;
    private readonly KeyValuePair<AnimationEntityVisualState, AnimationEntityFrameRange>[] _orderedAnimations;

    public EntityAnimationProfile(
        string id,
        AnimationEntityKind kind,
        string spriteId,
        IEnumerable<string>? bindings,
        IEnumerable<KeyValuePair<AnimationEntityVisualState, AnimationEntityFrameRange>> animations,
        string? eliteSpriteId = null,
        AnimationEntityMotionStyle motionStyle = AnimationEntityMotionStyle.Bob | AnimationEntityMotionStyle.SquashStretch,
        bool isFlying = false,
        bool isElite = false,
        bool castsShadow = true,
        float displayScale = 1f,
        float walkSpeedThreshold = 4f,
        float runSpeedThreshold = 72f,
        float airborneSpeedThreshold = 18f,
        float bobAmplitude = 1.25f,
        float shadowWidthFactor = 0.75f,
        float shadowOpacity = 0.34f,
        int hurtLockTicks = 6,
        int attackLockTicks = 12)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        ArgumentNullException.ThrowIfNull(animations);
        if (eliteSpriteId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(eliteSpriteId);
        }

        if (!float.IsFinite(displayScale) || displayScale <= 0f ||
            !float.IsFinite(walkSpeedThreshold) || walkSpeedThreshold < 0f ||
            !float.IsFinite(runSpeedThreshold) || runSpeedThreshold < walkSpeedThreshold ||
            !float.IsFinite(airborneSpeedThreshold) || airborneSpeedThreshold < 0f ||
            !float.IsFinite(bobAmplitude) || bobAmplitude < 0f ||
            !float.IsFinite(shadowWidthFactor) || shadowWidthFactor <= 0f ||
            !float.IsFinite(shadowOpacity) || shadowOpacity is < 0f or > 1f)
        {
            throw new ArgumentException($"Entity animation profile '{id}' contains invalid presentation tuning.");
        }

        if (hurtLockTicks < 0 || attackLockTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hurtLockTicks), "Entity animation lock ticks cannot be negative.");
        }

        _animations = new Dictionary<AnimationEntityVisualState, AnimationEntityFrameRange>();
        foreach (var animation in animations.OrderBy(pair => pair.Key))
        {
            if (!_animations.TryAdd(animation.Key, animation.Value))
            {
                throw new ArgumentException(
                    $"Duplicate entity animation state '{animation.Key}' in profile '{id}'.",
                    nameof(animations));
            }
        }

        if (!_animations.ContainsKey(AnimationEntityVisualState.Idle))
        {
            throw new ArgumentException($"Entity animation profile '{id}' requires an Idle state.", nameof(animations));
        }

        Id = id;
        Kind = kind;
        SpriteId = spriteId;
        EliteSpriteId = eliteSpriteId;
        Bindings = bindings?
            .Where(binding => !string.IsNullOrWhiteSpace(binding))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        MotionStyle = motionStyle;
        IsFlying = isFlying;
        IsElite = isElite;
        CastsShadow = castsShadow;
        DisplayScale = displayScale;
        WalkSpeedThreshold = walkSpeedThreshold;
        RunSpeedThreshold = runSpeedThreshold;
        AirborneSpeedThreshold = airborneSpeedThreshold;
        BobAmplitude = bobAmplitude;
        ShadowWidthFactor = shadowWidthFactor;
        ShadowOpacity = shadowOpacity;
        HurtLockTicks = hurtLockTicks;
        AttackLockTicks = attackLockTicks;
        _orderedAnimations = _animations.OrderBy(pair => pair.Key).ToArray();
    }

    public string Id { get; }

    public AnimationEntityKind Kind { get; }

    public string SpriteId { get; }

    public string? EliteSpriteId { get; }

    public IReadOnlyList<string> Bindings { get; }

    public AnimationEntityMotionStyle MotionStyle { get; }

    public bool IsFlying { get; }

    public bool IsElite { get; }

    public bool CastsShadow { get; }

    public float DisplayScale { get; }

    public float WalkSpeedThreshold { get; }

    public float RunSpeedThreshold { get; }

    public float AirborneSpeedThreshold { get; }

    public float BobAmplitude { get; }

    public float ShadowWidthFactor { get; }

    public float ShadowOpacity { get; }

    public int HurtLockTicks { get; }

    public int AttackLockTicks { get; }

    public IReadOnlyList<KeyValuePair<AnimationEntityVisualState, AnimationEntityFrameRange>> Animations =>
        _orderedAnimations;

    public AnimationEntityFrameRange GetAnimation(AnimationEntityVisualState state)
    {
        return _animations.GetValueOrDefault(state, _animations[AnimationEntityVisualState.Idle]);
    }

    public int ResolveFrame(AnimationEntityVisualState state, long elapsedFixedTicks, int phaseOffset = 0)
    {
        return GetAnimation(state).ResolveFrame(elapsedFixedTicks, phaseOffset);
    }
}
