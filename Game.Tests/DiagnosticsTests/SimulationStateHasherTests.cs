using Game.Core.Diagnostics;
using Game.Core.Entities;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Physics;
using Game.Core.Time;
using Game.Core.World;
using Game.Core.Randomness;
using System.Numerics;
using Xunit;

namespace Game.Tests.DiagnosticsTests;

public sealed class SimulationStateHasherTests
{
    [Fact]
    public void Compute_EquivalentStatesProduceSameHashRegardlessOfChunkInsertionOrder()
    {
        var first = CreateState(reverseChunks: false);
        var second = CreateState(reverseChunks: true);

        var firstHash = SimulationStateHasher.Compute(
            first.World, first.Player, first.Inventory, first.Entities, first.Time);
        var secondHash = SimulationStateHasher.Compute(
            second.World, second.Player, second.Inventory, second.Entities, second.Time);

        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public void Compute_ChangesForGameplayRelevantState()
    {
        var state = CreateState(reverseChunks: false);
        var before = SimulationStateHasher.Compute(
            state.World, state.Player, state.Inventory, state.Entities, state.Time);

        state.Inventory.Hotbar.SetFavorite(0, true);
        state.World.SetTile(-1, 2, KnownTileIds.Stone);
        state.Time.Update(1);
        var after = SimulationStateHasher.Compute(
            state.World, state.Player, state.Inventory, state.Entities, state.Time);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Compute_IsStableAcrossRepeatedCallsWithoutMutation()
    {
        var state = CreateState(reverseChunks: false);

        var first = SimulationStateHasher.Compute(
            state.World, state.Player, state.Inventory, state.Entities, state.Time);
        var second = SimulationStateHasher.Compute(
            state.World, state.Player, state.Inventory, state.Entities, state.Time);

        Assert.NotEqual(0UL, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_TracksNamedRandomStreamProgress()
    {
        var state = CreateState(reverseChunks: false);
        var randoms = new SessionRandomRegistry(74);
        randoms.GetStream("combat.loot");
        var before = SimulationStateHasher.Compute(
            state.World,
            state.Player,
            state.Inventory,
            state.Entities,
            state.Time,
            randomStreams: randoms);

        randoms.GetStream("combat.loot").NextUInt64();
        var after = SimulationStateHasher.Compute(
            state.World,
            state.Player,
            state.Inventory,
            state.Entities,
            state.Time,
            randomStreams: randoms);

        Assert.NotEqual(before, after);
    }

    private static StateFixture CreateState(bool reverseChunks)
    {
        var world = new World(32, 32, WorldMetadata.CreateDefault(74), isHorizontallyInfinite: true);
        var positions = reverseChunks
            ? new[] { new ChunkPos(1, 0), new ChunkPos(-1, 0) }
            : new[] { new ChunkPos(-1, 0), new ChunkPos(1, 0) };
        foreach (var position in positions)
        {
            world.GetOrCreateChunk(position);
        }

        world.SetTile(-1, 2, KnownTileIds.Dirt);
        world.SetTile(32, 3, KnownTileIds.Stone);
        var items = ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "gel",
                DisplayName = "Gel",
                TexturePath = "items/gel",
                MaxStack = 999
            }
        });
        var inventory = new PlayerInventory(items);
        inventory.AddItem(new ItemStack("gel", 4));
        var player = new PlayerEntity(new Vector2(12, 24), new TileCollisionResolver());
        var entities = new EntityManager();
        entities.Add(new DroppedItemEntity(
            new ItemStack("gel", 2),
            new Vector2(40, 50),
            new TileCollisionResolver()));
        return new StateFixture(world, player, inventory, entities, new WorldTime());
    }

    private sealed record StateFixture(
        World World,
        PlayerEntity Player,
        PlayerInventory Inventory,
        EntityManager Entities,
        WorldTime Time);
}
