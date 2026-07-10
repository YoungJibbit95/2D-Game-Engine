using Game.Core.Characters;
using Game.Core.Effects;
using Game.Core.Equipment;
using Game.Core.Entities;
using Game.Core.Inventory;
using Game.Core.Items;
using System.Text.Json;
using InventoryModel = Game.Core.Inventory.Inventory;

namespace Game.Core.Saving;

public sealed class PlayerSaveService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public void Save(PlayerSaveData data, string filePath)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(data, Options));
    }

    public PlayerSaveData Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Player save file was not found.", filePath);
        }

        return JsonSerializer.Deserialize<PlayerSaveData>(File.ReadAllText(filePath), Options)
            ?? throw new InvalidDataException("Player save file was empty.");
    }

    public PlayerSaveData CreateSaveData(
        PlayerEntity player,
        PlayerInventory inventory,
        string playerId,
        string displayName,
        int? mana = null,
        EquipmentLoadout? equipmentLoadout = null,
        CharacterAppearance? characterAppearance = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new PlayerSaveData
        {
            PlayerId = playerId,
            DisplayName = displayName,
            PositionX = player.Body.Position.X,
            PositionY = player.Body.Position.Y,
            Health = player.Health,
            MaxHealth = player.MaxHealth,
            Mana = mana ?? player.Mana,
            SelectedHotbarSlot = inventory.SelectedHotbarSlot,
            InventorySlots = inventory.Hotbar.Slots
                .Concat(inventory.Main.Slots)
                .Select(slot => slot.Stack)
                .ToArray(),
            EquipmentLoadout = equipmentLoadout is null
                ? null
                : new EquipmentLoadoutSaveData
                {
                    Slots = equipmentLoadout.Slots
                        .Where(pair => !pair.Value.IsEmpty)
                        .OrderBy(pair => pair.Key)
                        .Select(pair => new EquipmentSlotSaveData
                        {
                            SlotId = pair.Key.ToString(),
                            ItemId = pair.Value.ItemId
                        })
                        .ToArray()
                },
            ActiveStatusEffects = player.StatusEffects.ActiveEffects
                .Where(effect => float.IsFinite(effect.RemainingSeconds) && effect.RemainingSeconds > 0)
                .OrderBy(effect => effect.Definition.Id, StringComparer.OrdinalIgnoreCase)
                .Select(effect => new ActiveStatusEffectSaveData
                {
                    EffectId = effect.Definition.Id,
                    RemainingDurationSeconds = effect.RemainingSeconds
                })
                .ToArray(),
            CharacterAppearance = characterAppearance
        };
    }

    public EquipmentLoadout ToEquipmentLoadout(
        PlayerSaveData data,
        ItemRegistry itemDefinitions,
        ICollection<PlayerLoadWarning>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(itemDefinitions);

        var loadout = new EquipmentLoadout();
        var restoredSlots = new HashSet<EquipmentSlotType>();
        var slots = data.EquipmentLoadout?.Slots ?? Array.Empty<EquipmentSlotSaveData>();

        foreach (var entry in slots)
        {
            if (entry is null)
            {
                AddWarning(
                    warnings,
                    PlayerLoadWarningKind.InvalidEquipmentEntry,
                    null,
                    "Skipped a null equipment entry.");
                continue;
            }

            if (!Enum.TryParse<EquipmentSlotType>(entry.SlotId, ignoreCase: true, out var slot) ||
                !Enum.IsDefined(slot))
            {
                AddWarning(
                    warnings,
                    PlayerLoadWarningKind.InvalidEquipmentSlot,
                    entry.SlotId,
                    $"Skipped equipment entry with unknown slot '{entry.SlotId}'.");
                continue;
            }

            if (restoredSlots.Contains(slot))
            {
                AddWarning(
                    warnings,
                    PlayerLoadWarningKind.DuplicateEquipmentSlot,
                    entry.SlotId,
                    $"Skipped duplicate equipment slot '{entry.SlotId}'.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.ItemId))
            {
                AddWarning(
                    warnings,
                    PlayerLoadWarningKind.MissingEquipmentItemId,
                    entry.SlotId,
                    $"Skipped equipment slot '{entry.SlotId}' because its item id was missing.");
                continue;
            }

            if (!itemDefinitions.TryGetById(entry.ItemId, out var definition))
            {
                AddWarning(
                    warnings,
                    PlayerLoadWarningKind.UnknownEquipmentItem,
                    entry.ItemId,
                    $"Skipped unknown equipment item '{entry.ItemId}'.");
                continue;
            }

            var change = loadout.TryEquip(new ItemStack(definition.Id, 1), itemDefinitions, slot);
            if (!change.Success)
            {
                AddWarning(
                    warnings,
                    PlayerLoadWarningKind.IncompatibleEquipmentItem,
                    entry.ItemId,
                    change.Error ?? $"Skipped item '{entry.ItemId}' in incompatible slot '{entry.SlotId}'.");
                continue;
            }

            restoredSlots.Add(slot);
        }

        return loadout;
    }

    public void RestoreStatusEffects(
        PlayerSaveData data,
        StatusEffectCollection statusEffects,
        StatusEffectRegistry effectDefinitions,
        ICollection<PlayerLoadWarning>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(statusEffects);
        ArgumentNullException.ThrowIfNull(effectDefinitions);

        statusEffects.Clear();
        var restoredEffects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var effects = data.ActiveStatusEffects ?? Array.Empty<ActiveStatusEffectSaveData>();

        foreach (var entry in effects)
        {
            if (entry is null)
            {
                AddWarning(
                    warnings,
                    PlayerLoadWarningKind.InvalidStatusEffectEntry,
                    null,
                    "Skipped a null status effect entry.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.EffectId))
            {
                AddWarning(
                    warnings,
                    PlayerLoadWarningKind.MissingStatusEffectId,
                    null,
                    "Skipped a status effect with a missing id.");
                continue;
            }

            if (restoredEffects.Contains(entry.EffectId))
            {
                AddWarning(
                    warnings,
                    PlayerLoadWarningKind.DuplicateStatusEffect,
                    entry.EffectId,
                    $"Skipped duplicate status effect '{entry.EffectId}'.");
                continue;
            }

            if (!float.IsFinite(entry.RemainingDurationSeconds) || entry.RemainingDurationSeconds <= 0)
            {
                AddWarning(
                    warnings,
                    PlayerLoadWarningKind.InvalidStatusEffectDuration,
                    entry.EffectId,
                    $"Skipped status effect '{entry.EffectId}' with invalid remaining duration.");
                continue;
            }

            if (!effectDefinitions.TryGetById(entry.EffectId, out var definition))
            {
                AddWarning(
                    warnings,
                    PlayerLoadWarningKind.UnknownStatusEffect,
                    entry.EffectId,
                    $"Skipped unknown status effect '{entry.EffectId}'.");
                continue;
            }

            statusEffects.Apply(definition, entry.RemainingDurationSeconds);
            restoredEffects.Add(definition.Id);
        }
    }

    public CharacterAppearance ToCharacterAppearance(PlayerSaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.CharacterAppearance ?? new CharacterAppearance();
    }

    public InventoryModel ToInventory(PlayerSaveData data, IItemDefinitionProvider itemDefinitions)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(itemDefinitions);

        var inventory = new InventoryModel(data.InventorySlots.Count, itemDefinitions);
        for (var index = 0; index < data.InventorySlots.Count; index++)
        {
            inventory.Slots[index].SetStack(data.InventorySlots[index]);
        }

        return inventory;
    }

    public PlayerInventory ToPlayerInventory(PlayerSaveData data, IItemDefinitionProvider itemDefinitions)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(itemDefinitions);

        var hotbar = new InventoryModel(PlayerInventory.HotbarSlotCount, itemDefinitions);
        var main = new InventoryModel(PlayerInventory.MainSlotCount, itemDefinitions);

        for (var index = 0; index < Math.Min(PlayerInventory.HotbarSlotCount, data.InventorySlots.Count); index++)
        {
            hotbar.Slots[index].SetStack(data.InventorySlots[index]);
        }

        var mainSourceStart = PlayerInventory.HotbarSlotCount;
        for (var index = 0; index < PlayerInventory.MainSlotCount && mainSourceStart + index < data.InventorySlots.Count; index++)
        {
            main.Slots[index].SetStack(data.InventorySlots[mainSourceStart + index]);
        }

        var inventory = new PlayerInventory(hotbar, main, itemDefinitions);
        inventory.SelectHotbarSlot(Math.Clamp(data.SelectedHotbarSlot, 0, PlayerInventory.HotbarSlotCount - 1));
        return inventory;
    }

    private static void AddWarning(
        ICollection<PlayerLoadWarning>? warnings,
        PlayerLoadWarningKind kind,
        string? savedId,
        string message)
    {
        warnings?.Add(new PlayerLoadWarning(kind, savedId, message));
    }
}
