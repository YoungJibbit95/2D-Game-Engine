namespace Game.Client.Rendering.Entities;

public sealed class EntityVisualCommandBuffer
{
    private readonly EntityVisualDrawCommand[] _commands;

    public EntityVisualCommandBuffer(int capacity = 2_048, int submissionBucketCapacity = 2_048)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _commands = new EntityVisualDrawCommand[capacity];
        SubmissionPlan = new EntityVisualSubmissionPlan(
            capacity,
            Math.Min(capacity, submissionBucketCapacity));
    }

    public int Capacity => _commands.Length;

    public int Count { get; private set; }

    public EntityVisualTelemetry Telemetry { get; internal set; }

    public EntityVisualSubmissionPlan SubmissionPlan { get; }

    public EntityVisualDrawCommand this[int index] =>
        (uint)index < (uint)Count ? _commands[index] : throw new ArgumentOutOfRangeException(nameof(index));

    public ReadOnlySpan<EntityVisualDrawCommand> Commands => _commands.AsSpan(0, Count);

    internal void Clear()
    {
        Count = 0;
        Telemetry = default;
        SubmissionPlan.Clear();
    }

    internal bool TryAppend(in EntityVisualDrawCommand command)
    {
        if (Count >= _commands.Length)
        {
            return false;
        }

        _commands[Count++] = command;
        return true;
    }

    internal bool HasCapacity(int commandCount)
    {
        return commandCount >= 0 && Count <= _commands.Length - commandCount;
    }

    internal EntityVisualSubmissionTelemetry BuildSubmissionPlan()
    {
        return SubmissionPlan.Build(Commands);
    }
}
