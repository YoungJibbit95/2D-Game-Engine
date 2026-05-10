using Game.Client.Input;
using Game.Client.Rendering;
using Game.Core.Settings;
using Game.Core.UI.Animation;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class PauseMenuOverlay
{
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
    private int _sectionIndex;
    private int _optionIndex;
    private int _hoveredSectionIndex = -1;
    private int _hoveredOptionIndex = -1;
    private KeyCapture? _keyCapture;
    private string _status = "SETTINGS AUTOSAVE ON CHANGE";
    private bool _confirmResetKeybinds;

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
    }

    public bool IsOpen { get; private set; }

    public GameSettings Settings { get; private set; }

    public void Open()
    {
        IsOpen = true;
        _keyCapture = null;
        _status = "PAUSED";
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

        _openAnimation.Update((float)deltaSeconds, Settings.Ui.AnimationSpeed);

        if (_keyCapture is not null)
        {
            UpdateKeyCapture(input);
            return true;
        }

        if (input.IsBindingPressed(Settings.Input.KeyBindings.Pause) ||
            input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
        {
            Close();
            return true;
        }

        UpdateMouse(input);

        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Tab) ||
            input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.E) ||
            input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.PageDown))
        {
            MoveSection(1);
        }
        else if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Q) ||
                 input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.PageUp))
        {
            MoveSection(-1);
        }

        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Down) ||
            input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.S))
        {
            MoveOption(1);
        }
        else if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Up) ||
                 input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.W))
        {
            MoveOption(-1);
        }

        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Left) ||
            input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.A))
        {
            CurrentOption()?.Change(-1);
        }
        else if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Right) ||
                 input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.D))
        {
            CurrentOption()?.Change(1);
        }

        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Enter) ||
            input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Space))
        {
            CurrentOption()?.Activate();
        }

        return true;
    }

    public void Draw(RenderContext context)
    {
        if (!IsOpen)
        {
            return;
        }

        var palette = UiTheme.Resolve(Settings);
        var fade = _openAnimation.GetValue(UiAnimationProperty.Opacity, 1f);
        var offsetY = (int)MathF.Round(_openAnimation.GetValue(UiAnimationProperty.OffsetY, 0f));
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, UiTheme.WithAlpha(palette.Backdrop, Settings.Ui.MenuBackdropOpacity * fade));

        var panelWidth = Math.Min(980, context.ViewportBounds.Width - 56);
        var panelHeight = Math.Min(610, context.ViewportBounds.Height - 48);
        var panel = new Rectangle(
            context.ViewportBounds.Width / 2 - panelWidth / 2,
            context.ViewportBounds.Height / 2 - panelHeight / 2 + offsetY,
            panelWidth,
            panelHeight);

        UiTheme.DrawPanel(context, panel, palette, Settings.Ui.PanelOpacity * fade);

        context.DebugText.Draw(new Vector2(panel.X + 24, panel.Y + 20), _title, palette.Accent, 4);
        if (Settings.Ui.ShowControlHints)
        {
            context.DebugText.Draw(new Vector2(panel.X + 26, panel.Y + 64), "Q/E TABS   UP/DOWN SELECT   LEFT/RIGHT CHANGE   ENTER/CLICK EDIT", palette.TextMuted, 2);
        }

        _tabHitZones.Clear();
        _optionHitZones.Clear();
        DrawTabs(context, panel, palette);
        DrawOptions(context, panel, palette);

        var footer = new Rectangle(panel.X + 18, panel.Bottom - 46, panel.Width - 36, 28);
        context.SpriteBatch.Draw(context.Pixel, footer, UiTheme.WithAlpha(palette.Surface, Settings.Ui.PanelOpacity));
        UiTheme.DrawBorder(context, footer, UiTheme.WithAlpha(palette.SurfaceHover, 0.9f), 1);
        var footerText = _keyCapture is null
            ? _status
            : $"PRESS A KEY FOR {_keyCapture.Label}   ESC CANCEL";
        context.DebugText.Draw(new Vector2(footer.X + 10, footer.Y + 7), footerText, palette.Warning, 2);
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
            Toggle("VSYNC", () => Settings.Video.VSync, value => SetVideo(v => v with { VSync = value })),
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
            Choice("WORLD PROFILE", () => Settings.World.WorldProfileId, profiles, value => SetWorld(w => w with { WorldProfileId = value })),
            Number("CHUNK LOAD MARGIN", () => Settings.World.ChunkLoadMargin, 0, 16, 1, value => SetWorld(w => w with { ChunkLoadMargin = value })),
            Number("CHUNK UNLOAD MARGIN", () => Settings.World.ChunkUnloadMargin, Settings.World.ChunkLoadMargin, 32, 1, value => SetWorld(w => w with { ChunkUnloadMargin = Math.Max(value, Settings.World.ChunkLoadMargin) })),
            Toggle("KEEP DIRTY CHUNKS", () => Settings.World.KeepDirtyChunksLoaded, value => SetWorld(w => w with { KeepDirtyChunksLoaded = value })),
            Toggle("PRELOAD VERTICAL SLICE", () => Settings.World.PreloadFullVerticalSlice, value => SetWorld(w => w with { PreloadFullVerticalSlice = value })),
            Toggle("SAVE BEFORE UNLOAD", () => Settings.World.SaveChunksBeforeUnload, value => SetWorld(w => w with { SaveChunksBeforeUnload = value })),
            Number("STREAMING BUDGET", () => Settings.World.StreamingBudgetChunksPerFrame, 1, 512, 8, value => SetWorld(w => w with { StreamingBudgetChunksPerFrame = value }))
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
            Choice("PARTICLE QUALITY", ParticleQualityLabel, new[] { "OFF", "LOW", "MEDIUM", "HIGH" }, SetParticleQuality),
            Number("LIQUID OPACITY", () => Settings.Rendering.LiquidOpacity, 0f, 1f, 0.05f, value => SetRendering(r => r with { LiquidOpacity = value })),
            Number("LIGHTING BLEND", () => Settings.Rendering.LightingBlendStrength, 0f, 1f, 0.05f, value => SetRendering(r => r with { LightingBlendStrength = value })),
            Number("CHUNK CACHE LIMIT", () => Settings.Rendering.MaxChunkRenderCacheEntries, 32, 4096, 32, value => SetRendering(r => r with { MaxChunkRenderCacheEntries = value })),
            Toggle("ENTITY INTERPOLATION", () => Settings.Rendering.EntityInterpolation, value => SetRendering(r => r with { EntityInterpolation = value }))
        ];
    }

    private IReadOnlyList<MenuOption> BuildUiOptions()
    {
        return
        [
            Choice("THEME", () => Settings.Ui.Theme, new[] { "Midnight", "Forest", "Ember" }, value => SetUi(ui => ui with { Theme = value })),
            Number("PANEL OPACITY", () => Settings.Ui.PanelOpacity, 0.35f, 1f, 0.05f, value => SetUi(ui => ui with { PanelOpacity = value })),
            Number("HUD OPACITY", () => Settings.Ui.HudOpacity, 0.35f, 1f, 0.05f, value => SetUi(ui => ui with { HudOpacity = value })),
            Number("BACKDROP OPACITY", () => Settings.Ui.MenuBackdropOpacity, 0f, 1f, 0.05f, value => SetUi(ui => ui with { MenuBackdropOpacity = value })),
            Number("ANIMATION SPEED", () => Settings.Ui.AnimationSpeed, 0.1f, 4f, 0.1f, value => SetUi(ui => ui with { AnimationSpeed = value })),
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
            Key("PRIMARY USE", () => Settings.Input.KeyBindings.AttackPrimary, value => SetKeyBindings(k => k with { AttackPrimary = value })),
            Key("SECONDARY USE", () => Settings.Input.KeyBindings.AttackSecondary, value => SetKeyBindings(k => k with { AttackSecondary = value })),
            Key("INVENTORY", () => Settings.Input.KeyBindings.OpenInventory, value => SetKeyBindings(k => k with { OpenInventory = value })),
            Key("CRAFTING", () => Settings.Input.KeyBindings.OpenCrafting, value => SetKeyBindings(k => k with { OpenCrafting = value })),
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
        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
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

    private void UpdateMouse(InputManager input)
    {
        _hoveredSectionIndex = FindHit(_tabHitZones, input.MousePosition);
        _hoveredOptionIndex = FindHit(_optionHitZones, input.MousePosition);

        if (_hoveredSectionIndex >= 0 && input.IsLeftMousePressed)
        {
            _sectionIndex = _hoveredSectionIndex;
            _optionIndex = Math.Clamp(_optionIndex, 0, _sections[_sectionIndex].Options.Count - 1);
            _confirmResetKeybinds = false;
            _status = _sections[_sectionIndex].Title;
            return;
        }

        if (_hoveredOptionIndex >= 0)
        {
            _optionIndex = _hoveredOptionIndex;

            if (input.IsLeftMousePressed)
            {
                CurrentOption()?.Activate();
            }
            else if (input.IsRightMousePressed)
            {
                CurrentOption()?.Change(-1);
            }
        }
    }

    private void DrawTabs(RenderContext context, Rectangle panel, UiPalette palette)
    {
        var tabX = panel.X + 22;
        var tabY = panel.Y + 96;
        for (var index = 0; index < _sections.Count; index++)
        {
            var title = _sections[index].Title;
            var width = Math.Max(90, title.Length * 11 + 22);
            var bounds = new Rectangle(tabX, tabY, width, 28);
            var selected = index == _sectionIndex;
            var hovered = index == _hoveredSectionIndex;
            UiTheme.DrawButton(context, bounds, palette, selected, hovered);
            context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 7), title, selected ? palette.Text : palette.TextMuted, 1);
            _tabHitZones.Add(new HitZone(bounds, index));
            tabX += width + 8;
        }
    }

    private void DrawOptions(RenderContext context, Rectangle panel, UiPalette palette)
    {
        var section = _sections[_sectionIndex];
        var list = section.Options;
        var startY = panel.Y + 140;
        var rowHeight = Settings.Ui.CompactLists ? 26 : 30;
        var visibleRows = Math.Max(1, (panel.Bottom - 198 - startY) / rowHeight);
        var scroll = Math.Clamp(_optionIndex - visibleRows + 1, 0, Math.Max(0, list.Count - visibleRows));
        var end = Math.Min(list.Count, scroll + visibleRows);

        for (var index = scroll; index < end; index++)
        {
            var row = index - scroll;
            var option = list[index];
            var bounds = new Rectangle(panel.X + 24, startY + row * rowHeight, panel.Width - 48, 25);
            var selected = index == _optionIndex;
            var hovered = index == _hoveredOptionIndex;

            if (selected)
            {
                UiTheme.DrawButton(context, bounds, palette, selected: true, hovered: false);
            }
            else if (hovered)
            {
                UiTheme.DrawButton(context, bounds, palette, selected: false, hovered: true);
            }

            context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 6), option.Label, selected ? palette.Text : palette.TextMuted, 1);
            var value = option.Value();
            var valueX = bounds.Right - Math.Min(360, Math.Max(118, value.Length * 9 + 12));
            context.DebugText.Draw(new Vector2(valueX, bounds.Y + 6), value, selected ? palette.Warning : palette.TextMuted, 1);
            _optionHitZones.Add(new HitZone(bounds, index));
        }

        if (list.Count > visibleRows)
        {
            context.DebugText.Draw(new Vector2(panel.Right - 118, panel.Bottom - 84), $"{_optionIndex + 1}/{list.Count}", palette.TextMuted, 1);
        }
    }

    private void MoveSection(int direction)
    {
        _sectionIndex = ((_sectionIndex + direction) % _sections.Count + _sections.Count) % _sections.Count;
        _optionIndex = Math.Clamp(_optionIndex, 0, _sections[_sectionIndex].Options.Count - 1);
        _confirmResetKeybinds = false;
        _status = _sections[_sectionIndex].Title;
    }

    private void MoveOption(int direction)
    {
        var count = _sections[_sectionIndex].Options.Count;
        _optionIndex = ((_optionIndex + direction) % count + count) % count;
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
            label,
            () => get() ? "ON" : "OFF",
            direction => Commit(() => set(!get())),
            () => Commit(() => set(!get())));
    }

    private MenuOption Number(string label, Func<float> get, float min, float max, float step, Action<float> set)
    {
        return new MenuOption(
            label,
            () => get().ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            direction => Commit(() => set(Math.Clamp(get() + step * Math.Sign(direction), min, max))),
            () => Commit(() => set(Math.Clamp(get() + step, min, max))));
    }

    private MenuOption Number(string label, Func<int> get, int min, int max, int step, Action<int> set)
    {
        return new MenuOption(
            label,
            () => get().ToString(System.Globalization.CultureInfo.InvariantCulture),
            direction => Commit(() => set(Math.Clamp(get() + step * Math.Sign(direction), min, max))),
            () => Commit(() => set(Math.Clamp(get() + step, min, max))));
    }

    private MenuOption Choice(string label, Func<string> get, IReadOnlyList<string> choices, Action<string> set)
    {
        return new MenuOption(
            label,
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
            }));
    }

    private MenuOption Key(string label, Func<string> get, Action<string> set)
    {
        return new MenuOption(
            label,
            get,
            _ => BeginKeyCapture(label, set),
            () => BeginKeyCapture(label, set));
    }

    private static MenuOption Command(string label, Action action)
    {
        return new MenuOption(label, () => ">", _ => action(), action);
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

    private void SetVideo(Func<VideoSettings, VideoSettings> update)
    {
        Settings = Settings with { Video = update(Settings.Video) };
    }

    private void SetRendering(Func<RenderingSettings, RenderingSettings> update)
    {
        Settings = Settings with { Rendering = update(Settings.Rendering) };
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
        yield return ("PRIMARY USE", bindings.AttackPrimary);
        yield return ("SECONDARY USE", bindings.AttackSecondary);
        yield return ("INVENTORY", bindings.OpenInventory);
        yield return ("CRAFTING", bindings.OpenCrafting);
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

    private static int FindHit(IEnumerable<HitZone> zones, Point point)
    {
        foreach (var zone in zones)
        {
            if (zone.Bounds.Contains(point))
            {
                return zone.Index;
            }
        }

        return -1;
    }

    private sealed record MenuSection(string Title, IReadOnlyList<MenuOption> Options);

    private sealed record MenuOption(string Label, Func<string> Value, Action<int> Change, Action Activate);

    private sealed record KeyCapture(string Label, Action<string> Setter);

    private readonly record struct HitZone(Rectangle Bounds, int Index);
}
