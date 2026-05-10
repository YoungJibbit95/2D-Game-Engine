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

- [ ] Add inventory screen toggle.
- [ ] Add inventory slot widgets.
- [ ] Add equipment slot widgets.
- [ ] Wire core slot click rules into visible inventory widgets.
- [ ] Add shift-click quick move.
- [ ] Add item tooltip with damage, defense, tool power, status effects, and stat modifiers.
- [ ] Add stat summary panel for armor/accessory bonuses.

## Crafting UI

- [ ] Add recipe list.
- [ ] Add selected recipe details.
- [ ] Add ingredient availability display.
- [ ] Add station requirement display.
- [ ] Add craft button and repeated crafting input behavior.

## Menus

- [ ] Add world select state.
- [ ] Add create world flow.
- [ ] Add local splitscreen setup flow.
- [ ] Add multiplayer host/join flow.
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
