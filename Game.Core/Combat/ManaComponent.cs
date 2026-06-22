namespace Game.Core.Combat;

public sealed class ManaComponent
{
    private const float DefaultRegenDelaySeconds = 1.2f;

    private float _regenAccumulator;
    private float _regenDelayRemaining;

    public ManaComponent(int maxMana = 20, int? currentMana = null)
    {
        Max = Math.Max(0, maxMana);
        Current = Math.Clamp(currentMana ?? Max, 0, Max);
    }

    public int Current { get; private set; }

    public int Max { get; private set; }

    public float RegenDelayRemaining => _regenDelayRemaining;

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (Current < amount)
        {
            return false;
        }

        Current -= amount;
        _regenDelayRemaining = DefaultRegenDelaySeconds;
        _regenAccumulator = 0;
        return true;
    }

    public void Restore(int amount)
    {
        if (amount <= 0 || Max <= 0)
        {
            return;
        }

        Current = Math.Min(Max, Current + amount);
    }

    public void RestoreFull()
    {
        Current = Max;
        _regenAccumulator = 0;
        _regenDelayRemaining = 0;
    }

    public void SetMax(int maxMana, bool preserveRatio = true)
    {
        var oldMax = Max;
        var oldCurrent = Current;
        Max = Math.Max(0, maxMana);

        if (Max == 0)
        {
            Current = 0;
            _regenAccumulator = 0;
            return;
        }

        Current = preserveRatio && oldMax > 0
            ? Math.Clamp((int)MathF.Round(Max * (oldCurrent / (float)oldMax)), 0, Max)
            : Math.Clamp(oldCurrent, 0, Max);
    }

    public void Update(float deltaSeconds, float regenPerSecond = 6f, float regenMultiplier = 1f)
    {
        if (deltaSeconds <= 0 || Max <= 0 || Current >= Max)
        {
            return;
        }

        if (_regenDelayRemaining > 0)
        {
            _regenDelayRemaining = Math.Max(0, _regenDelayRemaining - deltaSeconds);
            return;
        }

        var regen = Math.Max(0, regenPerSecond * Math.Max(0, regenMultiplier));
        if (regen <= 0)
        {
            return;
        }

        _regenAccumulator += regen * deltaSeconds;
        var restore = (int)MathF.Floor(_regenAccumulator);
        if (restore <= 0)
        {
            return;
        }

        _regenAccumulator -= restore;
        Restore(restore);
    }
}
