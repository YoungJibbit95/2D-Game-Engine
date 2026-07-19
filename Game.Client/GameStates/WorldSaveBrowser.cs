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
    public string DisplayName { get; } = WorldMenuText.Ellipsize(Name, 28);

    public string CompactDisplayName { get; } = WorldMenuText.Ellipsize(Name, 20);

    public string SizeLabel { get; } = IsHorizontallyInfinite
        ? $"INF X {HeightTiles}"
        : $"{WidthTiles} X {HeightTiles}";

    public string SeedLabel { get; } = $"SEED {Seed}";

    public string CreatedLabel { get; } = CreatedAtUtc
        .ToLocalTime()
        .ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
        .ToUpperInvariant();

    public string CompactMetadataLabel { get; } = IsHorizontallyInfinite
        ? $"INF X {HeightTiles}   SEED {Seed}"
        : $"{WidthTiles} X {HeightTiles}   SEED {Seed}";

    public string MetadataLabel { get; } = IsHorizontallyInfinite
        ? $"INF X {HeightTiles}   SEED {Seed}   {CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant()}"
        : $"{WidthTiles} X {HeightTiles}   SEED {Seed}   {CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant()}";
}

public sealed class WorldSaveBrowser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<string> _rootResolver;

    public WorldSaveBrowser(Func<string>? rootResolver = null)
    {
        _rootResolver = rootResolver ?? ClientPaths.WorldSavesRoot;
    }

    public IReadOnlyList<WorldSaveEntry> ListWorlds()
    {
        string root;
        try
        {
            root = _rootResolver();
        }
        catch (Exception exception) when (IsRecoverableFileSystemFailure(exception))
        {
            return Array.Empty<WorldSaveEntry>();
        }

        if (!Directory.Exists(root))
        {
            return Array.Empty<WorldSaveEntry>();
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(root).ToArray();
        }
        catch (Exception exception) when (IsRecoverableFileSystemFailure(exception))
        {
            return Array.Empty<WorldSaveEntry>();
        }

        var entries = new List<WorldSaveEntry>();
        foreach (var directory in directories)
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
            catch (Exception exception) when (IsRecoverableFileSystemFailure(exception))
            {
            }
        }

        return entries
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsRecoverableFileSystemFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or FormatException;
    }
}
