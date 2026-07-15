namespace Game.Core.Animation;

public enum AnimationActionLockMode
{
    None,
    FixedTicks,
    UntilClipComplete
}

public enum AnimationStateRequestResult
{
    Applied,
    AlreadyActive,
    BlockedByActionLock,
    LayerNotFound,
    StateNotFound
}

public sealed class AnimationStateDefinition
{
    public AnimationStateDefinition(
        string id,
        AnimationClip clip,
        AnimationPlaybackRate? playbackRate = null,
        bool scaleWithLocomotion = false,
        int locomotionReferenceSpeedMilliUnitsPerSecond = 1000,
        int minimumLocomotionRatePercentage = 50,
        int maximumLocomotionRatePercentage = 200,
        AnimationActionLockMode actionLockMode = AnimationActionLockMode.None,
        int actionLockTicks = 0,
        string? completionStateId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));
        if (locomotionReferenceSpeedMilliUnitsPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(locomotionReferenceSpeedMilliUnitsPerSecond));
        }

        if (minimumLocomotionRatePercentage < 0 ||
            maximumLocomotionRatePercentage < minimumLocomotionRatePercentage)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLocomotionRatePercentage));
        }

        if (actionLockMode == AnimationActionLockMode.FixedTicks && actionLockTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actionLockTicks),
                "A fixed action lock needs at least one tick.");
        }

        if (actionLockMode == AnimationActionLockMode.UntilClipComplete &&
            clip.LoopMode != AnimationLoopMode.Once)
        {
            throw new ArgumentException(
                "An until-complete action lock requires a once clip.",
                nameof(actionLockMode));
        }

        if (completionStateId is not null && clip.LoopMode != AnimationLoopMode.Once)
        {
            throw new ArgumentException(
                "A completion state requires a once clip.",
                nameof(completionStateId));
        }

        Id = id;
        PlaybackRate = playbackRate ?? AnimationPlaybackRate.Normal;
        ScaleWithLocomotion = scaleWithLocomotion;
        LocomotionReferenceSpeedMilliUnitsPerSecond = locomotionReferenceSpeedMilliUnitsPerSecond;
        MinimumLocomotionRatePercentage = minimumLocomotionRatePercentage;
        MaximumLocomotionRatePercentage = maximumLocomotionRatePercentage;
        ActionLockMode = actionLockMode;
        ActionLockTicks = actionLockTicks;
        CompletionStateId = completionStateId;
    }

    public string Id { get; }

    public AnimationClip Clip { get; }

    public AnimationPlaybackRate PlaybackRate { get; }

    public bool ScaleWithLocomotion { get; }

    public int LocomotionReferenceSpeedMilliUnitsPerSecond { get; }

    public int MinimumLocomotionRatePercentage { get; }

    public int MaximumLocomotionRatePercentage { get; }

    public AnimationActionLockMode ActionLockMode { get; }

    public int ActionLockTicks { get; }

    public string? CompletionStateId { get; }

    internal AnimationPlaybackRate ResolvePlaybackRate(int locomotionSpeedMilliUnitsPerSecond)
    {
        if (!ScaleWithLocomotion)
        {
            return PlaybackRate;
        }

        var locomotionRate = AnimationPlaybackRate.FromLocomotionSpeed(
            locomotionSpeedMilliUnitsPerSecond,
            LocomotionReferenceSpeedMilliUnitsPerSecond,
            MinimumLocomotionRatePercentage,
            MaximumLocomotionRatePercentage);

        return PlaybackRate.Multiply(locomotionRate);
    }
}

public sealed record AnimationTransitionDefinition
{
    public const string AnyState = "*";

    public AnimationTransitionDefinition(
        string fromStateId,
        string toStateId,
        int blendTicks = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromStateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toStateId);
        if (blendTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blendTicks));
        }

        FromStateId = fromStateId;
        ToStateId = toStateId;
        BlendTicks = blendTicks;
    }

    public string FromStateId { get; }

    public string ToStateId { get; }

    public int BlendTicks { get; }
}

public sealed class AnimationStateLayerDefinition
{
    private readonly Dictionary<string, AnimationStateDefinition> _states;
    private readonly AnimationTransitionDefinition[] _transitions;

