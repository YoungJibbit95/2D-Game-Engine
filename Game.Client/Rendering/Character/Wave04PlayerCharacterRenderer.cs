using Game.Core.Animation;
using Game.Core.Characters;
using Game.Core.Combat;
using Game.Core.Equipment;
using Game.Core.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CoreVector2 = System.Numerics.Vector2;

namespace Game.Client.Rendering.Character;

public sealed class Wave04PlayerCharacterRenderer
{
    private const string BodySprite = "entities/player/character_v1_wave04/body";
    private const string HairSprite = "entities/player/character_v1_wave04/hair";
    private const string ClothesSprite = "entities/player/character_v1_wave04/clothes";
    private const string ArmorSprite = "entities/player/character_v1_wave04/armor";
    private const string EquipmentSprite = "entities/player/character_v1_wave04/equipment";

    private readonly PlayerAnimationSnapshotAdapter _snapshotAdapter = new();
    private readonly CharacterSpriteLayerPoseBuilder _poseBuilder = new();
    private readonly CharacterDrawCommandBuilder _commandBuilder = new();
    private readonly List<CharacterDrawCommand> _commands = new(10);
    private LayeredAnimationStateMachine _machine = CreateStateMachine();
    private CharacterRigProfile _rig = CreateRig();
    private CharacterAnimationProfile? _contentProfile;
    private CharacterAppearance? _sourceAppearance;
    private CharacterAppearanceProfile? _appearance;
    private bool _armorVisible;

    public long FixedTick => _machine.FixedTick;

    public int DrawCommandCount => _commands.Count;

    public string CurrentStateId => _machine.TryGetCurrentState("base", out var stateId)
        ? stateId
        : string.Empty;

    public void Configure(CharacterAnimationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.Equals(_contentProfile?.Id, profile.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _contentProfile = profile;
        _machine = profile.CreateStateMachine();
        _rig = profile.Rig;
        _sourceAppearance = null;
        _appearance = null;
    }

    public void RequestAction(CharacterAnimationState action)
    {
        if (_contentProfile is not null)
        {
            _contentProfile.RequestAction(_machine, action);
            return;
        }

        var state = action switch
        {
            CharacterAnimationState.Attack or
            CharacterAnimationState.Mine or
            CharacterAnimationState.UseItem => "tool",
            CharacterAnimationState.Hurt => "hurt",
            _ => null
        };
        if (state is null)
        {
            return;
        }

        _machine.RequestState(
            "base",
            state,
            new AnimationStateRequest(
                BypassActionLock: state == "hurt",
                RestartIfActive: true,
                BlendTicksOverride: 1));
    }

    public void Advance(
        PlayerFrameSnapshot snapshot,
        GuardRuntimeState? guard,
        CharacterAppearance appearance,
        EquipmentLoadout equipment)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(equipment);
        EnsureAppearance(appearance, HasVisibleArmor(equipment));

        _snapshotAdapter.SynchronizeAndAdvance(_machine, "base", snapshot, guard);
        var root = new Vector2(
            snapshot.Bounds.X + snapshot.Bounds.Width * 0.5f,
            snapshot.Bounds.Bottom);
        var pose = _poseBuilder.Build(
            _rig,
            _appearance!,
            _machine.Sample(),
            root);
        _commandBuilder.BuildInto(pose, _commands, baseLayerDepth: 0.42f);
    }

    public bool Draw(
        RenderContext context,
        ClientTextureRegistry? textures,
        Camera2D camera)
    {
        if (textures is null || _commands.Count == 0)
        {
            return false;
        }

        var drew = false;
        for (var index = 0; index < _commands.Count; index++)
        {
            var command = _commands[index];
            var sprite = textures.Get(command.SpriteId, command.SpriteFrameIndex);
            if (sprite.IsPlaceholder || sprite.SourceRectangle.IsEmpty)
            {
                continue;
            }

            var screen = camera.WorldToScreen(command.Position, context.ViewportBounds);
            context.SpriteBatch.Draw(
                sprite.Texture,
                screen,
                sprite.SourceRectangle,
                command.Color,
                command.RotationRadians,
                command.Origin,
                command.Scale * camera.Zoom,
                command.Effects,
                command.LayerDepth);
            drew = true;
        }

        return drew;
    }

    private void EnsureAppearance(CharacterAppearance appearance, bool armorVisible)
    {
        if (_appearance is not null && _sourceAppearance == appearance && _armorVisible == armorVisible)
        {
            return;
        }

        _sourceAppearance = appearance;
        _armorVisible = armorVisible;
        _appearance = new CharacterAppearanceProfile(
            new CharacterPartAppearance(ResolveSprite(CharacterAppearanceSlot.Body, BodySprite), ParseColor(appearance.SkinTone)),
            hair: new CharacterPartAppearance(ResolveSprite(CharacterAppearanceSlot.Hair, HairSprite), ParseColor(appearance.HairColor)),
            clothing: new CharacterPartAppearance(ResolveSprite(CharacterAppearanceSlot.Clothing, ClothesSprite), ParseColor(appearance.ShirtColor)),
            armor: armorVisible
                ? new CharacterPartAppearance(ResolveSprite(CharacterAppearanceSlot.Armor, ArmorSprite))
                : CharacterPartAppearance.Hidden,
            tool: new CharacterPartAppearance(ResolveSprite(CharacterAppearanceSlot.Tool, EquipmentSprite)));
    }

