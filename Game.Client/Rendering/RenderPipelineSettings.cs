namespace Game.Client.Rendering;

public sealed record RenderPipelineSettings
{
    public PostProcessingSettings PostProcessing { get; init; } = new();

    public bool DrawLiquids { get; init; } = true;

    public bool DrawLightingOverlay { get; init; } = true;

    public bool DrawDebugOverlays { get; init; } = true;
}
