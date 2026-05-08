using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Projectiles;

public sealed class ProjectileDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public ProjectileRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return ProjectileRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<ProjectileDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<ProjectileDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public ProjectileDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public ProjectileDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static ProjectileDefinition LoadDefinition(Stream stream, string source)
    {
        var dto = JsonSerializer.Deserialize<ProjectileDefinitionDto>(stream, Options);
        if (dto is null)
        {
            throw new JsonException($"Projectile definition was empty: {source}");
        }

        return dto.ToDefinition();
    }

    private sealed record ProjectileDefinitionDto
    {
        public string? Id { get; init; }

        public string? TexturePath { get; init; }

        [JsonPropertyName("texture")]
        public string? Texture { get; init; }

        public float Speed { get; init; }

        public int Damage { get; init; }

        public float Gravity { get; init; }

        public int Pierce { get; init; }

        public float Lifetime { get; init; }

        public ProjectileDefinition ToDefinition()
        {
            return new ProjectileDefinition
            {
                Id = Id ?? string.Empty,
                TexturePath = TexturePath ?? Texture ?? string.Empty,
                Speed = Speed,
                Damage = Damage,
                Gravity = Gravity,
                Pierce = Pierce,
                Lifetime = Lifetime
            };
        }
    }
}
