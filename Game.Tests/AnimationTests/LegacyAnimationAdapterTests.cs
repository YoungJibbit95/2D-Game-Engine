using Game.Core.Animation;
using Game.Core.Animations;
using Game.Core.Characters;
using Xunit;

namespace Game.Tests.AnimationTests;

public sealed class LegacyAnimationAdapterTests
{
    [Fact]
    public void ClipAdapter_QuantizesDurationsOffsetsEventsAndPerFrameSpriteIds()
    {
        var legacy = new SpriteAnimationClip
        {
            Id = "player.mine",
            LoopMode = SpriteAnimationLoopMode.Once,
            Frames = new[]
            {
                new SpriteAnimationFrame
                {
                    SpriteId = "player/base",
                    FrameIndex = 2,
                    DurationSeconds = 0.1f,
                    OffsetX = 1,
                    EventId = "windup"
                },
                new SpriteAnimationFrame
                {
                    SpriteId = "player/tool-actions",
                    FrameIndex = 7,
                    DurationSeconds = 0.05f,
                    OffsetY = -2,
                    EventId = "impact"
                }
            }
        };

        var clip = LegacySpriteAnimationClipAdapter.ToFixedTickClip(legacy, fixedTicksPerSecond: 60);
        var player = new AnimationClipPlayer(clip);

        Assert.Equal(AnimationLoopMode.Once, clip.LoopMode);
        Assert.Equal(9, clip.DurationTicks);
        Assert.Equal("windup", Assert.Single(player.Events.ConsumeAll()).EventId);
        for (var index = 0; index < 6; index++)
        {
            player.AdvanceFixedTick();
        }

        var sample = Assert.Single(player.Sample().Tracks);
        Assert.Equal("player/tool-actions", sample.SpriteId);
        Assert.Equal(7, sample.SpriteFrameIndex);
        Assert.Equal(-2, sample.Transform.Translation.Y);
        Assert.Equal("impact", Assert.Single(player.Events.ConsumeAll()).EventId);
    }

    [Fact]
    public void CharacterSetAdapter_MapsLegacyStatesWithoutCreatingAnotherAnimator()
    {
        var registry = SpriteAnimationRegistry.Create(new[]
        {
            LegacyClip("player.idle", SpriteAnimationLoopMode.Loop),
            LegacyClip("player.walk", SpriteAnimationLoopMode.Loop),
            LegacyClip("player.mine", SpriteAnimationLoopMode.Once),
            LegacyClip("player.block", SpriteAnimationLoopMode.Loop)
        });
        var set = new CharacterAnimationSetDefinition
        {
            Id = "player.default",
            StateClips = new Dictionary<CharacterAnimationState, string>
            {
                [CharacterAnimationState.Idle] = "player.idle",
                [CharacterAnimationState.Walk] = "player.walk",
                [CharacterAnimationState.Mine] = "player.mine",
                [CharacterAnimationState.Block] = "player.block"
            }
        };

        var layer = LegacyCharacterAnimationSetAdapter.ToStateLayer(set, registry);
        var states = layer.States.ToDictionary(state => state.Id, StringComparer.OrdinalIgnoreCase);

        Assert.Equal("idle", layer.InitialStateId);
        Assert.True(states["run"].ScaleWithLocomotion);
        Assert.Equal(AnimationActionLockMode.UntilClipComplete, states["mine"].ActionLockMode);
        Assert.Equal("idle", states["mine"].CompletionStateId);
        Assert.Equal(AnimationActionLockMode.None, states["block"].ActionLockMode);
        Assert.Equal(AnimationLoopMode.Loop, states["block"].Clip.LoopMode);
    }

    [Fact]
    public void AppearanceAdapter_ReusesLegacyStyleAndColorChoicesThroughBindings()
    {
        var legacy = new Game.Core.Characters.CharacterAppearance
        {
            BodySpriteId = "player/body",
            SkinTone = "#102030",
            EyeColor = "#405060",
            HairStyleId = "messy",
            HairColor = "#708090",
            ClothesStyleId = "miner",
            ShirtColor = "#a0b0c0",
            AccessoryId = "helmet"
        };
        var bindings = new LegacyCharacterSpriteBindings
        {
            EyesSpriteId = "player/eyes",
            HairSpriteResolver = style => $"player/hair/{style}",
            ClothingSpriteResolver = style => $"player/clothing/{style}",
            AccessorySpriteResolver = accessory => accessory == "none" ? null : $"player/accessory/{accessory}"
        };

        var appearance = LegacyCharacterAppearanceAdapter.ToProfile(legacy, bindings);

        Assert.Equal("player/body", appearance.Body.SpriteId);
        Assert.Equal(new AnimationColor(0x10, 0x20, 0x30), appearance.Body.Tint);
        Assert.Equal("player/hair/messy", appearance.Hair.SpriteId);
        Assert.Equal(new AnimationColor(0x70, 0x80, 0x90), appearance.Hair.Tint);
        Assert.Equal("player/clothing/miner", appearance.Clothing.SpriteId);
        Assert.Equal("player/accessory/helmet", appearance.Accessory.SpriteId);
        Assert.Equal("player/body", appearance.Hands.SpriteId);
    }

    private static SpriteAnimationClip LegacyClip(string id, SpriteAnimationLoopMode loopMode)
    {
        return new SpriteAnimationClip
        {
            Id = id,
            LoopMode = loopMode,
            Frames = new[]
            {
                new SpriteAnimationFrame
                {
                    SpriteId = "player/base",
                    FrameIndex = 0,
                    DurationSeconds = 0.1f
                }
            }
        };
    }
}
