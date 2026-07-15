using Game.Core.Assets;
using Game.Core.Characters;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Animation;

public sealed class AnimationContentJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AnimationContentJsonLoader(int fixedTicksPerSecond = 60)
    {
        if (fixedTicksPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedTicksPerSecond));
        }

        FixedTicksPerSecond = fixedTicksPerSecond;
    }

    public int FixedTicksPerSecond { get; }

    public AnimationContentLoadResult LoadFromRoot(
        string contentRoot,
        SpriteAssetRegistry spriteAssets)
    {
        return LoadWithMods(contentRoot, modsRoot: null, spriteAssets);
    }

    public AnimationContentLoadResult LoadWithMods(
        string contentRoot,
        string? modsRoot,
        SpriteAssetRegistry spriteAssets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        ArgumentNullException.ThrowIfNull(spriteAssets);
        if (!Directory.Exists(contentRoot))
        {
            throw new DirectoryNotFoundException($"Content root does not exist: {contentRoot}");
        }

        var builder = new ContentBuilder(FixedTicksPerSecond);
        builder.MergePack(contentRoot, "base");
        if (!string.IsNullOrWhiteSpace(modsRoot) && Directory.Exists(modsRoot))
        {
            foreach (var modDirectory in Directory
                         .EnumerateDirectories(modsRoot)
                         .Order(StringComparer.OrdinalIgnoreCase))
            {
                builder.MergePack(modDirectory, ResolvePackId(modDirectory));
            }
        }

        return builder.Build(spriteAssets);
    }

    public AnimationContentLoadResult LoadFromJson(
        string json,
        SpriteAssetRegistry spriteAssets,
        string source = "inline json")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(spriteAssets);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var builder = new ContentBuilder(FixedTicksPerSecond);
        builder.MergeDocument(ParseDocument(json, source), source, "inline");
        return builder.Build(spriteAssets);
    }

    private static AnimationContentDocumentDto ParseDocument(string json, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<AnimationContentDocumentDto>(json, Options)
                ?? throw new JsonException($"Animation content document was empty: {source}");
        }
        catch (JsonException exception)
        {
            throw new AnimationContentValidationException(new[]
            {
                new AnimationContentDiagnostic(
                    AnimationContentDiagnosticSeverity.Error,
                    "json.invalid",
                    source,
                    exception.Message)
            });
        }
    }

    private static string ResolvePackId(string modDirectory)
    {
        var fallback = Path.GetFileName(modDirectory);
        var manifestPath = Path.Combine(modDirectory, "mod.json");
        if (!File.Exists(manifestPath))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return document.RootElement.TryGetProperty("id", out var id) &&
                   !string.IsNullOrWhiteSpace(id.GetString())
                ? id.GetString()!
                : fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private sealed class ContentBuilder
    {
        private readonly int _fixedTicksPerSecond;
        private readonly Dictionary<string, Packed<FixedTickClipDto>> _clips = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<CharacterRigDto>> _rigs = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<StateMachineDto>> _stateMachines = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<CharacterProfileDto>> _characters = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Packed<EntityProfileDto>> _entities = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<AnimationEntityKind, Packed<FallbackDto>> _fallbacks = new();
        private readonly List<AnimationContentDiagnostic> _diagnostics = new();

        public ContentBuilder(int fixedTicksPerSecond)
        {
            _fixedTicksPerSecond = fixedTicksPerSecond;
        }

        public void MergePack(string root, string packId)
        {
            var files = EnumerateDefinitionFiles(root);
            for (var index = 0; index < files.Length; index++)
            {
                var file = files[index];
                AnimationContentDocumentDto document;
                try
                {
                    document = ParseDocument(File.ReadAllText(file), file);
                }
                catch (AnimationContentValidationException exception)
                {
                    _diagnostics.AddRange(exception.Diagnostics);
                    continue;
                }

                MergeDocument(document, file, packId);
            }
        }

        public void MergeDocument(AnimationContentDocumentDto document, string source, string packId)
        {
            var content = document.RuntimeAnimationContent;
            if (content is null)
            {
                return;
            }

            var documentAnimations = document.Animations
                .Where(animation => !string.IsNullOrWhiteSpace(animation.Id))
                .ToDictionary(animation => animation.Id!, StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < content.ClipIds.Count; index++)
            {
                var clipId = content.ClipIds[index];
                if (!documentAnimations.TryGetValue(clipId, out var clip))
                {
                    AddError(
                        "clip.import-missing",
                        source,
                        $"Runtime animation content imports missing top-level animation '{clipId}'.");
                    continue;
                }

                MergeById(_clips, clip, clip.Id, source, packId, "fixed-tick clip");
            }

            for (var index = 0; index < content.Clips.Count; index++)
            {
                var clip = content.Clips[index];
                MergeById(_clips, clip, clip.Id, source, packId, "fixed-tick clip");
            }

            for (var index = 0; index < content.Rigs.Count; index++)
            {
                var rig = content.Rigs[index];
                MergeById(_rigs, rig, rig.Id, source, packId, "character rig");
            }

            for (var index = 0; index < content.StateMachines.Count; index++)
            {
                var machine = content.StateMachines[index];
                MergeById(_stateMachines, machine, machine.Id, source, packId, "state machine");
            }

            for (var index = 0; index < content.CharacterProfiles.Count; index++)
            {
                var character = content.CharacterProfiles[index];
                MergeById(_characters, character, character.Id, source, packId, "character profile");
            }

            for (var index = 0; index < content.EntityProfiles.Count; index++)
            {
                var entity = content.EntityProfiles[index];
                MergeById(_entities, entity, entity.Id, source, packId, "entity profile");
            }

            for (var index = 0; index < content.Fallbacks.Count; index++)
            {
                var fallback = content.Fallbacks[index];
                if (_fallbacks.TryGetValue(fallback.Kind, out var previous))
                {
                    _diagnostics.Add(new AnimationContentDiagnostic(
                        AnimationContentDiagnosticSeverity.Info,
                        "content.override",
                        source,
                        $"Pack '{packId}' overrides entity fallback '{fallback.Kind}' from pack '{previous.PackId}'."));
                }

                _fallbacks[fallback.Kind] = new Packed<FallbackDto>(fallback, source, packId);
            }
        }

        public AnimationContentLoadResult Build(SpriteAssetRegistry spriteAssets)
        {
            var clips = BuildDefinitions(
                _clips,
                packed => packed.Definition.ToClip(spriteAssets, _fixedTicksPerSecond));
            var rigs = BuildDefinitions(_rigs, packed => packed.Definition.ToRig());
            var stateMachines = BuildDefinitions(
                _stateMachines,
                packed => packed.Definition.ToProfile(clips));
            var entities = BuildDefinitions(
                _entities,
                packed => packed.Definition.ToProfile(spriteAssets));
            var characters = BuildDefinitions(
                _characters,
                packed => packed.Definition.ToProfile(rigs, stateMachines, spriteAssets));

            var resolvedFallbacks = BuildFallbacks(entities);
            if (_diagnostics.Any(diagnostic =>
                    diagnostic.Severity == AnimationContentDiagnosticSeverity.Error))
            {
                throw new AnimationContentValidationException(_diagnostics);
            }

            try
            {
                var registry = new AnimationContentRegistry(
                    clips.Values,
                    rigs.Values,
                    stateMachines.Values,
                    characters.Values,
                    entities.Values,
                    resolvedFallbacks);
                return new AnimationContentLoadResult(registry, _diagnostics.ToArray());
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                AddError("registry.invalid", "merged content", exception.Message);
                throw new AnimationContentValidationException(_diagnostics);
            }
        }

        private Dictionary<string, TOutput> BuildDefinitions<TDto, TOutput>(
            Dictionary<string, Packed<TDto>> definitions,
            Func<Packed<TDto>, TOutput> build)
        {
            var result = new Dictionary<string, TOutput>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in definitions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                try
                {
                    result.Add(pair.Key, build(pair.Value));
                }
                catch (Exception exception) when (exception is
                    ArgumentException or
                    InvalidOperationException or
                    KeyNotFoundException or
                    OverflowException or
                    FormatException)
                {
                    AddError("definition.invalid", pair.Value.Source, exception.Message);
                }
            }

            return result;
        }

        private Dictionary<AnimationEntityKind, EntityAnimationProfile> BuildFallbacks(
            IReadOnlyDictionary<string, EntityAnimationProfile> entities)
        {
            var result = new Dictionary<AnimationEntityKind, EntityAnimationProfile>();
            foreach (var kind in Enum.GetValues<AnimationEntityKind>())
            {
                if (_fallbacks.TryGetValue(kind, out var packed))
                {
                    if (!entities.TryGetValue(packed.Definition.ProfileId ?? string.Empty, out var profile))
                    {
                        AddError(
                            "fallback.profile-missing",
                            packed.Source,
                            $"Fallback '{kind}' references missing entity profile '{packed.Definition.ProfileId}'.");
                    }
                    else if (profile.Kind != kind)
                    {
                        AddError(
                            "fallback.kind-mismatch",
                            packed.Source,
                            $"Fallback '{kind}' references profile '{profile.Id}' of kind '{profile.Kind}'.");
                    }
                    else
                    {
                        result.Add(kind, profile);
                        continue;
                    }
                }

                var generated = CreateGeneratedFallback(kind);
                result[kind] = generated;
                _diagnostics.Add(new AnimationContentDiagnostic(
                    AnimationContentDiagnosticSeverity.Warning,
                    "fallback.generated",
                    "merged content",
                    $"Generated renderer-neutral fallback profile for entity kind '{kind}'."));
            }

            return result;
        }

        private static EntityAnimationProfile CreateGeneratedFallback(AnimationEntityKind kind)
        {
            return new EntityAnimationProfile(
                $"__fallback.{kind.ToString().ToLowerInvariant()}",
                kind,
                "missing_sprite",
                bindings: null,
                new[]
                {
                    new KeyValuePair<AnimationEntityVisualState, AnimationEntityFrameRange>(
                        AnimationEntityVisualState.Idle,
                        new AnimationEntityFrameRange(0, 1, 1))
                },
                motionStyle: AnimationEntityMotionStyle.None,
                castsShadow: false);
        }

        private void MergeById<T>(
            Dictionary<string, Packed<T>> target,
            T definition,
            string? id,
            string source,
            string packId,
            string kind)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                AddError("definition.id-required", source, $"A {kind} definition is missing its id.");
                return;
            }

            if (target.TryGetValue(id, out var previous))
            {
                _diagnostics.Add(new AnimationContentDiagnostic(
                    AnimationContentDiagnosticSeverity.Info,
                    "content.override",
                    source,
                    $"Pack '{packId}' overrides {kind} '{id}' from pack '{previous.PackId}'."));
            }

            target[id] = new Packed<T>(definition, source, packId);
        }

        private void AddError(string code, string source, string message)
        {
            _diagnostics.Add(new AnimationContentDiagnostic(
                AnimationContentDiagnosticSeverity.Error,
                code,
                source,
                message));
        }

        private static string[] EnumerateDefinitionFiles(string root)
        {
            return new[] { "animations", "characters" }
                .Select(directory => Path.Combine(root, directory))
                .Where(Directory.Exists)
                .SelectMany(directory => Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    private sealed record AnimationContentDocumentDto
    {
        public List<FixedTickClipDto> Animations { get; init; } = new();

        public RuntimeAnimationContentDto? RuntimeAnimationContent { get; init; }
    }

    private sealed record RuntimeAnimationContentDto
    {
        public List<string> ClipIds { get; init; } = new();

        public List<FixedTickClipDto> Clips { get; init; } = new();

        public List<CharacterRigDto> Rigs { get; init; } = new();

        public List<StateMachineDto> StateMachines { get; init; } = new();

        public List<CharacterProfileDto> CharacterProfiles { get; init; } = new();

        public List<EntityProfileDto> EntityProfiles { get; init; } = new();

        public List<FallbackDto> Fallbacks { get; init; } = new();
    }

    private sealed record FixedTickClipDto
    {
        public string? Id { get; init; }

        public AnimationLoopMode LoopMode { get; init; } = AnimationLoopMode.Loop;

        public string? SpriteId { get; init; }

        [JsonPropertyName("sprite")]
        public string? Sprite { get; init; }

        public string TrackId { get; init; } = "body";

        public string TargetLayerId { get; init; } = "body";

        public int FrameStart { get; init; }

        public int FrameCount { get; init; }

        public int DurationTicks { get; init; }

        public float FrameDurationSeconds { get; init; }

        [JsonPropertyName("frameDuration")]
        public float? FrameDuration { get; init; }

        public List<FixedTickTrackDto> Tracks { get; init; } = new();

        public List<FixedTickFrameDto> Frames { get; init; } = new();

        public List<EventDto> Events { get; init; } = new();

        public AnimationClip ToClip(SpriteAssetRegistry spriteAssets, int fixedTicksPerSecond)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Id);
            var tracks = Tracks.Count > 0
                ? Tracks.Select(track => track.ToTrack(spriteAssets, fixedTicksPerSecond)).ToArray()
                : new[] { ToShorthandTrack(spriteAssets, fixedTicksPerSecond) };
            return new AnimationClip(
                Id,
                LoopMode,
                tracks,
                Events.Select(animationEvent => animationEvent.ToMarker()));
        }

        private AnimationTrack ToShorthandTrack(
            SpriteAssetRegistry spriteAssets,
            int fixedTicksPerSecond)
        {
            var spriteId = SpriteId ?? Sprite;
            ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
            var frames = Frames.Count > 0
                ? Frames.Select(frame => frame.ToFrame(spriteId, spriteAssets, fixedTicksPerSecond)).ToArray()
                : CreateRangeFrames(spriteId, spriteAssets, fixedTicksPerSecond);
            return new AnimationTrack(TrackId, TargetLayerId, spriteId, frames);
        }

        private AnimationFrame[] CreateRangeFrames(
            string spriteId,
            SpriteAssetRegistry spriteAssets,
            int fixedTicksPerSecond)
        {
            if (FrameCount <= 0)
            {
                throw new ArgumentException($"Fixed-tick clip '{Id}' must contain frames.");
            }

            var durationTicks = ResolveDurationTicks(
                DurationTicks,
                durationMs: null,
                FrameDuration ?? FrameDurationSeconds,
                fixedTicksPerSecond,
                $"clip '{Id}'");
            var frames = new AnimationFrame[FrameCount];
            for (var index = 0; index < frames.Length; index++)
            {
                var frameIndex = checked(FrameStart + index);
                ValidateFrameReference(spriteAssets, spriteId, frameIndex, null, null, $"clip '{Id}'");
                frames[index] = new AnimationFrame(frameIndex, durationTicks);
            }

            return frames;
        }
    }

    private sealed record FixedTickTrackDto
    {
        public string? Id { get; init; }

        public string? TargetLayerId { get; init; }

        public string? SpriteId { get; init; }

        public List<FixedTickFrameDto> Frames { get; init; } = new();

        public AnimationTrack ToTrack(SpriteAssetRegistry spriteAssets, int fixedTicksPerSecond)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(TargetLayerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(SpriteId);
            return new AnimationTrack(
                Id,
                TargetLayerId,
                SpriteId,
                Frames.Select(frame => frame.ToFrame(SpriteId, spriteAssets, fixedTicksPerSecond)));
        }
    }

    private sealed record FixedTickFrameDto
    {
        public int FrameIndex { get; init; }

        [JsonPropertyName("frame")]
        public int? Frame { get; init; }

        public string? FrameId { get; init; }

        public SourceRectangleDto? SourceRectangle { get; init; }

        public int DurationTicks { get; init; }

        public int? DurationMs { get; init; }

        public float DurationSeconds { get; init; }

        public string? SpriteIdOverride { get; init; }

        public int OffsetX { get; init; }

        public int OffsetY { get; init; }

        public float RotationRadians { get; init; }

        public float ScaleX { get; init; } = 1f;

        public float ScaleY { get; init; } = 1f;

        public string? Tint { get; init; }

        public bool Visible { get; init; } = true;

        public List<SocketDto> Sockets { get; init; } = new();

        public AnimationFrame ToFrame(
            string defaultSpriteId,
            SpriteAssetRegistry spriteAssets,
            int fixedTicksPerSecond)
        {
            var frameIndex = Frame ?? FrameIndex;
            var spriteId = SpriteIdOverride ?? defaultSpriteId;
            ValidateFrameReference(
                spriteAssets,
                spriteId,
                frameIndex,
                FrameId,
                SourceRectangle,
                $"sprite '{spriteId}'");
            return new AnimationFrame(
                frameIndex,
                ResolveDurationTicks(
                    DurationTicks,
                    DurationMs,
                    DurationSeconds,
                    fixedTicksPerSecond,
                    $"sprite '{spriteId}' frame {frameIndex}"),
                new AnimationTransform2D(
                    new Vector2(OffsetX, OffsetY),
                    RotationRadians,
                    new Vector2(ScaleX, ScaleY)),
                ParseColor(Tint),
                Visible,
                Sockets.Select(socket => socket.ToPose()),
                SpriteIdOverride);
        }
    }

    private sealed record EventDto
    {
        public string? Id { get; init; }

        public int Tick { get; init; }

        public string? Payload { get; init; }

        public AnimationEventMarker ToMarker()
        {
            return new AnimationEventMarker(Id ?? string.Empty, Tick, Payload);
        }
    }

    private sealed record SocketDto
    {
        public string? Id { get; init; }

        public float X { get; init; }

        public float Y { get; init; }

        public float RotationRadians { get; init; }

        public AttachmentSocketPose ToPose()
        {
            return new AttachmentSocketPose(Id ?? string.Empty, new Vector2(X, Y), RotationRadians);
        }
    }

    private sealed record SourceRectangleDto
    {
        public int X { get; init; }

        public int Y { get; init; }

        public int Width { get; init; }

        public int Height { get; init; }
    }

    private sealed record CharacterRigDto
    {
        public string? Id { get; init; }

        public List<CharacterRigLayerDto> Layers { get; init; } = new();

        public CharacterRigProfile ToRig()
        {
            return new CharacterRigProfile(
                Id ?? string.Empty,
                Layers.Select(layer => layer.ToLayer()));
        }
    }

    private sealed record CharacterRigLayerDto
    {
        public string? Id { get; init; }

        public CharacterAppearanceSlot AppearanceSlot { get; init; }

        public string? AnimationTrackId { get; init; }

        public int DrawOrder { get; init; }

        public string? ParentLayerId { get; init; }

        public string? ParentSocketId { get; init; }

        public TransformDto? LocalTransform { get; init; }

        public PointDto? Pivot { get; init; }

        public string? Tint { get; init; }

        public bool Visible { get; init; } = true;

        public bool UseTrackSprite { get; init; }

        public CharacterRigLayerDefinition ToLayer()
        {
            return new CharacterRigLayerDefinition(
                Id ?? string.Empty,
                AppearanceSlot,
                AnimationTrackId ?? string.Empty,
                DrawOrder,
                ParentLayerId,
                ParentSocketId,
                LocalTransform?.ToTransform(),
                Pivot?.ToVector(),
                ParseColor(Tint),
                Visible,
                UseTrackSprite);
        }
    }

    private sealed record TransformDto
    {
        public float X { get; init; }

        public float Y { get; init; }

        public float RotationRadians { get; init; }

        public float ScaleX { get; init; } = 1f;

        public float ScaleY { get; init; } = 1f;

        public AnimationTransform2D ToTransform()
        {
            return new AnimationTransform2D(
                new Vector2(X, Y),
                RotationRadians,
                new Vector2(ScaleX, ScaleY));
        }
    }

    private sealed record PointDto
    {
        public float X { get; init; }

        public float Y { get; init; }

        public Vector2 ToVector()
        {
            return new Vector2(X, Y);
        }
    }

    private sealed record StateMachineDto
    {
        public string? Id { get; init; }

        public List<StateLayerDto> Layers { get; init; } = new();

        public AnimationStateMachineProfile ToProfile(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            return new AnimationStateMachineProfile(
                Id ?? string.Empty,
                new LayeredAnimationStateMachineDefinition(
                    Layers.Select(layer => layer.ToLayer(clips))));
        }
    }

    private sealed record StateLayerDto
    {
        public string? Id { get; init; }

        public int Priority { get; init; }

        public string? InitialStateId { get; init; }

        public List<StateDto> States { get; init; } = new();

        public List<TransitionDto> Transitions { get; init; } = new();

        public string? Tint { get; init; }

        public bool Visible { get; init; } = true;

        public float Opacity { get; init; } = 1f;

        public AnimationStateLayerDefinition ToLayer(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            return new AnimationStateLayerDefinition(
                Id ?? string.Empty,
                Priority,
                InitialStateId ?? string.Empty,
                States.OrderBy(state => state.Id, StringComparer.Ordinal)
                    .Select(state => state.ToState(clips)),
                Transitions.OrderBy(transition => transition.FromStateId, StringComparer.Ordinal)
                    .ThenBy(transition => transition.ToStateId, StringComparer.Ordinal)
                    .Select(transition => transition.ToTransition()),
                ParseColor(Tint),
                Visible,
                Opacity);
        }
    }

    private sealed record StateDto
    {
        public string? Id { get; init; }

        public string? ClipId { get; init; }

        public int PlaybackRateNumerator { get; init; } = 1;

        public int PlaybackRateDenominator { get; init; } = 1;

        public bool ScaleWithLocomotion { get; init; }

        public int LocomotionReferenceSpeedMilliUnitsPerSecond { get; init; } = 1000;

        public int MinimumLocomotionRatePercentage { get; init; } = 50;

        public int MaximumLocomotionRatePercentage { get; init; } = 200;

        public AnimationActionLockMode ActionLockMode { get; init; }

        public int ActionLockTicks { get; init; }

        public string? CompletionStateId { get; init; }

        public AnimationStateDefinition ToState(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            if (!clips.TryGetValue(ClipId ?? string.Empty, out var clip))
            {
                throw new KeyNotFoundException(
                    $"Animation state '{Id}' references missing fixed-tick clip '{ClipId}'.");
            }

            return new AnimationStateDefinition(
                Id ?? string.Empty,
                clip,
                new AnimationPlaybackRate(PlaybackRateNumerator, PlaybackRateDenominator),
                ScaleWithLocomotion,
                LocomotionReferenceSpeedMilliUnitsPerSecond,
                MinimumLocomotionRatePercentage,
                MaximumLocomotionRatePercentage,
                ActionLockMode,
                ActionLockTicks,
                CompletionStateId);
        }
    }

    private sealed record TransitionDto
    {
        public string? FromStateId { get; init; }

        public string? ToStateId { get; init; }

        public int BlendTicks { get; init; }

        public AnimationTransitionDefinition ToTransition()
        {
            return new AnimationTransitionDefinition(
                FromStateId ?? string.Empty,
                ToStateId ?? string.Empty,
                BlendTicks);
        }
    }

    private sealed record CharacterProfileDto
    {
        public string? Id { get; init; }

        public string? RigId { get; init; }

        public string? StateMachineId { get; init; }

        public List<SpriteBindingDto> SpriteBindings { get; init; } = new();

        public List<ActionBindingDto> Actions { get; init; } = new();

        public CharacterAnimationProfile ToProfile(
            IReadOnlyDictionary<string, CharacterRigProfile> rigs,
            IReadOnlyDictionary<string, AnimationStateMachineProfile> stateMachines,
            SpriteAssetRegistry spriteAssets)
        {
            if (!rigs.TryGetValue(RigId ?? string.Empty, out var rig))
            {
                throw new KeyNotFoundException($"Character profile '{Id}' references missing rig '{RigId}'.");
            }

            if (!stateMachines.TryGetValue(StateMachineId ?? string.Empty, out var stateMachine))
            {
                throw new KeyNotFoundException(
                    $"Character profile '{Id}' references missing state machine '{StateMachineId}'.");
            }

            var bindings = new CharacterRigSpriteBindings(
                SpriteBindings.Select(binding => binding.ToBinding()));
            var profile = new CharacterAnimationProfile(
                Id ?? string.Empty,
                rig,
                stateMachine,
                bindings,
                Actions.Select(action => action.ToBinding()));
            ValidateCharacterProfile(profile, spriteAssets);
            return profile;
        }
    }

    private sealed record SpriteBindingDto
    {
        public CharacterAppearanceSlot Slot { get; init; }

        public string? SpriteId { get; init; }

        public bool VisibleByDefault { get; init; } = true;

        public CharacterRigSpriteBinding ToBinding()
        {
            return new CharacterRigSpriteBinding(Slot, SpriteId ?? string.Empty, VisibleByDefault);
        }
    }

    private sealed record ActionBindingDto
    {
        public CharacterAnimationState Action { get; init; }

        public string? LayerId { get; init; }

        public string? StateId { get; init; }

        public bool BypassActionLock { get; init; }

        public bool RestartIfActive { get; init; } = true;

        public int? BlendTicksOverride { get; init; }

        public CharacterAnimationActionBinding ToBinding()
        {
            return new CharacterAnimationActionBinding(
                Action,
                LayerId ?? string.Empty,
                StateId ?? string.Empty,
                BypassActionLock,
                RestartIfActive,
                BlendTicksOverride);
        }
    }

    private sealed record EntityProfileDto
    {
        public string? Id { get; init; }

        public AnimationEntityKind Kind { get; init; } = AnimationEntityKind.Enemy;

        public string? SpriteId { get; init; }

        public string? EliteSpriteId { get; init; }

        public List<string> Bindings { get; init; } = new();

        public AnimationEntityMotionStyle MotionStyle { get; init; } =
            AnimationEntityMotionStyle.Bob | AnimationEntityMotionStyle.SquashStretch;

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

        public List<EntityAnimationDto> Animations { get; init; } = new();

        public EntityAnimationProfile ToProfile(SpriteAssetRegistry spriteAssets)
        {
            var profile = new EntityAnimationProfile(
                Id ?? string.Empty,
                Kind,
                SpriteId ?? string.Empty,
                Bindings,
                Animations.Select(animation => animation.ToPair()),
                EliteSpriteId,
                MotionStyle,
                IsFlying,
                IsElite,
                CastsShadow,
                DisplayScale,
                WalkSpeedThreshold,
                RunSpeedThreshold,
                AirborneSpeedThreshold,
                BobAmplitude,
                ShadowWidthFactor,
                ShadowOpacity,
                HurtLockTicks,
                AttackLockTicks);
            ValidateEntityProfile(profile, spriteAssets);
            return profile;
        }
    }

    private sealed record EntityAnimationDto
    {
        public AnimationEntityVisualState State { get; init; }

        public int StartFrame { get; init; }

        public int FrameCount { get; init; } = 1;

        public int TicksPerFrame { get; init; } = 1;

        public AnimationEntityPlayback Playback { get; init; }

        public KeyValuePair<AnimationEntityVisualState, AnimationEntityFrameRange> ToPair()
        {
            return new KeyValuePair<AnimationEntityVisualState, AnimationEntityFrameRange>(
                State,
                new AnimationEntityFrameRange(StartFrame, FrameCount, TicksPerFrame, Playback));
        }
    }

    private sealed record FallbackDto
    {
        public AnimationEntityKind Kind { get; init; }

        public string? ProfileId { get; init; }
    }

    private static int ResolveDurationTicks(
        int durationTicks,
        int? durationMs,
        float durationSeconds,
        int fixedTicksPerSecond,
        string context)
    {
        if (durationTicks > 0)
        {
            return durationTicks;
        }

        double seconds;
        if (durationMs is > 0)
        {
            seconds = durationMs.Value / 1000d;
        }
        else if (float.IsFinite(durationSeconds) && durationSeconds > 0f)
        {
            seconds = durationSeconds;
        }
        else
        {
            throw new ArgumentException($"Animation {context} requires a positive fixed-tick duration.");
        }

        var exactTicks = seconds * fixedTicksPerSecond;
        if (!double.IsFinite(exactTicks) || exactTicks > int.MaxValue)
        {
            throw new ArgumentException($"Animation {context} duration is outside the supported fixed-tick range.");
        }

        return Math.Max(1, (int)Math.Round(exactTicks, MidpointRounding.AwayFromZero));
    }

    private static void ValidateFrameReference(
        SpriteAssetRegistry spriteAssets,
        string spriteId,
        int frameIndex,
        string? expectedFrameId,
        SourceRectangleDto? expectedRectangle,
        string context)
    {
        if (!spriteAssets.TryGetById(spriteId, out var asset))
        {
            throw new ArgumentException($"Animation {context} references missing sprite asset '{spriteId}'.");
        }

        var frame = ResolveAssetFrame(asset, frameIndex, context);
        if (expectedFrameId is not null &&
            !string.Equals(expectedFrameId, frame.Id, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Animation {context} expects frame id '{expectedFrameId}' at index {frameIndex}, but asset '{spriteId}' declares '{frame.Id}'.");
        }

        if (expectedRectangle is not null &&
            (expectedRectangle.X != frame.X ||
             expectedRectangle.Y != frame.Y ||
             expectedRectangle.Width != frame.Width ||
             expectedRectangle.Height != frame.Height))
        {
            throw new ArgumentException(
                $"Animation {context} source rectangle does not match sprite '{spriteId}' frame {frameIndex}.");
        }
    }

    private static AssetFrame ResolveAssetFrame(
        SpriteAssetDefinition asset,
        int frameIndex,
        string context)
    {
        if (frameIndex < 0)
        {
            throw new ArgumentException($"Animation {context} has a negative frame index.");
        }

        if (asset.Frames.Count == 0)
        {
            if (frameIndex != 0)
            {
                throw new ArgumentException(
                    $"Animation {context} references frame {frameIndex}, but sprite '{asset.Id}' has one implicit frame.");
            }

            return new AssetFrame(null, 0, 0, asset.Width, asset.Height);
        }

        if (frameIndex >= asset.Frames.Count)
        {
            throw new ArgumentException(
                $"Animation {context} references frame {frameIndex}, but sprite '{asset.Id}' has {asset.Frames.Count} frames.");
        }

        var frame = asset.Frames[frameIndex];
        return new AssetFrame(frame.Id, frame.X, frame.Y, frame.Width, frame.Height);
    }

    private static void ValidateCharacterProfile(
        CharacterAnimationProfile profile,
        SpriteAssetRegistry spriteAssets)
    {
        foreach (var binding in profile.SpriteBindings.Bindings)
        {
            if (!spriteAssets.TryGetById(binding.SpriteId, out _))
            {
                throw new ArgumentException(
                    $"Character profile '{profile.Id}' references missing sprite asset '{binding.SpriteId}'.");
            }
        }

        foreach (var rigLayer in profile.Rig.Layers)
        {
            if (rigLayer.UseTrackSprite)
            {
                continue;
            }

            if (!profile.SpriteBindings.TryGetBinding(rigLayer.AppearanceSlot, out var binding))
            {
                throw new ArgumentException(
                    $"Character profile '{profile.Id}' has no sprite binding for rig layer '{rigLayer.Id}' slot '{rigLayer.AppearanceSlot}'.");
            }

            foreach (var stateLayer in profile.StateMachine.Definition.Layers)
            {
                foreach (var state in stateLayer.States.OrderBy(state => state.Id, StringComparer.Ordinal))
                {
                    var track = state.Clip.Tracks.FirstOrDefault(track =>
                        string.Equals(track.Id, rigLayer.AnimationTrackId, StringComparison.OrdinalIgnoreCase));
                    if (track is null)
                    {
                        throw new ArgumentException(
                            $"Character profile '{profile.Id}' state '{stateLayer.Id}/{state.Id}' has no track '{rigLayer.AnimationTrackId}' for rig layer '{rigLayer.Id}'.");
                    }

                    for (var frameIndex = 0; frameIndex < track.Frames.Count; frameIndex++)
                    {
                        var animationFrame = track.Frames[frameIndex];
                        var trackSprite = animationFrame.SpriteIdOverride ?? track.SpriteId;
                        if (!spriteAssets.TryGetById(trackSprite, out var trackAsset) ||
                            !spriteAssets.TryGetById(binding.SpriteId, out var bindingAsset))
                        {
                            throw new ArgumentException(
                                $"Character profile '{profile.Id}' contains an unresolved layer sprite.");
                        }

                        var expected = ResolveAssetFrame(
                            trackAsset,
                            animationFrame.SpriteFrameIndex,
                            $"character profile '{profile.Id}' state '{state.Id}'");
                        var actual = ResolveAssetFrame(
                            bindingAsset,
                            animationFrame.SpriteFrameIndex,
                            $"character profile '{profile.Id}' rig layer '{rigLayer.Id}'");
                        if (!expected.HasSameRegistration(actual))
                        {
                            throw new ArgumentException(
                                $"Character profile '{profile.Id}' rig layer '{rigLayer.Id}' frame {animationFrame.SpriteFrameIndex} does not match track source rectangle registration.");
                        }
                    }
                }
            }
        }
    }

    private static void ValidateEntityProfile(
        EntityAnimationProfile profile,
        SpriteAssetRegistry spriteAssets)
    {
        if (!spriteAssets.TryGetById(profile.SpriteId, out var sprite))
        {
            throw new ArgumentException(
                $"Entity animation profile '{profile.Id}' references missing sprite asset '{profile.SpriteId}'.");
        }

        SpriteAssetDefinition? eliteSprite = null;
        if (profile.EliteSpriteId is not null &&
            !spriteAssets.TryGetById(profile.EliteSpriteId, out eliteSprite))
        {
            throw new ArgumentException(
                $"Entity animation profile '{profile.Id}' references missing elite sprite asset '{profile.EliteSpriteId}'.");
        }

        foreach (var animation in profile.Animations)
        {
            ValidateFrameRange(profile, sprite, animation.Key, animation.Value);
            if (eliteSprite is not null)
            {
                ValidateFrameRange(profile, eliteSprite, animation.Key, animation.Value);
            }
        }
    }

    private static void ValidateFrameRange(
        EntityAnimationProfile profile,
        SpriteAssetDefinition sprite,
        AnimationEntityVisualState state,
        AnimationEntityFrameRange range)
    {
        var frameCount = sprite.Frames.Count == 0 ? 1 : sprite.Frames.Count;
        if (range.EndFrameExclusive > frameCount)
        {
            throw new ArgumentException(
                $"Entity animation profile '{profile.Id}' state '{state}' uses frames [{range.StartFrame}, {range.EndFrameExclusive}) but sprite '{sprite.Id}' has {frameCount} frame(s).");
        }

        for (var frameIndex = range.StartFrame; frameIndex < range.EndFrameExclusive; frameIndex++)
        {
            _ = ResolveAssetFrame(sprite, frameIndex, $"entity profile '{profile.Id}' state '{state}'");
        }
    }

    private static AnimationColor? ParseColor(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("Animation color cannot be empty.");
        }

        var normalized = value[0] == '#' ? value[1..] : value;
        if (normalized.Length is not (6 or 8) ||
            !uint.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            throw new FormatException($"Animation color '{value}' must be #RRGGBB or #RRGGBBAA.");
        }

        return normalized.Length == 6
            ? new AnimationColor((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed)
            : new AnimationColor(
                (byte)(packed >> 24),
                (byte)(packed >> 16),
                (byte)(packed >> 8),
                (byte)packed);
    }

    private readonly record struct Packed<T>(T Definition, string Source, string PackId);

    private readonly record struct AssetFrame(string? Id, int X, int Y, int Width, int Height)
    {
        public bool HasSameRegistration(AssetFrame other)
        {
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                X == other.X &&
                Y == other.Y &&
                Width == other.Width &&
                Height == other.Height;
        }
    }
}
