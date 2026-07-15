using Game.Client.Rendering.Character;
using Game.Core.Animation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using CoreVector2 = System.Numerics.Vector2;

namespace Game.Tests.AnimationTests;

public sealed class CharacterSpriteLayerPoseBuilderTests
{
    [Fact]
    public void Build_OrdersTerrariaStyleModularLayersByRigDrawOrder()
    {
        var rig = new CharacterRigProfile(
            "humanoid",
            new[]
            {
                Layer("accessory", CharacterAppearanceSlot.Accessory, 80),
                Layer("tool", CharacterAppearanceSlot.Tool, 60),
                Layer("body", CharacterAppearanceSlot.Body, 10),
                Layer("hair-back", CharacterAppearanceSlot.Hair, 0),
                Layer("eyes", CharacterAppearanceSlot.Eyes, 20),
                Layer("clothing", CharacterAppearanceSlot.Clothing, 30),
                Layer("armor", CharacterAppearanceSlot.Armor, 40),
                Layer("hands", CharacterAppearanceSlot.Hands, 50),
                Layer("hair-front", CharacterAppearanceSlot.Hair, 70)
            });

        var result = new CharacterSpriteLayerPoseBuilder().Build(
            rig,
            CreateAppearance(showOptionalParts: true),
            CreatePose(CreateMultiTrackClip(rig)),
            Vector2.Zero);

        Assert.Equal(
            new[] { "hair-back", "body", "eyes", "clothing", "armor", "hands", "tool", "hair-front", "accessory" },
            result.Layers.Select(layer => layer.RigLayerId));
        Assert.Equal(Enumerable.Range(0, 9), result.Layers.Select(layer => layer.DrawOrder / 10));
    }

    [Fact]
    public void Build_UsesAppearanceSpritesAndComposesPerLayerTints()
    {
        var rig = new CharacterRigProfile("humanoid", new[]
        {
            new CharacterRigLayerDefinition(
                "body",
                CharacterAppearanceSlot.Body,
                "body",
                0,
                tint: new AnimationColor(128, 255, 255))
        });
        var appearance = new CharacterAppearanceProfile(new CharacterPartAppearance(
            "appearance/body",
            new AnimationColor(255, 128, 255)));
        var clip = CreateSingleTrackClip(
            "idle",
            new AnimationFrame(3, 2, tint: new AnimationColor(255, 255, 128)));

        var layer = Assert.Single(new CharacterSpriteLayerPoseBuilder().Build(
            rig,
            appearance,
            CreatePose(clip, new AnimationColor(255, 200, 255)),
            Vector2.Zero).Layers);

        Assert.Equal("appearance/body", layer.SpriteId);
        Assert.Equal(3, layer.SpriteFrameIndex);
        Assert.Equal(new AnimationColor(128, 100, 128), layer.Tint);
    }

    [Fact]
    public void Build_EquipmentAndActionOverridesApplyWithActionPrecedence()
    {
        var rig = new CharacterRigProfile("humanoid", new[]
        {
            Layer("armor", CharacterAppearanceSlot.Armor, 0),
            Layer("tool", CharacterAppearanceSlot.Tool, 10)
        });
        var overrides = new CharacterRenderOverrides(
            new[]
            {
                new CharacterLayerOverride("armor", "equipment/copper", frameIndexOffset: 1),
                new CharacterLayerOverride("tool", "tools/pickaxe", visible: true)
            },
            new[]
            {
                new CharacterLayerOverride("armor", "equipment/vanity", frameIndex: 7, frameIndexOffset: 2),
                new CharacterLayerOverride("tool", "tools/hammer", visible: true, frameIndex: 4)
            });

        var result = new CharacterSpriteLayerPoseBuilder().Build(
            rig,
            CreateAppearance(),
            CreatePose(CreateMultiTrackClip(rig)),
            Vector2.Zero,
            overrides);

        Assert.Collection(
            result.Layers,
            armor =>
            {
                Assert.Equal("equipment/vanity", armor.SpriteId);
                Assert.Equal(10, armor.SpriteFrameIndex);
                Assert.True(armor.Visible);
            },
            tool =>
            {
                Assert.Equal("tools/hammer", tool.SpriteId);
                Assert.Equal(4, tool.SpriteFrameIndex);
                Assert.True(tool.Visible);
            });
    }

