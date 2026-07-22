using Game.Core.World;
using System.Numerics;

namespace Game.Core.DeveloperTools;

public interface IDeveloperCommandIntent
{
}

public sealed record TeleportPlayerIntent(Vector2 Position) : IDeveloperCommandIntent;

public enum DeveloperMovementMode
{
    GodMode,
    NoClip,
    Fly
}

public enum DeveloperToggle
{
    Off,
    On,
    Toggle
}

public sealed record SetDeveloperMovementModeIntent(
    DeveloperMovementMode Mode,
    DeveloperToggle Value) : IDeveloperCommandIntent;

public sealed record SetDeveloperSpeedIntent(float Multiplier, bool Reset) : IDeveloperCommandIntent;

public sealed record ReloadChunkIntent(ChunkPos Position, bool Force) : IDeveloperCommandIntent;

public sealed record SetSpawnRateIntent(float Multiplier, bool Reset) : IDeveloperCommandIntent;

public enum DebugView
{
    Overlay,
    Collisions,
    Ai,
    Streaming,
    Lighting,
    Shadows,
    Reflections,
    Particles,
    Background,
    Combat,
    Spawns
}

public sealed record SetDebugViewIntent(DebugView View, DeveloperToggle Value) : IDeveloperCommandIntent;

public enum PerformanceRequestKind
{
    Summary,
    Capture,
    Reset
}

public sealed record PerformanceRequestIntent(PerformanceRequestKind Kind) : IDeveloperCommandIntent;

public enum EventDiagnosticsRequestKind
{
    List,
    Watch,
    Unwatch,
    Clear
}

public sealed record EventDiagnosticsRequestIntent(
    EventDiagnosticsRequestKind Kind,
    string? EventName) : IDeveloperCommandIntent;

public enum WeatherOverrideRequestKind
{
    Status,
    Set,
    Reset
}

public sealed record WeatherOverrideRequestIntent(
    WeatherOverrideRequestKind Kind,
    string? WeatherId,
    float? Intensity) : IDeveloperCommandIntent;

public enum BiomeOverrideRequestKind
{
    Status,
    Set,
    Reset
}

public sealed record BiomeOverrideRequestIntent(
    BiomeOverrideRequestKind Kind,
    string? BiomeId) : IDeveloperCommandIntent;

public enum DeveloperSaveMode
{
    Quick,
    Full,
    Checkpoint
}

public sealed record DeveloperSaveRequestIntent(DeveloperSaveMode Mode) : IDeveloperCommandIntent;

public sealed record SetRenderingFeatureIntent(
    string FeatureId,
    DeveloperToggle Value) : IDeveloperCommandIntent;

public sealed record SetLightingDebugViewIntent(
    string ViewId,
    DeveloperToggle Value) : IDeveloperCommandIntent;

public enum GameRuleRequestKind
{
    Read,
    Set,
    Reset
}

public sealed record GameRuleRequestIntent(
    GameRuleRequestKind Kind,
    string RuleId,
    string? Value) : IDeveloperCommandIntent;

public enum PlayerResourceKind
{
    Health,
    Mana
}

public enum PlayerResourceOperation
{
    Status,
    Fill,
    Set,
    Add
}

public sealed record PlayerResourceRequestIntent(
    PlayerResourceKind Resource,
    PlayerResourceOperation Operation,
    float? Amount) : IDeveloperCommandIntent;

public sealed record SpawnProjectileRequestIntent(
    string ProjectileId,
    Vector2 Position,
    Vector2 Direction) : IDeveloperCommandIntent;
