using Game.Core.Characters;
using Game.Core.Physics;
using System.Numerics;
using Xunit;

namespace Game.Tests.CharacterTests;

public sealed class CharacterSystemTests
{
    [Fact]
    public void Loader_ReadsCharacterAnimationSetAndAppearance()
    {
        const string json = """
        {
          "id": "player",
          "displayName": "Player",
          "width": 12,
          "height": 28,
          "defaultAppearance": {
            "bodySpriteId": "entities/player/base_actions",
            "skinTone": "#c98f62",
            "hairStyleId": "short"
          },
          "animationSet": {
            "id": "player.default",
            "states": {
              "Idle": "player.idle",
              "Walk": "player.walk"
            }
          },
          "tags": ["player", "editable"]
        }
        """;

        var character = new CharacterDefinitionJsonLoader().LoadDefinitionFromJson(json);

        Assert.Equal("player", character.Id);
        Assert.Equal("entities/player/base_actions", character.DefaultAppearance.BodySpriteId);
        Assert.Equal("player.walk", character.AnimationSet.ResolveClipId(CharacterAnimationState.Walk));
        Assert.Equal("player.idle", character.AnimationSet.ResolveClipId(CharacterAnimationState.Fall));
        Assert.True(character.HasTag("editable"));
    }

    [Fact]
    public void Registry_RejectsCharacterWithoutAnimationReferences()
    {
        var character = new CharacterDefinition
        {
            Id = "broken",
            DisplayName = "Broken",
            AnimationSet = new CharacterAnimationSetDefinition
            {
                Id = "broken.animations",
                StateClips = new Dictionary<CharacterAnimationState, string>()
            }
        };

        Assert.Throws<Game.Core.Data.RegistryValidationException>(() => CharacterDefinitionRegistry.Create(new[] { character }));
    }

    [Fact]
    public void EditorSession_AppliesOnlyAllowedAppearanceChoices()
    {
        var session = new CharacterEditorSession(new CharacterAppearance());

        Assert.True(session.SetHairStyle("messy"));
        Assert.False(session.SetHairStyle("missing_style"));
        Assert.Equal("messy", session.Appearance.HairStyleId);
    }

    [Fact]
    public void StateResolver_PrioritizesActionStatesThenMovement()
    {
        var resolver = new CharacterAnimationStateResolver();
        var body = new PhysicsBody
        {
            Position = Vector2.Zero,
            Size = new Vector2(12, 28),
            Velocity = new Vector2(80, 0),
            OnGround = true
        };

        Assert.Equal(CharacterAnimationState.Attack, resolver.Resolve(body, isAttacking: true));
        Assert.Equal(CharacterAnimationState.Mine, resolver.Resolve(body, isMining: true));
        Assert.Equal(CharacterAnimationState.Walk, resolver.Resolve(body));

        body.OnGround = false;
        body.Velocity = new Vector2(0, -40);
        Assert.Equal(CharacterAnimationState.Jump, resolver.Resolve(body));

        body.Velocity = new Vector2(0, 40);
        Assert.Equal(CharacterAnimationState.Fall, resolver.Resolve(body));
    }
}
