# JetBrains Rider Setup

## Requirements

- Install JetBrains Rider.
- Install a .NET 8 SDK or newer and make sure Rider detects it under `Settings > Build, Execution, Deployment > Toolset and Build`.
- On this machine, Rider may pick `C:\Users\Administrator\.dotnet` first, which currently contains only a .NET 10 SDK/runtime. The repository sets `NetCoreTargetingPackRoot` to `C:\Program Files\dotnet\packs` when available so the `net8.0` targeting packs can still be resolved. The shared run profile also sets `DOTNET_ROOT` and `DOTNET_ROOT_X64` to `C:\Program Files\dotnet` so the .NET 8 runtime can be found at launch time.

## Open And Run

1. Open `TerrariaLike.sln` in Rider.
2. Wait for NuGet restore to finish.
3. Select the shared `Game.Client` run configuration.
4. Press Run or Debug.

The client project copies `Game.Data` into its output directory, so the runtime content database can load tiles, items, biomes, entities, projectiles, loot tables, recipes, and spawn rules without extra setup.

## Useful Runtime Controls

- `A`/`D` or arrow keys: move.
- `Space`/`W`/`Up`: jump.
- `F3`: hold debug grid.
- `F10`: open debug command console.

Example debug commands:

```text
/give dirt_block 99
/time night
/spawn slime
```

## Fallback CLI Commands

```powershell
dotnet restore TerrariaLike.sln
dotnet test TerrariaLike.sln
dotnet run --project Game.Client
```

If Rider still reports `NETSDK1127` for `Microsoft.NETCore.App`, switch Rider's .NET CLI executable to `C:\Program Files\dotnet\dotnet.exe` or run the restore/build once from that CLI path.
