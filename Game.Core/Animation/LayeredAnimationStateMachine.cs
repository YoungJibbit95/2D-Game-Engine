namespace Game.Core.Animation;

public readonly record struct LayeredAnimationEventOccurrence(
    long Sequence,
    string LayerId,
    string StateId,
    AnimationEventOccurrence ClipEvent);

public sealed class LayeredAnimationEventCursor
{
    private readonly List<LayeredAnimationEventOccurrence> _pending = new();
    private long _nextSequence;

    public int PendingCount => _pending.Count;

    public LayeredAnimationEventOccurrence[] ConsumeAll()
    {
        if (_pending.Count == 0)
        {
            return Array.Empty<LayeredAnimationEventOccurrence>();
        }

        var result = _pending.ToArray();
        _pending.Clear();
        return result;
    }

    internal void Append(string layerId, string stateId, AnimationEventOccurrence clipEvent)
    {
        _pending.Add(new LayeredAnimationEventOccurrence(_nextSequence++, layerId, stateId, clipEvent));
    }
}

public sealed record AnimationStateLayerPose
{
    public required string LayerId { get; init; }

    public required int Priority { get; init; }

    public required string StateId { get; init; }

    public required AnimationClipSample Current { get; init; }

    public AnimationClipSample? Outgoing { get; init; }

    public float BlendWeight { get; init; } = 1f;

    public AnimationColor Tint { get; init; } = AnimationColor.White;

    public bool Visible { get; init; } = true;

    public float Opacity { get; init; } = 1f;
}

public sealed record LayeredAnimationPose
{
    public required CharacterFacingDirection FacingDirection { get; init; }

    public required long FixedTick { get; init; }

    public required IReadOnlyList<AnimationStateLayerPose> Layers { get; init; }
}

public sealed class LayeredAnimationStateMachine
{
    private sealed class LayerRuntime
    {
        public LayerRuntime(AnimationStateLayerDefinition definition)
        {
            Definition = definition;
            definition.TryGetState(definition.InitialStateId, out var initialState);
            CurrentState = initialState;
            CurrentPlayer = new AnimationClipPlayer(initialState.Clip);
            RemainingLockTicks = initialState.ActionLockTicks;
            Tint = definition.Tint;
            Visible = definition.Visible;
            Opacity = definition.Opacity;
        }

        public AnimationStateLayerDefinition Definition { get; }

        public AnimationStateDefinition CurrentState { get; set; }

        public AnimationClipPlayer CurrentPlayer { get; set; }

        public AnimationStateDefinition? OutgoingState { get; set; }

        public AnimationClipPlayer? OutgoingPlayer { get; set; }

        public int TransitionTicks { get; set; }

        public int TransitionElapsedTicks { get; set; }

        public int RemainingLockTicks { get; set; }

        public AnimationColor Tint { get; set; }

        public bool Visible { get; set; }

        public float Opacity { get; set; }

        public bool IsActionLocked => CurrentState.ActionLockMode switch
        {
            AnimationActionLockMode.FixedTicks => RemainingLockTicks > 0,
            AnimationActionLockMode.UntilClipComplete => !CurrentPlayer.IsComplete,
            _ => false
        };
    }

    private readonly LayerRuntime[] _orderedLayers;
    private readonly Dictionary<string, LayerRuntime> _layersById;

    public LayeredAnimationStateMachine(LayeredAnimationStateMachineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _orderedLayers = new LayerRuntime[definition.Layers.Count];
        _layersById = new Dictionary<string, LayerRuntime>(StringComparer.OrdinalIgnoreCase);
        Events = new LayeredAnimationEventCursor();

        for (var index = 0; index < definition.Layers.Count; index++)
        {
            var runtime = new LayerRuntime(definition.Layers[index]);
            _orderedLayers[index] = runtime;
            _layersById.Add(runtime.Definition.Id, runtime);
            DrainCurrentEvents(runtime);
        }
    }

    public LayeredAnimationEventCursor Events { get; }

    public CharacterFacingDirection FacingDirection { get; private set; } = CharacterFacingDirection.Right;

    public long FixedTick { get; private set; }

    public AnimationStateRequestResult RequestState(
        string layerId,
        string stateId,
        AnimationStateRequest request = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        if (!_layersById.TryGetValue(layerId, out var runtime))
        {
            return AnimationStateRequestResult.LayerNotFound;
        }

        if (!runtime.Definition.TryGetState(stateId, out var targetState))
        {
            return AnimationStateRequestResult.StateNotFound;
        }

        if (string.Equals(runtime.CurrentState.Id, stateId, StringComparison.OrdinalIgnoreCase))
        {
            if (!request.RestartIfActive)
            {
                return AnimationStateRequestResult.AlreadyActive;
            }

            if (runtime.IsActionLocked && !request.BypassActionLock)
            {
                return AnimationStateRequestResult.BlockedByActionLock;
            }
        }
        else if (runtime.IsActionLocked && !request.BypassActionLock)
        {
            return AnimationStateRequestResult.BlockedByActionLock;
        }

        ApplyState(runtime, targetState, request);
        return AnimationStateRequestResult.Applied;
    }