    public AnimationStateLayerDefinition(
        string id,
        int priority,
        string initialStateId,
        IEnumerable<AnimationStateDefinition> states,
        IEnumerable<AnimationTransitionDefinition>? transitions = null,
        AnimationColor? tint = null,
        bool visible = true,
        float opacity = 1f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialStateId);
        ArgumentNullException.ThrowIfNull(states);
        if (opacity is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity));
        }

        Id = id;
        Priority = priority;
        InitialStateId = initialStateId;
        Tint = tint ?? AnimationColor.White;
        Visible = visible;
        Opacity = opacity;
        _states = new Dictionary<string, AnimationStateDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var state in states)
        {
            if (!_states.TryAdd(state.Id, state))
            {
                throw new ArgumentException($"Duplicate state id '{state.Id}' in layer '{id}'.", nameof(states));
            }
        }

        if (!_states.ContainsKey(initialStateId))
        {
            throw new ArgumentException(
                $"Initial state '{initialStateId}' does not exist in layer '{id}'.",
                nameof(initialStateId));
        }

        _transitions = transitions?.ToArray() ?? Array.Empty<AnimationTransitionDefinition>();
        ValidateReferences();
    }

    public string Id { get; }

    public int Priority { get; }

    public string InitialStateId { get; }

    public AnimationColor Tint { get; }

    public bool Visible { get; }

    public float Opacity { get; }

    public IReadOnlyCollection<AnimationStateDefinition> States => _states.Values;

    public IReadOnlyList<AnimationTransitionDefinition> Transitions => _transitions;

    public bool TryGetState(string stateId, out AnimationStateDefinition state)
    {
        return _states.TryGetValue(stateId, out state!);
    }

    internal AnimationTransitionDefinition? FindTransition(string fromStateId, string toStateId)
    {
        AnimationTransitionDefinition? wildcard = null;
        for (var index = 0; index < _transitions.Length; index++)
        {
            var transition = _transitions[index];
            if (!string.Equals(transition.ToStateId, toStateId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(transition.FromStateId, fromStateId, StringComparison.OrdinalIgnoreCase))
            {
                return transition;
            }

            if (string.Equals(transition.FromStateId, AnimationTransitionDefinition.AnyState, StringComparison.Ordinal))
            {
                wildcard = transition;
            }
        }

        return wildcard;
    }

    private void ValidateReferences()
    {
        foreach (var state in _states.Values)
        {
            if (state.CompletionStateId is not null && !_states.ContainsKey(state.CompletionStateId))
            {
                throw new ArgumentException(
                    $"Completion state '{state.CompletionStateId}' from '{state.Id}' does not exist in layer '{Id}'.",
                    nameof(States));
            }
        }

        for (var index = 0; index < _transitions.Length; index++)
        {
            var transition = _transitions[index];
            if (!string.Equals(transition.FromStateId, AnimationTransitionDefinition.AnyState, StringComparison.Ordinal) &&
                !_states.ContainsKey(transition.FromStateId))
            {
                throw new ArgumentException(
                    $"Transition source '{transition.FromStateId}' does not exist in layer '{Id}'.",
                    nameof(Transitions));
            }

            if (!_states.ContainsKey(transition.ToStateId))
            {
                throw new ArgumentException(
                    $"Transition target '{transition.ToStateId}' does not exist in layer '{Id}'.",
                    nameof(Transitions));
            }
        }
    }
}

public sealed class LayeredAnimationStateMachineDefinition
{
    private readonly AnimationStateLayerDefinition[] _layers;

    public LayeredAnimationStateMachineDefinition(IEnumerable<AnimationStateLayerDefinition> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        _layers = layers.OrderBy(layer => layer.Priority).ThenBy(layer => layer.Id, StringComparer.Ordinal).ToArray();
        if (_layers.Length == 0)
        {
            throw new ArgumentException("A state machine needs at least one layer.", nameof(layers));
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < _layers.Length; index++)
        {
            if (!ids.Add(_layers[index].Id))
            {
                throw new ArgumentException($"Duplicate animation layer id '{_layers[index].Id}'.", nameof(layers));
            }
        }
    }

    public IReadOnlyList<AnimationStateLayerDefinition> Layers => _layers;
}

public readonly record struct AnimationUpdateContext(
    CharacterFacingDirection FacingDirection,
    int LocomotionSpeedMilliUnitsPerSecond = 0);

public readonly record struct AnimationStateRequest(
    bool BypassActionLock = false,
    bool RestartIfActive = false,
    int? BlendTicksOverride = null);
