# Character And Animation System

YjsE now has an engine-level runtime animation and character definition foundation. The goal is to keep character/gameplay data in `Game.Core` and `Game.Data`, while the MonoGame client only resolves sprite frames and draws them.

## Core Types

- `Game.Core.Animations.SpriteAnimationClip`: data-driven animation clip with frame list, loop mode, tags, and total duration.
- `Game.Core.Animations.SpriteAnimationFrame`: references a logical sprite id, source frame index, duration, optional offset, and optional event id.
- `Game.Core.Animations.SpriteAnimator`: lightweight runtime player for `Once`, `Loop`, and `PingPong` clips.
- `Game.Core.Animations.SpriteAnimationJsonLoader`: loads single clips or manifests from `Game.Data/animations/**/*.json`.
- `Game.Core.Characters.CharacterDefinition`: editable character contract with size, default appearance, animation set, and tags.
- `Game.Core.Characters.CharacterAnimationStateResolver`: maps physics/action state into `Idle`, `Walk`, `Jump`, `Fall`, `Mine`, `Attack`, `UseItem`, `Hurt`, or `Dead`.
- `Game.Core.Characters.CharacterEditorSession`: first engine-neutral editor session for appearance palette, hair, clothes, and accessory choices.

## Data Flow

```text
Game.Data/assets/sprites.json
  -> SpriteAssetRegistry
Game.Data/animations/player.json
  -> SpriteAnimationRegistry
Game.Data/characters/player.json
  -> CharacterDefinitionRegistry
PlayingState
  -> CharacterAnimationStateResolver
  -> SpriteAnimator
  -> ClientTextureRegistry.Get(spriteId, frameIndex)
```

The current player character uses `entities/player/base_actions`, backed by `sprites/entities/player/player_base_actions.png` at `192x32`. Frames are 12 horizontal `16x32` cells:

```text
0 idle_0
1 idle_1
2 walk_0
3 walk_1
4 walk_2
5 walk_3
6 jump
7 fall
8 mine_0
9 mine_1
10 attack_0
11 attack_1
```

## Content Contracts

- Animation clips reference logical sprite ids, not files.
- Characters reference animation clip ids through `animationSet.states`.
- `ContentReferenceValidator` checks animation sprite references and character animation references.
- Mods can override animation clips and character definitions through the same base/mod merge path as items, tiles, entities, and recipes.
- Sprite generation briefs for the character wave live in `Game.Data/asset_briefs/character_animation_wave_briefs.json`.

## Current Client Integration

`Game.Client.GameStates.PlayingState` now uses the player character animation set for idle, walk, jump, fall, mining/use, and attack hints. The player renderer composes body, clothes, hair, and accessory layers from sprite sheets over the base action sheet. Enemy, critter, dropped item, and projectile rendering also try real sprite assets first and fall back to debug rectangles/placeholders when no PNG exists. Animated entity sheets tagged `animated` advance frames in the client.

## Character Editor Foundation

The first editor layer is data-safe and UI-neutral: `CharacterAppearance`, `CharacterEditorOptionSet`, and `CharacterEditorSession`. The MonoGame client now has an F2 in-game character editor overlay with live animation preview, skin/hair/shirt/pants swatches, hair style rows, clothing style rows, and accessory rows. The next persistence step is saving `CharacterAppearance` in player save data and exposing the same editor before world launch.

## Next Steps

- Move action-animation triggers out of `PlayingState` into a reusable player presentation/controller service.
- Add animation sets for critters, slime, bat, cave worm, projectiles, crops, and world objects.
- Add armor and held-item layers on top of the current body, hair, clothes, and accessory composition.
- Persist `CharacterAppearance` in player saves.
- Add randomize, reset, save-slot selection, and pre-game character creation flow.
- Add animation events for hit windows, mining impact, particles, sounds, and footstep timing.
