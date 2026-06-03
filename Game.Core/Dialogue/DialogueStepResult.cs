namespace Game.Core.Dialogue;

public sealed record DialogueStepResult(
    bool Success,
    DialogueSession? Session,
    DialogueNodeDefinition? Node,
    DialogueOptionDefinition? SelectedOption,
    bool Ended,
    string? FailureReason)
{
    public static DialogueStepResult Failed(DialogueSession? session, string reason)
    {
        return new DialogueStepResult(false, session, null, null, session?.IsEnded ?? false, reason);
    }

    public static DialogueStepResult AtNode(DialogueSession session, DialogueNodeDefinition node, DialogueOptionDefinition? selectedOption = null)
    {
        return new DialogueStepResult(true, session, node, selectedOption, session.IsEnded, null);
    }
}
