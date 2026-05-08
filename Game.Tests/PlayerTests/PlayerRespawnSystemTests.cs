using Game.Core;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.PlayerTests;

public sealed class PlayerRespawnSystemTests
{
    [Fact]
    public void Update_RespawnsDeadPlayerAtWorldSpawnAfterDelay()
    {
        var world = new World(32, 32, WorldMetadata.CreateDefault(seed: 1) with
        {
            SpawnTile = new TilePos(10, 5)
        });
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver(), maxHealth: 20);
        player.ApplyDamage(new DamageInfo(20, DamageType.Generic, null, Vector2.Zero, 0), invulnerabilitySeconds: 0);
        var respawn = new PlayerRespawnSystem(respawnDelaySeconds: 1);

        var pending = respawn.Update(player, world, 0.5f);
        var completed = respawn.Update(player, world, 0.5f);

        Assert.False(pending.Respawned);
        Assert.True(pending.IsPending);
        Assert.True(completed.Respawned);
        Assert.Equal(20, player.Health);
        Assert.Equal(new Vector2(10 * GameConstants.TileSize + 2, 5 * GameConstants.TileSize), player.Body.Position);
    }
}
