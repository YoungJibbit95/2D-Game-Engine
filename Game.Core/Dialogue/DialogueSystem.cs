namespace Game.Core.Dialogue;

public sealed class DialogueSystem
{
    public DialogueStepResult Start(DialogueRegistry registry, string dialogueId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(dialogueId);

        if (!registry.TryGetById(dialogueId, out var dialogue))
        {
            return DialogueStepResult.Failed(null, "dialogue_missing");
        }

        var session = new DialogueSession(dialogue.Id, dialogue.StartNodeId);
        return DialogueStepResult.AtNode(session, dialogue.GetNode(dialogue.StartNodeId));
    }

    public DialogueStepResult Advance(DialogueRegistry registry, DialogueSession session, int? optionIndex = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(session);

        if (session.IsEnded)
        {
            return DialogueStepResult.Failed(session, "dialogue_already_ended");
        }

        if (!registry.TryGetById(session.DialogueId, out var dialogue))
        {
            return DialogueStepResult.Failed(session, "dialogue_missing");
        }

        var current = dialogue.GetNode(session.CurrentNodeId);
        DialogueOptionDefinition? selectedOption = null;
        string? targetNodeId = current.NextNodeId;
        var shouldEnd = current.EndsConversation;

        if (optionIndex is not null)
        {
            if (optionIndex.Value < 0 || optionIndex.Value >= current.Options.Count)
            {
                return DialogueStepResult.Failed(session, "option_out_of_range");
            }

            selectedOption = current.Options[optionIndex.Value];
            targetNodeId = selectedOption.TargetNodeId;
            shouldEnd = selectedOption.EndsConversation || string.IsNullOrWhiteSpace(targetNodeId);
        }
        else if (current.Options.Count > 0)
        {
            return DialogueStepResult.AtNode(session, current);
        }

        if (shouldEnd || string.IsNullOrWhiteSpace(targetNodeId))
        {
            session.End();
            return new DialogueStepResult(true, session, current, selectedOption, true, null);
        }

        if (!dialogue.TryGetNode(targetNodeId, out var next))
        {
            return DialogueStepResult.Failed(session, "target_node_missing");
        }

        session.MoveTo(next.Id);
        return DialogueStepResult.AtNode(session, next, selectedOption);
    }
}
