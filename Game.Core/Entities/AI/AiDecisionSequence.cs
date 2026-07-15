namespace Game.Core.Entities.AI;

internal struct AiDecisionSequence
{
    private uint _state;

    public float NextUnit(int entityId)
    {
        if (_state == 0)
        {
            _state = unchecked((uint)(entityId * 747796405)) + 2891336453u;
        }

        _state = unchecked(_state * 1664525u + 1013904223u);
        return (_state >> 8) * (1f / 16777216f);
    }
}
