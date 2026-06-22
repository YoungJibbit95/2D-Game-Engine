# UI Checklist

Only open work is kept here. Completed items are removed after implementation.

## Architecture

- [ ] Wire core cursor item drag state into UI focus, hover, and hit-test flow.
- [ ] Integrate `Game.Core.UI` layout/focus/hit-testing into the existing client overlays.
- [ ] Add renderer widgets for core UI primitives: panel, button, label, image, slot, list, tab, splitter, and window.

## HUD

- [ ] Add mining progress at cursor.
- [ ] Add selected item tooltip.
- [ ] Add buff/debuff icon row with remaining-time radial or bar.
- [ ] Add interaction prompt for chests, doors, signs, NPCs, and crafting stations.
- [ ] Add liquid/debug overlay showing tile liquid amount and flow direction.
- [ ] Add combat feedback overlays for damage numbers, hit flashes, and cooldowns.

## Inventory And Equipment UI

- [ ] Add sorting, stack compaction, and category filters.

## Crafting UI
- [ ] Add recipe search.
- [ ] Add craft amount controls and hold-to-repeat timing.
- [ ] Add recipe pinning/tracking in HUD.
- [ ] Add station-range indicator and recipe unlock source hints.

## Menus

- [ ] Add local splitscreen setup flow.
- [ ] Add multiplayer host/join flow.
- [ ] Persist character editor appearance into player saves and expose it before world launch.
- [ ] Add typed value entry for settings rows such as resolution, reach, and volume.
- [ ] Add keybind conflict resolution UI that can jump to the conflicting action.
- [ ] Add graphics apply safety timeout for fullscreen/resolution changes.
- [ ] Add gamepad navigation for menus and settings.
- [ ] Add save/load error report screen.

## Debug UI

- [ ] Add ImGui renderer integration.
- [ ] Add debug windows for world/player.
- [ ] Add debug windows for liquids, status effects, equipment, spawns, and dirty regions.
- [ ] Add content browser for registries: tiles, items, effects, entities, recipes.
- [ ] Add in-game profiler overlay for active chunks, dirty systems, and allocations.
- [ ] Add worldgen seed preview and analysis panel.
