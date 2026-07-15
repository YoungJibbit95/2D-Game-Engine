namespace Game.Core.Animation;

public enum AnimationContentDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record AnimationContentDiagnostic(
    AnimationContentDiagnosticSeverity Severity,
    string Code,
    string Source,
    string Message);

public sealed class AnimationContentValidationException : Exception
{
    public AnimationContentValidationException(IEnumerable<AnimationContentDiagnostic> diagnostics)
        : base(CreateMessage(diagnostics, out var captured))
    {
        Diagnostics = captured;
    }

    public IReadOnlyList<AnimationContentDiagnostic> Diagnostics { get; }

    private static string CreateMessage(
        IEnumerable<AnimationContentDiagnostic> diagnostics,
        out AnimationContentDiagnostic[] captured)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        captured = diagnostics.ToArray();
        var errors = captured.Where(diagnostic =>
            diagnostic.Severity == AnimationContentDiagnosticSeverity.Error).ToArray();
        return errors.Length == 0
            ? "Animation content validation failed."
            : $"Animation content validation failed with {errors.Length} error(s): {errors[0].Message}";
    }
}

public sealed record AnimationContentLoadResult(
    AnimationContentRegistry Registry,
    IReadOnlyList<AnimationContentDiagnostic> Diagnostics);

public readonly record struct EntityAnimationProfileResolution(
    EntityAnimationProfile Profile,
    bool UsedFallback,
    string? DiagnosticCode);

public sealed class AnimationContentRegistry
{
    private readonly Dictionary<string, AnimationClip> _clips;
    private readonly Dictionary<string, CharacterRigProfile> _rigs;
    private readonly Dictionary<string, AnimationStateMachineProfile> _stateMachines;
    private readonly Dictionary<string, CharacterAnimationProfile> _characters;
    private readonly Dictionary<string, EntityAnimationProfile> _entities;
    private readonly Dictionary<string, EntityAnimationProfile> _entityBindings;
    private readonly Dictionary<AnimationEntityKind, EntityAnimationProfile> _fallbacks;
    private readonly AnimationClip[] _orderedClips;
    private readonly CharacterRigProfile[] _orderedRigs;
    private readonly AnimationStateMachineProfile[] _orderedStateMachines;
    private readonly CharacterAnimationProfile[] _orderedCharacters;
    private readonly EntityAnimationProfile[] _orderedEntities;

    internal AnimationContentRegistry(
        IEnumerable<AnimationClip> clips,
        IEnumerable<CharacterRigProfile> rigs,
        IEnumerable<AnimationStateMachineProfile> stateMachines,
        IEnumerable<CharacterAnimationProfile> characters,
        IEnumerable<EntityAnimationProfile> entities,
        IReadOnlyDictionary<AnimationEntityKind, EntityAnimationProfile> fallbacks)
    {
        _clips = CreateById(clips, clip => clip.Id, "fixed-tick clip");
        _rigs = CreateById(rigs, rig => rig.Id, "character rig");
        _stateMachines = CreateById(stateMachines, machine => machine.Id, "state machine");
        _characters = CreateById(characters, character => character.Id, "character animation profile");
        _entities = CreateById(entities, entity => entity.Id, "entity animation profile");
        _entityBindings = new Dictionary<string, EntityAnimationProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in _entities.Values.OrderBy(entity => entity.Id, StringComparer.Ordinal))
        {
            foreach (var binding in entity.Bindings)
            {
                var key = CreateEntityBindingKey(entity.Kind, binding);
                if (!_entityBindings.TryAdd(key, entity))
                {
                    throw new ArgumentException(
                        $"Duplicate entity animation binding '{binding}' for kind '{entity.Kind}'.",
                        nameof(entities));
                }
            }
        }

        _fallbacks = new Dictionary<AnimationEntityKind, EntityAnimationProfile>(fallbacks);
        _orderedClips = _clips.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        _orderedRigs = _rigs.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        _orderedStateMachines = _stateMachines.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        _orderedCharacters = _characters.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        _orderedEntities = _entities.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<AnimationClip> Clips => _orderedClips;

    public IReadOnlyList<CharacterRigProfile> Rigs => _orderedRigs;

    public IReadOnlyList<AnimationStateMachineProfile> StateMachines => _orderedStateMachines;

    public IReadOnlyList<CharacterAnimationProfile> Characters => _orderedCharacters;

    public IReadOnlyList<EntityAnimationProfile> Entities => _orderedEntities;

    public AnimationClip GetClip(string id)
    {
        return GetRequired(_clips, id, "Fixed-tick animation clip");
    }

    public CharacterRigProfile GetRig(string id)
    {
        return GetRequired(_rigs, id, "Character rig");
    }

    public AnimationStateMachineProfile GetStateMachine(string id)
    {
        return GetRequired(_stateMachines, id, "Animation state machine");
    }

    public CharacterAnimationProfile GetCharacter(string id)
    {
        return GetRequired(_characters, id, "Character animation profile");
    }

    public bool TryGetCharacter(string id, out CharacterAnimationProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _characters.TryGetValue(id, out profile!);
    }

    public EntityAnimationProfile GetEntity(string id)
    {
        return GetRequired(_entities, id, "Entity animation profile");
    }

    public EntityAnimationProfileResolution ResolveEntity(string binding, AnimationEntityKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binding);
        if (_entityBindings.TryGetValue(CreateEntityBindingKey(kind, binding), out var profile))
        {
            return new EntityAnimationProfileResolution(profile, false, null);
        }

        return new EntityAnimationProfileResolution(
            _fallbacks[kind],
            true,
            "entity-profile.fallback");
    }

    public EntityAnimationProfile GetFallback(AnimationEntityKind kind)
    {
        return _fallbacks[kind];
    }

    private static Dictionary<string, T> CreateById<T>(
        IEnumerable<T> values,
        Func<T, string> getId,
        string kind)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var id = getId(value);
            if (!result.TryAdd(id, value))
            {
                throw new ArgumentException($"Duplicate {kind} id '{id}'.", nameof(values));
            }
        }

        return result;
    }

    private static T GetRequired<T>(Dictionary<string, T> values, string id, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return values.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"{kind} '{id}' was not registered.");
    }

    private static string CreateEntityBindingKey(AnimationEntityKind kind, string binding)
    {
        return $"{(int)kind}:{binding}";
    }
}
