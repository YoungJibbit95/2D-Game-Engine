using System.Numerics;

namespace Game.Core.Entities.AI;

internal sealed class AiPerceptionMemory
{
    private float _remainingSeconds;

    public int? TargetEntityId { get; private set; }

    public Vector2 LastKnownPosition { get; private set; }

    public bool IsActive => TargetEntityId is not null && _remainingSeconds > 0;

    public float RemainingSeconds => _remainingSeconds;

    public void Advance(float deltaSeconds)
    {
        if (deltaSeconds <= 0 || TargetEntityId is null)
        {
            return;
        }

        _remainingSeconds = Math.Max(0, _remainingSeconds - deltaSeconds);
        if (_remainingSeconds <= 0)
        {
            Clear();
        }
    }

    public void Observe(Entity target, float memorySeconds)
    {
        ArgumentNullException.ThrowIfNull(target);
        TargetEntityId = target.Id;
        LastKnownPosition = Sensing.DistanceSensor.GetCenter(target);
        _remainingSeconds = Math.Max(0, memorySeconds);
    }

    public void Clear()
    {
        TargetEntityId = null;
        LastKnownPosition = default;
        _remainingSeconds = 0;
    }
}