    [Fact]
    public void Build_ResolvesParentSocketsForToolAndMirrorsFacingDirection()
    {
        var bodyFrame = new AnimationFrame(
            0,
            2,
            transform: new AnimationTransform2D(new CoreVector2(2, 0), 0f, CoreVector2.One),
            sockets: new[] { new AttachmentSocketPose("hand", new CoreVector2(3, 4)) });
        var bodyTrack = new AnimationTrack("body", "body", "body", new[] { bodyFrame });
        var toolTrack = new AnimationTrack("tool", "tool", "tool", new[] { new AnimationFrame(0, 2) });
        var clip = new AnimationClip("mine", AnimationLoopMode.Loop, new[] { bodyTrack, toolTrack });
        var rig = new CharacterRigProfile("humanoid", new[]
        {
            new CharacterRigLayerDefinition(
                "body",
                CharacterAppearanceSlot.Body,
                "body",
                0,
                localTransform: new AnimationTransform2D(new CoreVector2(1, 0), 0f, CoreVector2.One)),
            new CharacterRigLayerDefinition(
                "tool",
                CharacterAppearanceSlot.Tool,
                "tool",
                10,
                parentLayerId: "body",
                parentSocketId: "hand",
                localTransform: new AnimationTransform2D(new CoreVector2(2, 1), 0f, CoreVector2.One))
        });
        var appearance = new CharacterAppearanceProfile(
            new CharacterPartAppearance("body"),
            tool: new CharacterPartAppearance("tool"));
        var builder = new CharacterSpriteLayerPoseBuilder();

        var right = builder.Build(rig, appearance, CreatePose(clip), new Vector2(10, 20));
        var left = builder.Build(
            rig,
            appearance,
            CreatePose(clip, direction: CharacterFacingDirection.Left),
            new Vector2(10, 20));

        Assert.Equal(new Vector2(13, 20), right.Layers[0].Position);
        Assert.Equal(new Vector2(18, 25), right.Layers[1].Position);
        Assert.Equal(new Vector2(7, 20), left.Layers[0].Position);
        Assert.Equal(new Vector2(2, 25), left.Layers[1].Position);
        Assert.True(left.Layers[0].FlipHorizontally);
        Assert.True(left.Layers[1].FlipHorizontally);
    }

    [Fact]
    public void Build_TransformsSocketRotationAndScaleIntoWorldPose()
    {
        var frame = new AnimationFrame(
            0,
            1,
            transform: new AnimationTransform2D(CoreVector2.Zero, MathF.PI / 2f, new CoreVector2(2, 1)),
            sockets: new[] { new AttachmentSocketPose("hand", new CoreVector2(2, 0), 0.25f) });
        var rig = new CharacterRigProfile("humanoid", new[] { Layer("body", CharacterAppearanceSlot.Body, 0) });

        var body = Assert.Single(new CharacterSpriteLayerPoseBuilder().Build(
            rig,
            CreateAppearance(),
            CreatePose(CreateSingleTrackClip("pose", frame)),
            new Vector2(5, 6)).Layers);
        var socket = Assert.Single(body.Sockets);

        Assert.InRange(socket.Position.X, 4.999f, 5.001f);
        Assert.InRange(socket.Position.Y, 9.999f, 10.001f);
        Assert.InRange(socket.RotationRadians, (MathF.PI / 2f) + 0.249f, (MathF.PI / 2f) + 0.251f);
    }

    [Fact]
    public void Build_UsesHighestPriorityAnimationLayerThatTargetsRigTrack()
    {
        var baseClip = CreateSingleTrackClip("idle", new AnimationFrame(1, 2));
        var actionClip = CreateSingleTrackClip("hurt", new AnimationFrame(7, 2));
        var baseLayer = new AnimationStateLayerDefinition(
            "base",
            0,
            "idle",
            new[] { new AnimationStateDefinition("idle", baseClip) });
        var actionLayer = new AnimationStateLayerDefinition(
            "action",
            100,
            "hurt",
            new[] { new AnimationStateDefinition("hurt", actionClip) });
        var machine = new LayeredAnimationStateMachine(new LayeredAnimationStateMachineDefinition(
            new[] { actionLayer, baseLayer }));
        var rig = new CharacterRigProfile("humanoid", new[] { Layer("body", CharacterAppearanceSlot.Body, 0) });

        var layer = Assert.Single(new CharacterSpriteLayerPoseBuilder().Build(
            rig,
            CreateAppearance(),
            machine.Sample(),
            Vector2.Zero).Layers);

        Assert.Equal(7, layer.SpriteFrameIndex);
    }

