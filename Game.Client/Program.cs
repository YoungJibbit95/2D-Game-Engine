using Serilog;

namespace Game.Client;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            Log.Information("Starting YjsE client");
            using var game = new MainGame();
            game.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Game crashed");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
