using Game.Core.Data;

namespace Game.Core.World.Generation;

public sealed class WorldGenerationProfileRegistry
{
    private readonly Dictionary<string, WorldGenerationProfile> _byId;

    private WorldGenerationProfileRegistry(IEnumerable<WorldGenerationProfile> profiles)
    {
        _byId = new Dictionary<string, WorldGenerationProfile>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profiles)
        {
            AddValidated(profile);
        }
    }

    public IReadOnlyCollection<WorldGenerationProfile> Profiles => _byId.Values;

    public static WorldGenerationProfileRegistry Create(IEnumerable<WorldGenerationProfile> profiles)
    {
        return new WorldGenerationProfileRegistry(profiles);
    }

    public bool TryGetById(string id, out WorldGenerationProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out profile!);
    }

    public WorldGenerationProfile GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.GetValueOrDefault(id)
            ?? throw new KeyNotFoundException($"World generation profile '{id}' was not found.");
    }

    private void AddValidated(WorldGenerationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            throw new RegistryValidationException("World generation profile id is required.");
        }

        if (profile.WidthTiles <= 0 || profile.HeightTiles <= 0)
        {
            throw new RegistryValidationException($"World generation profile '{profile.Id}' has invalid dimensions.");
        }

        if (_byId.ContainsKey(profile.Id))
        {
            throw new RegistryValidationException($"Duplicate world generation profile id '{profile.Id}'.");
        }

        _byId.Add(profile.Id, profile);
    }
}
