namespace Game.Core.Maps;

public sealed class TopDownMapObjectRuntimeState
{
    public TopDownMapObjectRuntimeState(string objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        ObjectId = objectId;
    }

    public string ObjectId { get; }

    public bool IsEnabled { get; set; } = true;

    public bool IsOpen { get; set; }

    public int InteractionCount { get; set; }

    public long LastInteractionTick { get; set; } = -1;

    public IDictionary<string, string> Properties { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
