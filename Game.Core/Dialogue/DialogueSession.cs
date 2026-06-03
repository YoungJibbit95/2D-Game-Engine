namespace Game.Core.Dialogue;

public sealed class DialogueSession
{
    public DialogueSession(string dialogueId, string currentNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialogueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentNodeId);

        DialogueId = dialogueId;
        CurrentNodeId = currentNodeId;
    }

    public string DialogueId { get; }

    public string CurrentNodeId { get; private set; }

    public bool IsEnded { get; private set; }

    public IReadOnlyList<string> VisitedNodeIds => _visitedNodeIds;

    private readonly List<string> _visitedNodeIds = new();

    public void MoveTo(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        _visitedNodeIds.Add(CurrentNodeId);
        CurrentNodeId = nodeId;
    }

    public void End()
    {
        if (!IsEnded)
        {
            _visitedNodeIds.Add(CurrentNodeId);
        }

        IsEnded = true;
    }
}
