using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public sealed class ShaderEffectRegistry
{
    private readonly Dictionary<string, Effect> _effects = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, Effect> Effects => _effects;

    public void Register(string id, Effect effect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(effect);
        _effects[id] = effect;
    }

    public void Load(ContentManager content, IReadOnlyDictionary<string, string> effectAssets)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(effectAssets);

        foreach (var (id, assetName) in effectAssets)
        {
            Register(id, content.Load<Effect>(assetName));
        }
    }

    public bool TryGet(string id, out Effect effect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _effects.TryGetValue(id, out effect!);
    }
}
