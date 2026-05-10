using Game.Client.Configuration;
using Game.Core.Saving;
using System.Globalization;
using System.Text.Json;

namespace Game.Client.GameStates;

public sealed record WorldSaveEntry(
    string Name,
    int Seed,
    DateTimeOffset CreatedAtUtc,
    int WidthTiles,
    int HeightTiles,
    bool IsHorizontallyInfinite,
    string DirectoryPath)
{
    public string SizeLabel => IsHorizontallyInfinite
        ? $"INF X {HeightTiles}"
        : $"{WidthTiles} X {HeightTiles}";

    public string CreatedLabel => CreatedAtUtc.ToLocalTime().ToString("yyyyMMdd HHmm", CultureInfo.InvariantCulture);
}

public sealed class WorldSaveBrowser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<WorldSaveEntry> ListWorlds()
    {
        var root = ClientPaths.WorldSavesRoot();
        if (!Directory.Exists(root))
        {
            return Array.Empty<WorldSaveEntry>();
        }

        var entries = new List<WorldSaveEntry>();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var metadataPath = Path.Combine(directory, "metadata.json");
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            try
            {
                var metadata = JsonSerializer.Deserialize<WorldSaveMetadata>(File.ReadAllText(metadataPath), Options);
                if (metadata is null)
                {
                    continue;
                }

                entries.Add(new WorldSaveEntry(
                    metadata.Name,
                    metadata.Seed,
                    metadata.CreatedAtUtc,
                    metadata.WidthTiles,
                    metadata.HeightTiles,
                    metadata.IsHorizontallyInfinite,
                    directory));
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
        }

        return entries
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
