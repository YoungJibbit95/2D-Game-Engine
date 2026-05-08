namespace Game.Core.Entities;

public readonly record struct PlayerRespawnResult(bool Respawned, bool IsPending, float TimeRemaining)
{
    public static PlayerRespawnResult None { get; } = new(false, false, 0);
}
