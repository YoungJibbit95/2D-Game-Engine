using Game.Core.DeveloperTools;
using Game.Core.Projectiles;
using Game.Core.Runtime;
using Game.Core.Saving;
using Game.Core.Settings;
using Game.Core.Weather;
using System.Globalization;

namespace Game.Client.GameStates;

public sealed partial class PlayingState
{
    private readonly ProjectileFactory _developerProjectileFactory = new();
    private IDisposable? _developerIntentSubscription;
    private bool _developerBackgroundEnabled = true;
    private bool _developerWeatherEnabled = true;
    private string? _developerLightingDebugView;

    private void InitializeDeveloperCommandRouting()
    {
        DisposeDeveloperCommandRouting();
        _developerIntentSubscription =
            _events.Subscribe<DeveloperCommandIntentRequestedEvent>(HandleDeveloperIntent);
    }

    private void DisposeDeveloperCommandRouting()
    {
        _developerIntentSubscription?.Dispose();
        _developerIntentSubscription = null;
    }

    private void HandleDeveloperIntent(DeveloperCommandIntentRequestedEvent request)
    {
        try
        {
            switch (request.Intent)
            {
                case TeleportPlayerIntent value: Teleport(value); break;
                case SetDeveloperMovementModeIntent value: SetMovementMode(value); break;
                case SetDeveloperSpeedIntent value: SetSpeed(value); break;
                case ReloadChunkIntent value: ReloadChunk(value); break;
                case SetSpawnRateIntent value: SetSpawnRate(value); break;
                case SetDebugViewIntent value: SetDebugView(value); break;
                case PerformanceRequestIntent value: HandlePerformance(value); break;
                case EventDiagnosticsRequestIntent value: HandleEvents(value); break;
                case WeatherOverrideRequestIntent value: SetWeather(value); break;
                case BiomeOverrideRequestIntent value: SetBiome(value); break;
                case DeveloperSaveRequestIntent value: Save(value); break;
                case SetRenderingFeatureIntent value: SetRenderFeature(value); break;
                case SetLightingDebugViewIntent value: SetLightingDebug(value); break;
                case GameRuleRequestIntent value: SetGameRule(value); break;
                case PlayerResourceRequestIntent value: SetResource(value); break;
                case SpawnProjectileRequestIntent value: SpawnProjectile(value); break;
                default: ConsoleInfo($"UNHANDLED: {request.CommandName}."); break;
            }
        }
        catch (Exception exception)
        {
            ConsoleInfo($"ERROR: {exception.Message}");
        }
    }

    private void Teleport(TeleportPlayerIntent intent)
    {
        var player = _player ?? throw new InvalidOperationException("Player unavailable.");
        player.Teleport(intent.Position);
        _camera.Position = intent.Position;
        _hasStreamingViewKey = false;
        ConsoleInfo($"APPLIED: teleport {intent.Position.X:0.##}, {intent.Position.Y:0.##}.");
    }

    private void SetMovementMode(SetDeveloperMovementModeIntent intent)
    {
        var simulation = RequireSimulation();
        var options = simulation.Options;
        options = intent.Mode switch
        {
            DeveloperMovementMode.GodMode => options with
            {
                PlayerInvulnerable = Toggle(options.PlayerInvulnerable, intent.Value)
            },
            DeveloperMovementMode.NoClip => options with
            {
                PlayerNoClip = Toggle(options.PlayerNoClip, intent.Value)
            },
            DeveloperMovementMode.Fly => options with
            {
                PlayerFreeFlight = Toggle(options.PlayerFreeFlight, intent.Value)
            },
            _ => options
        };
        simulation.ConfigureOptions(options);
        ConsoleInfo($"APPLIED: {intent.Mode}.");
    }

    private void SetSpeed(SetDeveloperSpeedIntent intent)
    {
        var simulation = RequireSimulation();
        var multiplier = intent.Reset ? 1f : intent.Multiplier;
        simulation.ConfigureOptions(simulation.Options with
        {
            PlayerMovementSpeedMultiplier = multiplier
        });
        ConsoleInfo($"APPLIED: speed x{multiplier:0.##}.");
    }

