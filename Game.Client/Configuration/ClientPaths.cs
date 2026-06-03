using Game.Core.Data;
using Game.Core.Projects;
using System.Text.Json;

namespace Game.Client.Configuration;

public static class ClientPaths
{
    public const string AppName = "YjsE";
    public const string GameRootEnvironmentVariable = "YJSE_GAME_ROOT";
    public const string GameDataEnvironmentVariable = "YJSE_GAME_DATA";
    public const string ModsRootEnvironmentVariable = "YJSE_MODS_ROOT";

    public static string SettingsPath()
    {
        return Path.Combine(AppDataRoot(), "settings.json");
    }

    public static string AppDataRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var projectId = TryLoadGameProjectManifest()?.Id ?? AppName;
        return Path.Combine(appData, AppName, SanitizePathSegment(projectId));
    }

    public static string WorldSavesRoot()
    {
        var savesRootName = TryLoadGameProjectManifest()?.SavesRootName ?? "Saves";
        return Path.Combine(AppDataRoot(), SanitizePathSegment(savesRootName), "Worlds");
    }

    public static string WorldSaveDirectory(string worldName, int seed)
    {
        return Path.Combine(WorldSavesRoot(), $"{SanitizePathSegment(worldName)}_{seed}");
    }

    public static string FindGameDataRoot()
    {
        return FindGameProjectPaths().ContentRoot;
    }

    public static GameProjectPaths FindGameProjectPaths()
    {
        var resolver = new GameProjectResolver();

        var explicitDataRoot = Environment.GetEnvironmentVariable(GameDataEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitDataRoot))
        {
            return resolver.Resolve(explicitDataRoot);
        }

        var explicitGameRoot = Environment.GetEnvironmentVariable(GameRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitGameRoot))
        {
            return resolver.Resolve(explicitGameRoot);
        }

        return resolver.ResolveFirstExisting(CandidateProjectRoots());
    }

    public static GameProjectManifest LoadGameProjectManifest()
    {
        var resolver = new GameProjectResolver();
        return resolver.LoadManifest(FindGameProjectPaths());
    }

    public static string ModsRoot()
    {
        var explicitModsRoot = Environment.GetEnvironmentVariable(ModsRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitModsRoot))
        {
            return Path.GetFullPath(explicitModsRoot);
        }

        return FindGameProjectPaths().ModsRoot;
    }

    private static GameProjectManifest? TryLoadGameProjectManifest()
    {
        try
        {
            return LoadGameProjectManifest();
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (RegistryValidationException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> CandidateProjectRoots()
    {
        foreach (var root in CandidateBaseRoots())
        {
            yield return root;
            yield return Path.Combine(root, "Game.Data");
        }
    }

    private static IEnumerable<string> CandidateBaseRoots()
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

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "World";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value
            .Trim()
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray();
        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "World" : sanitized;
    }
}
