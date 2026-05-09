# Asset Pipeline Plan

This is the working asset plan for the Terraria-like engine. It defines how sprites should be organized before the real art pass begins.

## Goals

- Keep gameplay definitions stable even if source files move.
- Let tools and mods validate missing sprites early.
- Support atlas packing later without changing item, tile, or entity definitions.
- Keep sprite categories clear for artists, designers, and engine code.

## Sprite Id Convention

Sprite ids are logical ids. They are used by JSON definitions and should remain stable.

- `tiles/dirt`
- `tiles/workbench`
- `walls/dirt_wall`
- `items/dirt_block`
- `items/workbench`
- `items/copper_pickaxe`
- `entities/slime`
- `projectiles/wooden_arrow`
- `particles/tile_dust_dirt`
- `effects/hit_flash`
- `ui/hotbar_slot`
- `backgrounds/forest_day`

## Source Folder Plan

Recommended source layout for real art:

```text
Game.Content/
├─ Sprites/
│  ├─ World/
│  │  ├─ Tiles/
│  │  ├─ Walls/
│  │  ├─ Objects/
│  │  ├─ Liquids/
│  │  └─ Backgrounds/
│  ├─ Items/
│  │  ├─ Blocks/
│  │  ├─ Materials/
│  │  ├─ Tools/
│  │  ├─ Weapons/
│  │  ├─ Armor/
│  │  ├─ Accessories/
│  │  ├─ Consumables/
│  │  └─ Stations/
│  ├─ Entities/
│  │  ├─ Player/
│  │  ├─ NPCs/
│  │  ├─ Enemies/
│  │  └─ Critters/
│  ├─ Projectiles/
│  ├─ Particles/
│  ├─ Effects/
│  └─ UI/
│     ├─ HUD/
│     ├─ Inventory/
│     ├─ Crafting/
│     ├─ Menus/
│     └─ Debug/
└─ Atlases/
   ├─ world.atlas.json
   ├─ items.atlas.json
   ├─ entities.atlas.json
   └─ ui.atlas.json
```

## Current Data Contract

Sprite metadata lives in `Game.Data/assets/*.json`.

Example:

```json
{
  "sprites": [
    {
      "id": "items/copper_pickaxe",
      "path": "sprites/tools/copper_pickaxe.png",
      "category": "Tool",
      "width": 16,
      "height": 16,
      "tags": ["item", "tool", "pickaxe"]
    }
  ]
}
```

Definition files use the logical id:

```json
{
  "id": "copper_pickaxe",
  "texture": "items/copper_pickaxe"
}
```

## Categories

- `Tile`: tile sprites.
- `Wall`: background wall sprites.
- `Item`: generic inventory icons.
- `Tool`: tools such as pickaxes and axes.
- `Weapon`: melee, ranged, magic, and thrown weapons.
- `Entity`: player, NPC, enemy, and critter sprites.
- `Projectile`: arrows, bullets, magic bolts, thrown objects.
- `Particle`: dust, sparks, debris, hit particles.
- `Effect`: temporary visual effects.
- `Ui`: HUD, inventory, crafting, menu, and debug interface sprites.
- `Background`: sky, parallax, biome backgrounds.
- `WorldObject`: furniture, stations, decorations, structures.

## Atlas Strategy

Initial renderer can load individual textures or placeholders. The next production step should pack sprites into atlases:

- `world`: tiles, walls, liquids, foliage, furniture.
- `items`: all inventory icons.
- `entities`: player, NPCs, enemies, critters.
- `effects`: projectiles, particles, combat effects.
- `ui`: HUD, menus, inventory, crafting, debug.

`SpriteAssetDefinition.AtlasId` is already reserved for this.

## Authoring Rules

- Default tile size is 16x16.
- Inventory icons should start at 16x16 or 32x32 source size.
- Entity sprites should define logical body size separately in entity definitions.
- Animation frames should be represented in sprite metadata, not hardcoded in renderers.
- File paths may change; sprite ids should not.
- Mods may override sprite ids, but content reports should show the override.

## Next Pipeline Steps

- Add client-side `TextureRegistry` that resolves `SpriteAssetDefinition` into MonoGame textures.
- Add placeholder texture generation for missing sprite ids.
- Add atlas build reports and duplicate-source checks.
- Add animation clip metadata.
- Add Aseprite export conventions.
- Add sprite preview/editor tooling.
- Add asset provenance in content reports for base game vs mods.