    private void ReloadChunk(ReloadChunkIntent intent)
    {
        var world = _world ?? throw new InvalidOperationException("World unavailable.");
        if (!world.TryGetChunk(intent.Position, out var chunk) || chunk is null)
        {
            ConsoleInfo("INFO: chunk is not loaded.");
            return;
        }

        if (chunk.IsDirty && !intent.Force)
        {
            ConsoleInfo("REJECTED: chunk is dirty; use force.");
            return;
        }

        if (!world.UnloadChunk(intent.Position, requireClean: !intent.Force))
        {
            ConsoleInfo("REJECTED: chunk could not be unloaded.");
            return;
        }

        _tilemapRenderer.ClearCache();
        _hasStreamingViewKey = false;
        _streamingUpdateElapsedSeconds = double.MaxValue;
        ConsoleInfo($"APPLIED: chunk {intent.Position.X}, {intent.Position.Y} reload.");
    }

    private void SetSpawnRate(SetSpawnRateIntent intent)
    {
        var simulation = RequireSimulation();
        var multiplier = intent.Reset ? 1f : intent.Multiplier;
        simulation.ConfigureOptions(simulation.Options with
        {
            EnemySpawnRateMultiplier = multiplier
        });
        ConsoleInfo($"APPLIED: spawn rate x{multiplier:0.##}.");
    }

    private void SetDebugView(SetDebugViewIntent intent)
    {
        var settings = _pauseMenu.Settings;
        var debug = settings.Debug;
        debug = intent.View switch
        {
            DebugView.Overlay => debug with
            {
                ShowDebugOverlay = Toggle(debug.ShowDebugOverlay, intent.Value)
            },
            DebugView.Collisions => debug with
            {
                ShowGrid = Toggle(debug.ShowGrid, intent.Value),
                ShowDebugOverlay = true
            },
            DebugView.Streaming => debug with
            {
                ShowStreamingMetrics = Toggle(debug.ShowStreamingMetrics, intent.Value),
                ShowDebugOverlay = true
            },
            _ => debug with
            {
                ShowRenderMetrics = Toggle(debug.ShowRenderMetrics, intent.Value),
                ShowDebugOverlay = true
            }
        };
        ApplySettings(settings with
        {
            Debug = debug,
            Rendering = settings.Rendering with { DrawDebugOverlays = true }
        });
        ConsoleInfo($"APPLIED: debug {intent.View}.");
    }

    private void HandlePerformance(PerformanceRequestIntent intent)
    {
        if (intent.Kind == PerformanceRequestKind.Reset)
        {
            _states.Performance.Clear();
            ConsoleInfo("APPLIED: performance telemetry cleared.");
            return;
        }

        var settings = _pauseMenu.Settings;
        ApplySettings(settings with
        {
            Debug = settings.Debug with
            {
                ShowPerformanceProfiler = true,
                ShowDebugOverlay = true
            }
        });
        if (intent.Kind == PerformanceRequestKind.Capture)
        {
            _states.Performance.ResetPeaks();
        }

        var metrics = _states.Performance.SnapshotSlowest(3);
        ConsoleInfo($"PERF: frame {_states.Performance.FrameIndex}, {metrics.Count} slow scopes.");
        for (var index = 0; index < metrics.Count; index++)
        {
            var metric = metrics[index];
            ConsoleInfo(
                $"{metric.Name}: {metric.AverageMilliseconds:0.###}ms avg, " +
                $"{metric.PeakMilliseconds:0.###}ms peak, {metric.LastAllocatedBytes}B.");
        }
    }

    private void HandleEvents(EventDiagnosticsRequestIntent intent)
    {
        if (intent.Kind == EventDiagnosticsRequestKind.Clear)
        {
            _eventJournal?.Clear();
        }
        else if (intent.Kind == EventDiagnosticsRequestKind.Watch)
        {
            _eventJournalOverlay.Watch(intent.EventName!);
        }
        else if (intent.Kind == EventDiagnosticsRequestKind.Unwatch)
        {
            _eventJournalOverlay.ClearWatch();
        }

        var settings = _pauseMenu.Settings;
        ApplySettings(settings with
        {
            Debug = settings.Debug with { ShowEventJournal = true, ShowDebugOverlay = true }
        });
        ConsoleInfo($"EVENTS: {_eventJournal?.Count ?? 0}; filter {_eventJournalOverlay.WatchedEventName ?? "none"}.");
    }

