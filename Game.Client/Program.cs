using Game.Client.Diagnostics;
using Serilog;

namespace Game.Client;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        ClientSmokeOptions? smokeOptions = null;
        MainGame? game = null;
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            smokeOptions = ClientSmokeOptions.Parse(args);
            Log.Information("Starting YjsE client");
            game = new MainGame(smokeOptions);
            try
            {
                game.Run();
                if (smokeOptions is not null)
                {
                    var result = game.SmokeResult ?? ClientSmokeResult.CaptureFailed(
                        0,
                        smokeOptions.ScreenshotPath,
                        new InvalidOperationException("The client exited before a smoke frame was captured."));
                    WriteSmokeResult(result);
                    return result.Passed ? 0 : 3;
                }

                return 0;
            }
            finally
            {
                game.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Game crashed");
            CrashReportWriter.TryWrite(ex, game?.CurrentStateName);
            if (smokeOptions is not null)
            {
                var result = game?.SmokeResult ?? ClientSmokeResult.CaptureFailed(
                    0,
                    smokeOptions.ScreenshotPath,
                    ex);
                WriteSmokeResult(result);
            }

            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void WriteSmokeResult(ClientSmokeResult result)
    {
        result.WriteJsonForScreenshot();
        Log.Information(
            "Client smoke {Result}: frame={Frame} nonBlack={NonBlack} colors={Colors} resources={Resources} frames={Frames} entities={Entities} visible={VisibleEntities}",
            result.Passed ? "passed" : "failed",
            result.CapturedFrame,
            result.NonBlackPixels,
            result.DistinctColors,
            result.TextureResourceCount,
            result.TextureFrameCount,
            result.Gameplay.TotalActiveEntities,
            result.Gameplay.VisibleEntities);
    }
}