    private string ResolveSprite(CharacterAppearanceSlot slot, string fallback)
    {
        return _contentProfile is not null &&
            _contentProfile.SpriteBindings.TryGetBinding(slot, out var binding)
                ? binding.SpriteId
                : fallback;
    }

    private static bool HasVisibleArmor(EquipmentLoadout equipment)
    {
        return !equipment.GetStack(EquipmentSlotType.Head).IsEmpty ||
            !equipment.GetStack(EquipmentSlotType.Body).IsEmpty ||
            !equipment.GetStack(EquipmentSlotType.Legs).IsEmpty;
    }

    private static CharacterRigProfile CreateRig()
    {
        return new CharacterRigProfile(
            "player.character-v1.wave04",
            new[]
            {
                Layer("body", CharacterAppearanceSlot.Body, 0),
                Layer("clothes", CharacterAppearanceSlot.Clothing, 10),
                Layer("hair", CharacterAppearanceSlot.Hair, 20),
                Layer("armor", CharacterAppearanceSlot.Armor, 30),
                Layer("equipment", CharacterAppearanceSlot.Tool, 40)
            });
    }

    private static CharacterRigLayerDefinition Layer(
        string id,
        CharacterAppearanceSlot slot,
        int drawOrder)
    {
        return new CharacterRigLayerDefinition(
            id,
            slot,
            animationTrackId: "body",
            drawOrder,
            pivot: new CoreVector2(8, 32));
    }

    private static LayeredAnimationStateMachine CreateStateMachine()
    {
        var states = new[]
        {
            State("idle", AnimationLoopMode.PingPong, 18, 0, 1),
            State(
                "run",
                AnimationLoopMode.Loop,
                5,
                2,
                new[] { 3, 4, 5, 6, 7 },
                scaleWithLocomotion: true),
            State("jump", AnimationLoopMode.Once, 8, 8),
            State("fall", AnimationLoopMode.Once, 8, 9),
            State(
                "tool",
                AnimationLoopMode.Once,
                5,
                10,
                new[] { 11, 12 },
                actionLockMode: AnimationActionLockMode.UntilClipComplete,
                completionStateId: "idle"),
            State("block", AnimationLoopMode.Loop, 8, 13),
            State(
                "hurt",
                AnimationLoopMode.Once,
                5,
                14,
                new[] { 15 },
                actionLockMode: AnimationActionLockMode.UntilClipComplete,
                completionStateId: "idle"),
            State("death", AnimationLoopMode.Once, 60, 15)
        };
        var transitions = new[]
        {
            new AnimationTransitionDefinition(AnimationTransitionDefinition.AnyState, "idle", 2),
            new AnimationTransitionDefinition(AnimationTransitionDefinition.AnyState, "run", 2),
            new AnimationTransitionDefinition(AnimationTransitionDefinition.AnyState, "jump", 1),
            new AnimationTransitionDefinition(AnimationTransitionDefinition.AnyState, "fall", 1),
            new AnimationTransitionDefinition(AnimationTransitionDefinition.AnyState, "tool", 1),
            new AnimationTransitionDefinition(AnimationTransitionDefinition.AnyState, "block", 1),
            new AnimationTransitionDefinition(AnimationTransitionDefinition.AnyState, "hurt", 1),
            new AnimationTransitionDefinition(AnimationTransitionDefinition.AnyState, "death", 0)
        };
        var layer = new AnimationStateLayerDefinition("base", 0, "idle", states, transitions);
        return new LayeredAnimationStateMachine(new LayeredAnimationStateMachineDefinition(new[] { layer }));
    }

    private static AnimationStateDefinition State(
        string id,
        AnimationLoopMode loopMode,
        int durationTicks,
        int firstFrame,
        params int[] remainingFrames)
    {
        return State(
            id,
            loopMode,
            durationTicks,
            firstFrame,
            remainingFrames,
            scaleWithLocomotion: false,
            actionLockMode: AnimationActionLockMode.None,
            completionStateId: null);
    }

    private static AnimationStateDefinition State(
        string id,
        AnimationLoopMode loopMode,
        int durationTicks,
        int firstFrame,
        int[] remainingFrames,
        bool scaleWithLocomotion = false,
        AnimationActionLockMode actionLockMode = AnimationActionLockMode.None,
        string? completionStateId = null)
    {
        var frames = new AnimationFrame[remainingFrames.Length + 1];
        frames[0] = new AnimationFrame(firstFrame, durationTicks);
        for (var index = 0; index < remainingFrames.Length; index++)
        {
            frames[index + 1] = new AnimationFrame(remainingFrames[index], durationTicks);
        }

        var clip = new AnimationClip(
            $"player.wave04.{id}",
            loopMode,
            new[] { new AnimationTrack("body", "body", BodySprite, frames) });
        return new AnimationStateDefinition(
            id,
            clip,
            scaleWithLocomotion: scaleWithLocomotion,
            locomotionReferenceSpeedMilliUnitsPerSecond: 120_000,
            actionLockMode: actionLockMode,
            completionStateId: completionStateId);
    }

    private static AnimationColor ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#')
        {
            return AnimationColor.White;
        }

        return byte.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var r) &&
               byte.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
               byte.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)
            ? new AnimationColor(r, g, b)
            : AnimationColor.White;
    }
}
