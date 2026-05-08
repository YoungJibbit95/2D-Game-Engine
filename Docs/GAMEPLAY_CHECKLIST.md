# Gameplay Checklist

## Player

- [x] Player runtime model.
- [x] Player entity with physics body.
- [x] Keyboard movement commands.
- [x] Walk acceleration and max speed.
- [x] Jump and gravity.
- [x] Tile collision.
- [x] Camera follow.
- [x] Respawn point.
- [x] Damage and invulnerability.

## World Interaction

- [ ] Mouse world position.
- [ ] Mouse tile target.
- [x] Mining reach check.
- [x] Mining progress timer.
- [x] Tile removal and item drop.
- [x] Building placement validation.
- [x] Prevent block placement inside player AABB.
- [x] Placeable tile item consumption.
- [x] Selected item core action routing for mining, building, and melee.

## Items And Inventory

- [x] Item stack model.
- [x] Inventory slots.
- [x] Add item stack merge.
- [x] Remove item atomically.
- [x] Swap slots.
- [x] Split stack.
- [x] Merge stack.
- [x] Placeable tile item definitions.
- [x] Hotbar split from main inventory.
- [x] Hotbar selected slot input.
- [x] Pickup dropped items.
- [x] Selected hotbar item use.

## Crafting

- [x] Recipe definitions.
- [x] Recipe registry.
- [x] Ingredient availability checks.
- [x] Craft operation.
- [ ] Station requirement checks.

## Combat

- [x] Health component.
- [x] Damage info model.
- [x] Melee hitbox.
- [x] Projectile entity.
- [x] Knockback.
- [x] Slime enemy runtime baseline.
- [x] Loot table definitions.
- [x] Loot roller.
- [x] Loot drops on death.

## World Generation Expansion

- [x] Surface heightmap.
- [x] Dirt/grass/stone layers.
- [x] Random-walk caves.
- [x] Copper/iron ore veins.
- [x] Underground water pockets.
- [x] Trees.
- [x] Spawn point on surface.
- [x] Forest biome baseline.
- [x] Biome definitions and registry.
- [x] Autotile neighbor mask logic.

## Engine-Ready Gameplay Infrastructure

- [x] Tile lighting values available for gameplay/rendering.
- [x] Spatial query structure ready for projectiles, pickups, enemies, triggers.
- [x] Gameplay event bus for pickups, mining, combat, and commands.
- [x] Debug command backend can mutate inventory, time, and entity runtime state.
- [x] Client runtime has inventory, content database, entity manager, and command context wired.
- [x] World save/load can persist changed tile data.
- [x] Player save/load.
- [x] Entity save/load.
- [x] Dropped item persistence.

## Time And World Rules

- [x] Day/night clock.
- [x] `/time` debug command.
- [x] Time-based enemy spawn tables.
- [x] Time-based sky color.
- [ ] Time-based music/ambient hooks.

## Spawning

- [x] Data-driven spawn rule definitions.
- [x] Biome-gated spawns.
- [x] Day/night-gated spawns.
- [x] Active enemy cap per spawn rule.
- [x] `/spawn` debug command for entity definitions.
- [x] Player-distance spawn bands.
- [ ] Spawn rate tuning by biome/depth.
