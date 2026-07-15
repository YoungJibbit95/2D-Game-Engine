using System.Numerics;

namespace Game.Core.Animation;

public enum CharacterAppearanceSlot
{
    Body,
    Eyes,
    Hair,
    Clothing,
    Armor,
    Hands,
    Shield,
    Tool,
    Accessory
}

public sealed record CharacterPartAppearance
{
    public CharacterPartAppearance(
        string spriteId,
        AnimationColor? tint = null,
        bool visible = true)
    {
        if (visible)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        }

        SpriteId = spriteId ?? string.Empty;
        Tint = tint ?? AnimationColor.White;
        Visible = visible;
    }

    public string SpriteId { get; }

    public AnimationColor Tint { get; }

    public bool Visible { get; }

    public static CharacterPartAppearance Hidden { get; } = new(string.Empty, visible: false);
}

public sealed class CharacterAppearanceProfile
{
    public CharacterAppearanceProfile(
        CharacterPartAppearance body,
        CharacterPartAppearance? eyes = null,
        CharacterPartAppearance? hair = null,
        CharacterPartAppearance? clothing = null,
        CharacterPartAppearance? armor = null,
        CharacterPartAppearance? hands = null,
        CharacterPartAppearance? shield = null,
        CharacterPartAppearance? tool = null,
        CharacterPartAppearance? accessory = null)
    {
        Body = body ?? throw new ArgumentNullException(nameof(body));
        Eyes = eyes ?? CharacterPartAppearance.Hidden;
        Hair = hair ?? CharacterPartAppearance.Hidden;
        Clothing = clothing ?? CharacterPartAppearance.Hidden;
        Armor = armor ?? CharacterPartAppearance.Hidden;
        Hands = hands ?? body;
        Shield = shield ?? CharacterPartAppearance.Hidden;
        Tool = tool ?? CharacterPartAppearance.Hidden;
        Accessory = accessory ?? CharacterPartAppearance.Hidden;
    }

    public CharacterPartAppearance Body { get; }

    public CharacterPartAppearance Eyes { get; }

    public CharacterPartAppearance Hair { get; }

    public CharacterPartAppearance Clothing { get; }

    public CharacterPartAppearance Armor { get; }

    public CharacterPartAppearance Hands { get; }

    public CharacterPartAppearance Shield { get; }

    public CharacterPartAppearance Tool { get; }

    public CharacterPartAppearance Accessory { get; }

    public CharacterPartAppearance GetPart(CharacterAppearanceSlot slot)
    {
        return slot switch
        {
            CharacterAppearanceSlot.Body => Body,
            CharacterAppearanceSlot.Eyes => Eyes,
            CharacterAppearanceSlot.Hair => Hair,
            CharacterAppearanceSlot.Clothing => Clothing,
            CharacterAppearanceSlot.Armor => Armor,
            CharacterAppearanceSlot.Hands => Hands,
            CharacterAppearanceSlot.Shield => Shield,
            CharacterAppearanceSlot.Tool => Tool,
            CharacterAppearanceSlot.Accessory => Accessory,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
    }
}

public sealed class CharacterRigLayerDefinition
{
    public CharacterRigLayerDefinition(
        string id,
        CharacterAppearanceSlot appearanceSlot,
        string animationTrackId,
        int drawOrder,
        string? parentLayerId = null,
        string? parentSocketId = null,
        AnimationTransform2D? localTransform = null,
        Vector2? pivot = null,
        AnimationColor? tint = null,
        bool visible = true,
        bool useTrackSprite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(animationTrackId);
        if ((parentLayerId is null) != (parentSocketId is null))
        {
            throw new ArgumentException("Parent layer and parent socket must be supplied together.");
        }

        Id = id;
        AppearanceSlot = appearanceSlot;
        AnimationTrackId = animationTrackId;
        DrawOrder = drawOrder;
        ParentLayerId = parentLayerId;
        ParentSocketId = parentSocketId;
        LocalTransform = localTransform ?? AnimationTransform2D.Identity;
        Pivot = pivot ?? Vector2.Zero;
        Tint = tint ?? AnimationColor.White;
        Visible = visible;
        UseTrackSprite = useTrackSprite;
    }

    public string Id { get; }

    public CharacterAppearanceSlot AppearanceSlot { get; }

    public string AnimationTrackId { get; }

    public int DrawOrder { get; }

    public string? ParentLayerId { get; }

    public string? ParentSocketId { get; }

