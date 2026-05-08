namespace Game.Core.Settings;

public sealed record GameSettings
{
    public VideoSettings Video { get; init; } = new();

    public AudioSettings Audio { get; init; } = new();

    public GameplaySettings Gameplay { get; init; } = new();

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
}

public sealed record DebugSettings
{
    public bool ShowDebugOverlay { get; init; } = true;

    public bool ShowGrid { get; init; }
}
