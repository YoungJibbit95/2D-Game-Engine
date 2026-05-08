using Game.Core.World;
using System.Numerics;
using Game.Core;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities;

public sealed class PlayerRespawnSystem
{
    private readonly float _respawnDelaySeconds;
    private float _timeRemaining;
    private bool _pending;

    public PlayerRespawnSystem(float respawnDelaySeconds = 3f)
    {
        if (respawnDelaySeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(respawnDelaySeconds), "Respawn delay must not be negative.");
        }

        _respawnDelaySeconds = respawnDelaySeconds;
    }

    public PlayerRespawnResult Update(PlayerEntity player, GameWorld world, float deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(world);

        if (!player.HealthComponent.IsDead && !_pending)
        {
            return PlayerRespawnResult.None;
        }

        if (!_pending)
        {
            _pending = true;
            _timeRemaining = _respawnDelaySeconds;
        }

        _timeRemaining = Math.Max(0, _timeRemaining - Math.Max(0, deltaSeconds));
        if (_timeRemaining > 0)
        {
            return new PlayerRespawnResult(false, true, _timeRemaining);
        }

        player.Respawn(GetSpawnWorldPosition(world));
        _pending = false;
        return new PlayerRespawnResult(true, false, 0);
    }

    private static Vector2 GetSpawnWorldPosition(GameWorld world)
    {
        return new Vector2(
            world.Metadata.SpawnTile.X * GameConstants.TileSize + 2,
            world.Metadata.SpawnTile.Y * GameConstants.TileSize);
    }
}