    public bool TryGetCurrentState(string layerId, out string stateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        if (_layersById.TryGetValue(layerId, out var runtime))
        {
            stateId = runtime.CurrentState.Id;
            return true;
        }

        stateId = string.Empty;
        return false;
    }

    public bool SetLayerPresentation(
        string layerId,
        AnimationColor tint,
        bool visible,
        float opacity = 1f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        if (opacity is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity));
        }

        if (!_layersById.TryGetValue(layerId, out var runtime))
        {
            return false;
        }

        runtime.Tint = tint;
        runtime.Visible = visible;
        runtime.Opacity = opacity;
        return true;
    }

    public void AdvanceFixedTick(AnimationUpdateContext context)
    {
        FixedTick++;
        FacingDirection = context.FacingDirection;
        for (var index = 0; index < _orderedLayers.Length; index++)
        {
            var runtime = _orderedLayers[index];
            AdvanceOutgoing(runtime, context);

            var currentRate = runtime.CurrentState.ResolvePlaybackRate(context.LocomotionSpeedMilliUnitsPerSecond);
            runtime.CurrentPlayer.AdvanceFixedTick(currentRate);
            DrainCurrentEvents(runtime);

            if (runtime.RemainingLockTicks > 0)
            {
                runtime.RemainingLockTicks--;
            }

            AdvanceTransition(runtime);
            TryCompleteState(runtime);
        }
    }

    public LayeredAnimationPose Sample()
    {
        var layers = new AnimationStateLayerPose[_orderedLayers.Length];
        for (var index = 0; index < _orderedLayers.Length; index++)
        {
            var runtime = _orderedLayers[index];
            var blendWeight = runtime.OutgoingPlayer is null || runtime.TransitionTicks <= 0
                ? 1f
                : runtime.TransitionElapsedTicks / (float)runtime.TransitionTicks;

            layers[index] = new AnimationStateLayerPose
            {
                LayerId = runtime.Definition.Id,
                Priority = runtime.Definition.Priority,
                StateId = runtime.CurrentState.Id,
                Current = runtime.CurrentPlayer.Sample(),
                Outgoing = runtime.OutgoingPlayer?.Sample(),
                BlendWeight = Math.Clamp(blendWeight, 0f, 1f),
                Tint = runtime.Tint,
                Visible = runtime.Visible,
                Opacity = runtime.Opacity
            };
        }

        return new LayeredAnimationPose
        {
            FacingDirection = FacingDirection,
            FixedTick = FixedTick,
            Layers = layers
        };
    }

    private void ApplyState(
        LayerRuntime runtime,
        AnimationStateDefinition targetState,
        AnimationStateRequest request)
    {
        var transition = runtime.Definition.FindTransition(runtime.CurrentState.Id, targetState.Id);
        var blendTicks = request.BlendTicksOverride ?? transition?.BlendTicks ?? 0;
        if (blendTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Blend override cannot be negative.");
        }

        if (blendTicks > 0)
        {
            runtime.OutgoingState = runtime.CurrentState;
            runtime.OutgoingPlayer = runtime.CurrentPlayer;
            runtime.TransitionTicks = blendTicks;
            runtime.TransitionElapsedTicks = 0;
        }
        else
        {
            runtime.OutgoingState = null;
            runtime.OutgoingPlayer = null;
            runtime.TransitionTicks = 0;
            runtime.TransitionElapsedTicks = 0;
        }

        runtime.CurrentState = targetState;
        runtime.CurrentPlayer = new AnimationClipPlayer(targetState.Clip);
        runtime.RemainingLockTicks = targetState.ActionLockTicks;
        DrainCurrentEvents(runtime);
    }

    private static void AdvanceOutgoing(LayerRuntime runtime, AnimationUpdateContext context)
    {
        if (runtime.OutgoingPlayer is null || runtime.OutgoingState is null)
        {
            return;
        }

        var outgoingRate = runtime.OutgoingState.ResolvePlaybackRate(context.LocomotionSpeedMilliUnitsPerSecond);
        runtime.OutgoingPlayer.AdvanceFixedTick(outgoingRate);
        runtime.OutgoingPlayer.Events.Clear();
    }

    private static void AdvanceTransition(LayerRuntime runtime)
    {
        if (runtime.OutgoingPlayer is null)
        {
            return;
        }

        runtime.TransitionElapsedTicks++;
        if (runtime.TransitionElapsedTicks < runtime.TransitionTicks)
        {
            return;
        }

        runtime.OutgoingState = null;
        runtime.OutgoingPlayer = null;
        runtime.TransitionElapsedTicks = runtime.TransitionTicks;
    }

    private void TryCompleteState(LayerRuntime runtime)
    {
        if (!runtime.CurrentPlayer.IsComplete || runtime.CurrentState.CompletionStateId is null)
        {
            return;
        }

        runtime.Definition.TryGetState(runtime.CurrentState.CompletionStateId, out var completionState);
        ApplyState(
            runtime,
            completionState,
            new AnimationStateRequest(BypassActionLock: true));
    }

    private void DrainCurrentEvents(LayerRuntime runtime)
    {
        var pending = runtime.CurrentPlayer.Events.ConsumeAll();
        for (var index = 0; index < pending.Length; index++)
        {
            Events.Append(runtime.Definition.Id, runtime.CurrentState.Id, pending[index]);
        }
    }
}
