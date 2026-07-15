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
    Streaming
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
