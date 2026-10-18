using Game.Client.Configuration;
using Game.Core.Combat;
using Game.Core.Diagnostics;
using Game.Core.Projectiles;
using Game.Core.Sessions;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Game.Client.GameStates;

public sealed partial class PlayingState
{
    private LoadedGameSession CreateGoldenSession()
    {
        var fixture = _goldenFixture!;
        return new GameSessionBootstrapper().LoadOrCreate(new GameSessionBootstrapRequest(
            ClientPaths.FindGameProjectPaths().ProjectRoot,
            ClientPaths.WorldSaveDirectory(fixture.SceneId, fixture.Seed),
            fixture.Seed, fixture.SceneId, _pauseMenu.Settings) { LoadExistingSave = false }).Session;
    }

    private void InitializeGoldenScene()
    {
        if (_goldenFixture is not { } fixture || _simulation is null || _player is null || _content is null)
        {
            return;
        }
        // Only fixture inputs change. Generation, simulation, lighting and rendering
        // still use their ordinary implementation and the session's one simulation.
        _pauseMenu.ApplyRuntimeSettings(_pauseMenu.Settings with
        {
            Video = _pauseMenu.Settings.Video with
            {
                Width = fixture.Width, Height = fixture.Height, Fullscreen = false,
                VSync = false, LowLatencyFramePacing = false, FrameRateLimit = 60
            },
            Gameplay = _pauseMenu.Settings.Gameplay with { MaxActiveEnemies = 0, CameraZoom = fixture.Zoom }
        });
        _simulation.ConfigureOptions(_simulation.Options with { WorldTimeRateMultiplier = 0, PlayerInvulnerable = true });
        _goldenCamera = new Vector2(fixture.CameraX ?? _player.Body.Center.X, fixture.CameraY ?? _player.Body.Center.Y);
        if (Vector2.Distance(_goldenCamera, new Vector2(_player.Body.Center.X, _player.Body.Center.Y)) > 256)
        {
            throw new InvalidDataException("Golden camera anchor is outside the materialized scene; review the fixture coordinates.");
        }
        _camera.Position = _goldenCamera;
        _camera.Zoom = fixture.Zoom;
        _camera.Recalculate(_lastViewportBounds);
        if (!fixture.DynamicLights)
        {
            return;
        }
        AddGoldenProjectile("arcane_mote", DamageType.Magic, -96, -6);
        AddGoldenProjectile("firebolt", DamageType.Fire, 96, 6);
    }

    private void AddGoldenProjectile(string id, DamageType damageType, float offsetX, float velocityX)
    {
        var definition = _content!.Projectiles.GetById(id) with
        {
            DamageType = damageType, Damage = 0, Speed = Math.Abs(velocityX), Lifetime = 60,
            Gravity = 0, DragPerSecond = 0, HomingTurnRateRadiansPerSecond = 0,
            TileCollisionBehavior = ProjectileTileCollisionBehavior.Ignore,
            EntityCollisionBehavior = ProjectileEntityCollisionBehavior.Ignore
        };
        _entities.Add(new ProjectileEntity(definition,
            new System.Numerics.Vector2(_goldenCamera.X + offsetX, _goldenCamera.Y - 24),
            new System.Numerics.Vector2(velocityX, 0)));
    }

    private void UpdateGoldenFrame(double deltaSeconds)
    {
        _gameplayFeedback.Update((float)deltaSeconds);
        EnsurePresentationResources(_pauseMenu.Settings);
        _screenSpaceEffects.SetSceneCaptureRequired(false);
        _camera.Position = _goldenCamera;
        _camera.Zoom = _goldenFixture!.Zoom;
        _camera.Recalculate(_lastViewportBounds);
        SuppressGameplayInput();
        // The existing forced-biome setup materialized the fixed visible+margin
        // chunk set before measured ticks. Static fixtures issue no streaming jobs.
    }

    internal bool GoldenSceneContractPassed()
    {
        return _goldenFixture is { } fixture && _frameSnapshot is { } frame &&
            frame.TickNumber == fixture.Frames &&
            _camera.Position == _goldenCamera && _camera.Zoom == fixture.Zoom &&
            (fixture.BiomeId == "forest" ? frame.LivingWorld.BiomeId == "forest" : frame.LivingWorld.CaveProfileId == fixture.BiomeId) &&
            frame.LivingWorld.Weather == fixture.Weather &&
            Math.Abs(frame.WorldTime.NormalizedTimeOfDay - fixture.TimeOfDay) < 1e-9 &&
            (!fixture.DynamicLights || _goldenDynamicLightsCollected == 2);
    }

    internal void CaptureGoldenScene(Dictionary<string, object> data)
    {
        var frame = _frameSnapshot!;
        data["SimulationTick"] = frame.TickNumber;
        data["CameraX"] = _camera.Position.X;
        data["CameraY"] = _camera.Position.Y;
        data["CameraZoom"] = _camera.Zoom;
        data["TimeOfDay"] = frame.WorldTime.NormalizedTimeOfDay;
        data["Weather"] = frame.LivingWorld.Weather.ToString();
        data["WeatherIntensity"] = frame.LivingWorld.WeatherIntensity;
        data["Wind"] = frame.LivingWorld.Wind;
        data["CloudCover"] = frame.LivingWorld.CloudCover;
        data["Day"] = frame.WorldTime.Day;
        data["BiomeId"] = frame.LivingWorld.BiomeId;
        data["CaveProfileId"] = frame.LivingWorld.CaveProfileId ?? "";
        data["PointLightsUsed"] = _lightingRenderer.LastTelemetry.PointLightsUsed;
        data["DynamicLightsCollected"] = _goldenDynamicLightsCollected;
        data["LoadedChunks"] = _world!.Chunks.Count;
        data["SettingsFingerprint"] = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(_pauseMenu.Settings)));
        data["StateHash"] = SimulationStateHasher.Compute(_world, _player!, _inventory!, _entities, _worldTime,
            _farmPlots, _equipmentLoadout, _simulation!.RandomStreams).ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }
}
