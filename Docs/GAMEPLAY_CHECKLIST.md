# Gameplay Checklist

Only open work is kept here. Completed items are removed after implementation.

## Player And Controls

- [ ] Add configurable reach from final player stats.
- [ ] Add client mode switch or controller profile for sideview sandbox movement vs topdown RPG/life-sim movement.

## Mining, Building, And Interaction

- [ ] Expand placement anchor/support rules for platforms, walls, furniture, and tile entities.
- [ ] Add interact actions for chests, doors, signs, NPCs, crafting stations, and tile entities.
- [ ] Add liquid interaction rules for placing blocks in water and mining flooded tiles.

## Items, Equipment, And Effects

- [ ] Expand startup profiles and starter progression beyond current wood/copper/iron weapons/tools and first copper armor pieces.
- [ ] Expand consumables beyond healing/mana potions with regeneration food, antidote and buff/debuff presentation assets.

## Crafting

- [ ] Add craft-from-nearby-chests option as a later quality-of-life feature.

## Farming And Life-Sim Gameplay

- [ ] Render tilled soil, watered soil, crop stages, and harvest-ready highlights.
- [ ] Wire harvest/interact actions into selected item use without requiring a dedicated harvest tool.
- [ ] Add rain/weather watering and seasonal calendar helpers.
- [ ] Wire shop transaction results into client UI and add shipping-bin daily payout flow.
- [ ] Wire core topdown map sessions, movement, transitions, and object action results into client gameplay.
- [ ] Add actual screens for topdown shops, shipping bins, containers, dialogue, and scripted interaction hooks.
- [ ] Expand authored topdown object content with fences, furniture footprints, buildings, and farm/town decoration sets.
- [ ] Add NPC schedules, dialogue, relationship data, gifts, and daily routines.
- [ ] Add farm map authoring support for ground/object/building layers.

## Combat

- [ ] Use world-aware line-of-sight melee in selected item use.
- [ ] Make selected-item weapons consume the existing attack phase/combo/swept-shape definitions and add authored multi-hit weapons.
- [ ] Route block/parry/guard-break, attack, projectile hit and death events to Wave 04/05 particles/icons plus production audio.
- [ ] Balance projectile knockback, homing, gravity, drag, pierce and bounce per weapon/ammo definition.
- [ ] Add ranged weapon spread, charge, reload, and ammo preference rules.
- [ ] Add shield/equipment item definitions that modify guard angle, stamina, parry window and break recovery.
- [ ] Add death particles and audio hooks; hurt flash and elite outlines already use the entity visual pipeline.

## Enemies And Spawning

- [ ] Balance activity-source spawn weights/caps by biome, vertical layer, time, weather and event using recorded population distributions.
- [ ] Add an underground danger curve that modifies composition and elite chance without exceeding local/region/global caps.
- [ ] Add despawn fade/cleanup feedback for debug builds.

## World Generation Gameplay

- [ ] Add chest loot rooms generated near caves.
- [ ] Expand biome-specific structure variety beyond Forest Camp, Amber Workshop and Marsh Lantern Grove; add spacing/rarity and encounter rules.
- [ ] Add mineable/placeable background wall items and biome-specific wall materials.
- [ ] Add ore progression balance pass using profile-driven ore definitions and generation analysis metrics.

## Time And World Rules

- [ ] Ship real time/biome/weather music and ambient assets through the existing soundscape crossfade path.
- [ ] Add weather gameplay effects such as rain watering, wetness and event-specific hazards beyond current sky/spawn/particle/soundscape presentation.
- [ ] Add optional survival-style comfort stats: warmth, wetness, rested.

## Character And Living World Presentation

- [ ] Expand character-editor choices into game-owned body, face, hair, clothing, armor and accessory palettes backed by validated Wave 04/05-compatible layers.
- [ ] Add authored animation/event definitions for more enemy attacks, deaths, critter perching and flock transitions.
- [ ] Route biome critter/icon/elite presentation IDs everywhere they are declared and add debug forcing commands for each variant.
- [ ] Add furniture/decor interaction sets, placement previews, anchors and persistent sideview tile entities.
