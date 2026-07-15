using Game.Core.Entities;
using Game.Core.Equipment;
using Game.Core.Farming;
using Game.Core.Inventory;
using Game.Core.Projectiles;
using Game.Core.Time;
using Game.Core.World;
using Game.Core.Randomness;

namespace Game.Core.Diagnostics;

public static class SimulationStateHasher
{
    public static ulong Compute(
        World.World world,
        PlayerEntity player,
        PlayerInventory inventory,
        EntityManager entities,
        WorldTime time,
        FarmPlotManager? farmPlots = null,
        EquipmentLoadout? equipment = null,
        SessionRandomRegistry? randomStreams = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(time);

        var hash = new DeterministicHashBuilder();
        AddWorld(ref hash, world);
        AddPlayer(ref hash, player);
        AddInventory(ref hash, inventory);
        AddEntities(ref hash, entities);
        AddTime(ref hash, time);
        AddFarmPlots(ref hash, farmPlots);
        AddEquipment(ref hash, equipment);
        AddRandomStreams(ref hash, randomStreams);
        return hash.Value;
    }

    private static void AddWorld(ref DeterministicHashBuilder hash, World.World world)
    {
        hash.Add(world.WidthTiles);
        hash.Add(world.HeightTiles);
        hash.Add(world.IsHorizontallyInfinite);
        hash.Add(world.Metadata.Seed);
        hash.Add(world.Metadata.Name);
        hash.Add(world.Metadata.SpawnTile.X);
        hash.Add(world.Metadata.SpawnTile.Y);

        foreach (var pair in world.Chunks.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            hash.Add(pair.Key.X);
            hash.Add(pair.Key.Y);
            var chunk = pair.Value;
            for (var index = 0; index < chunk.Tiles.Length; index++)
            {
                var tile = chunk.Tiles[index];
                hash.Add(tile.TileId);
                hash.Add(tile.WallId);
                hash.Add(tile.LiquidAmount);
                hash.Add(tile.Light);
                hash.Add((ushort)tile.Flags);
            }

        }
    }

    private static void AddPlayer(ref DeterministicHashBuilder hash, PlayerEntity player)
    {
        hash.Add(player.Id);
        hash.Add(player.Body.Position.X);
        hash.Add(player.Body.Position.Y);
        hash.Add(player.Body.Velocity.X);
        hash.Add(player.Body.Velocity.Y);
        hash.Add(player.Body.OnGround);
        hash.Add(player.Health);
        hash.Add(player.MaxHealth);
        hash.Add(player.Mana);
        hash.Add(player.MaxMana);
        foreach (var effect in player.StatusEffects.ActiveEffects
                     .OrderBy(effect => effect.Definition.Id, StringComparer.OrdinalIgnoreCase))
        {
            hash.Add(effect.Definition.Id);
            hash.Add(effect.RemainingSeconds);
        }
    }

    private static void AddInventory(ref DeterministicHashBuilder hash, PlayerInventory inventory)
    {
        hash.Add(inventory.SelectedHotbarSlot);
        AddInventorySlots(ref hash, inventory.Hotbar);
        AddInventorySlots(ref hash, inventory.Main);
    }

    private static void AddInventorySlots(ref DeterministicHashBuilder hash, Inventory.Inventory inventory)
    {
        hash.Add(inventory.Slots.Count);
        for (var index = 0; index < inventory.Slots.Count; index++)
        {
            var slot = inventory.Slots[index];
            hash.Add(slot.Stack.ItemId);
            hash.Add(slot.Stack.Count);
            hash.Add(slot.IsFavorite);
        }
    }

