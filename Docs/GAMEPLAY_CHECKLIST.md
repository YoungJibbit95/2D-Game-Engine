# Gameplay Checklist

Only open work is kept here. Completed items are removed after implementation.

## Player And Controls

- [ ] Add armor/accessory equip flow.
- [ ] Add consumable use flow that applies status effects.
- [ ] Add configurable reach, mining speed, movement speed, and damage from final player stats.
- [ ] Add client mode switch or controller profile for sideview sandbox movement vs topdown RPG/life-sim movement.

## Mining, Building, And Interaction

- [ ] Expand placement anchor/support rules for platforms, walls, furniture, and tile entities.
- [ ] Add mining progress UI at the cursor.
- [ ] Add tile hit particles and audio hooks on mining progress and completion.
- [ ] Add interact actions for chests, doors, signs, NPCs, crafting stations, and tile entities.
- [ ] Add liquid interaction rules for placing blocks in water and mining flooded tiles.

## Items, Equipment, And Effects

- [ ] Expand starter progression beyond current wood/copper/iron weapons/tools and first copper armor pieces.
- [ ] Add first accessories with movement, mining, defense, and light bonuses.
- [ ] Add first consumables: healing potion, regeneration food, antidote.
- [ ] Add equipment persistence.
- [ ] Add status effect persistence.
- [ ] Add item use cooldown display and blocked-use feedback.

## Crafting

- [ ] Add furnace and anvil station definitions.
- [ ] Add craft-from-nearby-chests option as a later quality-of-life feature.

## Farming And Life-Sim Gameplay

- [ ] Render tilled soil, watered soil, crop stages, and harvest-ready highlights.
- [ ] Wire harvest/interact actions into selected item use without requiring a dedicated harvest tool.
- [ ] Add rain/weather watering and seasonal calendar helpers.
- [ ] Add shops, shipping bin, money economy, daily summary, and sell prices.
- [ ] Wire core topdown map sessions, movement, transitions, signs, containers, shops, NPC spawns, and farm areas into client gameplay.
- [ ] Expand topdown map objects with doors, fences, gates, furniture footprints, and scripted interaction hooks.
- [ ] Add NPC schedules, dialogue, relationship data, gifts, and daily routines.
- [ ] Add farm map authoring support for ground/object/building layers.

## Combat

- [ ] Use world-aware line-of-sight melee in selected item use.
- [ ] Add attack arcs, animation phases, and multi-hit windows for melee weapons.
- [ ] Add status-effect UI, audio, particle, and balancing feedback for weapon/projectile/enemy applications.
- [ ] Add projectile knockback, pierce tuning, and hit effects.
- [ ] Add ranged weapon spread, charge, reload, and ammo preference rules.
- [ ] Add enemy hurt flash, death particles, and audio hooks.

## Enemies And Spawning

- [ ] Add spawn rate tuning by biome and depth.
- [ ] Add underground danger curve based on depth and time.
- [ ] Add enemy variants with different AI behaviors.
- [ ] Add line-of-sight chase rules for AI.
- [ ] Add despawn fade/cleanup feedback for debug builds.

## World Generation Gameplay

- [ ] Add chest loot rooms generated near caves.
- [ ] Add surface lakes and cave pools that interact with liquid simulation.
- [ ] Add biome-specific small structures.
- [ ] Add background wall distribution for underground areas.
- [ ] Add ore progression balance pass using profile-driven ore definitions and generation analysis metrics.

## Time And World Rules

- [ ] Add time-based music and ambient hooks.
- [ ] Add simple weather hooks affecting sky, spawn rules, and ambient audio.
- [ ] Add world events triggered by time, depth, biome, or player actions.
- [ ] Add optional survival-style comfort stats: warmth, wetness, rested.
