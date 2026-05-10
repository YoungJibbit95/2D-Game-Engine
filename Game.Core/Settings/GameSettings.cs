namespace Game.Core.Settings;

public sealed record GameSettings
{
    public VideoSettings Video { get; init; } = new();

    public RenderingSettings Rendering { get; init; } = new();

    public UiSettings Ui { get; init; } = new();

    public AudioSettings Audio { get; init; } = new();

    public GameplaySettings Gameplay { get; init; } = new();

    public WorldSettings World { get; init; } = new();

    public InputSettings Input { get; init; } = new();

    public DebugSettings Debug { get; init; } = new();

    public static GameSettings CreateDefault()
    {
        return new GameSettings();
    }
}

public sealed record VideoSettings
{
    public int Width { get; init; } = 1280;

    public int Height { get; init; } = 720;

    public bool Fullscreen { get; init; }

    public bool VSync { get; init; } = true;

    public float UiScale { get; init; } = 1f;

    public float RenderScale { get; init; } = 1f;
}

public sealed record RenderingSettings
{
    public bool DrawLiquids { get; init; } = true;

    public bool DrawLightingOverlay { get; init; } = true;

    public bool DrawDebugOverlays { get; init; } = true;

    public bool PostProcessingEnabled { get; init; }

    public bool PixelSnap { get; init; } = true;

    public float BloomStrength { get; init; }

    public float VignetteStrength { get; init; }

    public float ColorGradeIntensity { get; init; }

    public int ParticleQuality { get; init; } = 2;

    public float LiquidOpacity { get; init; } = 0.72f;

    public float LightingBlendStrength { get; init; } = 1f;

    public int MaxChunkRenderCacheEntries { get; init; } = 512;

    public bool EntityInterpolation { get; init; } = true;
}

public sealed record UiSettings
{
    public string Theme { get; init; } = "Midnight";

    public float PanelOpacity { get; init; } = 0.92f;

    public float HudOpacity { get; init; } = 0.88f;

    public float MenuBackdropOpacity { get; init; } = 0.58f;

    public float AnimationSpeed { get; init; } = 1f;

    public bool ReducedMotion { get; init; }

    public bool CompactLists { get; init; }

    public bool ShowControlHints { get; init; } = true;
}

public sealed record AudioSettings
{
    public float MasterVolume { get; init; } = 1f;

    public float MusicVolume { get; init; } = 0.8f;

    public float SfxVolume { get; init; } = 0.9f;

    public float UiVolume { get; init; } = 0.8f;
}

public sealed record GameplaySettings
{
    public float AutosaveMinutes { get; init; } = 5f;

    public int MaxActiveEnemies { get; init; } = 32;

    public float CameraZoom { get; init; } = 2f;

    public float InteractionReachPixels { get; init; } = 96f;

    public bool ShowInteractionTarget { get; init; } = true;

    public bool HoldToMine { get; init; } = true;

    public bool PauseOnFocusLost { get; init; } = true;

    public float EnemySpawnRateMultiplier { get; init; } = 1f;

    public bool AutoPickupItems { get; init; } = true;

    public bool UseLineOfSightForCombat { get; init; } = true;

    public float RespawnDelaySeconds { get; init; } = 3f;

    public float CameraLookAheadPixels { get; init; } = 32f;

    public float ScreenShakeMultiplier { get; init; } = 1f;
}

public sealed record WorldSettings
{
    public bool InfiniteHorizontalGeneration { get; init; } = true;

    public string WorldProfileId { get; init; } = "small";

    public int ChunkLoadMargin { get; init; } = 1;

    public int ChunkUnloadMargin { get; init; } = 4;

    public bool KeepDirtyChunksLoaded { get; init; } = true;

    public bool PreloadFullVerticalSlice { get; init; } = true;

    public bool SaveChunksBeforeUnload { get; init; } = true;

    public int StreamingBudgetChunksPerFrame { get; init; } = 32;
}

public sealed record InputSettings
{
    public KeyBindingSettings KeyBindings { get; init; } = new();

    public float MouseSensitivity { get; init; } = 1f;

    public bool InvertHotbarScroll { get; init; }
}

public sealed record KeyBindingSettings
{
    public string MoveLeft { get; init; } = "A,Left";

    public string MoveRight { get; init; } = "D,Right";

    public string Jump { get; init; } = "Space,W,Up";

    public string AttackPrimary { get; init; } = "MouseLeft";

    public string AttackSecondary { get; init; } = "MouseRight";

    public string OpenInventory { get; init; } = "I";

    public string OpenCrafting { get; init; } = "C";

    public string Interact { get; init; } = "E";

    public string Pause { get; init; } = "Escape";

    public string DebugConsole { get; init; } = "F10";

    public string DebugToggle { get; init; } = "F3";

    public string Hotbar1 { get; init; } = "D1";

    public string Hotbar2 { get; init; } = "D2";

    public string Hotbar3 { get; init; } = "D3";

    public string Hotbar4 { get; init; } = "D4";

    public string Hotbar5 { get; init; } = "D5";

    public string Hotbar6 { get; init; } = "D6";

    public string Hotbar7 { get; init; } = "D7";

    public string Hotbar8 { get; init; } = "D8";

    public string Hotbar9 { get; init; } = "D9";

    public string Hotbar10 { get; init; } = "D0";
}

public sealed record DebugSettings
{
    public bool ShowDebugOverlay { get; init; } = true;

    public bool ShowGrid { get; init; }

    public bool ShowSaveMetrics { get; init; } = true;

    public bool ShowStreamingMetrics { get; init; } = true;

    public bool ShowRenderMetrics { get; init; } = true;

    public bool ShowMouseTile { get; init; } = true;

    public bool ShowEventJournal { get; init; }
}
