using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public readonly record struct ShaderEffectHandle(int Value)
{
    public static ShaderEffectHandle Invalid { get; } = new(-1);

    public bool IsValid => Value >= 0;
}

public sealed class ShaderEffectRegistry
{
    private readonly Dictionary<string, Effect> _effects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ShaderEffectHandle> _handles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Effect> _effectsByHandle = new();

    public IReadOnlyDictionary<string, Effect> Effects => _effects;

    public ShaderEffectHandle Register(string id, Effect effect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(effect);
        if (_handles.TryGetValue(id, out var existing))
        {
            _effects[id] = effect;
            _effectsByHandle[existing.Value] = effect;
            return existing;
        }

        var handle = new ShaderEffectHandle(_effectsByHandle.Count);
        _effects[id] = effect;
        _handles[id] = handle;
        _effectsByHandle.Add(effect);
        return handle;
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

    public bool TryGetHandle(string id, out ShaderEffectHandle handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _handles.TryGetValue(id, out handle);
    }

    public bool TryGet(ShaderEffectHandle handle, out Effect effect)
    {
        if ((uint)handle.Value < (uint)_effectsByHandle.Count)
        {
            effect = _effectsByHandle[handle.Value];
            return true;
        }

        effect = null!;
        return false;
    }
}