    private void SetWeather(WeatherOverrideRequestIntent intent)
    {
        var runtime = RequireSimulation().LivingWorld;
        if (intent.Kind == WeatherOverrideRequestKind.Status)
        {
            ConsoleInfo($"WEATHER: {runtime.WeatherOverrideKind?.ToString() ?? "biome-driven"}.");
            return;
        }

        if (intent.Kind == WeatherOverrideRequestKind.Reset)
        {
            runtime.ClearWeatherOverride();
            ConsoleInfo("APPLIED: biome weather.");
            return;
        }

        if (!Enum.TryParse<WeatherKind>(intent.WeatherId, true, out var weather))
        {
            throw new InvalidOperationException($"Unknown weather '{intent.WeatherId}'.");
        }

        runtime.SetWeatherOverride(weather, Math.Clamp(intent.Intensity ?? 1f, 0f, 1f));
        ConsoleInfo($"APPLIED: weather {weather} {runtime.WeatherOverrideIntensity:0.##}.");
    }

    private void SetBiome(BiomeOverrideRequestIntent intent)
    {
        var runtime = RequireSimulation().LivingWorld;
        if (intent.Kind == BiomeOverrideRequestKind.Status)
        {
            ConsoleInfo($"BIOME: {_frameSnapshot?.LivingWorld.BiomeId}; override {runtime.BiomeOverrideId ?? "none"}.");
        }
        else if (intent.Kind == BiomeOverrideRequestKind.Reset)
        {
            runtime.ClearBiomeOverride();
            ConsoleInfo("APPLIED: regional biome.");
        }
        else
        {
            runtime.SetBiomeOverride(intent.BiomeId!);
            ConsoleInfo($"APPLIED: biome {runtime.BiomeOverrideId}.");
        }
    }

    private void Save(DeveloperSaveRequestIntent intent)
    {
        if (_world is null || _player is null || _inventory is null ||
            string.IsNullOrWhiteSpace(_worldSaveDirectory))
        {
            throw new InvalidOperationException("Session has no writable save directory.");
        }

        var mode = intent.Mode == DeveloperSaveMode.Full
            ? WorldSaveMode.AllChunks
            : WorldSaveMode.DirtyChunksOnly;
        _lastSave = _saves.Save(
            new GameSaveRequest(_world, _player, _inventory, _entities)
            {
                TileEntities = _tileEntities,
                FarmPlots = _farmPlots,
                EquipmentLoadout = _equipmentLoadout,
                CharacterAppearance = _characterEditorOverlay.Appearance,
                WorldTime = _worldTime,
                RandomStreams = _simulation?.RandomStreams,
                WorldEventState = _simulation?.LivingWorld.CaptureWorldEventState()
            },
            _worldSaveDirectory,
            new GameSaveCoordinatorOptions
            {
                ChunkStorageMode = WorldChunkStorageMode.RegionFiles,
                WorldSaveMode = mode,
                PlayerDisplayName = _world.Metadata.Name
            },
            GameSaveReason.Manual,
            _events);
        _saves.ResetAutosaveTimer();
        ConsoleInfo($"APPLIED: {intent.Mode} save ({_lastSave.WorldChunksConsidered} chunks).");
    }

    private void SetRenderFeature(SetRenderingFeatureIntent intent)
    {
        var settings = _pauseMenu.Settings;
        var rendering = settings.Rendering;
        var debug = settings.Debug;
        switch (intent.FeatureId.ToLowerInvariant())
        {
            case "lighting":
                rendering = rendering with
                {
                    DrawLightingOverlay = Toggle(rendering.DrawLightingOverlay, intent.Value)
                };
                break;
            case "shadows":
                {
                    var enabled = Toggle(rendering.ShadowQuality > 0, intent.Value);
                    rendering = rendering with
                    {
                        ShadowQuality = enabled ? Math.Max(2, rendering.ShadowQuality) : 0,
                        SoftShadows = enabled
                    };
                    break;
                }
            case "reflections":
                {
                    var enabled = Toggle(rendering.ScreenSpaceReflections, intent.Value);
                    rendering = rendering with
                    {
                        ScreenSpaceReflections = enabled,
                        ReflectionQuality = enabled ? Math.Max(1, rendering.ReflectionQuality) : 0
                    };
                    break;
                }
            case "raytracing":
                {
                    var enabled = Toggle(rendering.SunLightShafts, intent.Value);
                    rendering = rendering with
                    {
                        SunLightShafts = enabled,
                        AmbientOcclusion = enabled,
                        RaymarchStepBudget = enabled ? Math.Max(24, rendering.RaymarchStepBudget) : 4
                    };
                    break;
                }
            case "particles":
                {
                    var enabled = Toggle(rendering.ParticleQuality > 0, intent.Value);
                    rendering = rendering with
                    {
                        ParticleQuality = enabled ? Math.Max(2, rendering.ParticleQuality) : 0
                    };
                    break;
                }
            case "background":
                _developerBackgroundEnabled = Toggle(_developerBackgroundEnabled, intent.Value);
                break;
            case "weather":
                _developerWeatherEnabled = Toggle(_developerWeatherEnabled, intent.Value);
                break;
            case "postfx":
                rendering = rendering with
                {
                    PostProcessingEnabled = Toggle(rendering.PostProcessingEnabled, intent.Value)
                };
                break;
            case "ui-debug":
                debug = debug with
                {
                    ShowDebugOverlay = Toggle(debug.ShowDebugOverlay, intent.Value)
                };
                break;
            default:
                throw new InvalidOperationException($"Unknown render feature '{intent.FeatureId}'.");
        }

        ApplySettings(settings with { Rendering = rendering, Debug = debug });
        ConsoleInfo($"APPLIED: render {intent.FeatureId}.");
    }