    [Fact]
    public void Build_EmitsOutgoingBeforeCurrentPoseDuringBlend()
    {
        var idle = new AnimationStateDefinition("idle", CreateSingleTrackClip("idle", new AnimationFrame(1, 2)));
        var run = new AnimationStateDefinition("run", CreateSingleTrackClip("run", new AnimationFrame(5, 2)));
        var machine = AnimationTestFactory.CreateMachine(AnimationTestFactory.CreateLayer(
            "base",
            "idle",
            new[] { idle, run },
            new[] { new AnimationTransitionDefinition("idle", "run", 2) }));
        machine.RequestState("base", "run");
        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right));
        var rig = new CharacterRigProfile("humanoid", new[] { Layer("body", CharacterAppearanceSlot.Body, 0) });

        var layers = new CharacterSpriteLayerPoseBuilder().Build(
            rig,
            CreateAppearance(),
            machine.Sample(),
            Vector2.Zero).Layers;

        Assert.Collection(
            layers,
            outgoing =>
            {
                Assert.Equal(SpriteLayerBlendRole.Outgoing, outgoing.BlendRole);
                Assert.Equal(1, outgoing.SpriteFrameIndex);
                Assert.Equal(0.5f, outgoing.Opacity);
            },
            current =>
            {
                Assert.Equal(SpriteLayerBlendRole.Current, current.BlendRole);
                Assert.Equal(5, current.SpriteFrameIndex);
                Assert.Equal(0.5f, current.Opacity);
            });
    }

    [Fact]
    public void DrawCommandBuilder_FiltersHiddenLayersAndProducesResidentAssetKeysOnly()
    {
        var rig = new CharacterRigProfile("humanoid", new[]
        {
            Layer("body", CharacterAppearanceSlot.Body, 0),
            Layer("armor", CharacterAppearanceSlot.Armor, 10)
        });
        var pose = new CharacterSpriteLayerPoseBuilder().Build(
            rig,
            CreateAppearance(),
            CreatePose(CreateMultiTrackClip(rig)),
            new Vector2(10, 20));

        var commands = new CharacterDrawCommandBuilder().Build(pose);

        var command = Assert.Single(commands);
        Assert.Equal("body", command.RigLayerId);
        Assert.Equal("appearance/body", command.SpriteId);
        Assert.Equal(SpriteEffects.None, command.Effects);
        Assert.DoesNotContain(
            typeof(CharacterDrawCommand).GetProperties(),
            property => typeof(Texture2D).IsAssignableFrom(property.PropertyType));
    }

    [Fact]
    public void DrawCommandBuilder_MapsBlendOpacityDirectionAndLayerDepth()
    {
        var idle = new AnimationStateDefinition("idle", CreateSingleTrackClip("idle", new AnimationFrame(1, 2)));
        var run = new AnimationStateDefinition("run", CreateSingleTrackClip("run", new AnimationFrame(2, 2)));
        var machine = AnimationTestFactory.CreateMachine(AnimationTestFactory.CreateLayer(
            "base",
            "idle",
            new[] { idle, run },
            new[] { new AnimationTransitionDefinition("idle", "run", 2) }));
        machine.RequestState("base", "run");
        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Left));
        var rig = new CharacterRigProfile("humanoid", new[]
        {
            new CharacterRigLayerDefinition("body", CharacterAppearanceSlot.Body, "body", 25, pivot: new CoreVector2(8, 14))
        });
        var pose = new CharacterSpriteLayerPoseBuilder().Build(
            rig,
            CreateAppearance(),
            machine.Sample(),
            Vector2.Zero);

        var commands = new CharacterDrawCommandBuilder().Build(pose, 0.4f, 0.001f);

        Assert.Equal(2, commands.Length);
        Assert.All(commands, command => Assert.Equal((byte)128, command.Color.A));
        Assert.All(commands, command => Assert.Equal(SpriteEffects.FlipHorizontally, command.Effects));
        Assert.All(commands, command => Assert.Equal(0.425f, command.LayerDepth, 3));
        Assert.All(commands, command => Assert.Equal(new Vector2(8, 14), command.Origin));
    }

    private static CharacterRigLayerDefinition Layer(
        string id,
        CharacterAppearanceSlot appearanceSlot,
        int drawOrder)
    {
        return new CharacterRigLayerDefinition(id, appearanceSlot, id, drawOrder);
    }

    private static CharacterAppearanceProfile CreateAppearance(bool showOptionalParts = false)
    {
        var optional = showOptionalParts
            ? new CharacterPartAppearance("appearance/optional")
            : CharacterPartAppearance.Hidden;

        return new CharacterAppearanceProfile(
            new CharacterPartAppearance("appearance/body"),
            eyes: showOptionalParts ? new CharacterPartAppearance("appearance/eyes") : null,
            hair: showOptionalParts ? new CharacterPartAppearance("appearance/hair") : null,
            clothing: showOptionalParts ? new CharacterPartAppearance("appearance/clothing") : null,
            armor: optional,
            tool: optional,
            accessory: optional);
    }

    private static AnimationClip CreateMultiTrackClip(CharacterRigProfile rig)
    {
        var tracks = new AnimationTrack[rig.Layers.Count];
        for (var index = 0; index < rig.Layers.Count; index++)
        {
            var layer = rig.Layers[index];
            tracks[index] = new AnimationTrack(
                layer.AnimationTrackId,
                layer.Id,
                $"track/{layer.Id}",
                new[] { new AnimationFrame(index, 2) });
        }

        return new AnimationClip("modular", AnimationLoopMode.Loop, tracks);
    }

    private static AnimationClip CreateSingleTrackClip(string id, AnimationFrame frame)
    {
        return new AnimationClip(
            id,
            AnimationLoopMode.Loop,
            new[] { new AnimationTrack("body", "body", $"track/{id}", new[] { frame }) });
    }

    private static LayeredAnimationPose CreatePose(
        AnimationClip clip,
        AnimationColor? tint = null,
        CharacterFacingDirection direction = CharacterFacingDirection.Right)
    {
        var layer = new AnimationStateLayerDefinition(
            "base",
            0,
            "active",
            new[] { new AnimationStateDefinition("active", clip) },
            tint: tint);
        var machine = new LayeredAnimationStateMachine(new LayeredAnimationStateMachineDefinition(new[] { layer }));
        machine.AdvanceFixedTick(new AnimationUpdateContext(direction));
        return machine.Sample();
    }
}
