namespace Game.Core.Combat;

public sealed class HealthComponent
{
    public HealthComponent(int maxHealth, int? currentHealth = null)
    {
        if (maxHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHealth), "Max health must be greater than zero.");
        }

        Max = maxHealth;
        Current = Math.Clamp(currentHealth ?? maxHealth, 0, maxHealth);
    }

    public int Current { get; private set; }

    public int Max { get; private set; }

    public float InvulnerabilityTimeRemaining { get; private set; }

    public bool IsDead => Current <= 0;

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0)
        {
            return;
        }

        InvulnerabilityTimeRemaining = Math.Max(0, InvulnerabilityTimeRemaining - deltaSeconds);
    }

    public bool ApplyDamage(DamageInfo damage, float invulnerabilitySeconds = 0.45f)
    {
        if (damage.Amount <= 0 || IsDead || InvulnerabilityTimeRemaining > 0)
        {
            return false;
        }

        Current = Math.Max(0, Current - damage.Amount);
        InvulnerabilityTimeRemaining = Math.Max(0, invulnerabilitySeconds);
        return true;
    }

    public bool ApplyRawDamage(int amount)
    {
        if (amount <= 0 || IsDead)
        {
            return false;
        }

        Current = Math.Max(0, Current - amount);
        return true;
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead)
        {
            return;
        }

        Current = Math.Min(Max, Current + amount);
    }

    public void SetCurrent(int currentHealth)
    {
        Current = Math.Clamp(currentHealth, 0, Max);
        if (Current > 0)
        {
            InvulnerabilityTimeRemaining = 0f;
        }
    }
    public void SetMax(int maxHealth, bool preserveRatio = true)
    {
        if (maxHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHealth), "Max health must be greater than zero.");
        }

        var oldMax = Max;
        var oldCurrent = Current;
        Max = maxHealth;
        Current = preserveRatio && oldMax > 0
            ? Math.Clamp((int)MathF.Round(Max * (oldCurrent / (float)oldMax)), 0, Max)
            : Math.Clamp(oldCurrent, 0, Max);
    }

    public void RestoreFull()
    {
        Current = Max;
        InvulnerabilityTimeRemaining = 0;
    }
}
