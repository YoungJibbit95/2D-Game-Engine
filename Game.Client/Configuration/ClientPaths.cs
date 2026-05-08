namespace Game.Client.Configuration;

public static class ClientPaths
{
    public static string SettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "TerrariaLike", "settings.json");
    }

    public static string FindGameDataRoot()
    {
        foreach (var root in CandidateRoots())
        {
            var gameData = Path.Combine(root, "Game.Data");
            if (Directory.Exists(gameData))
            {
                return gameData;
            }
        }

        throw new DirectoryNotFoundException("Could not locate Game.Data next to the workspace or output directory.");
    }

    public static string ModsRoot()
    {
        var dataRoot = FindGameDataRoot();
        return Path.Combine(Directory.GetParent(dataRoot)!.FullName, "Mods");
    }

    private static IEnumerable<string> CandidateRoots()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && current is not null; i++)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }
}
