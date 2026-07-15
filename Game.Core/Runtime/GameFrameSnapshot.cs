using Game.Core.Inventory;
using Game.Core.Equipment;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Weather;
using Game.Core.World;
using System.Numerics;

namespace Game.Core.Runtime;

public sealed record GameFrameSnapshot(
    long TickNumber,
    PlayerFrameSnapshot Player,
    WorldTimeFrameSnapshot WorldTime,
    LivingWorldFrameSnapshot LivingWorld,
    ImmutableSnapshotList<EntityFrameSnapshot> Entities,
    ImmutableSnapshotList<FarmPlotFrameSnapshot> FarmPlots,
    HudFrameSnapshot Hud);

public sealed record PlayerFrameSnapshot(
    Vector2 Position,
    Vector2 Velocity,
    RectI Bounds,
    bool IsOnGround,
    bool IsDead,
    int Health,
    int MaxHealth,
    int Mana,
    int MaxMana,
    PlayerStatBlock Stats,
    bool IsGuarding,
    bool IsGuardBroken,
    float GuardStamina,
    float MaxGuardStamina,
    int SelectedHotbarSlot,
    ImmutableSnapshotList<InventorySlotFrameSnapshot> Hotbar);

public readonly record struct InventorySlotFrameSnapshot(ItemStack Stack, bool IsFavorite);

public readonly record struct WorldTimeFrameSnapshot(
    int Day,
    double TimeOfDaySeconds,
    double DayLengthSeconds,
    double NormalizedTimeOfDay,
    bool IsNight);

public readonly record struct LivingWorldFrameSnapshot(
    long RegionIndex,
    long RegionStartTileX,
    long RegionEndTileXInclusive,
    string BiomeId,
    string BiomeDisplayName,
    string? SubBiomeId,
    string? SubBiomeDisplayName,
    string BiomeLayerId,
    string? CaveProfileId,
    bool IsUnderground,
    string SoundscapeId,
    float AmbientLight,
    float Visibility,
    float Temperature,
    float Humidity,
    string ColorGradeId,
    float SkyLightMultiplier,
    float EmissiveLightMultiplier,
    float FogDensity,
    float SpawnDensityMultiplier,
    float OreDensityMultiplier,
    float VegetationDensityMultiplier,
    float ForageDensityMultiplier,
    WeatherKind Weather,
    float WeatherIntensity,
    float Wind,
    float CloudCover,
    long WeatherStartTick,
    long WeatherEndTickExclusive,
    bool IsWorldEventActive,
    string? WorldEventId,
    float WorldEventProgress,
    float WorldEventIntensity,
    LivingWorldPresentationFrameSnapshot Presentation)
{
    public string? WorldEventPhaseId { get; init; }

    public float WorldEventPhaseProgress { get; init; }

    public string? WorldEventParticleSpriteId { get; init; }

    public float WorldEventPresentationIntensity { get; init; }

    public float LootQuantityMultiplier { get; init; } = 1f;

    public float RareLootChanceMultiplier { get; init; } = 1f;
}

public readonly record struct LivingWorldPresentationFrameSnapshot(
    string? BackgroundSpriteId,
    string? AmbientParticleSpriteId,
    string? AmbientCritterSpriteId,
    string? BiomeIconSpriteId,
    string? EliteSpriteId,
    float AmbientParticleDensity,
    float CaveReverb,
    float SurfaceReflectionStrength,
    float WindResponse);

public enum EntityFrameKind
{
    Entity,
    Enemy,
    DroppedItem,
    Projectile
}

public readonly record struct EntityFrameSnapshot(
    int Id,
    EntityFrameKind Kind,
    string ContentId,
    Vector2 Position,
    Vector2 Velocity,
    RectI Bounds,
    bool IsActive,
    int Health,
    int MaxHealth,
    ItemStack ItemStack,
    bool IsDamageFlashing,
    DamageType DamageType)
{
    public EntityFaction Faction { get; init; } = EntityFaction.Neutral;

    public AiState? AiState { get; init; }

    public int? AiTargetEntityId { get; init; }

    public AiTelemetrySnapshot AiTelemetry { get; init; }
}

public readonly record struct FarmPlotFrameSnapshot(
    TilePos Position,
    bool IsTilled,
    bool IsWatered,
    string? CropId,
    int PlantedDay,
    int DaysUntilHarvest,
    int HarvestCount,
    bool IsMature);

public readonly record struct HudFrameSnapshot(
    int Health,
    int MaxHealth,
    int Mana,
    int MaxMana,
    int SelectedHotbarSlot,
    int ActiveEntities,
    int ActiveEnemies,
    int DroppedItems,
    int Projectiles,
    int OccupiedInventorySlots,
    int TotalInventoryItems,
    int FarmPlots,
    int PickedUpItemsThisTick,
    int SpawnedEnemiesThisTick,
    int EnemyDeathsThisTick);
