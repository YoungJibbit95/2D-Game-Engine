using Game.Core.Characters;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Equipment;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Inventory;
using Game.Core.Projects;
using Game.Core.Runtime;
using Game.Core.Saving;
using Game.Core.Startup;
using Game.Core.Time;
using Game.Core.World.Generation;
using Game.Core.World.TileEntities;
using Game.Core.Randomness;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Sessions;

public sealed class LoadedGameSession : IDisposable
{
    private bool _disposed;

    public LoadedGameSession(
        GameContentDatabase content,
        GameWorld world,
        PlayerEntity player,
        PlayerInventory inventory,
        EntityManager entities,
        GameEventBus events,
        WorldTime worldTime,
        GameSimulation simulation,
        WorldGenerationProfile? worldGenerationProfile = null,
        string? worldSaveDirectory = null,
        TileEntityManager? tileEntities = null,
        FarmPlotManager? farmPlots = null,
        GameProjectManifest? manifest = null,
        GameProjectPaths? projectPaths = null,
        GameStartupDefinition? startup = null,
        StarterInventoryResult? startupInventory = null,
        bool loadedFromSave = false,
        EquipmentLoadout? equipmentLoadout = null,
        CharacterAppearance? characterAppearance = null,
        IReadOnlyList<PlayerLoadWarning>? playerLoadWarnings = null,
        InfiniteWorldChunkGenerator? infiniteChunkGenerator = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        World = world ?? throw new ArgumentNullException(nameof(world));
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        Entities = entities ?? throw new ArgumentNullException(nameof(entities));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        WorldTime = worldTime ?? throw new ArgumentNullException(nameof(worldTime));
        Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        FarmPlots = farmPlots ?? simulation.FarmPlots;
        EquipmentLoadout = equipmentLoadout ?? simulation.EquipmentLoadout;

        ValidateSimulationIdentity();

        WorldGenerationProfile = worldGenerationProfile;
        WorldSaveDirectory = worldSaveDirectory;
        TileEntities = tileEntities;
        Manifest = manifest;
        ProjectPaths = projectPaths;
        Startup = startup;
        StartupInventory = startupInventory;
        LoadedFromSave = loadedFromSave;
        CharacterAppearance = characterAppearance;
        PlayerLoadWarnings = playerLoadWarnings;
        InfiniteChunkGenerator = infiniteChunkGenerator;
    }

    public GameContentDatabase Content { get; }

    public GameWorld World { get; }

    public PlayerEntity Player { get; }

    public PlayerInventory Inventory { get; }

    public EntityManager Entities { get; }

    public GameEventBus Events { get; }

    public WorldTime WorldTime { get; }

    public GameSimulation Simulation { get; }

    public WorldGenerationProfile? WorldGenerationProfile { get; }

    public string? WorldSaveDirectory { get; }

    public TileEntityManager? TileEntities { get; }

    public FarmPlotManager FarmPlots { get; }

    public GameProjectManifest? Manifest { get; }

    public GameProjectPaths? ProjectPaths { get; }

    public GameStartupDefinition? Startup { get; }

    public StarterInventoryResult? StartupInventory { get; }

    public bool LoadedFromSave { get; }

    public EquipmentLoadout EquipmentLoadout { get; }

    public CharacterAppearance? CharacterAppearance { get; }

    public IReadOnlyList<PlayerLoadWarning>? PlayerLoadWarnings { get; }

    public InfiniteWorldChunkGenerator? InfiniteChunkGenerator { get; }

    public SessionRandomRegistry RandomStreams => Simulation.RandomStreams;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Simulation.Dispose();
    }

    private void ValidateSimulationIdentity()
    {
        if (!ReferenceEquals(Content, Simulation.Content) ||
            !ReferenceEquals(World, Simulation.World) ||
            !ReferenceEquals(Player, Simulation.Player) ||
            !ReferenceEquals(Inventory, Simulation.PlayerInventory) ||
            !ReferenceEquals(Entities, Simulation.Entities) ||
            !ReferenceEquals(Events, Simulation.Events) ||
            !ReferenceEquals(WorldTime, Simulation.Time) ||
            !ReferenceEquals(FarmPlots, Simulation.FarmPlots) ||
            !ReferenceEquals(EquipmentLoadout, Simulation.EquipmentLoadout))
        {
            throw new ArgumentException("The simulation must own the session's exact live state instances.", nameof(Simulation));
        }
    }
}
