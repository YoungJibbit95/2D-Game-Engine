# Asset Pipeline And AI Sprite Briefs

This document is the handoff contract for another AI model, artist, or tool that generates sprites for YjsE, a multi-genre 2D engine that starts with Terraria-like sandbox action and is expanding toward Stardew-like topdown farming, RPGs, and action-adventure games. The goal is that an asset generator can read the plan, read `Game.Data/assets/*.json`, read `Game.Data/asset_briefs/*.json`, and produce PNG files without guessing style, paths, sizes, or naming.

## Generation Contract

Use the machine-readable briefs in `Game.Data/asset_briefs/*.json` as the source of truth for AI generation. `base_sprite_generation_briefs.json` covers the starter world/items set; `character_animation_wave_briefs.json` covers the first character, ambient life, enemy, foliage, and background wave. Each brief maps one logical sprite id to one output PNG.

For every brief:

- Generate exactly one PNG at `outputPath`.
- Use the exact `width` and `height`.
- Preserve transparent background unless the brief says the sprite fills a tile.
- Do not add text, logos, watermarks, signatures, UI frames, mockups, or background scenery.
- Use crisp pixel art with hard edges, no anti-aliasing, no blur, no photographic rendering, and no sub-pixel gradients.
- Keep the sprite aligned to the pixel grid and readable at native size.
- Keep lighting consistent from the upper-left.
- Prefer 2-4 shade clusters per material.

The asset generator should write files relative to `Game.Content/` or another chosen source-art root, while preserving the path after `sprites/`. Example:

```text
spriteId: tiles/dirt
outputPath: sprites/world/tiles/dirt.png
final source file: Game.Content/Sprites/World/Tiles/dirt.png
runtime copy path later: Game.Data/sprites/world/tiles/dirt.png or packed atlas entry
```

## AI Model Prompt Template

For each entry in `Game.Data/asset_briefs/*.json`, build the generation prompt like this:

```text
SYSTEM:
You generate production-ready 2D pixel art sprites for a C# 2D engine supporting Terraria-like sandbox action, Stardew-like topdown farming, RPGs, and action-adventure games.
Return only the image. No text, frame, watermark, mockup, or extra canvas.

STYLE:
{globalStyle}

SPRITE:
id: {spriteId}
subject: {subject}
canvas: {width}x{height}
background: {background}
palette hints: {palette}

PROMPT:
{prompt}

REQUIREMENTS:
{globalRequirements}
{requirements}

NEGATIVE:
{globalNegativePrompt}
{negativePrompt}
```

If the model supports a negative prompt field, put all negative text there instead of in the main prompt.

## Sprite Id Convention

Sprite ids are stable logical ids. Gameplay definitions reference ids, not file paths.

- `tiles/dirt_autotile`
- `tiles/workbench`
- `walls/dirt_wall`
- `items/dirt_block`
- `items/workbench`
- `items/copper_pickaxe`
- `items/parsnip_seeds`
- `crops/parsnip`
- `entities/slime`
- `projectiles/wooden_arrow`
- `particles/tile_dust_dirt`
- `effects/hit_flash`
- `ui/hotbar_slot`
- `backgrounds/forest_day`

Terrain blockface sprites that should visually connect use the suffix `_autotile` and provide 16 horizontal frames for masks `0..15`. The runtime tile definitions should reference these logical ids directly:

- `tiles/dirt_autotile`
- `tiles/grass_autotile`
- `tiles/stone_autotile`
- `tiles/wood_autotile`
- `tiles/leaves_autotile`

## Source Folder Plan

Recommended source layout for real art:

```text
Game.Content/
|-- Sprites/
|   |-- World/
|   |   |-- Tiles/
|   |   |-- Walls/
|   |   |-- Objects/
|   |   |-- Crops/
|   |   |-- Liquids/
|   |   `-- Backgrounds/
|   |-- Items/
|   |   |-- Blocks/
|   |   |-- Materials/
|   |   |-- Tools/
|   |   |-- Weapons/
|   |   |-- Armor/
|   |   |-- Accessories/
|   |   |-- Consumables/
|   |   |-- Seeds/
|   |   `-- Stations/
|   |-- Entities/
|   |   |-- Player/
|   |   |-- NPCs/
|   |   |-- Enemies/
|   |   `-- Critters/
|   |-- Projectiles/
|   |-- Particles/
|   |-- Effects/
|   `-- UI/
|       |-- HUD/
|       |-- Inventory/
|       |-- Crafting/
|       |-- Menus/
|       `-- Debug/
`-- Atlases/
    |-- world.atlas.json
    |-- items.atlas.json
    |-- entities.atlas.json
    `-- ui.atlas.json
