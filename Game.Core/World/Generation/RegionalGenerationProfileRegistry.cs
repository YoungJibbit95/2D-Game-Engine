using Game.Core.Data;

namespace Game.Core.World.Generation;

public sealed class RegionalGenerationProfileRegistry
{
    private readonly Dictionary<string, RegionalGenerationProfile> _byId;

    private RegionalGenerationProfileRegistry(IEnumerable<RegionalGenerationProfile> profiles)
    {
        _byId = new Dictionary<string, RegionalGenerationProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            ArgumentNullException.ThrowIfNull(profile);
            if (string.IsNullOrWhiteSpace(profile.Id))
            {
                throw new RegistryValidationException("Regional generation profile id is required.");
            }

            if (!_byId.TryAdd(profile.Id, profile))
            {
                throw new RegistryValidationException($"Duplicate regional generation profile id '{profile.Id}'.");
            }
        }
    }

    public IReadOnlyCollection<RegionalGenerationProfile> Profiles => _byId.Values;

    public static RegionalGenerationProfileRegistry Create(IEnumerable<RegionalGenerationProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        return new RegionalGenerationProfileRegistry(profiles);
    }

    public bool TryGetById(string id, out RegionalGenerationProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out profile!);
    }

    public RegionalGenerationProfile GetById(string id)
    {
        return TryGetById(id, out var profile)
            ? profile
            : throw new KeyNotFoundException($"Regional generation profile '{id}' was not found.");
    }
}
