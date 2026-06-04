using Game.Client.Configuration;
using Game.Core.Sessions;
using Game.Core.Settings;

namespace Game.Client.GameStates;

public static class WorldSessionFactory
{
    public static LoadedGameSession CreateSingleplayer(int seed, string worldName)
    {
        var projectRoot = ClientPaths.FindGameProjectPaths().ProjectRoot;
        var settings = LoadSettings();
        var saveDirectory = ClientPaths.WorldSaveDirectory(worldName, seed);

        return new GameSessionBootstrapper()
            .LoadOrCreate(new GameSessionBootstrapRequest(projectRoot, saveDirectory, seed, worldName, settings))
            .Session;
    }

    private static GameSettings LoadSettings()
    {
        try
        {
            return new GameSettingsService().LoadOrCreate(ClientPaths.SettingsPath());
        }
        catch (InvalidDataException)
        {
            return GameSettings.CreateDefault();
        }
    }
}
