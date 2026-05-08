using System.Text.Json;

namespace Game.Core.Settings;

public sealed class GameSettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly GameSettingsValidator _validator;

    public GameSettingsService(GameSettingsValidator? validator = null)
    {
        _validator = validator ?? new GameSettingsValidator();
    }

    public GameSettings LoadOrCreate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            var defaults = GameSettings.CreateDefault();
            Save(path, defaults);
            return defaults;
        }

        return Load(path);
    }

    public GameSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var settings = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"Settings file was empty: {path}");

        EnsureValid(settings);
        return settings;
    }

    public void Save(string path, GameSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(settings);

        EnsureValid(settings);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
    }

    private void EnsureValid(GameSettings settings)
    {
        var validation = _validator.Validate(settings);
        if (validation.IsValid)
        {
            return;
        }

        var message = string.Join(Environment.NewLine, validation.Issues.Select(issue => $"{issue.Path}: {issue.Message}"));
        throw new InvalidDataException($"Settings are invalid:{Environment.NewLine}{message}");
    }
}
