using Game.Client.Diagnostics;
using Game.Client.GameStates;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Reflection;

namespace Game.Client;

public sealed partial class MainGame
{
    private int _goldenUpdatedFrame = -1;
    private string _goldenContentFingerprint = "";
    private string _goldenClientAssemblyHash = "";
    private string _goldenCoreAssemblyHash = "";

    private bool GoldenSceneContractPassed()
    {
        if (_smokeOptions?.GoldenFixture is not { } fixture)
        {
            return true;
        }
        return _drawnFrames == fixture.Frames &&
            GraphicsDevice.PresentationParameters.BackBufferWidth == fixture.Width &&
            GraphicsDevice.PresentationParameters.BackBufferHeight == fixture.Height &&
            _frameTimes.Count == fixture.Frames - fixture.WarmupFrames &&
            (_states.CurrentState is PlayingState playing
            ? playing.GoldenSceneContractPassed()
            : fixture.StartState == ClientSmokeStartState.WorldSelect && _states.CurrentStateName == "WorldSelect");
    }

    private IReadOnlyDictionary<string, object>? CaptureGoldenScene()
    {
        if (_smokeOptions?.GoldenFixture is not { } fixture)
        {
            return null;
        }
        var data = new Dictionary<string, object>
        {
            ["Fixture"] = fixture,
            ["PresentationFrame"] = _drawnFrames,
            ["SimulationTick"] = 0L,
            ["ContentFingerprint"] = _goldenContentFingerprint
        };
        data["SettingsFingerprint"] = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(_settings)));
        if (_states.CurrentState is PlayingState playing)
        {
            playing.CaptureGoldenScene(data);
        }
        return data;
    }

    private IReadOnlyDictionary<string, string>? CaptureGoldenEnvironment()
    {
        if (_smokeOptions?.GoldenFixture is null)
        {
            return null;
        }
        return new Dictionary<string, string>
        {
            ["OS"] = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : "Other",
            ["Runtime"] = RuntimeInformation.FrameworkDescription,
            ["Architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["MonoGame"] = typeof(GraphicsDevice).Assembly.GetName().Version?.ToString() ?? "unknown",
            ["GraphicsProfile"] = GraphicsDevice.GraphicsProfile.ToString(),
            ["Renderer"] = GraphicsAdapter.DefaultAdapter.Description,
            ["DriverIdentity"] = Environment.GetEnvironmentVariable("YJSE_RENDER_ENVIRONMENT") ?? "unspecified"
        };
    }

    private IReadOnlyDictionary<string, string>? CaptureBenchmarkContext()
    {
        return _smokeOptions?.GoldenFixture is null ? null : new Dictionary<string, string>
        {
            ["OSDescription"] = RuntimeInformation.OSDescription,
            ["CPU"] = Environment.GetEnvironmentVariable("YJSE_CAPTURE_CPU") ?? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown",
            ["LogicalProcessors"] = Environment.ProcessorCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Configuration"] =
#if DEBUG
                "Debug",
#else
                "Release",
#endif
            ["CheckoutRevision"] = Environment.GetEnvironmentVariable("YJSE_CAPTURE_REVISION") ?? "unknown",
            ["ClientAssemblyVersion"] = typeof(MainGame).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
            ["CoreAssemblyVersion"] = typeof(Game.Core.Runtime.GameSimulation).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
            ["ClientAssemblySHA256"] = _goldenClientAssemblyHash,
            ["CoreAssemblySHA256"] = _goldenCoreAssemblyHash,
            ["GPUTime"] = "unknown",
            ["TimingPolicy"] = "Real Stopwatch; 60 FPS cap; frame nearest-rank p95/p99; profiler average is EMA; screenshot readback excluded from Draw"
        };
    }

    private void FingerprintGoldenAssemblies()
    {
        // Called during smoke preload, never during the timed render window.
        _goldenClientAssemblyHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(MainGame).Assembly.Location)));
        _goldenCoreAssemblyHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Game.Core.Runtime.GameSimulation).Assembly.Location)));
    }
}