    private void SetLightingDebug(SetLightingDebugViewIntent intent)
    {
        var active = string.Equals(_developerLightingDebugView, intent.ViewId, StringComparison.OrdinalIgnoreCase);
        _developerLightingDebugView = Toggle(active, intent.Value) ? intent.ViewId : null;
        var settings = _pauseMenu.Settings;
        ApplySettings(settings with
        {
            Debug = settings.Debug with { ShowDebugOverlay = true, ShowRenderMetrics = true }
        });
        ConsoleInfo($"APPLIED: lighting debug {_developerLightingDebugView ?? "off"}.");
    }

    private void SetGameRule(GameRuleRequestIntent intent)
    {
        var simulation = RequireSimulation();
        if (intent.Kind == GameRuleRequestKind.Read)
        {
            ConsoleInfo(DescribeRule(intent.RuleId, simulation.Options));
            return;
        }

        var defaults = GameSimulationOptions.Default;
        var options = simulation.Options;
        var reset = intent.Kind == GameRuleRequestKind.Reset;
        switch (intent.RuleId.ToLowerInvariant())
        {
            case "enemy-spawning":
                options = options with
                {
                    EnemySpawnRateMultiplier = reset ? defaults.EnemySpawnRateMultiplier : Multiplier(intent, 0f, 10f)
                };
                break;
            case "time-flow":
                options = options with
                {
                    WorldTimeRateMultiplier = reset ? defaults.WorldTimeRateMultiplier : Multiplier(intent, 0f, 20f)
                };
                break;
            case "mana-cost":
                options = options with
                {
                    PlayerManaCostMultiplier = reset ? defaults.PlayerManaCostMultiplier : Multiplier(intent, 0f, 10f)
                };
                break;
            case "mining-speed":
                options = options with
                {
                    PlayerMiningSpeedMultiplier = reset ? defaults.PlayerMiningSpeedMultiplier : Multiplier(intent, 0.05f, 20f)
                };
                break;
            case "friendly-fire":
                options = options with
                {
                    FriendlyFireEnabled = reset ? defaults.FriendlyFireEnabled : Boolean(intent.Value, options.FriendlyFireEnabled)
                };
                break;
            case "weather":
                if (reset || Boolean(intent.Value, true))
                {
                    simulation.LivingWorld.ClearWeatherOverride();
                }
                else
                {
                    simulation.LivingWorld.SetWeatherOverride(WeatherKind.Clear, 0f);
                }

                ConsoleInfo("APPLIED: weather rule.");
                return;
            case "building-range":
                {
                    var settings = _pauseMenu.Settings;
                    var defaultReach = GameSettings.CreateDefault().Gameplay.InteractionReachPixels;
                    var multiplier = reset ? 1f : Multiplier(intent, 0.25f, 5f);
                    ApplySettings(settings with
                    {
                        Gameplay = settings.Gameplay with { InteractionReachPixels = defaultReach * multiplier }
                    });
                    ConsoleInfo($"APPLIED: building-range x{multiplier:0.##}.");
                    return;
                }
            default:
                throw new InvalidOperationException($"Rule '{intent.RuleId}' is not runtime-writable.");
        }

        simulation.ConfigureOptions(options);
        ConsoleInfo($"APPLIED: {DescribeRule(intent.RuleId, options)}");
    }

    private static string DescribeRule(string id, GameSimulationOptions options) =>
        id.ToLowerInvariant() switch
        {
            "enemy-spawning" => $"enemy-spawning = {options.EnemySpawnRateMultiplier:0.##}",
            "time-flow" => $"time-flow = {options.WorldTimeRateMultiplier:0.##}",
            "mana-cost" => $"mana-cost = {options.PlayerManaCostMultiplier:0.##}",
            "mining-speed" => $"mining-speed = {options.PlayerMiningSpeedMultiplier:0.##}",
            "friendly-fire" => $"friendly-fire = {options.FriendlyFireEnabled}",
            _ => $"{id} = client-managed"
        };

