using Game.Core.Animation;
using Game.Core.Animations;
using Game.Core.Assets;
using Game.Core.Characters;
using Game.Client.Rendering.Character;
using Xunit;

namespace Game.Tests.AnimationTests;

public sealed class AnimationContentJsonLoaderTests
{
    [Fact]
    public void LoadFromRoot_LoadsActiveWave06RuntimeProfilesInDeterministicOrder()
    {
        var contentRoot = LocateGameDataRoot();
        var result = new AnimationContentJsonLoader().LoadFromRoot(contentRoot, LoadSpriteAssets(contentRoot));

        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Severity == AnimationContentDiagnosticSeverity.Error);
        Assert.Equal(
            result.Registry.Clips.Select(clip => clip.Id).Order(StringComparer.Ordinal),
            result.Registry.Clips.Select(clip => clip.Id));
        Assert.Equal(
            result.Registry.Entities.Select(profile => profile.Id).Order(StringComparer.Ordinal),
            result.Registry.Entities.Select(profile => profile.Id));

        var character = result.Registry.GetCharacter("player.wave06");
        Assert.Equal("player.character-v2.wave06", character.Rig.Id);
        Assert.Equal("player.wave06.states", character.StateMachine.Id);
        Assert.Equal(
            new[] { "body", "clothes", "hair", "armor", "equipment" },
            character.Rig.Layers.Select(layer => layer.Id));
        Assert.All(
            character.Rig.Layers,
            layer => Assert.Equal(new System.Numerics.Vector2(12f, 40f), layer.Pivot));
        Assert.Equal(8, character.StateMachine.Definition.Layers.Single().States.Count);
        Assert.Equal(14, result.Registry.Entities.Count);
        Assert.Equal(16, result.Registry.Clips.Count);
    }

    [Fact]
    public void LoadedCharacterProfile_DrivesExistingFixedTickMachineAndActionsWithoutSecondClock()
    {
        var contentRoot = LocateGameDataRoot();
        var profile = new AnimationContentJsonLoader()
            .LoadFromRoot(contentRoot, LoadSpriteAssets(contentRoot))
            .Registry
            .GetCharacter("player.wave06");
        var machine = profile.CreateStateMachine();

        Assert.True(machine.TryGetCurrentState("base", out var initial));
        Assert.Equal("idle", initial);
        Assert.Equal(AnimationStateRequestResult.Applied, profile.RequestAction(machine, CharacterAnimationState.Attack));
        Assert.True(machine.TryGetCurrentState("base", out var action));
        Assert.Equal("tool", action);

        for (var tick = 0; tick < 15; tick++)
        {
            machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right, 120_000));
        }

        Assert.Equal(15, machine.FixedTick);
        Assert.True(machine.TryGetCurrentState("base", out var completed));
        Assert.Equal("idle", completed);

        Assert.Equal(AnimationStateRequestResult.Applied, machine.RequestState("base", "run"));
        for (var tick = 0; tick < 5; tick++)
        {
            machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right, 120_000));
        }

        var track = Assert.Single(Assert.Single(machine.Sample().Layers).Current.Tracks);
        Assert.Equal(3, track.SpriteFrameIndex);
        Assert.Equal(20, machine.FixedTick);

        var appearance = profile.SpriteBindings.CreateAppearance();
        Assert.Equal("entities/player/character_v2_wave06/body", appearance.Body.SpriteId);
        Assert.Equal("entities/player/character_v2_wave06/equipment", appearance.Tool.SpriteId);
        Assert.False(appearance.Armor.Visible);
    }

    [Fact]
    public void LoadedCharacterProfile_FeedsExistingPoseAndDrawCommandBuilders()
    {
        var contentRoot = LocateGameDataRoot();
        var profile = new AnimationContentJsonLoader()
            .LoadFromRoot(contentRoot, LoadSpriteAssets(contentRoot))
            .Registry
            .GetCharacter("player.wave06");
        var machine = profile.CreateStateMachine();
        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right));

        var pose = new CharacterSpriteLayerPoseBuilder().Build(
            profile.Rig,
            profile.SpriteBindings.CreateAppearance(),
            machine.Sample(),
            Microsoft.Xna.Framework.Vector2.Zero);
        var commands = new CharacterDrawCommandBuilder().Build(pose);

        Assert.Equal(4, commands.Length);
        Assert.Equal(
            new[]
            {
                "entities/player/character_v2_wave06/body",
                "entities/player/character_v2_wave06/clothes",
                "entities/player/character_v2_wave06/hair",
                "entities/player/character_v2_wave06/equipment"
            },
            commands.Select(command => command.SpriteId));
        Assert.All(commands, command => Assert.Equal(0, command.SpriteFrameIndex));
        Assert.All(commands, command => Assert.Equal(new Microsoft.Xna.Framework.Vector2(12f, 40f), command.Origin));
        Assert.Equal(1, machine.FixedTick);
    }

    [Fact]
    public void Wave06LayerSheetsShareNativeFrameRegistrationAndBaseline()
    {
        var contentRoot = LocateGameDataRoot();
        var sprites = LoadSpriteAssets(contentRoot);
        var layerIds = new[] { "body", "clothes", "hair", "armor", "equipment" };

        foreach (var layerId in layerIds)
        {
            var sprite = sprites.GetById($"entities/player/character_v2_wave06/{layerId}");

            Assert.Equal(384, sprite.Width);
            Assert.Equal(40, sprite.Height);
            Assert.Equal(12, sprite.OriginX);
            Assert.Equal(40, sprite.OriginY);
            Assert.Equal(16, sprite.Frames.Count);
            Assert.Collection(
                sprite.Frames,
                Enumerable.Range(0, 16)
                    .Select<int, Action<SpriteFrameDefinition>>(frame => definition =>
                    {
                        Assert.Equal(frame * 24, definition.X);
                        Assert.Equal(0, definition.Y);
                        Assert.Equal(24, definition.Width);
                        Assert.Equal(40, definition.Height);
                        Assert.Equal(12, definition.OriginX);
                        Assert.Equal(40, definition.OriginY);
                    })
                    .ToArray());
        }
    }

    [Fact]
    public void EntityProfiles_ResolveAliasesFallbacksAndFramesFromCallerOwnedFixedTicks()
    {
        var contentRoot = LocateGameDataRoot();
        var registry = new AnimationContentJsonLoader()
            .LoadFromRoot(contentRoot, LoadSpriteAssets(contentRoot))
            .Registry;

        var rabbit = registry.ResolveEntity("MEADOW_RABBIT", AnimationEntityKind.Enemy);
        Assert.False(rabbit.UsedFallback);
        Assert.Equal("rabbit", rabbit.Profile.Id);
        Assert.Equal(0, rabbit.Profile.ResolveFrame(AnimationEntityVisualState.Idle, 0));
        Assert.Equal(1, rabbit.Profile.ResolveFrame(AnimationEntityVisualState.Idle, 10));
        Assert.Equal(3, rabbit.Profile.ResolveFrame(AnimationEntityVisualState.Idle, 30));
        Assert.Equal(2, rabbit.Profile.ResolveFrame(AnimationEntityVisualState.Idle, 40));

        var repeated = rabbit.Profile.ResolveFrame(AnimationEntityVisualState.Run, 99, phaseOffset: 3);
        Assert.Equal(repeated, rabbit.Profile.ResolveFrame(AnimationEntityVisualState.Run, 99, phaseOffset: 3));

        var missing = registry.ResolveEntity("unknown-enemy", AnimationEntityKind.Enemy);
        Assert.True(missing.UsedFallback);
        Assert.Equal("entity-profile.fallback", missing.DiagnosticCode);
        Assert.Equal("slime", missing.Profile.Id);
    }

    [Fact]
    public void LoadFromJson_RejectsSourceRectangleMismatchWithSourceDiagnostic()
    {
        var contentRoot = LocateGameDataRoot();
        var path = Path.Combine(contentRoot, "animations", "wave04_runtime_profiles.json");
        var json = File.ReadAllText(path).Replace(
            "\"x\": 0, \"y\": 0, \"width\": 16, \"height\": 32",
            "\"x\": 1, \"y\": 0, \"width\": 16, \"height\": 32",
            StringComparison.Ordinal);

        var exception = Assert.Throws<AnimationContentValidationException>(() =>
            new AnimationContentJsonLoader().LoadFromJson(json, LoadSpriteAssets(contentRoot), "bad-rectangle.json"));

        Assert.Contains(
            exception.Diagnostics,
            diagnostic => diagnostic.Source == "bad-rectangle.json" &&
                diagnostic.Message.Contains("source rectangle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadFromRoot_RejectsActionThatTargetsMissingState()
    {
        var contentRoot = LocateGameDataRoot();
        var tempRoot = CreateTempRoot();
        try
        {
            CopyRuntimeDocuments(contentRoot, tempRoot, characterTransform: json => json.Replace(
                "\"stateId\": \"tool\"",
                "\"stateId\": \"missing\"",
                StringComparison.Ordinal));

            var exception = Assert.Throws<AnimationContentValidationException>(() =>
                new AnimationContentJsonLoader().LoadFromRoot(tempRoot, LoadSpriteAssets(contentRoot)));

            Assert.Contains(
                exception.Diagnostics,
                diagnostic => diagnostic.Message.Contains("missing state", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadFromRoot_RejectsLayerSheetThatDoesNotMatchTrackRegistration()
    {
        var contentRoot = LocateGameDataRoot();
        var tempRoot = CreateTempRoot();
        try
        {
            CopyRuntimeDocuments(contentRoot, tempRoot, characterTransform: json => json.Replace(
                "entities/player/character_v2_wave06/hair",
                "entities/critters/rabbit",
                StringComparison.Ordinal));

            var exception = Assert.Throws<AnimationContentValidationException>(() =>
                new AnimationContentJsonLoader().LoadFromRoot(tempRoot, LoadSpriteAssets(contentRoot)));

            Assert.Contains(
                exception.Diagnostics,
                diagnostic => diagnostic.Message.Contains("registration", StringComparison.OrdinalIgnoreCase) ||
                    diagnostic.Message.Contains("has 4 frame", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadWithMods_AppliesSortedOverridesAndReportsEveryReplacement()
    {
        var root = CreateTempRoot();
        var modsRoot = CreateTempRoot();
        try
        {
            WriteClipDocument(root, "base.json", durationTicks: 1);
            WriteClipDocument(Path.Combine(modsRoot, "01_first"), "first.json", durationTicks: 2);
            WriteClipDocument(Path.Combine(modsRoot, "02_second"), "second.json", durationTicks: 3);

            var result = new AnimationContentJsonLoader().LoadWithMods(
                root,
                modsRoot,
                SpriteAssetRegistry.Create(Array.Empty<SpriteAssetDefinition>()));

            Assert.Equal(3, result.Registry.GetClip("test.override").DurationTicks);
            Assert.Equal(
                2,
                result.Diagnostics.Count(diagnostic => diagnostic.Code == "content.override"));
            Assert.Equal(4, result.Diagnostics.Count(diagnostic => diagnostic.Code == "fallback.generated"));
            Assert.True(result.Registry.ResolveEntity("missing", AnimationEntityKind.Projectile).UsedFallback);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(modsRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadFromJson_RejectsEntityFrameRangePastSpriteFrames()
    {
        const string json = """
            {
              "runtimeAnimationContent": {
                "entityProfiles": [
                  {
                    "id": "bad.entity",
                    "kind": "Entity",
                    "spriteId": "missing_sprite",
                    "animations": [
                      { "state": "Idle", "startFrame": 0, "frameCount": 2, "ticksPerFrame": 1 }
                    ]
                  }
                ]
              }
            }
            """;

        var exception = Assert.Throws<AnimationContentValidationException>(() =>
            new AnimationContentJsonLoader().LoadFromJson(
                json,
                SpriteAssetRegistry.Create(Array.Empty<SpriteAssetDefinition>()),
                "bad-entity.json"));

        Assert.Contains(
            exception.Diagnostics,
            diagnostic => diagnostic.Message.Contains("has 1 frame", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Wave04Manifest_RemainsCompatibleWithLegacySpriteAnimationLoader()
    {
        var contentRoot = LocateGameDataRoot();
        var registry = new SpriteAnimationJsonLoader().LoadRegistryFromDirectory(
            Path.Combine(contentRoot, "animations"));

        var clip = registry.GetById("player.wave04.tool");
        Assert.Equal(3, clip.Frames.Count);
        Assert.Equal("entities/player/character_v1_wave04/body", clip.Frames[0].SpriteId);
        Assert.Equal(10, clip.Frames[0].FrameIndex);
    }

    private static SpriteAssetRegistry LoadSpriteAssets(string contentRoot)
    {
        return new SpriteAssetJsonLoader().LoadRegistryFromDirectory(Path.Combine(contentRoot, "assets"));
    }

    private static void CopyRuntimeDocuments(
        string contentRoot,
        string destinationRoot,
        Func<string, string>? animationTransform = null,
        Func<string, string>? characterTransform = null)
    {
        var animationDirectory = Directory.CreateDirectory(Path.Combine(destinationRoot, "animations"));
        var characterDirectory = Directory.CreateDirectory(Path.Combine(destinationRoot, "characters"));
        var animationJson = File.ReadAllText(
            Path.Combine(contentRoot, "animations", "wave04_runtime_profiles.json"));
        var wave06AnimationJson = File.ReadAllText(
            Path.Combine(contentRoot, "animations", "wave06_character_runtime_profiles.json"));
        var characterJson = File.ReadAllText(Path.Combine(contentRoot, "characters", "player.json"));
        File.WriteAllText(
            Path.Combine(animationDirectory.FullName, "wave04_runtime_profiles.json"),
            animationTransform?.Invoke(animationJson) ?? animationJson);
        File.WriteAllText(
            Path.Combine(animationDirectory.FullName, "wave06_character_runtime_profiles.json"),
            animationTransform?.Invoke(wave06AnimationJson) ?? wave06AnimationJson);
        File.WriteAllText(
            Path.Combine(characterDirectory.FullName, "player.json"),
            characterTransform?.Invoke(characterJson) ?? characterJson);
    }

    private static void WriteClipDocument(string root, string fileName, int durationTicks)
    {
        var animationDirectory = Directory.CreateDirectory(Path.Combine(root, "animations"));
        var json = $$"""
            {
              "animations": [
                {
                  "id": "test.override",
                  "displayName": "Override Compatibility Clip",
                  "loopMode": "Loop",
                  "sprite": "missing_sprite",
                  "frames": [
                    { "frame": 0, "durationSeconds": 0.1, "durationTicks": {{durationTicks}} }
                  ]
                }
              ],
              "runtimeAnimationContent": {
                "clipIds": ["test.override"]
              }
            }
            """;
        File.WriteAllText(Path.Combine(animationDirectory.FullName, fileName), json);
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"yjse-animation-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string LocateGameDataRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }
}