    private static void AddEntities(ref DeterministicHashBuilder hash, EntityManager entities)
    {
        foreach (var entity in entities.Entities.OrderBy(entity => entity.Id))
        {
            hash.Add(entity.Id);
            hash.Add(entity.GetType().FullName);
            hash.Add(entity.IsActive);
            hash.Add(entity.Position.X);
            hash.Add(entity.Position.Y);
            hash.Add(entity.Bounds.X);
            hash.Add(entity.Bounds.Y);
            hash.Add(entity.Bounds.Width);
            hash.Add(entity.Bounds.Height);

            switch (entity)
            {
                case EnemyEntity enemy:
                    hash.Add(enemy.DefinitionId);
                    hash.Add(enemy.Body.Velocity.X);
                    hash.Add(enemy.Body.Velocity.Y);
                    hash.Add(enemy.Health.Current);
                    hash.Add(enemy.Health.Max);
                    break;
                case DroppedItemEntity dropped:
                    hash.Add(dropped.Stack.ItemId);
                    hash.Add(dropped.Stack.Count);
                    hash.Add(dropped.Body.Velocity.X);
                    hash.Add(dropped.Body.Velocity.Y);
                    break;
                case ProjectileEntity projectile:
                    hash.Add(projectile.ProjectileId);
                    hash.Add(projectile.Velocity.X);
                    hash.Add(projectile.Velocity.Y);
                    hash.Add(projectile.Damage);
                    hash.Add((int)projectile.DamageType);
                    hash.Add(projectile.Pierce);
                    hash.Add(projectile.Age);
                    break;
            }
        }
    }

    private static void AddTime(ref DeterministicHashBuilder hash, WorldTime time)
    {
        hash.Add(time.Day);
        hash.Add(time.DayLengthSeconds);
        hash.Add(time.TimeOfDaySeconds);
    }

    private static void AddFarmPlots(ref DeterministicHashBuilder hash, FarmPlotManager? farmPlots)
    {
        if (farmPlots is null)
        {
            hash.Add(0);
            return;
        }

        var ordered = farmPlots.Plots.OrderBy(plot => plot.Position.Y).ThenBy(plot => plot.Position.X).ToArray();
        hash.Add(ordered.Length);
        foreach (var plot in ordered)
        {
            hash.Add(plot.Position.X);
            hash.Add(plot.Position.Y);
            hash.Add(plot.IsTilled);
            hash.Add(plot.IsWatered);
            hash.Add(plot.Crop?.CropId);
            hash.Add(plot.Crop?.PlantedDay ?? 0);
            hash.Add(plot.Crop?.DaysUntilHarvest ?? 0);
            hash.Add(plot.Crop?.HarvestCount ?? 0);
        }
    }

    private static void AddEquipment(ref DeterministicHashBuilder hash, EquipmentLoadout? equipment)
    {
        if (equipment is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(equipment.Slots.Count);
        foreach (var pair in equipment.Slots.OrderBy(pair => pair.Key))
        {
            hash.Add((int)pair.Key);
            hash.Add(pair.Value.ItemId);
            hash.Add(pair.Value.Count);
        }
    }

    private static void AddRandomStreams(
        ref DeterministicHashBuilder hash,
        SessionRandomRegistry? randomStreams)
    {
        if (randomStreams is null)
        {
            hash.Add(0);
            return;
        }

        var registry = randomStreams.ExportSnapshot();
        hash.Add(unchecked((long)registry.SessionSeed));
        hash.Add(registry.Streams.Count);
        for (var index = 0; index < registry.Streams.Count; index++)
        {
            var stream = registry.Streams[index];
            hash.Add(stream.Name);
            hash.Add(unchecked((long)stream.State0));
            hash.Add(unchecked((long)stream.State1));
            hash.Add(unchecked((long)stream.State2));
            hash.Add(unchecked((long)stream.State3));
            hash.Add(unchecked((long)stream.DrawCount));
        }
    }

    private struct DeterministicHashBuilder
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        private ulong _value;

        public readonly ulong Value => _value == 0 ? Offset : _value;

        public void Add(bool value) => Add(value ? 1 : 0);

        public void Add(byte value) => Add((ulong)value);

        public void Add(ushort value) => Add((ulong)value);

        public void Add(int value) => Add(unchecked((ulong)(uint)value));

        public void Add(long value) => Add(unchecked((ulong)value));

        public void Add(float value) => Add(BitConverter.SingleToUInt32Bits(value));

        public void Add(double value) => Add(unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));

        public void Add(string? value)
        {
            if (value is null)
            {
                Add(ulong.MaxValue);
                return;
            }

            Add(value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                Add(char.ToUpperInvariant(value[index]));
            }
        }

        private void Add(ulong value)
        {
            if (_value == 0)
            {
                _value = Offset;
            }

            for (var shift = 0; shift < 64; shift += 8)
            {
                _value ^= (byte)(value >> shift);
                _value *= Prime;
            }
        }
    }
}
