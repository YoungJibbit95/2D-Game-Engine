namespace Game.Client.Diagnostics;

public sealed record ClientGameplaySmokeTelemetry(
    bool Captured,
    int TotalActiveEntities,
    int ActiveEnemies,
    int VisibleEntities,
    int VisibleEnemies,
    IReadOnlyList<string> VisibleContentIds)
{
    public static ClientGameplaySmokeTelemetry NotCaptured { get; } = new(
        false,
        0,
        0,
        0,
        0,
        Array.Empty<string>());
}

public interface IClientGameplaySmokeTelemetryProvider
{
    ClientGameplaySmokeTelemetry CaptureGameplaySmokeTelemetry();
}
