# Engine And Game Separation

YjsE is being shaped as an engine-first repository. A concrete game can live in a separate repository and still use the engine through the same content, modding, save, settings, and runtime contracts.

## Repository Roles

The engine repository owns reusable code and contracts:

- `Game.Core`: renderer-neutral engine, simulation, data contracts, content registries, world systems, UI primitives, save/load, gameplay modules, and tests.
- `Game.Client`: the current MonoGame host/client used to run and verify the engine.
- `Game.Tests`: engine and contract tests.
- `Docs`: engine architecture, checklists, status, setup, and external game guidance.
- `Game.Data`: sample/dev content pack for validating the engine locally.

A future game repository should own game-specific material:

- Its own `yjse.game.json`.
- Its own content root, usually `Game.Data` or `Content`.
- Game-specific sprite/audio/source assets.
- Game-specific balance, worldgen profiles, maps, dialogue, shops, recipes, enemies, loot, and progression.
- Optional `Mods` folder or mod-pack distribution.
- Game-specific README, changelog, roadmap, and releases.

## Game Project Manifest

Every game repo can define a root-level `yjse.game.json`.

```json
{
  "schemaVersion": 1,
  "id": "my_game",
  "displayName": "My Game",
  "version": "0.1.0",
  "engineVersion": "local",
  "contentRoot": "Game.Data",
  "modsRoot": "Mods",
  "savesRootName": "Saves",
  "defaultWorldProfileId": "small",
  "startupMapId": "farmstead",
  "tags": ["sandbox", "rpg"]
}
```

`Game.Core.Projects` loads this manifest, resolves content/mod/save paths, and can load the project through `GameProjectContentLoader`.

## Running An External Game Repo

The current MonoGame client supports external game roots through environment variables:

- `YJSE_GAME_ROOT`: path to a game repository containing `yjse.game.json`.
- `YJSE_GAME_DATA`: path directly to a loose content root when no manifest exists.
- `YJSE_MODS_ROOT`: optional override for the mods folder.

PowerShell example:

```powershell
$env:YJSE_GAME_ROOT = "F:\Games\MyYjsEGame"
dotnet run --project Game.Client
```

When no environment variable is set, the client resolves the local `yjse.game.json` and uses this repo's sample `Game.Data`.

## Save And Settings Isolation

Client settings and saves are now scoped by game project id under:

```text
%APPDATA%/YjsE/<project-id>/
```

This keeps separate games from sharing settings, worlds, keybinds, and autosaves by accident.

## Engineering Rules Going Forward

- Engine-grade systems go into `Game.Core` and must not depend on MonoGame.
- Game-specific balancing and authored content go into a game content root, not hard-coded engine switches.
- New content types need JSON loaders, registries, validation, tests, and docs before client UI depends on them.
- Client features should consume core result objects rather than duplicating gameplay rules.
- Sample `Game.Data` may remain in this repo as an engine validation pack, but it should be treated as replaceable.

## Future Physical Split

When the first standalone game repo is created, the recommended dependency path is:

1. Keep this repository as the engine source and package root.
2. Create a separate game repo with `yjse.game.json` and content data.
3. Reference the engine through a project reference during development or a NuGet/package artifact later.
4. Run the engine client with `YJSE_GAME_ROOT` pointing to the game repo.
5. Move game-only roadmap/issues/assets into the game repo while keeping engine issues here.