    public AnimationTransform2D LocalTransform { get; }

    public Vector2 Pivot { get; }

    public AnimationColor Tint { get; }

    public bool Visible { get; }

    public bool UseTrackSprite { get; }
}

public sealed class CharacterRigProfile
{
    private readonly CharacterRigLayerDefinition[] _layers;

    public CharacterRigProfile(string id, IEnumerable<CharacterRigLayerDefinition> layers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(layers);
        Id = id;
        _layers = layers
            .OrderBy(layer => layer.DrawOrder)
            .ThenBy(layer => layer.Id, StringComparer.Ordinal)
            .ToArray();

        if (_layers.Length == 0)
        {
            throw new ArgumentException("A character rig needs at least one layer.", nameof(layers));
        }

        ValidateLayers();
    }

    public string Id { get; }

    public IReadOnlyList<CharacterRigLayerDefinition> Layers => _layers;

    private void ValidateLayers()
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < _layers.Length; index++)
        {
            var layer = _layers[index];
            if (!seenIds.Add(layer.Id))
            {
                throw new ArgumentException($"Duplicate character rig layer id '{layer.Id}'.", nameof(Layers));
            }

            if (layer.ParentLayerId is not null && !seenIds.Contains(layer.ParentLayerId))
            {
                throw new ArgumentException(
                    $"Parent layer '{layer.ParentLayerId}' for '{layer.Id}' must appear earlier in draw order.",
                    nameof(Layers));
            }
        }
    }
}

public sealed record CharacterLayerOverride
{
    public CharacterLayerOverride(
        string layerId,
        string? spriteId = null,
        AnimationColor? tint = null,
        bool? visible = null,
        int? frameIndex = null,
        int frameIndexOffset = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        if (spriteId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        }

        if (frameIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        LayerId = layerId;
        SpriteId = spriteId;
        Tint = tint;
        Visible = visible;
        FrameIndex = frameIndex;
        FrameIndexOffset = frameIndexOffset;
    }

    public string LayerId { get; }

    public string? SpriteId { get; }

    public AnimationColor? Tint { get; }

    public bool? Visible { get; }

    public int? FrameIndex { get; }

    public int FrameIndexOffset { get; }
}

public readonly record struct ResolvedCharacterLayerOverride(
    string? SpriteId,
    AnimationColor? Tint,
    bool? Visible,
    int? FrameIndex,
    int FrameIndexOffset)
{
    public static ResolvedCharacterLayerOverride None { get; } = new(null, null, null, null, 0);
}

public sealed class CharacterRenderOverrides
{
    private readonly Dictionary<string, CharacterLayerOverride> _equipment;
    private readonly Dictionary<string, CharacterLayerOverride> _action;

    public CharacterRenderOverrides(
        IEnumerable<CharacterLayerOverride>? equipmentOverrides = null,
        IEnumerable<CharacterLayerOverride>? actionOrToolOverrides = null)
    {
        _equipment = BuildMap(equipmentOverrides, nameof(equipmentOverrides));
        _action = BuildMap(actionOrToolOverrides, nameof(actionOrToolOverrides));
    }

    public static CharacterRenderOverrides Empty { get; } = new();

    public ResolvedCharacterLayerOverride Resolve(string layerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        _equipment.TryGetValue(layerId, out var equipment);
        _action.TryGetValue(layerId, out var action);

        if (equipment is null && action is null)
        {
            return ResolvedCharacterLayerOverride.None;
        }

        return new ResolvedCharacterLayerOverride(
            action?.SpriteId ?? equipment?.SpriteId,
            action?.Tint ?? equipment?.Tint,
            action?.Visible ?? equipment?.Visible,
            action?.FrameIndex ?? equipment?.FrameIndex,
            checked((equipment?.FrameIndexOffset ?? 0) + (action?.FrameIndexOffset ?? 0)));
    }

    private static Dictionary<string, CharacterLayerOverride> BuildMap(
        IEnumerable<CharacterLayerOverride>? overrides,
        string argumentName)
    {
        var result = new Dictionary<string, CharacterLayerOverride>(StringComparer.OrdinalIgnoreCase);
        if (overrides is null)
        {
            return result;
        }

        foreach (var layerOverride in overrides)
        {
            if (!result.TryAdd(layerOverride.LayerId, layerOverride))
            {
                throw new ArgumentException(
                    $"Duplicate character layer override '{layerOverride.LayerId}'.",
                    argumentName);
            }
        }

        return result;
    }
}