```

## Runtime Data Contract

Sprite metadata lives in `Game.Data/assets/*.json`.

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

Generation briefs live separately in `Game.Data/asset_briefs/*.json` so gameplay loading does not depend on generation-only prompt text.

## Category Rules

- `Tile`: usually 16x16. Terrain tiles must fill the canvas and state whether they tile on all edges or only horizontally.
- `Wall`: background wall sprites, lower contrast than terrain.
- `WorldObject`: furniture, stations, and decorations. Usually transparent, grounded at the bottom of the canvas.
- `Crop`: world crop sprites. Prefer horizontal growth strips where each 16x16 frame is one stage, transparent background, grounded to the lower center so it reads on tilled soil.
- `Item`, `Tool`, `Weapon`: inventory icons. Use transparent background and leave 1-2 pixels of padding when possible.
- `Armor`: currently encoded as `Item` category with `armor` tags.
- `Entity`: side-view runtime creature sprites. Must be grounded at the bottom and fit the entity's definition body.
- `Projectile`: small readable motion sprites. Usually long, thin, and transparent.
- `Particle` and `Effect`: tiny visual feedback pieces, no UI framing.
- `Ui`: interface pieces. Use clear edges and avoid decorative noise.
- `Background`: larger parallax or sky assets. These can use full-canvas backgrounds and are not item icons.

## Current Base Generation Set

The current base brief file covers all sprite ids in `Game.Data/assets/sprites.json`:

- Terrain and world: air, dirt, grass, stone, copper ore, iron ore, wood, leaves, workbench.
- Materials and blocks: dirt block, stone block, wood, gel, copper ore, iron ore, copper coin.
- Tools and weapons: copper pickaxe, iron pickaxe, wooden sword, copper sword, iron sword, wooden bow.
- Armor and accessories: copper helmet, copper chestplate, copper greaves, mining charm.
- Consumables/ammo: healing potion, wooden arrow, poison arrow.
- Farming: copper hoe, watering can, parsnip seeds, harvested parsnip, and four-stage parsnip crop strip.
- Runtime sprites: slime, player action sheet, player editor overlays, v2 body/hair/clothes/accessory sheets, rabbit, bird, bat, cave worm, foliage props, forest/cave parallax layers, wooden arrow projectile, poison arrow projectile.

The latest programmer-art pass also adds corrected blockface sheets for dirt, grass, stone, wood, and leaves. These are deterministic starter assets, not final art direction, but they are valid production-shaped inputs for autotile rendering and asset validation.

When new sprites are added to `Game.Data/assets`, add a matching brief in `Game.Data/asset_briefs` in the same change. The tests enforce this.

## Atlas Strategy

Initial renderer can load individual textures or placeholders through `ClientTextureRegistry`. The next production step should pack sprites into atlases:

- `world`: tiles, walls, liquids, foliage, furniture.
- `crops`: crop growth strips, tilled soil overlays, watered soil overlays, farm objects.
- `items`: all inventory icons.
- `entities`: player, NPCs, enemies, critters.
- `effects`: projectiles, particles, combat effects.
- `ui`: HUD, menus, inventory, crafting, debug.

`SpriteAssetDefinition.AtlasId` is already reserved for this.

## Acceptance Checklist For Generated Sprites

- [ ] Every generated PNG exists at the path requested by its brief.
- [ ] Width and height match the brief exactly.
- [ ] Transparent-background sprites have real alpha, not a flat background color.
- [ ] Terrain tiles tile correctly as specified.
- [ ] Inventory items remain readable at 1x scale.
- [ ] Related materials share palette language: copper is warm orange, iron is muted pale tan, wood is warm brown, stone is neutral grey.
- [ ] Crop growth strips have exactly the requested frame count and each stage remains readable on both dry and watered soil.
- [ ] No sprite contains text, watermark, signature, prompt artifacts, or UI frame unless specifically requested.
- [ ] Sprite ids in `Game.Data/assets` and briefs in `Game.Data/asset_briefs` stay in sync.

## Next Pipeline Steps

- Add atlas packing and source-rect reports.
- Extend animation clip metadata beyond the player into enemies, critters, projectiles, liquids, crops, and effects.
- Add Aseprite export conventions.
- Add sprite preview/editor tooling.
- Add duplicate-source and missing-PNG validation.
- Add asset provenance in content reports for base game vs mods.
