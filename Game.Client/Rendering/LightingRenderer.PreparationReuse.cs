using Game.Client.Rendering.Lighting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public sealed partial class LightingRenderer
{
    public const string DisablePreparationReuseEnvironmentVariable = "YJSE_DISABLE_LIGHTING_PREPARATION_REUSE";
    private bool _preparationReuseEnabled = !string.Equals(
        Environment.GetEnvironmentVariable(DisablePreparationReuseEnvironmentVariable), "1", StringComparison.Ordinal);
    private LightingPreparationCache? _preparationCache;
    private GraphicsDevice? _preparationDevice;
    private bool _deviceResourcesInvalidated;

    /// <summary>Immediate CPU-path fallback; changing this also invalidates the cache.</summary>
    public bool PreparationReuseEnabled
    {
        get => _preparationReuseEnabled;
        set
        {
            if (_preparationReuseEnabled == value) { return; }
            _preparationReuseEnabled = value;
            InvalidatePreparationReuse();
        }
    }

    internal LightingPreparationReuseTelemetry LastPreparationReuseTelemetry { get; private set; }
    internal long PreparationCachePayloadBytes => _preparationCache?.PayloadBytes ?? 0;

    /// <summary>Call on session replacement, even when reusing the same World object.</summary>
    public void InvalidatePreparationReuse()
    {
        _preparationCache?.Reset();
        LastPreparationReuseTelemetry = default;
    }

    private void CreatePreparationReuseResources(GraphicsDevice device)
    {
        if (_quality.MaskPixelCount is > 0 and <= LightingPreparationCache.MaximumMaskPixels)
        {
            _preparationCache = new LightingPreparationCache(_quality.MaskPixelCount, _quality.MaskSize.X);
        }
        _preparationDevice = device;
        device.DeviceResetting += OnPreparationDeviceReset;
        device.DeviceReset += OnPreparationDeviceReset;
    }

    private void ReleasePreparationReuseResources()
    {
        if (_preparationDevice is not null)
        {
            _preparationDevice.DeviceResetting -= OnPreparationDeviceReset;
            _preparationDevice.DeviceReset -= OnPreparationDeviceReset;
        }
        _preparationDevice = null;
        InvalidatePreparationReuse();
        _preparationCache = null;
        _deviceResourcesInvalidated = false;
    }

    private void OnPreparationDeviceReset(object? sender, EventArgs args)
    {
        // Recreate in the existing Update-time resource preparation, never here/Draw.
        _deviceResourcesInvalidated = true;
        _hasPreparedFrame = false;
        InvalidatePreparationReuse();
    }

    private LightingPreparationCache? PrepareReuseFrame(Camera2D camera, Rectangle viewport)
    {
        if (!PreparationReuseEnabled || _preparationCache is null) { return null; }
        _preparationCache.SetContext(viewport, camera.Position, camera.Zoom, DiagnosticResourceRebuilds);
        return _preparationCache;
    }
}