    private void SetResource(PlayerResourceRequestIntent intent)
    {
        var player = _player ?? throw new InvalidOperationException("Player unavailable.");
        if (intent.Resource == PlayerResourceKind.Health)
        {
            var health = player.HealthComponent;
            if (intent.Operation == PlayerResourceOperation.Fill) health.RestoreFull();
            if (intent.Operation == PlayerResourceOperation.Set) health.SetCurrent(Amount(intent.Amount));
            if (intent.Operation == PlayerResourceOperation.Add)
                health.SetCurrent(SaturatingAdd(health.Current, Amount(intent.Amount), health.Max));
            ConsoleInfo($"HEALTH: {health.Current}/{health.Max}.");
            return;
        }

        var mana = player.ManaComponent;
        if (intent.Operation == PlayerResourceOperation.Fill) mana.RestoreFull();
        if (intent.Operation == PlayerResourceOperation.Set) mana.SetCurrent(Amount(intent.Amount));
        if (intent.Operation == PlayerResourceOperation.Add)
            mana.SetCurrent(SaturatingAdd(mana.Current, Amount(intent.Amount), mana.Max));
        ConsoleInfo($"MANA: {mana.Current}/{mana.Max}.");
    }

    private void SpawnProjectile(SpawnProjectileRequestIntent intent)
    {
        if (_content is null || !_content.Projectiles.TryGetById(intent.ProjectileId, out var definition))
        {
            throw new InvalidOperationException($"Projectile '{intent.ProjectileId}' is not registered.");
        }

        var projectile = _developerProjectileFactory.Create(
            definition,
            intent.Position,
            intent.Direction,
            _player?.Id,
            ownerFaction: Game.Core.Entities.EntityFaction.Friendly);
        _entities.Add(projectile);
        ConsoleInfo($"APPLIED: projectile {intent.ProjectileId} entity {projectile.Id}.");
    }

    private LivingWorldFrameSnapshot ResolveDeveloperPresentation(LivingWorldFrameSnapshot snapshot) =>
        _developerWeatherEnabled
            ? snapshot
            : snapshot with
            {
                Weather = WeatherKind.Clear,
                WeatherIntensity = 0f,
                Wind = 0f,
                CloudCover = 0f
            };

    private GameSimulation RequireSimulation() =>
        _simulation ?? throw new InvalidOperationException("Simulation unavailable.");

    private void ApplySettings(GameSettings settings) => _pauseMenu.ApplyRuntimeSettings(settings);

    private void ConsoleInfo(string message) => _debugConsole.State.AddInformation(message);

    private static bool Toggle(bool current, DeveloperToggle value) =>
        value switch
        {
            DeveloperToggle.On => true,
            DeveloperToggle.Off => false,
            _ => !current
        };

    private static float Multiplier(GameRuleRequestIntent intent, float minimum, float maximum)
    {
        if (intent.Value is not null &&
            float.TryParse(intent.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            float.IsFinite(value) &&
            value >= minimum &&
            value <= maximum)
        {
            return value;
        }

        if (TryBoolean(intent.Value, out var enabled))
        {
            return enabled ? 1f : 0f;
        }

        throw new InvalidOperationException(
            $"{intent.RuleId} must be {minimum:0.##}-{maximum:0.##}, or on/off.");
    }

    private static bool Boolean(string? value, bool current)
    {
        if (value?.Equals("toggle", StringComparison.OrdinalIgnoreCase) == true) return !current;
        if (TryBoolean(value, out var result)) return result;
        throw new InvalidOperationException("Value must be on/off, true/false, 1/0, or toggle.");
    }

    private static bool TryBoolean(string? value, out bool result)
    {
        if (value is not null &&
            (value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1"))
        {
            result = true;
            return true;
        }

        if (value is not null &&
            (value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0"))
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    private static int Amount(float? value)
    {
        if (value is null || !float.IsFinite(value.Value) || value.Value < 0f)
            throw new InvalidOperationException("Resource amount must be finite and non-negative.");
        return value.Value >= int.MaxValue ? int.MaxValue : (int)MathF.Round(value.Value);
    }

    private static int SaturatingAdd(int current, int addition, int maximum) =>
        (int)Math.Min(maximum, (long)current + addition);
}
