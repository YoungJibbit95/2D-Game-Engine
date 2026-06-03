using Game.Core.Data;
using System.Text.Json;

namespace Game.Core.Projects;

public sealed class GameProjectManifestLoader
{
    public const string ManifestFileName = "yjse.game.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public GameProjectManifest LoadFromFile(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Game project manifest does not exist: {manifestPath}", manifestPath);
        }

        return LoadFromJson(File.ReadAllText(manifestPath), manifestPath);
    }

    public GameProjectManifest LoadFromJson(string json)
    {
        return LoadFromJson(json, "inline json");
    }

    private static GameProjectManifest LoadFromJson(string json, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var dto = JsonSerializer.Deserialize<GameProjectManifestDto>(json, Options);
        if (dto is null)
        {
            throw new JsonException($"Game project manifest was empty: {source}");
        }

        var manifest = dto.ToManifest();
        Validate(manifest, source);
        return manifest;
    }

    public GameProjectManifest CreateFallbackForContentRoot(string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        var id = Path.GetFileName(Path.GetFullPath(contentRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var manifest = new GameProjectManifest
        {
            Id = string.IsNullOrWhiteSpace(id) ? "external_game" : id,
            DisplayName = string.IsNullOrWhiteSpace(id) ? "External Game" : id,
            ContentRoot = ".",
            ModsRoot = "../Mods"
        };

        Validate(manifest, contentRoot);
        return manifest;
    }

    private static void Validate(GameProjectManifest manifest, string source)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.SchemaVersion <= 0)
        {
            throw new RegistryValidationException($"Game project manifest '{source}' must use a positive schemaVersion.");
        }

        RequireText(manifest.Id, nameof(manifest.Id), source);
        RequireText(manifest.DisplayName, nameof(manifest.DisplayName), source);
        RequireText(manifest.Version, nameof(manifest.Version), source);
        RequireText(manifest.ContentRoot, nameof(manifest.ContentRoot), source);
        RequireText(manifest.ModsRoot, nameof(manifest.ModsRoot), source);
        RequireText(manifest.SavesRootName, nameof(manifest.SavesRootName), source);
    }

    private static void RequireText(string value, string fieldName, string source)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Game project manifest '{source}' field '{fieldName}' is required.");
        }
    }

    private sealed record GameProjectManifestDto
    {
        public int SchemaVersion { get; init; } = 1;

        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public string Version { get; init; } = "0.1.0";

        public string EngineVersion { get; init; } = "local";

        public string ContentRoot { get; init; } = "Game.Data";

        public string ModsRoot { get; init; } = "Mods";

        public string SavesRootName { get; init; } = "Saves";

        public string? DefaultWorldProfileId { get; init; }

        public string? StartupMapId { get; init; }

        public string[] Tags { get; init; } = Array.Empty<string>();

        public GameProjectManifest ToManifest()
        {
            return new GameProjectManifest
            {
                SchemaVersion = SchemaVersion,
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                Version = Version,
                EngineVersion = EngineVersion,
                ContentRoot = ContentRoot,
                ModsRoot = ModsRoot,
                SavesRootName = SavesRootName,
                DefaultWorldProfileId = DefaultWorldProfileId,
                StartupMapId = StartupMapId,
                Tags = DefinitionTags.Normalize(Tags)
            };
        }
    }
}
