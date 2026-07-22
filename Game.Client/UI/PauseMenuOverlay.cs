using Game.Client.Input;
using Game.Client.Rendering;
using Game.Core.Settings;
using Game.Core.UI;
using Game.Core.UI.Animation;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class PauseMenuOverlay
{
    private const int TabRegionBase = 1_000;
    private const int OptionRegionBase = 2_000;
    private const int ControlRegionBase = 3_000;
    private static readonly string[] FrameRateChoices = ["UNLIMITED", "30 FPS", "60 FPS", "90 FPS", "120 FPS", "144 FPS", "165 FPS", "240 FPS", "360 FPS"];

    private readonly GameSettingsService _settingsService = new();
    private readonly string _settingsPath;
    private readonly Action _resume;
    private readonly Action _mainMenu;
    private readonly Action _exitGame;
    private readonly Action<GameSettings> _settingsChanged;
    private readonly string _title;
    private readonly string _resumeLabel;
    private readonly UiAnimationPlayer _openAnimation = new();
    private readonly List<MenuSection> _sections = new();
    private readonly List<HitZone> _tabHitZones = new();
    private readonly List<HitZone> _optionHitZones = new();
    private readonly List<ControlHitZone> _controlHitZones = new();
    private readonly List<UiHitRegion> _pointerRegions = new();
    private readonly List<PixelUiMotionState> _controlMotions = new();
    private readonly UiPointerRouter _pointer = new();
    private readonly UiGamepadNavigator _gamepad = new();
    private readonly PixelUiMotionState _tooltipMotion = new();
    private int _sectionIndex;
    private int _optionIndex;
    private int _hoveredSectionIndex = -1;
    private int _hoveredOptionIndex = -1;
    private int _openDropdownOptionIndex = -1;
    private int _tooltipOptionIndex = -1;
    private float _tooltipHoverSeconds;
    private int _draggingSliderOptionIndex = -1;
    private Point _mousePosition;
    private KeyCapture? _keyCapture;
    private string _status = "SETTINGS AUTOSAVE ON CHANGE";
    private bool _confirmResetKeybinds;
    private bool _focusVisible = true;
    private UiTypographyTokens _typography = UiTheme.Contract.Typography;

    public PauseMenuOverlay(
        string settingsPath,
        Action resume,
        Action mainMenu,
        Action exitGame,
        string title = "PAUSED",
        string resumeLabel = "RESUME",
        Action<GameSettings>? settingsChanged = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = settingsPath;
        _resume = resume;
        _mainMenu = mainMenu;
        _exitGame = exitGame;
        _settingsChanged = settingsChanged ?? (_ => { });
        _title = title;
        _resumeLabel = resumeLabel;
        Settings = LoadSettings();
        BuildSections();
        InitializeMotionStates();
    }

    public bool IsOpen { get; private set; }

    public GameSettings Settings { get; private set; }

    public void ApplyRuntimeSettings(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var validation = new GameSettingsValidator().Validate(settings);
        if (!validation.IsValid)
        {
            var issue = validation.Issues[0];
            throw new InvalidDataException($"{issue.Path}: {issue.Message}");
        }

        Settings = settings;
        _settingsChanged(settings);
        _status = "RUNTIME SETTINGS APPLIED";
    }

    public void Open()
    {
        IsOpen = true;
        _keyCapture = null;
        _openDropdownOptionIndex = -1;
        _pointer.Reset();
        _gamepad.Reset();
        ResetMotionStates();
        _focusVisible = true;
        _status = _title;
        var duration = Settings.Ui.ReducedMotion ? 0.001f : 0.18f;
        _openAnimation.Play(UiAnimationClip.SlideFadeIn(duration, -16f));
    }

    public void Close()
    {
        IsOpen = false;
        _keyCapture = null;
        _resume();
    }

    public bool Update(InputManager input, double deltaSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!IsOpen)
        {
            return false;
        }

        _gamepad.Update(deltaSeconds);
        _pointer.Update(input.MousePosition, input.IsLeftMouseDown, _pointerRegions);
        _openAnimation.Update((float)deltaSeconds, Settings.Ui.AnimationSpeed);

        if (_keyCapture is not null)
        {
            UpdateKeyCapture(input);
            UpdateMotionStates((float)deltaSeconds);
            return true;
        }

        if (input.IsBindingPressed(Settings.Input.KeyBindings.Pause) ||
            input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape) ||
            _gamepad.CancelPressed)
        {
            Close();
            return true;
        }

        UpdateMouse(input, (float)deltaSeconds);

        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Tab) ||
            input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.E) ||
            input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.PageDown) ||
            _gamepad.NextTabPressed)
        {
            MoveSection(1);
            _focusVisible = true;
        }
        else if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Q) ||
                 input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.PageUp) ||
                 _gamepad.PreviousTabPressed)
        {
            MoveSection(-1);
            _focusVisible = true;
        }

        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Down) ||
            input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.S) ||
            _gamepad.DownPressed)
        {
            MoveOption(1);
            _focusVisible = true;
        }
        else if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Up) ||
                 input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.W) ||
                 _gamepad.UpPressed)
        {
            MoveOption(-1);
            _focusVisible = true;
        }

        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Home))
        {
            _optionIndex = 0;
            CloseDropdown();
        }
        else if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.End))
        {
            _optionIndex = _sections[_sectionIndex].Options.Count - 1;
            CloseDropdown();
        }

        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Left) ||
            input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.A) ||
            _gamepad.LeftPressed)
        {
            CurrentOption()?.Change(-1);
            _focusVisible = true;
        }
        else if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Right) ||
                 input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.D) ||
                 _gamepad.RightPressed)
        {
            CurrentOption()?.Change(1);
            _focusVisible = true;
        }

        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Enter) ||
            input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Space) ||
            _gamepad.ConfirmPressed)
        {
            ActivateCurrentOption();
            _focusVisible = true;
        }

        UpdateMotionStates((float)deltaSeconds);
        return true;
    }

    public void Draw(RenderContext context)
    {
        if (!IsOpen)
        {
            return;
        }

        var palette = UiTheme.Resolve(Settings);
        _typography = UiTheme.ResolveContract(Settings).Typography;
        var fade = _openAnimation.GetValue(UiAnimationProperty.Opacity, 1f);
        var offsetY = (int)MathF.Round(_openAnimation.GetValue(UiAnimationProperty.OffsetY, 0f));
        UiTheme.DrawBackdrop(context, palette, Settings.Ui.MenuBackdropOpacity * fade, Settings);

        var panelWidth = Math.Min(1040, context.ViewportBounds.Width - 32);
        var panelHeight = Math.Min(650, context.ViewportBounds.Height - 28);
        var panel = new Rectangle(
            context.ViewportBounds.Width / 2 - panelWidth / 2,
            context.ViewportBounds.Height / 2 - panelHeight / 2 + offsetY,
            panelWidth,
            panelHeight);

        UiTheme.DrawPanel(context, panel, palette, Settings.Ui.PanelOpacity * fade, settings: Settings);
        UiTheme.DrawHeader(context, new Rectangle(panel.X + 1, panel.Y + 1, panel.Width - 2, 58), palette, settings: Settings);

        context.DebugText.Draw(new Vector2(panel.X + 22, panel.Y + 18), _title, palette.Accent, _typography.TitleScale);
        context.DebugText.Draw(new Vector2(panel.Right - 184, panel.Y + 24), "AUTOSAVE", palette.TextMuted, _typography.CaptionScale);
        DrawSaveIndicator(context, palette, new Rectangle(panel.Right - 80, panel.Y + 19, 54, 22), _typography.CaptionScale);

        _tabHitZones.Clear();
        _optionHitZones.Clear();
        _controlHitZones.Clear();
        DrawTabs(context, panel, palette);
        DrawOptions(context, panel, palette);

        var footer = new Rectangle(panel.X + 18, panel.Bottom - 51, panel.Width - 36, 34);
        PixelUiPrimitives.DrawTooltip(
            context,
            footer,
            palette,
            Math.Max(_tooltipMotion.Hover, _tooltipMotion.Focus),
            Settings.Ui.PanelOpacity,
            Settings);
        var footerText = _keyCapture is null
            ? TooltipText()
            : $"PRESS A KEY FOR {_keyCapture.Label}   ESC CANCEL";
        var footerTextLimit = CharactersThatFit(Math.Max(24, footer.Width - 184), _typography.CaptionScale);
        context.DebugText.Draw(
            new Vector2(footer.X + 10, footer.Y + 11),
            Abbreviate(footerText, footerTextLimit),
            _keyCapture is null ? palette.TextMuted : palette.Warning,
            _typography.CaptionScale);
        var statusLimit = CharactersThatFit(Math.Min(142, footer.Width / 4), _typography.CaptionScale);
        var shownStatus = Abbreviate(_status, statusLimit);
        context.DebugText.Draw(
            new Vector2(footer.Right - shownStatus.Length * 6 * _typography.CaptionScale - 10, footer.Y + 11),
            shownStatus,
            palette.Warning,
            _typography.CaptionScale);
        RebuildPointerRegions();
        UiTheme.DrawCursorAccent(context, _mousePosition, palette, Settings);
    }

    private GameSettings LoadSettings()
    {
        try
        {
            return _settingsService.LoadOrCreate(_settingsPath);
        }
        catch (InvalidDataException)
        {
            return GameSettings.CreateDefault();
        }
    }

    private void BuildSections()
    {
        _sections.Clear();
        _sections.Add(new MenuSection("GAMEPLAY", BuildGameplayOptions()));
        _sections.Add(new MenuSection("WORLD", BuildWorldOptions()));
        _sections.Add(new MenuSection("GRAPHICS", BuildGraphicsOptions()));
        _sections.Add(new MenuSection("RENDERING", BuildRenderingOptions()));
        _sections.Add(new MenuSection("UI", BuildUiOptions()));
        _sections.Add(new MenuSection("DEBUG", BuildDebugOptions()));
        _sections.Add(new MenuSection("AUDIO", BuildAudioOptions()));
        _sections.Add(new MenuSection("KEYBINDS", BuildKeybindOptions()));
        _sections.Add(new MenuSection("ACCESSIBILITY", BuildAccessibilityOptions()));
        _sections.Add(new MenuSection("SYSTEM", BuildSystemOptions()));
    }

    private IReadOnlyList<MenuOption> BuildGameplayOptions()
    {
        return
        [
            Number("AUTOSAVE MIN", () => Settings.Gameplay.AutosaveMinutes, 0.5f, 60f, 0.5f, value => SetGameplay(g => g with { AutosaveMinutes = value })),
            Number("MAX ENEMIES", () => Settings.Gameplay.MaxActiveEnemies, 0, 512, 8, value => SetGameplay(g => g with { MaxActiveEnemies = value })),
            Number("CAMERA ZOOM", () => Settings.Gameplay.CameraZoom, 0.5f, 6f, 0.25f, value => SetGameplay(g => g with { CameraZoom = value })),
            Number("INTERACTION REACH", () => Settings.Gameplay.InteractionReachPixels, 16f, 512f, 8f, value => SetGameplay(g => g with { InteractionReachPixels = value })),
            Toggle("SHOW TARGET TILE", () => Settings.Gameplay.ShowInteractionTarget, value => SetGameplay(g => g with { ShowInteractionTarget = value })),
            Toggle("HOLD TO MINE", () => Settings.Gameplay.HoldToMine, value => SetGameplay(g => g with { HoldToMine = value })),
            Toggle("HOLD TO BLOCK", () => Settings.Gameplay.HoldToBlock, value => SetGameplay(g => g with { HoldToBlock = value })),
            Toggle("PAUSE ON FOCUS LOST", () => Settings.Gameplay.PauseOnFocusLost, value => SetGameplay(g => g with { PauseOnFocusLost = value })),
            Number("SPAWN RATE", () => Settings.Gameplay.EnemySpawnRateMultiplier, 0f, 5f, 0.25f, value => SetGameplay(g => g with { EnemySpawnRateMultiplier = value })),
            Toggle("AUTO PICKUP", () => Settings.Gameplay.AutoPickupItems, value => SetGameplay(g => g with { AutoPickupItems = value })),
            Toggle("COMBAT LINE OF SIGHT", () => Settings.Gameplay.UseLineOfSightForCombat, value => SetGameplay(g => g with { UseLineOfSightForCombat = value })),
            Number("RESPAWN DELAY", () => Settings.Gameplay.RespawnDelaySeconds, 0f, 30f, 0.5f, value => SetGameplay(g => g with { RespawnDelaySeconds = value })),
            Number("CAMERA LOOK AHEAD", () => Settings.Gameplay.CameraLookAheadPixels, 0f, 256f, 8f, value => SetGameplay(g => g with { CameraLookAheadPixels = value })),
            Number("SCREEN SHAKE", () => Settings.Gameplay.ScreenShakeMultiplier, 0f, 4f, 0.1f, value => SetGameplay(g => g with { ScreenShakeMultiplier = value }))
        ];
    }

    private IReadOnlyList<MenuOption> BuildGraphicsOptions()
    {
        var resolutions = new[] { "960x540", "1280x720", "1600x900", "1920x1080", "2560x1440", "3840x2160" };
        return
        [
            Choice("RESOLUTION", CurrentResolution, resolutions, SetResolution),
            Toggle("FULLSCREEN", () => Settings.Video.Fullscreen, value => SetVideo(v => v with { Fullscreen = value })),
            Toggle("LOW LATENCY PACING", () => Settings.Video.LowLatencyFramePacing, value => SetVideo(v => v with { LowLatencyFramePacing = value })),
            Toggle("VSYNC", () => Settings.Video.VSync, value => SetVideo(v => v with { VSync = value })),
            Choice("FRAME LIMIT", CurrentFrameRateLimit, FrameRateChoices, SetFrameRateLimit, UiControlKind.Dropdown),
            Number("UI SCALE", () => Settings.Video.UiScale, 0.5f, 4f, 0.25f, value => SetVideo(v => v with { UiScale = value })),
            Number("RENDER SCALE", () => Settings.Video.RenderScale, 0.25f, 2f, 0.25f, value => SetVideo(v => v with { RenderScale = value }))
        ];
    }

    private IReadOnlyList<MenuOption> BuildWorldOptions()
    {
        var profiles = new[] { "small", "medium", "large" };
        return
        [
            Toggle("INFINITE HORIZONTAL", () => Settings.World.InfiniteHorizontalGeneration, value => SetWorld(w => w with { InfiniteHorizontalGeneration = value })),
            Choice("WORLD PROFILE", () => Settings.World.WorldProfileId, profiles, value => SetWorld(w => w with { WorldProfileId = value }), UiControlKind.Segmented),
            Number("CHUNK LOAD MARGIN", () => Settings.World.ChunkLoadMargin, 0, 16, 1, value => SetWorld(w => w with { ChunkLoadMargin = value })),
            Number("CHUNK UNLOAD MARGIN", () => Settings.World.ChunkUnloadMargin, Settings.World.ChunkLoadMargin, 32, 1, value => SetWorld(w => w with { ChunkUnloadMargin = Math.Max(value, Settings.World.ChunkLoadMargin) })),
            Toggle("KEEP DIRTY CHUNKS", () => Settings.World.KeepDirtyChunksLoaded, value => SetWorld(w => w with { KeepDirtyChunksLoaded = value })),
            Toggle("PRELOAD VERTICAL SLICE", () => Settings.World.PreloadFullVerticalSlice, value => SetWorld(w => w with { PreloadFullVerticalSlice = value })),
            Toggle("SAVE BEFORE UNLOAD", () => Settings.World.SaveChunksBeforeUnload, value => SetWorld(w => w with { SaveChunksBeforeUnload = value })),
            Number("STREAM OPS", () => Settings.World.StreamingBudgetChunksPerFrame, 1, 512, 8, value => SetWorld(w => w with { StreamingBudgetChunksPerFrame = value })),
            Number("LOAD JOBS", () => Settings.World.StreamingConcurrentLoads, 1, 32, 1, value => SetWorld(w => w with { StreamingConcurrentLoads = value })),
            Number("SAVE JOBS", () => Settings.World.StreamingConcurrentSaves, 1, 8, 1, value => SetWorld(w => w with { StreamingConcurrentSaves = value })),
            Number("APPLY QUEUE", () => Settings.World.StreamingApplyQueueLimit, 8, 2048, 8, value => SetWorld(w => w with { StreamingApplyQueueLimit = value })),
            Number("APPLY TIME MS", () => Settings.World.StreamingApplyBudgetMilliseconds, 0.25f, 33f, 0.25f, value => SetWorld(w => w with { StreamingApplyBudgetMilliseconds = value })),
            Number("APPLY BUDGET KB", () => Settings.World.StreamingApplyBudgetKilobytes, 64, 16384, 64, value => SetWorld(w => w with { StreamingApplyBudgetKilobytes = value })),
            Number("RETRY ATTEMPTS", () => Settings.World.StreamingRetryAttempts, 1, 16, 1, value => SetWorld(w => w with { StreamingRetryAttempts = value })),
            Number("RETRY INITIAL", () => Settings.World.StreamingRetryInitialBackoffUpdates, 1, 256, 1, value => SetWorld(w => w with
            {
                StreamingRetryInitialBackoffUpdates = value,
                StreamingRetryMaximumBackoffUpdates = Math.Max(value, w.StreamingRetryMaximumBackoffUpdates)
            })),
            Number("RETRY MAX", () => Settings.World.StreamingRetryMaximumBackoffUpdates, Settings.World.StreamingRetryInitialBackoffUpdates, 4096, 8, value => SetWorld(w => w with
            {
                StreamingRetryMaximumBackoffUpdates = Math.Max(value, w.StreamingRetryInitialBackoffUpdates)
            }))
        ];
    }

    private void SetWorld(Func<WorldSettings, WorldSettings> update)
    {
        Settings = Settings with { World = update(Settings.World) };
    }

    private IReadOnlyList<MenuOption> BuildRenderingOptions()
    {
        return
        [
            Toggle("DRAW LIQUIDS", () => Settings.Rendering.DrawLiquids, value => SetRendering(r => r with { DrawLiquids = value })),
            Toggle("LIGHTING OVERLAY", () => Settings.Rendering.DrawLightingOverlay, value => SetRendering(r => r with { DrawLightingOverlay = value })),
            Toggle("DEBUG OVERLAYS", () => Settings.Rendering.DrawDebugOverlays, value => SetRendering(r => r with { DrawDebugOverlays = value })),
            Toggle("POST PROCESSING", () => Settings.Rendering.PostProcessingEnabled, value => SetRendering(r => r with { PostProcessingEnabled = value })),
            Toggle("PIXEL SNAP", () => Settings.Rendering.PixelSnap, value => SetRendering(r => r with { PixelSnap = value })),
            Number("BLOOM", () => Settings.Rendering.BloomStrength, 0f, 1f, 0.05f, value => SetRendering(r => r with { BloomStrength = value })),
            Number("VIGNETTE", () => Settings.Rendering.VignetteStrength, 0f, 1f, 0.05f, value => SetRendering(r => r with { VignetteStrength = value })),
            Number("COLOR GRADE", () => Settings.Rendering.ColorGradeIntensity, 0f, 1f, 0.05f, value => SetRendering(r => r with { ColorGradeIntensity = value })),
            Choice("PARTICLE QUALITY", ParticleQualityLabel, new[] { "OFF", "LOW", "MEDIUM", "HIGH" }, SetParticleQuality, UiControlKind.Segmented),
            Number("LIQUID OPACITY", () => Settings.Rendering.LiquidOpacity, 0f, 1f, 0.05f, value => SetRendering(r => r with { LiquidOpacity = value })),
            Number("LIGHTING BLEND", () => Settings.Rendering.LightingBlendStrength, 0f, 1f, 0.05f, value => SetRendering(r => r with { LightingBlendStrength = value })),
            Number("CHUNK CACHE LIMIT", () => Settings.Rendering.MaxChunkRenderCacheEntries, 32, 4096, 32, value => SetRendering(r => r with { MaxChunkRenderCacheEntries = value })),
            Toggle("ENTITY INTERPOLATION", () => Settings.Rendering.EntityInterpolation, value => SetRendering(r => r with { EntityInterpolation = value })),
            Choice("CADENCE PRESET", () => PresentationCadenceUi.ResolvePreset(Settings.Rendering), PresentationCadenceUi.Presets, SetPresentationCadencePreset, UiControlKind.Segmented),
            Toggle("ADAPTIVE CADENCE", () => Settings.Rendering.AdaptivePresentationCadence, value => SetRendering(r => r with { AdaptivePresentationCadence = value })),
            Number("LIGHTING RATE HZ", () => Settings.Rendering.LightingUpdateRateHz, 10, 240, 5, value => SetRendering(r => r with { LightingUpdateRateHz = value })),
            Number("REFLECTION RATE HZ", () => Settings.Rendering.ReflectionUpdateRateHz, 10, 240, 5, value => SetRendering(r => r with { ReflectionUpdateRateHz = value })),
            Number("ATMOSPHERE RATE HZ", () => Settings.Rendering.AtmosphereUpdateRateHz, 10, 240, 5, value => SetRendering(r => r with { AtmosphereUpdateRateHz = value })),
            Number("SCENE CAPTURE RATE HZ", () => Settings.Rendering.SceneCaptureUpdateRateHz, 10, 240, 5, value => SetRendering(r => r with { SceneCaptureUpdateRateHz = value })),
            Choice("UI EFFECT QUALITY", UiEffectQualityLabel, new[] { "OFF", "LOW", "MEDIUM", "HIGH" }, SetUiEffectQuality, UiControlKind.Segmented),
            Number("BLUR RADIUS", () => Settings.Rendering.BlurRadiusPixels, 0, 24, 1, value => SetRendering(r => r with { BlurRadiusPixels = value }))
        ];
    }

    private IReadOnlyList<MenuOption> BuildUiOptions()
    {
        return
        [
            Choice("THEME", () => Settings.Ui.Theme, new[] { "Midnight", "Forest", "Ember" }, value => SetUi(ui => ui with { Theme = value }), UiControlKind.Segmented),
            Number("PANEL OPACITY", () => Settings.Ui.PanelOpacity, 0.35f, 1f, 0.05f, value => SetUi(ui => ui with { PanelOpacity = value })),
            Number("HUD OPACITY", () => Settings.Ui.HudOpacity, 0.35f, 1f, 0.05f, value => SetUi(ui => ui with { HudOpacity = value })),
            Number("BACKDROP OPACITY", () => Settings.Ui.MenuBackdropOpacity, 0f, 1f, 0.05f, value => SetUi(ui => ui with { MenuBackdropOpacity = value })),
            Number("ANIMATION SPEED", () => Settings.Ui.AnimationSpeed, 0.1f, 4f, 0.1f, value => SetUi(ui => ui with { AnimationSpeed = value })),
            Number("CORNER RADIUS", () => Settings.Ui.CornerRadiusPixels, 0, 16, 1, value => SetUi(ui => ui with { CornerRadiusPixels = value })),
            Number("BACKDROP BLUR", () => Settings.Ui.BackdropBlurStrength, 0f, 1f, 0.05f, value => SetUi(ui => ui with { BackdropBlurStrength = value })),
            Number("GLOW STRENGTH", () => Settings.Ui.GlowStrength, 0f, 1f, 0.05f, value => SetUi(ui => ui with { GlowStrength = value })),
            Number("TEXT SCALE", () => Settings.Ui.TextScale, 0.75f, 2f, 0.05f, value => SetUi(ui => ui with { TextScale = value })),
            Toggle("HIGH CONTRAST PANELS", () => Settings.Ui.HighContrastPanels, value => SetUi(ui => ui with { HighContrastPanels = value })),
            Toggle("LARGE CURSOR", () => Settings.Ui.LargeCursor, value => SetUi(ui => ui with { LargeCursor = value })),
            Toggle("REDUCED MOTION", () => Settings.Ui.ReducedMotion, value => SetUi(ui => ui with { ReducedMotion = value })),
            Toggle("COMPACT LISTS", () => Settings.Ui.CompactLists, value => SetUi(ui => ui with { CompactLists = value })),
            Toggle("CONTROL HINTS", () => Settings.Ui.ShowControlHints, value => SetUi(ui => ui with { ShowControlHints = value }))
        ];
    }

    private IReadOnlyList<MenuOption> BuildAudioOptions()
    {
        return
        [
            Number("MASTER", () => Settings.Audio.MasterVolume, 0f, 1f, 0.05f, value => SetAudio(a => a with { MasterVolume = value })),
            Number("MUSIC", () => Settings.Audio.MusicVolume, 0f, 1f, 0.05f, value => SetAudio(a => a with { MusicVolume = value })),
            Number("SFX", () => Settings.Audio.SfxVolume, 0f, 1f, 0.05f, value => SetAudio(a => a with { SfxVolume = value })),
            Number("UI", () => Settings.Audio.UiVolume, 0f, 1f, 0.05f, value => SetAudio(a => a with { UiVolume = value }))
        ];
    }

    private IReadOnlyList<MenuOption> BuildDebugOptions()
    {
        return
        [
            Toggle("SHOW DEBUG HUD", () => Settings.Debug.ShowDebugOverlay, value => SetDebug(d => d with { ShowDebugOverlay = value })),
            Toggle("SHOW TILE GRID", () => Settings.Debug.ShowGrid, value => SetDebug(d => d with { ShowGrid = value })),
            Toggle("RENDER DEBUG TEXT", () => Settings.Rendering.DrawDebugOverlays, value => SetRendering(r => r with { DrawDebugOverlays = value })),
            Toggle("SAVE METRICS", () => Settings.Debug.ShowSaveMetrics, value => SetDebug(d => d with { ShowSaveMetrics = value })),
            Toggle("STREAMING METRICS", () => Settings.Debug.ShowStreamingMetrics, value => SetDebug(d => d with { ShowStreamingMetrics = value })),
            Toggle("RENDER METRICS", () => Settings.Debug.ShowRenderMetrics, value => SetDebug(d => d with { ShowRenderMetrics = value })),
            Toggle("PERFORMANCE PROFILER", () => Settings.Debug.ShowPerformanceProfiler, value => SetDebug(d => d with { ShowPerformanceProfiler = value })),
            Toggle("ALLOCATION METRICS", () => Settings.Debug.ShowAllocationMetrics, value => SetDebug(d => d with { ShowAllocationMetrics = value })),
            Number("PROFILER ROWS", () => Settings.Debug.ProfilerMetricLimit, 3, 32, 1, value => SetDebug(d => d with { ProfilerMetricLimit = value })),
            Toggle("MOUSE TILE", () => Settings.Debug.ShowMouseTile, value => SetDebug(d => d with { ShowMouseTile = value })),
            Toggle("EVENT JOURNAL", () => Settings.Debug.ShowEventJournal, value => SetDebug(d => d with { ShowEventJournal = value }))
        ];
    }

    private IReadOnlyList<MenuOption> BuildKeybindOptions()
    {
        return
        [
            Key("MOVE LEFT", () => Settings.Input.KeyBindings.MoveLeft, value => SetKeyBindings(k => k with { MoveLeft = value })),
            Key("MOVE RIGHT", () => Settings.Input.KeyBindings.MoveRight, value => SetKeyBindings(k => k with { MoveRight = value })),
            Key("JUMP", () => Settings.Input.KeyBindings.Jump, value => SetKeyBindings(k => k with { Jump = value })),
            Key("FLY", () => Settings.Input.KeyBindings.Fly, value => SetKeyBindings(k => k with { Fly = value })),
            Key("GLIDE", () => Settings.Input.KeyBindings.Glide, value => SetKeyBindings(k => k with { Glide = value })),
            Key("PRIMARY USE", () => Settings.Input.KeyBindings.AttackPrimary, value => SetKeyBindings(k => k with { AttackPrimary = value })),
            Key("SECONDARY USE", () => Settings.Input.KeyBindings.AttackSecondary, value => SetKeyBindings(k => k with { AttackSecondary = value })),
            Key("INVENTORY", () => Settings.Input.KeyBindings.OpenInventory, value => SetKeyBindings(k => k with { OpenInventory = value })),
            Key("CRAFTING", () => Settings.Input.KeyBindings.OpenCrafting, value => SetKeyBindings(k => k with { OpenCrafting = value })),
            Key("CHARACTER EDITOR", () => Settings.Input.KeyBindings.OpenCharacterEditor, value => SetKeyBindings(k => k with { OpenCharacterEditor = value })),
            Key("INTERACT", () => Settings.Input.KeyBindings.Interact, value => SetKeyBindings(k => k with { Interact = value })),
            Key("PAUSE", () => Settings.Input.KeyBindings.Pause, value => SetKeyBindings(k => k with { Pause = value })),
            Key("DEBUG CONSOLE", () => Settings.Input.KeyBindings.DebugConsole, value => SetKeyBindings(k => k with { DebugConsole = value })),
            Key("DEBUG TOGGLE", () => Settings.Input.KeyBindings.DebugToggle, value => SetKeyBindings(k => k with { DebugToggle = value })),
            Key("HOTBAR 1", () => Settings.Input.KeyBindings.Hotbar1, value => SetKeyBindings(k => k with { Hotbar1 = value })),
            Key("HOTBAR 2", () => Settings.Input.KeyBindings.Hotbar2, value => SetKeyBindings(k => k with { Hotbar2 = value })),
            Key("HOTBAR 3", () => Settings.Input.KeyBindings.Hotbar3, value => SetKeyBindings(k => k with { Hotbar3 = value })),
            Key("HOTBAR 4", () => Settings.Input.KeyBindings.Hotbar4, value => SetKeyBindings(k => k with { Hotbar4 = value })),
            Key("HOTBAR 5", () => Settings.Input.KeyBindings.Hotbar5, value => SetKeyBindings(k => k with { Hotbar5 = value })),
            Key("HOTBAR 6", () => Settings.Input.KeyBindings.Hotbar6, value => SetKeyBindings(k => k with { Hotbar6 = value })),
            Key("HOTBAR 7", () => Settings.Input.KeyBindings.Hotbar7, value => SetKeyBindings(k => k with { Hotbar7 = value })),
            Key("HOTBAR 8", () => Settings.Input.KeyBindings.Hotbar8, value => SetKeyBindings(k => k with { Hotbar8 = value })),
            Key("HOTBAR 9", () => Settings.Input.KeyBindings.Hotbar9, value => SetKeyBindings(k => k with { Hotbar9 = value })),
            Key("HOTBAR 10", () => Settings.Input.KeyBindings.Hotbar10, value => SetKeyBindings(k => k with { Hotbar10 = value })),
            Toggle("INVERT HOTBAR SCROLL", () => Settings.Input.InvertHotbarScroll, value => SetInput(i => i with { InvertHotbarScroll = value })),
            Number("MOUSE SENSITIVITY", () => Settings.Input.MouseSensitivity, 0.1f, 5f, 0.1f, value => SetInput(i => i with { MouseSensitivity = value })),
            Command("RESET KEYBINDS", ResetKeybinds)
        ];
    }

    private IReadOnlyList<MenuOption> BuildAccessibilityOptions()
    {
        return
        [
            Toggle("REDUCE SCREEN FLASH", () => Settings.Accessibility.ScreenFlashReduction, value => SetAccessibility(a => a with { ScreenFlashReduction = value })),
            Toggle("DISABLE CAMERA SHAKE", () => Settings.Accessibility.DisableCameraShake, value => SetAccessibility(a => a with { DisableCameraShake = value })),
            Toggle("HOLD ACTIONS AS TOGGLE", () => Settings.Accessibility.HoldActionsBecomeToggle, value => SetAccessibility(a => a with { HoldActionsBecomeToggle = value })),
            Toggle("HIGH CONTRAST OUTLINE", () => Settings.Accessibility.HighContrastInteractionOutline, value => SetAccessibility(a => a with { HighContrastInteractionOutline = value })),
            Toggle("COLORBLIND INDICATORS", () => Settings.Accessibility.ColorBlindFriendlyIndicators, value => SetAccessibility(a => a with { ColorBlindFriendlyIndicators = value })),
            Number("INTERFACE CONTRAST", () => Settings.Accessibility.InterfaceContrast, 0.5f, 2f, 0.05f, value => SetAccessibility(a => a with { InterfaceContrast = value })),
            Number("TOOLTIP DELAY", () => Settings.Accessibility.TooltipDelaySeconds, 0f, 2f, 0.05f, value => SetAccessibility(a => a with { TooltipDelaySeconds = value }))
        ];
    }

    private IReadOnlyList<MenuOption> BuildSystemOptions()
    {
        return
        [
            Command(_resumeLabel, Close),
            Command("SAVE SETTINGS", SaveSettings),
            Command("BACK TO MAIN MENU", _mainMenu),
            Command("EXIT GAME", _exitGame)
        ];
    }

    private void UpdateKeyCapture(InputManager input)
    {
        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape) || _gamepad.CancelPressed)
        {
            _status = "KEYBIND CANCELLED";
            _keyCapture = null;
            return;
        }

        var pressed = input.FirstPressedKeyName();
        if (string.IsNullOrWhiteSpace(pressed))
        {
            return;
        }

        var capture = _keyCapture;
        if (capture is null)
        {
            return;
        }

        capture.Setter(pressed);
        var conflict = FindBindingConflict(capture.Label, pressed);
        _status = conflict is null
            ? $"{capture.Label} = {pressed}"
            : $"{capture.Label} = {pressed}  CONFLICT: {conflict}";
        _keyCapture = null;
    }

    private void UpdateMouse(InputManager input, float deltaSeconds)
    {
        _mousePosition = input.MousePosition;
        if (_draggingSliderOptionIndex >= 0 && !input.IsLeftMouseDown)
        {
            _draggingSliderOptionIndex = -1;
            SaveSettings();
        }

        _hoveredSectionIndex = DecodeTabRegion(_pointer.HoveredId);
        _hoveredOptionIndex = DecodeOptionRegion(_pointer.HoveredId);
        if (_hoveredOptionIndex < 0)
        {
            var hoveredControl = FindControlHit(_pointer.HoveredId);
            _hoveredOptionIndex = hoveredControl?.OptionIndex ?? -1;
        }

        if (_hoveredOptionIndex != _tooltipOptionIndex)
        {
            _tooltipOptionIndex = _hoveredOptionIndex;
            _tooltipHoverSeconds = 0;
        }
        else if (_hoveredOptionIndex >= 0)
        {
            _tooltipHoverSeconds += Math.Max(0, deltaSeconds);
        }

        if (input.ScrollDelta != 0 && _hoveredSectionIndex < 0)
        {
            MoveOption(input.ScrollDelta < 0 ? 1 : -1);
            _focusVisible = false;
        }

        if (input.IsLeftMousePressed && _pointer.HoveredId < 0)
        {
            CloseDropdown();
        }

        var pressedControl = FindControlHit(_pointer.PressedId);
        if (pressedControl is { Part: ControlPart.Slider } sliderHit && input.IsLeftMouseDown)
        {
            _optionIndex = sliderHit.OptionIndex;
            var option = CurrentOption();
            if (option is not null)
            {
                _draggingSliderOptionIndex = sliderHit.OptionIndex;
                option.SetFromNormalized(PixelUiInteraction.SliderNormalizedAt(sliderHit.Bounds, input.MousePosition.X));
                _status = $"{option.Label} {option.Value()}";
                _focusVisible = false;
            }
        }

        var clickedTab = DecodeTabRegion(_pointer.ClickedId);
        if (clickedTab >= 0)
        {
            _sectionIndex = clickedTab;
            _optionIndex = Math.Clamp(_optionIndex, 0, _sections[_sectionIndex].Options.Count - 1);
            CloseDropdown();
            _confirmResetKeybinds = false;
            _status = _sections[_sectionIndex].Title;
            _focusVisible = false;
            return;
        }

        var clickedControl = FindControlHit(_pointer.ClickedId);
        if (clickedControl is { } hit)
        {
            _optionIndex = hit.OptionIndex;
            var option = CurrentOption();
            if (option is null)
            {
                return;
            }

            if (hit.Part != ControlPart.Slider)
            {
                HandleControlClick(option, hit);
            }

            _focusVisible = false;
            return;
        }

        var clickedOption = DecodeOptionRegion(_pointer.ClickedId);
        if (clickedOption >= 0)
        {
            _optionIndex = clickedOption;
            _focusVisible = false;
            var option = CurrentOption();
            if (option is not null &&
                option.Kind is UiControlKind.Toggle or UiControlKind.Dropdown or UiControlKind.KeyBinding or UiControlKind.Command)
            {
                ActivateCurrentOption();
            }
            else
            {
                CloseDropdown();
            }
        }
    }

    private void DrawTabs(RenderContext context, Rectangle panel, UiPalette palette)
    {
        var tabX = panel.X + 18;
        var tabY = panel.Y + 74;
        var tabWidth = Math.Clamp(panel.Width / 5, 148, 184);
        var tabStep = Math.Max(18, Math.Min(39, (panel.Height - 143) / _sections.Count));
        for (var index = 0; index < _sections.Count; index++)
        {
            var title = _sections[index].Title;
            var bounds = new Rectangle(tabX, tabY + index * tabStep, tabWidth, Math.Max(15, tabStep - 5));
            var selected = index == _sectionIndex;
            var hovered = index == _hoveredSectionIndex;
            UiTheme.DrawButton(context, bounds, palette, selected, hovered, pressed: _pointer.IsPressed(TabRegionId(index)), settings: Settings);
            if (selected)
            {
                context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 4, bounds.Y + 5, 3, bounds.Height - 10), palette.Accent);
            }

            context.DebugText.Draw(new Vector2(bounds.X + 14, bounds.Y + Math.Max(4, (bounds.Height - 7 * _typography.CaptionScale) / 2)), title, selected ? palette.Text : palette.TextMuted, _typography.CaptionScale);
            _tabHitZones.Add(new HitZone(bounds, index));
        }
    }

    private void DrawOptions(RenderContext context, Rectangle panel, UiPalette palette)
    {
        var section = _sections[_sectionIndex];
        var list = section.Options;
        var navWidth = Math.Clamp(panel.Width / 5, 148, 184);
        var content = new Rectangle(panel.X + navWidth + 34, panel.Y + 74, panel.Width - navWidth - 52, panel.Height - 143);
        context.DebugText.Draw(new Vector2(content.X + 2, content.Y + 2), section.Title, palette.Text, _typography.BodyScale);
        context.DebugText.Draw(
            new Vector2(content.X + 2, content.Y + 25),
            Abbreviate(SectionDescription(section.Title), CharactersThatFit(content.Width - 4, _typography.CaptionScale)),
            palette.TextMuted,
            _typography.CaptionScale);

        var startY = content.Y + 48;
        var rowHeight = Math.Max(Settings.Ui.CompactLists ? 39 : 44, _typography.DenseLineHeight + 22);
        var visibleRows = Math.Max(1, (content.Bottom - startY) / rowHeight);
        var scroll = Math.Clamp(_optionIndex - visibleRows + 1, 0, Math.Max(0, list.Count - visibleRows));
        var end = Math.Min(list.Count, scroll + visibleRows);

        for (var index = scroll; index < end; index++)
        {
            var row = index - scroll;
            var option = list[index];
            var bounds = new Rectangle(content.X, startY + row * rowHeight, content.Width, rowHeight - 5);
            var selected = index == _optionIndex;
            var hovered = index == _hoveredOptionIndex;
            var pressed = _pointer.IsPressed(OptionRegionId(index));
            UiTheme.DrawButton(context, bounds, palette, selected, hovered, pressed: pressed, focused: _focusVisible && selected, settings: Settings);

            var controlWidth = Math.Clamp(bounds.Width / 2, 220, 350);
            context.DebugText.Draw(
                new Vector2(bounds.X + 12, bounds.Y + 14 + (pressed ? 1 : 0)),
                Abbreviate(option.Label, CharactersThatFit(bounds.Width - controlWidth - 30, _typography.CaptionScale)),
                selected ? palette.Text : palette.TextMuted,
                _typography.CaptionScale);
            DrawOptionControl(context, palette, bounds, option, index, selected);
            _optionHitZones.Add(new HitZone(bounds, index));
        }

        if (list.Count > visibleRows)
        {
            DrawScrollRail(context, palette, new Rectangle(content.Right - 4, startY, 3, content.Bottom - startY), scroll, visibleRows, list.Count);
        }

        DrawOpenDropdown(context, palette);
    }

    private void DrawOptionControl(
        RenderContext context,
        UiPalette palette,
        Rectangle row,
        MenuOption option,
        int optionIndex,
        bool selected)
    {
        var controlWidth = Math.Clamp(row.Width / 2, 220, 350);
        var bounds = new Rectangle(row.Right - controlWidth - 10, row.Y + 6, controlWidth, row.Height - 12);
        switch (option.Kind)
        {
            case UiControlKind.Toggle:
                DrawToggle(context, palette, bounds, option, optionIndex);
                break;
            case UiControlKind.Slider:
                DrawSlider(context, palette, bounds, option, optionIndex);
                break;
            case UiControlKind.Stepper:
                DrawStepper(context, palette, bounds, option, optionIndex);
                break;
            case UiControlKind.Segmented:
                DrawSegments(context, palette, bounds, option, optionIndex);
                break;
            case UiControlKind.Dropdown:
                DrawDropdown(context, palette, bounds, option, optionIndex);
                break;
            case UiControlKind.KeyBinding:
            case UiControlKind.Command:
                var primaryId = ControlRegionId(optionIndex, ControlPart.Primary, -1);
                PixelUiPrimitives.DrawCommand(
                    context,
                    bounds,
                    option.Value(),
                    ResolveCommandIcon(option),
                    palette,
                    new PixelUiState(
                        Hovered: bounds.Contains(_mousePosition),
                        Pressed: _pointer.IsPressed(primaryId),
                        Focused: _focusVisible && selected,
                        Selected: option.Kind == UiControlKind.Command && selected),
                    _controlMotions[optionIndex],
                    _typography,
                    Settings);
                _controlHitZones.Add(new ControlHitZone(bounds, optionIndex, ControlPart.Primary, -1));
                break;
        }
    }

    private void DrawToggle(RenderContext context, UiPalette palette, Rectangle bounds, MenuOption option, int optionIndex)
    {
        var on = string.Equals(option.Value(), "ON", StringComparison.OrdinalIgnoreCase);
        var geometry = PixelUiGeometry.Toggle(bounds, UiTheme.ResolveContract(Settings).Controls);
        var regionId = ControlRegionId(optionIndex, ControlPart.Primary, -1);
        PixelUiPrimitives.DrawToggle(
            context,
            bounds,
            option.Value(),
            on,
            palette,
            new PixelUiState(
                Hovered: geometry.HitBounds.Contains(_mousePosition),
                Pressed: _pointer.IsPressed(regionId),
                Focused: _focusVisible && optionIndex == _optionIndex,
                Selected: on),
            _controlMotions[optionIndex],
            _typography,
            Settings);
        _controlHitZones.Add(new ControlHitZone(geometry.HitBounds, optionIndex, ControlPart.Primary, -1));
    }

    private void DrawSlider(RenderContext context, UiPalette palette, Rectangle bounds, MenuOption option, int optionIndex)
    {
        var normalized = option.NormalizedValue();
        var geometry = PixelUiGeometry.Slider(bounds, normalized, UiTheme.ResolveContract(Settings).Controls);
        var regionId = ControlRegionId(optionIndex, ControlPart.Slider, -1);
        PixelUiPrimitives.DrawSlider(
            context,
            bounds,
            option.Value(),
            normalized,
            palette,
            new PixelUiState(
                Hovered: geometry.HitBounds.Contains(_mousePosition),
                Pressed: _pointer.IsPressed(regionId),
                Focused: _focusVisible && optionIndex == _optionIndex),
            _controlMotions[optionIndex],
            _typography,
            Settings);
        _controlHitZones.Add(new ControlHitZone(geometry.HitBounds, optionIndex, ControlPart.Slider, -1));
    }

    private void DrawStepper(RenderContext context, UiPalette palette, Rectangle bounds, MenuOption option, int optionIndex)
    {
        var geometry = PixelUiGeometry.Stepper(bounds, UiTheme.ResolveContract(Settings).Controls);
        var decrementId = ControlRegionId(optionIndex, ControlPart.Decrement, -1);
        var incrementId = ControlRegionId(optionIndex, ControlPart.Increment, -1);
        PixelUiPrimitives.DrawStepper(
            context,
            bounds,
            Abbreviate(option.Value(), CharactersThatFit(geometry.Value.Width - 12, _typography.CaptionScale)),
            palette,
            new PixelUiState(Focused: _focusVisible && optionIndex == _optionIndex),
            _controlMotions[optionIndex],
            geometry.Decrement.Contains(_mousePosition),
            _pointer.IsPressed(decrementId),
            geometry.Increment.Contains(_mousePosition),
            _pointer.IsPressed(incrementId),
            _typography,
            Settings);
        _controlHitZones.Add(new ControlHitZone(geometry.Decrement, optionIndex, ControlPart.Decrement, -1));
        _controlHitZones.Add(new ControlHitZone(geometry.Increment, optionIndex, ControlPart.Increment, -1));
    }

    private void DrawSegments(RenderContext context, UiPalette palette, Rectangle bounds, MenuOption option, int optionIndex)
    {
        var choices = option.Choices;
        if (choices is null || choices.Count == 0)
        {
            return;
        }

        var tokens = UiTheme.ResolveContract(Settings).Controls;
        for (var index = 0; index < choices.Count; index++)
        {
            var segment = PixelUiGeometry.Segment(bounds, index, choices.Count, tokens);
            var active = string.Equals(choices[index], option.Value(), StringComparison.OrdinalIgnoreCase);
            var regionId = ControlRegionId(optionIndex, ControlPart.Choice, index);
            PixelUiPrimitives.DrawSegment(
                context,
                segment,
                Abbreviate(choices[index], CharactersThatFit(segment.Width - 10, _typography.CaptionScale)),
                palette,
                new PixelUiState(
                    Hovered: segment.Contains(_mousePosition),
                    Pressed: _pointer.IsPressed(regionId),
                    Focused: _focusVisible && optionIndex == _optionIndex && active,
                    Selected: active),
                _controlMotions[optionIndex],
                _typography,
                Settings);
            _controlHitZones.Add(new ControlHitZone(segment, optionIndex, ControlPart.Choice, index));
        }
    }

    private void DrawDropdown(RenderContext context, UiPalette palette, Rectangle bounds, MenuOption option, int optionIndex)
    {
        var open = optionIndex == _openDropdownOptionIndex;
        UiTheme.DrawButton(context, bounds, palette, open, bounds.Contains(_mousePosition), pressed: _pointer.IsPressed(ControlRegionId(optionIndex, ControlPart.Dropdown, -1)), settings: Settings);
        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 8), Abbreviate(option.Value(), CharactersThatFit(bounds.Width - 40, _typography.CaptionScale)), palette.Text, _typography.CaptionScale);
        context.DebugText.Draw(new Vector2(bounds.Right - 22, bounds.Y + 8), open ? "^" : "v", palette.Warning, _typography.CaptionScale);
        _controlHitZones.Add(new ControlHitZone(bounds, optionIndex, ControlPart.Dropdown, -1));
    }

    private void DrawOpenDropdown(RenderContext context, UiPalette palette)
    {
        if (_openDropdownOptionIndex < 0 || _openDropdownOptionIndex >= _sections[_sectionIndex].Options.Count)
        {
            return;
        }

        var option = _sections[_sectionIndex].Options[_openDropdownOptionIndex];
        var choices = option.Choices;
        var anchor = _controlHitZones.LastOrDefault(zone => zone.OptionIndex == _openDropdownOptionIndex && zone.Part == ControlPart.Dropdown).Bounds;
        if (choices is null || choices.Count == 0 || anchor == Rectangle.Empty)
        {
            return;
        }

        var itemHeight = 29;
        var dropUp = anchor.Bottom + choices.Count * itemHeight > _optionHitZones.LastOrDefault().Bounds.Bottom + 18;
        var startY = dropUp ? anchor.Y - choices.Count * itemHeight : anchor.Bottom + 3;
        for (var index = 0; index < choices.Count; index++)
        {
            var choiceBounds = new Rectangle(anchor.X, startY + index * itemHeight, anchor.Width, itemHeight - 1);
            var selected = string.Equals(option.Value(), choices[index], StringComparison.OrdinalIgnoreCase);
            UiTheme.DrawButton(context, choiceBounds, palette, selected, choiceBounds.Contains(_mousePosition), pressed: _pointer.IsPressed(ControlRegionId(_openDropdownOptionIndex, ControlPart.Choice, index)), settings: Settings);
            context.DebugText.Draw(new Vector2(choiceBounds.X + 10, choiceBounds.Y + 9), Abbreviate(choices[index], CharactersThatFit(choiceBounds.Width - 20, _typography.CaptionScale)), selected ? palette.Text : palette.TextMuted, _typography.CaptionScale);
            _controlHitZones.Add(new ControlHitZone(choiceBounds, _openDropdownOptionIndex, ControlPart.Choice, index));
        }
    }

    private static void DrawScrollRail(RenderContext context, UiPalette palette, Rectangle rail, int scroll, int visibleRows, int totalRows)
    {
        UiTheme.DrawScrollRail(context, rail, scroll, visibleRows, totalRows, palette);
    }

    private static void DrawSaveIndicator(RenderContext context, UiPalette palette, Rectangle bounds, int textScale)
    {
        context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(palette.AccentSoft, 0.75f));
        UiTheme.DrawBorder(context, bounds, palette.Accent, 1);
        context.DebugText.Draw(new Vector2(bounds.X + 11, bounds.Y + 7), "ON", palette.Text, textScale);
    }

    private void HandleControlClick(MenuOption option, ControlHitZone hit)
    {
        switch (hit.Part)
        {
            case ControlPart.Decrement:
                option.Change(-1);
                break;
            case ControlPart.Increment:
                option.Change(1);
                break;
            case ControlPart.Choice:
                option.SelectChoice(hit.ChoiceIndex);
                CloseDropdown();
                break;
            case ControlPart.Dropdown:
                _openDropdownOptionIndex = _openDropdownOptionIndex == hit.OptionIndex ? -1 : hit.OptionIndex;
                break;
            case ControlPart.Primary:
                option.Activate();
                CloseDropdown();
                break;
        }
    }

    private void ActivateCurrentOption()
    {
        var option = CurrentOption();
        if (option is null)
        {
            return;
        }

        if (option.Kind == UiControlKind.Dropdown)
        {
            _openDropdownOptionIndex = _openDropdownOptionIndex == _optionIndex ? -1 : _optionIndex;
            return;
        }

        option.Activate();
    }

    private ControlHitZone? FindControlHit(int regionId)
    {
        if (regionId < ControlRegionBase)
        {
            return null;
        }

        for (var index = _controlHitZones.Count - 1; index >= 0; index--)
        {
            var hit = _controlHitZones[index];
            if (ControlRegionId(hit.OptionIndex, hit.Part, hit.ChoiceIndex) == regionId)
            {
                return hit;
            }
        }

        return null;
    }

    private void RebuildPointerRegions()
    {
        _pointerRegions.Clear();
        foreach (var tab in _tabHitZones)
        {
            _pointerRegions.Add(new UiHitRegion(TabRegionId(tab.Index), tab.Bounds));
        }

        foreach (var option in _optionHitZones)
        {
            _pointerRegions.Add(new UiHitRegion(OptionRegionId(option.Index), option.Bounds));
        }

        foreach (var control in _controlHitZones)
        {
            _pointerRegions.Add(new UiHitRegion(
                ControlRegionId(control.OptionIndex, control.Part, control.ChoiceIndex),
                control.Bounds));
        }
    }

    private void InitializeMotionStates()
    {
        var maximumOptions = 0;
        for (var sectionIndex = 0; sectionIndex < _sections.Count; sectionIndex++)
        {
            maximumOptions = Math.Max(maximumOptions, _sections[sectionIndex].Options.Count);
        }

        while (_controlMotions.Count < maximumOptions)
        {
            _controlMotions.Add(new PixelUiMotionState());
        }
    }

    private void UpdateMotionStates(float deltaSeconds)
    {
        var section = _sections[_sectionIndex];
        var pressedControl = FindControlHit(_pointer.PressedId);
        var tooltipDelay = Math.Clamp(Settings.Accessibility.TooltipDelaySeconds, 0f, 2f);
        for (var index = 0; index < _controlMotions.Count; index++)
        {
            if (index >= section.Options.Count)
            {
                _controlMotions[index].Update(default, deltaSeconds, Settings.Ui.AnimationSpeed, Settings.Ui.ReducedMotion);
                continue;
            }

            var option = section.Options[index];
            var toggleSelected = option.Kind == UiControlKind.Toggle &&
                string.Equals(option.Value(), "ON", StringComparison.OrdinalIgnoreCase);
            var state = new PixelUiState(
                Hovered: index == _hoveredOptionIndex,
                Pressed: _pointer.IsPressed(OptionRegionId(index)) || pressedControl?.OptionIndex == index,
                Focused: _focusVisible && index == _optionIndex,
                Selected: toggleSelected || index == _optionIndex);
            _controlMotions[index].Update(state, deltaSeconds, Settings.Ui.AnimationSpeed, Settings.Ui.ReducedMotion);
        }

        var tooltipReady = _focusVisible ||
            (_hoveredOptionIndex >= 0 && _tooltipHoverSeconds >= tooltipDelay);
        _tooltipMotion.Update(
            new PixelUiState(Hovered: tooltipReady, Focused: _focusVisible),
            deltaSeconds,
            Settings.Ui.AnimationSpeed,
            Settings.Ui.ReducedMotion);
    }

    private void ResetMotionStates()
    {
        for (var index = 0; index < _controlMotions.Count; index++)
        {
            _controlMotions[index].Reset();
        }

        _tooltipMotion.Reset();
    }

    private static int TabRegionId(int index)
    {
        return TabRegionBase + index;
    }

    private static int OptionRegionId(int index)
    {
        return OptionRegionBase + index;
    }

    private static int ControlRegionId(int optionIndex, ControlPart part, int choiceIndex)
    {
        return ControlRegionBase + optionIndex * 100 + (int)part * 10 + choiceIndex + 1;
    }

    private int DecodeTabRegion(int regionId)
    {
        var index = regionId - TabRegionBase;
        return index >= 0 && index < _sections.Count ? index : -1;
    }

    private int DecodeOptionRegion(int regionId)
    {
        var index = regionId - OptionRegionBase;
        return index >= 0 && index < _sections[_sectionIndex].Options.Count ? index : -1;
    }

    private void CloseDropdown()
    {
        _openDropdownOptionIndex = -1;
    }

    private string TooltipText()
    {
        var delay = Math.Clamp(Settings.Accessibility.TooltipDelaySeconds, 0f, 2f);
        var index = _tooltipHoverSeconds >= delay ? _hoveredOptionIndex : _optionIndex;
        var options = _sections[_sectionIndex].Options;
        return index >= 0 && index < options.Count ? options[index].Description : _status;
    }

    private void MoveSection(int direction)
    {
        _sectionIndex = ((_sectionIndex + direction) % _sections.Count + _sections.Count) % _sections.Count;
        _optionIndex = Math.Clamp(_optionIndex, 0, _sections[_sectionIndex].Options.Count - 1);
        CloseDropdown();
        _confirmResetKeybinds = false;
        _status = _sections[_sectionIndex].Title;
    }

    private void MoveOption(int direction)
    {
        var count = _sections[_sectionIndex].Options.Count;
        _optionIndex = ((_optionIndex + direction) % count + count) % count;
        CloseDropdown();
        _confirmResetKeybinds = false;
    }

    private MenuOption? CurrentOption()
    {
        var section = _sections[_sectionIndex];
        return _optionIndex >= 0 && _optionIndex < section.Options.Count
            ? section.Options[_optionIndex]
            : null;
    }

    private MenuOption Toggle(string label, Func<bool> get, Action<bool> set)
    {
        return new MenuOption(
            UiControlKind.Toggle,
            label,
            Describe(label),
            () => get() ? "ON" : "OFF",
            direction => Commit(() => set(!get())),
            () => Commit(() => set(!get())),
            () => get() ? 1f : 0f,
            normalized => Commit(() => set(normalized >= 0.5f)),
            null,
            null);
    }

    private MenuOption Number(string label, Func<float> get, float min, float max, float step, Action<float> set)
    {
        var range = new UiNumericRange(min, max, step);
        return new MenuOption(
            UiControlKind.Slider,
            label,
            Describe(label),
            () => get().ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            direction => Commit(() => set(range.StepBy(get(), direction))),
            () => Commit(() => set(range.StepBy(get(), 1))),
            () => range.Normalize(get()),
            normalized => set(range.ValueAt(normalized)),
            null,
            null);
    }

    private MenuOption Number(string label, Func<int> get, int min, int max, int step, Action<int> set)
    {
        var range = new UiNumericRange(min, max, step);
        return new MenuOption(
            UiControlKind.Stepper,
            label,
            Describe(label),
            () => get().ToString(System.Globalization.CultureInfo.InvariantCulture),
            direction => Commit(() => set((int)range.StepBy(get(), direction))),
            () => Commit(() => set((int)range.StepBy(get(), 1))),
            () => range.Normalize(get()),
            normalized => set((int)range.ValueAt(normalized)),
            null,
            null);
    }

    private MenuOption Choice(
        string label,
        Func<string> get,
        IReadOnlyList<string> choices,
        Action<string> set,
        UiControlKind kind = UiControlKind.Dropdown)
    {
        return new MenuOption(
            kind,
            label,
            Describe(label),
            get,
            direction => Commit(() =>
            {
                var current = IndexOf(choices, get());
                if (current < 0)
                {
                    current = 0;
                }

                var next = ((current + Math.Sign(direction)) % choices.Count + choices.Count) % choices.Count;
                set(choices[next]);
            }),
            () => Commit(() =>
            {
                var current = IndexOf(choices, get());
                var next = current < 0 ? 0 : (current + 1) % choices.Count;
                set(choices[next]);
            }),
            () => choices.Count <= 1 ? 0f : Math.Max(0, IndexOf(choices, get())) / (float)(choices.Count - 1),
            _ => { },
            choices,
            index => Commit(() => set(choices[Math.Clamp(index, 0, choices.Count - 1)])));
    }

    private MenuOption Key(string label, Func<string> get, Action<string> set)
    {
        return new MenuOption(
            UiControlKind.KeyBinding,
            label,
            Describe(label),
            get,
            _ => BeginKeyCapture(label, set),
            () => BeginKeyCapture(label, set),
            () => 0f,
            _ => { },
            null,
            null);
    }

    private static MenuOption Command(string label, Action action)
    {
        return new MenuOption(
            UiControlKind.Command,
            label,
            Describe(label),
            () => "SELECT",
            _ => action(),
            action,
            () => 0f,
            _ => { },
            null,
            null);
    }

    private void BeginKeyCapture(string label, Action<string> setter)
    {
        _keyCapture = new KeyCapture(label, value => Commit(() => setter(value)));
        _status = $"PRESS A KEY FOR {label}";
    }

    private void Commit(Action change)
    {
        change();
        SaveSettings();
    }

    private void SaveSettings()
    {
        try
        {
            _settingsService.Save(_settingsPath, Settings);
            _settingsChanged(Settings);
            _status = "SETTINGS SAVED";
        }
        catch (InvalidDataException ex)
        {
            _status = ex.Message.Split(Environment.NewLine).FirstOrDefault() ?? "SETTINGS INVALID";
        }
    }

    private void ResetKeybinds()
    {
        if (!_confirmResetKeybinds)
        {
            _confirmResetKeybinds = true;
            _status = "CONFIRM RESET KEYBINDS WITH ENTER OR CLICK";
            return;
        }

        Commit(() => SetInput(i => i with { KeyBindings = new KeyBindingSettings() }));
        _confirmResetKeybinds = false;
        _status = "KEYBINDS RESET";
    }

    private void SetResolution(string resolution)
    {
        var parts = resolution.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var width) ||
            !int.TryParse(parts[1], out var height))
        {
            return;
        }

        SetVideo(video => video with { Width = width, Height = height });
    }

    private string CurrentResolution()
    {
        return $"{Settings.Video.Width}x{Settings.Video.Height}";
    }

    private string CurrentFrameRateLimit()
    {
        return Settings.Video.FrameRateLimit == 0
            ? "UNLIMITED"
            : $"{Settings.Video.FrameRateLimit} FPS";
    }

    private void SetFrameRateLimit(string value)
    {
        if (string.Equals(value, "UNLIMITED", StringComparison.OrdinalIgnoreCase))
        {
            SetVideo(video => video with { FrameRateLimit = 0 });
            return;
        }

        var digits = value.AsSpan(0, Math.Max(0, value.IndexOf(' ')));
        if (int.TryParse(digits, out var limit))
        {
            SetVideo(video => video with { FrameRateLimit = limit });
        }
    }

    private string ParticleQualityLabel()
    {
        return Settings.Rendering.ParticleQuality switch
        {
            0 => "OFF",
            1 => "LOW",
            2 => "MEDIUM",
            _ => "HIGH"
        };
    }

    private void SetParticleQuality(string value)
    {
        var quality = value switch
        {
            "OFF" => 0,
            "LOW" => 1,
            "MEDIUM" => 2,
            _ => 3
        };

        SetRendering(rendering => rendering with { ParticleQuality = quality });
    }

    private string UiEffectQualityLabel()
    {
        return Settings.Rendering.UiEffectQuality switch
        {
            0 => "OFF",
            1 => "LOW",
            2 => "MEDIUM",
            _ => "HIGH"
        };
    }

    private void SetUiEffectQuality(string value)
    {
        var quality = value switch
        {
            "OFF" => 0,
            "LOW" => 1,
            "MEDIUM" => 2,
            _ => 3
        };
        SetRendering(rendering => rendering with { UiEffectQuality = quality });
    }

    private void SetVideo(Func<VideoSettings, VideoSettings> update)
    {
        Settings = Settings with { Video = update(Settings.Video) };
    }

    private void SetRendering(Func<RenderingSettings, RenderingSettings> update)
    {
        Settings = Settings with { Rendering = update(Settings.Rendering) };
    }

    private void SetPresentationCadencePreset(string preset)
    {
        SetRendering(rendering => PresentationCadenceUi.ApplyPreset(rendering, preset));
    }

    private void SetUi(Func<UiSettings, UiSettings> update)
    {
        Settings = Settings with { Ui = update(Settings.Ui) };
    }

    private void SetAudio(Func<AudioSettings, AudioSettings> update)
    {
        Settings = Settings with { Audio = update(Settings.Audio) };
    }

    private void SetGameplay(Func<GameplaySettings, GameplaySettings> update)
    {
        Settings = Settings with { Gameplay = update(Settings.Gameplay) };
    }

    private void SetInput(Func<InputSettings, InputSettings> update)
    {
        Settings = Settings with { Input = update(Settings.Input) };
    }

    private void SetDebug(Func<DebugSettings, DebugSettings> update)
    {
        Settings = Settings with { Debug = update(Settings.Debug) };
    }

    private void SetAccessibility(Func<AccessibilitySettings, AccessibilitySettings> update)
    {
        Settings = Settings with { Accessibility = update(Settings.Accessibility) };
    }

    private void SetKeyBindings(Func<KeyBindingSettings, KeyBindingSettings> update)
    {
        SetInput(input => input with { KeyBindings = update(input.KeyBindings) });
    }

    private static int IndexOf(IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private string? FindBindingConflict(string changedLabel, string binding)
    {
        var changedTokens = NormalizeBindingTokens(binding).ToArray();
        foreach (var (label, otherBinding) in EnumerateNamedBindings(Settings.Input.KeyBindings))
        {
            if (string.Equals(label, changedLabel, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (NormalizeBindingTokens(otherBinding).Any(token => changedTokens.Contains(token, StringComparer.OrdinalIgnoreCase)))
            {
                return label;
            }
        }

        return null;
    }

    private static IEnumerable<(string Label, string Binding)> EnumerateNamedBindings(KeyBindingSettings bindings)
    {
        yield return ("MOVE LEFT", bindings.MoveLeft);
        yield return ("MOVE RIGHT", bindings.MoveRight);
        yield return ("JUMP", bindings.Jump);
        yield return ("FLY", bindings.Fly);
        yield return ("GLIDE", bindings.Glide);
        yield return ("PRIMARY USE", bindings.AttackPrimary);
        yield return ("SECONDARY USE", bindings.AttackSecondary);
        yield return ("INVENTORY", bindings.OpenInventory);
        yield return ("CRAFTING", bindings.OpenCrafting);
        yield return ("CHARACTER EDITOR", bindings.OpenCharacterEditor);
        yield return ("INTERACT", bindings.Interact);
        yield return ("PAUSE", bindings.Pause);
        yield return ("DEBUG CONSOLE", bindings.DebugConsole);
        yield return ("DEBUG TOGGLE", bindings.DebugToggle);
        yield return ("HOTBAR 1", bindings.Hotbar1);
        yield return ("HOTBAR 2", bindings.Hotbar2);
        yield return ("HOTBAR 3", bindings.Hotbar3);
        yield return ("HOTBAR 4", bindings.Hotbar4);
        yield return ("HOTBAR 5", bindings.Hotbar5);
        yield return ("HOTBAR 6", bindings.Hotbar6);
        yield return ("HOTBAR 7", bindings.Hotbar7);
        yield return ("HOTBAR 8", bindings.Hotbar8);
        yield return ("HOTBAR 9", bindings.Hotbar9);
        yield return ("HOTBAR 10", bindings.Hotbar10);
    }

    private static IEnumerable<string> NormalizeBindingTokens(string binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            yield break;
        }

        foreach (var token in binding.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return token.ToUpperInvariant();
        }
    }

    private static string SectionDescription(string title)
    {
        return title switch
        {
            "GAMEPLAY" => "PACING, CAMERA AND MOMENT-TO-MOMENT PLAY",
            "WORLD" => "STREAMING AND WORLD SESSION BEHAVIOR",
            "GRAPHICS" => "DISPLAY MODE, SCALE AND PRESENTATION",
            "RENDERING" => "PIXEL EFFECTS, LIGHTING AND CACHE QUALITY",
            "UI" => "READABILITY, MOTION AND INTERFACE STYLE",
            "DEBUG" => "DIAGNOSTIC LAYERS AND PROFILER DETAILS",
            "AUDIO" => "MIX LEVELS FOR MUSIC, EFFECTS AND MENUS",
            "KEYBINDS" => "KEYBOARD AND MOUSE ACTION MAPPING",
            "ACCESSIBILITY" => "READABILITY, MOTION AND INPUT ASSISTANCE",
            "SYSTEM" => "SAVE, LEAVE OR RETURN TO THE GAME",
            _ => title
        };
    }

    private static string Describe(string label)
    {
        return label switch
        {
            "AUTOSAVE MIN" => "TIME BETWEEN AUTOMATIC PLAYER AND WORLD SAVES.",
            "MAX ENEMIES" => "MAXIMUM ACTIVE ENEMY COUNT AROUND THE PLAYER.",
            "CAMERA ZOOM" => "WORLD PIXEL SCALE. HIGHER VALUES SHOW A CLOSER VIEW.",
            "INTERACTION REACH" => "MAXIMUM DISTANCE FOR TILES, OBJECTS AND STATIONS.",
            "SHOW TARGET TILE" => "HIGHLIGHT THE TILE CURRENTLY TARGETED BY THE PLAYER.",
            "HOLD TO MINE" => "CONTINUE MINING WHILE THE PRIMARY ACTION IS HELD.",
            "HOLD TO BLOCK" => "HOLD SECONDARY USE TO GUARD. OFF USES PRESS-TO-TOGGLE GUARD.",
            "PAUSE ON FOCUS LOST" => "PAUSE THE SESSION WHEN THE GAME WINDOW LOSES FOCUS.",
            "SPAWN RATE" => "MULTIPLIER FOR ENEMY SPAWN ATTEMPTS.",
            "AUTO PICKUP" => "MOVE NEARBY DROPS INTO AVAILABLE INVENTORY SLOTS.",
            "COMBAT LINE OF SIGHT" => "REQUIRE AN UNBLOCKED PATH FOR COMBAT TARGETING.",
            "RESPAWN DELAY" => "SECONDS BEFORE THE PLAYER RETURNS AFTER DEFEAT.",
            "CAMERA LOOK AHEAD" => "CAMERA LEAD IN THE PLAYER'S MOVEMENT DIRECTION.",
            "SCREEN SHAKE" => "STRENGTH OF IMPACT AND DAMAGE CAMERA MOTION.",
            "RESOLUTION" => "WINDOW OR FULLSCREEN OUTPUT RESOLUTION.",
            "FULLSCREEN" => "USE BORDERLESS FULLSCREEN PRESENTATION.",
            "VSYNC" => "SYNCHRONIZE FRAME PRESENTATION TO THE DISPLAY.",
            "UI SCALE" => "SCALE MENUS, HUD AND INTERACTIVE CONTROLS.",
            "RENDER SCALE" => "INTERNAL WORLD RENDERING SCALE BEFORE PRESENTATION.",
            "WORLD PROFILE" => "BASE SIZE PROFILE USED WHEN CREATING WORLD CONTENT.",
            "CHUNK LOAD MARGIN" => "EXTRA CHUNK COLUMNS KEPT AHEAD OF THE CAMERA.",
            "CHUNK UNLOAD MARGIN" => "DISTANCE BEFORE INACTIVE CHUNKS MAY BE RELEASED.",
            "STREAMING BUDGET" => "MAXIMUM STREAMING RESULTS APPLIED PER FRAME.",
            "BLOOM" => "GLOW STRENGTH FOR BRIGHT PIXELS.",
            "VIGNETTE" => "DARKENING STRENGTH AROUND THE VIEW EDGES.",
            "COLOR GRADE" => "INTENSITY OF THE ACTIVE COLOR-GRADING PASS.",
            "PARTICLE QUALITY" => "DENSITY AND LIFETIME BUDGET FOR GAMEPLAY PARTICLES.",
            "CHUNK CACHE LIMIT" => "MAXIMUM RETAINED CHUNK RENDER CACHE ENTRIES.",
            "CADENCE PRESET" => "COORDINATED UPDATE RATES FOR LIGHTING, REFLECTIONS, ATMOSPHERE AND SCENE CAPTURE.",
            "ADAPTIVE CADENCE" => "ALLOW PRESENTATION PASSES TO REDUCE THEIR UPDATE RATE UNDER FRAME PRESSURE.",
            "LIGHTING RATE HZ" => "MAXIMUM LIGHTING PRESENTATION UPDATES PER SECOND.",
            "REFLECTION RATE HZ" => "MAXIMUM WATER AND WET-SURFACE REFLECTION UPDATES PER SECOND.",
            "ATMOSPHERE RATE HZ" => "MAXIMUM ATMOSPHERE AND WEATHER PRESENTATION UPDATES PER SECOND.",
            "SCENE CAPTURE RATE HZ" => "MAXIMUM BACKDROP AND SCENE-CAPTURE UPDATES PER SECOND.",
            "UI EFFECT QUALITY" => "QUALITY BUDGET FOR UI GRADIENTS, GLOW AND BACKDROP EFFECTS.",
            "BLUR RADIUS" => "MAXIMUM PIXEL RADIUS USED BY BLUR-CAPABLE UI EFFECTS.",
            "THEME" => "COLOR PALETTE USED BY HUDS, MENUS AND OVERLAYS.",
            "PANEL OPACITY" => "OPACITY OF MENU AND TOOL WINDOW SURFACES.",
            "HUD OPACITY" => "OPACITY OF IN-GAME STATUS AND HOTBAR SURFACES.",
            "BACKDROP OPACITY" => "DARKENING BEHIND OPEN MODAL MENUS.",
            "ANIMATION SPEED" => "PLAYBACK SPEED FOR MENU TRANSITIONS AND FEEDBACK.",
            "CORNER RADIUS" => "PIXEL RADIUS USED BY PANELS, BUTTONS AND TOOLTIPS.",
            "BACKDROP BLUR" => "STRENGTH APPLIED TO THE CONFIGURED UI BLUR RADIUS.",
            "GLOW STRENGTH" => "INTENSITY OF FOCUS AND RAISED-SURFACE EDGE GLOW.",
            "TEXT SCALE" => "SIZE OF MENU TITLES, LABELS AND SUPPORTING TEXT.",
            "HIGH CONTRAST PANELS" => "DARKEN SURFACES AND STRENGTHEN BORDERS FOR READABILITY.",
            "LARGE CURSOR" => "ADD A LARGE HIGH-CONTRAST POINTER ACCENT IN MENUS.",
            "REDUCED MOTION" => "MINIMIZE SLIDING AND TRANSITION ANIMATIONS.",
            "COMPACT LISTS" => "SHOW MORE ROWS WITH TIGHTER VERTICAL SPACING.",
            "CONTROL HINTS" => "SHOW SHORT CONTEXTUAL ACTION HINTS IN GAMEPLAY UI.",
            "MASTER" => "OVERALL OUTPUT LEVEL FOR ALL GAME AUDIO.",
            "MUSIC" => "BACKGROUND MUSIC LEVEL BEFORE MASTER VOLUME.",
            "SFX" => "WORLD AND COMBAT EFFECT LEVEL BEFORE MASTER VOLUME.",
            "UI" => "MENU AND INTERACTION SOUND LEVEL BEFORE MASTER VOLUME.",
            "MOUSE SENSITIVITY" => "POINTER AND AIM RESPONSE MULTIPLIER.",
            "INVERT HOTBAR SCROLL" => "REVERSE MOUSE-WHEEL HOTBAR DIRECTION.",
            "REDUCE SCREEN FLASH" => "REDUCE BRIGHT UI GLOW AND SCREEN-FLASH INTENSITY.",
            "DISABLE CAMERA SHAKE" => "DISABLE CAMERA SHAKE FROM IMPACTS AND DAMAGE.",
            "HOLD ACTIONS AS TOGGLE" => "ACCESSIBILITY OVERRIDE FOR SUPPORTED HOLD ACTIONS, INCLUDING GUARD.",
            "HIGH CONTRAST OUTLINE" => "USE A THICKER FOCUS AND INTERACTION OUTLINE.",
            "COLORBLIND INDICATORS" => "USE DISTINCT WARNING AND DANGER INDICATOR COLORS.",
            "INTERFACE CONTRAST" => "ADJUST CONTRAST BETWEEN UI SURFACES AND TEXT.",
            "TOOLTIP DELAY" => "SECONDS OF HOVER BEFORE THE CONTROL DESCRIPTION APPEARS.",
            "RESET KEYBINDS" => "RESTORE EVERY ACTION TO ITS DEFAULT BINDING.",
            "SAVE SETTINGS" => "WRITE THE CURRENT SETTINGS TO DISK NOW.",
            "BACK TO MAIN MENU" => "LEAVE THE CURRENT SCREEN AND RETURN TO THE MAIN MENU.",
            "EXIT GAME" => "CLOSE THE GAME APPLICATION.",
            _ when label.StartsWith("HOTBAR ", StringComparison.Ordinal) => "KEY USED TO SELECT THIS HOTBAR SLOT.",
            _ => $"CHANGE {label}."
        };
    }

    private static PixelUiCommandIcon ResolveCommandIcon(MenuOption option)
    {
        if (option.Kind == UiControlKind.KeyBinding)
        {
            return PixelUiCommandIcon.Keyboard;
        }

        if (option.Label.Contains("SAVE", StringComparison.Ordinal))
        {
            return PixelUiCommandIcon.Save;
        }

        return option.Label.Contains("RESET", StringComparison.Ordinal)
            ? PixelUiCommandIcon.Reset
            : PixelUiCommandIcon.Command;
    }

    private static string Abbreviate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(1, maxLength - 1)] + ".";
    }

    private static int CharactersThatFit(int width, int scale)
    {
        return Math.Max(1, width / Math.Max(6, 6 * scale));
    }

    private sealed record MenuSection(string Title, IReadOnlyList<MenuOption> Options);

    private sealed class MenuOption
    {
        private readonly Func<float> _normalizedValue;
        private readonly Action<float> _setFromNormalized;
        private readonly Action<int>? _selectChoice;

        public MenuOption(
            UiControlKind kind,
            string label,
            string description,
            Func<string> value,
            Action<int> change,
            Action activate,
            Func<float> normalizedValue,
            Action<float> setFromNormalized,
            IReadOnlyList<string>? choices,
            Action<int>? selectChoice)
        {
            Kind = kind;
            Label = label;
            Description = description;
            Value = value;
            Change = change;
            Activate = activate;
            _normalizedValue = normalizedValue;
            _setFromNormalized = setFromNormalized;
            Choices = choices;
            _selectChoice = selectChoice;
        }

        public UiControlKind Kind { get; }

        public string Label { get; }

        public string Description { get; }

        public Func<string> Value { get; }

        public Action<int> Change { get; }

        public Action Activate { get; }

        public IReadOnlyList<string>? Choices { get; }

        public float NormalizedValue()
        {
            return Math.Clamp(_normalizedValue(), 0f, 1f);
        }

        public void SetFromNormalized(float value)
        {
            _setFromNormalized(value);
        }

        public void SelectChoice(int index)
        {
            _selectChoice?.Invoke(index);
        }
    }

    private sealed record KeyCapture(string Label, Action<string> Setter);

    private readonly record struct HitZone(Rectangle Bounds, int Index);

    private readonly record struct ControlHitZone(Rectangle Bounds, int OptionIndex, ControlPart Part, int ChoiceIndex);

    private enum ControlPart
    {
        Primary,
        Slider,
        Decrement,
        Increment,
        Dropdown,
        Choice
    }
}
