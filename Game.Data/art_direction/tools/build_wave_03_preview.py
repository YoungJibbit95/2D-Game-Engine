#!/usr/bin/env python3
"""Build the deterministic visual QA sheet for production Wave 03."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "art_direction" / "wave_03_production_preview.png"
LIGHT = (232, 226, 214, 255)
DARK = (35, 32, 46, 255)
INK = (27, 24, 38, 255)
PANEL = (198, 192, 184, 255)
FONT = ImageFont.load_default()

FOREGROUND = [
    ("COPPER HOE", "sprites/tools/copper_hoe.png"),
    ("IRON HOE", "sprites/tools/iron_hoe.png"),
    ("WOODEN ARROW PROJECTILE", "sprites/projectiles/wooden_arrow.png"),
    ("MAGIC SPARK", "sprites/projectiles/magic_spark_particles.png"),
    ("PLAYER BASE ACTIONS", "sprites/entities/player/player_base_actions.png"),
    ("PLAYER BODY", "sprites/entities/player/player_body_variants.png"),
    ("PLAYER HAIR", "sprites/entities/player/player_hair_variants_v2.png"),
    ("PLAYER CLOTHES", "sprites/entities/player/player_clothes_variants_v2.png"),
    ("PLAYER ACCESSORIES", "sprites/entities/player/player_accessories_hats.png"),
    ("PLAYER COMPOSITE", "__player_composite__"),
    ("MEADOW AMBIENT", "sprites/entities/critters/meadow_butterfly.png"),
    ("MEADOW PARTICLE", "sprites/particles/meadow_pollen.png"),
    ("MEADOW ELITE", "sprites/entities/enemies/meadow_slime_elite.png"),
    ("MEADOW UI", "sprites/ui/biomes/meadow.png"),
    ("FOREST AMBIENT", "sprites/entities/critters/forest_moth.png"),
    ("FOREST PARTICLE", "sprites/particles/forest_leaf_drift.png"),
    ("FOREST ELITE", "sprites/entities/enemies/forest_boar_elite.png"),
    ("FOREST UI", "sprites/ui/biomes/forest.png"),
    ("CAVE AMBIENT", "sprites/entities/critters/cave_glowbug.png"),
    ("CAVE PARTICLE", "sprites/particles/cave_dust.png"),
    ("CAVE ELITE", "sprites/entities/enemies/cave_spider_elite.png"),
    ("CAVE UI", "sprites/ui/biomes/cave.png"),
]

BACKGROUNDS = [
    ("MEADOW", "sprites/world/backgrounds/meadow_parallax_layer.png", ["sprites/world/tiles/grass_autotile.png", "sprites/world/tiles/dirt_autotile.png"]),
    ("FOREST", "sprites/world/backgrounds/forest_parallax_layer.png", ["sprites/world/tiles/leaves_autotile.png", "sprites/world/tiles/oak_trunk_autotile.png"]),
    ("CAVE", "sprites/world/backgrounds/cave_parallax_layer.png", ["sprites/world/tiles/stone_autotile.png", "sprites/world/tiles/granite_autotile.png"]),
]


def load(relative_path: str) -> Image.Image:
    if relative_path == "__player_composite__":
        composite = load("sprites/entities/player/player_base_actions.png").crop((0, 0, 16, 32))
        for path in (
            "sprites/entities/player/player_body_variants.png",
            "sprites/entities/player/player_clothes_variants_v2.png",
            "sprites/entities/player/player_hair_variants_v2.png",
            "sprites/entities/player/player_accessories_hats.png",
        ):
            composite.alpha_composite(load(path).crop((0, 0, 16, 32)))
        return composite
    with Image.open(ROOT / relative_path) as opened:
        return opened.convert("RGBA")


def checker(width: int, height: int, a, b) -> Image.Image:
    target = Image.new("RGBA", (width, height), a)
    draw = ImageDraw.Draw(target)
    size = 8
    for y in range(0, height, size):
        for x in range(0, width, size):
            if (x // size + y // size) % 2:
                draw.rectangle((x, y, x + size - 1, y + size - 1), fill=b)
    return target


def paste_center(canvas: Image.Image, sprite: Image.Image, box, scale: int) -> None:
    x, y, width, height = box
    scaled = sprite.resize((sprite.width * scale, sprite.height * scale), Image.Resampling.NEAREST)
    canvas.alpha_composite(scaled, (x + (width - scaled.width) // 2, y + (height - scaled.height) // 2))


def main() -> None:
    canvas = Image.new("RGBA", (3600, 7600), (244, 240, 232, 255))
    draw = ImageDraw.Draw(canvas)
    draw.text((28, 20), "YjsE WAVE 03 - PLAYER / BIOME PRODUCTION / PRIORITY REGENERATION", font=FONT, fill=INK)
    draw.text((28, 38), "Nearest-neighbor QA: 1x / 2x / 3x / 4x, light and dark fields, tile-aligned seamless parallax", font=FONT, fill=INK)

    y = 70
    for label, path in FOREGROUND:
        sprite = load(path)
        row_height = max(112, sprite.height * 4 + 30)
        draw.rectangle((20, y, 3580, y + row_height), fill=PANEL)
        draw.text((30, y + 8), f"{label}  {sprite.width}x{sprite.height}", font=FONT, fill=INK)
        x = 210
        for background in (LIGHT, DARK):
            for scale in (1, 2, 3, 4):
                cell_w = max(86, sprite.width * scale + 16)
                shade = tuple(max(0, channel - 12) for channel in background[:3]) + (255,)
                field = checker(cell_w, row_height - 16, background, shade)
                canvas.alpha_composite(field, (x, y + 8))
                draw.text((x + 4, y + 10), f"{scale}x", font=FONT, fill=INK if background == LIGHT else LIGHT)
                paste_center(canvas, sprite, (x, y + 18, cell_w, row_height - 22), scale)
                x += cell_w + 8
        y += row_height + 10

    draw.text((28, y + 8), "PARALLAX / TILE COHESION - NATIVE PERIOD, WRAP SEAM, TILE PALETTE AND DETAIL", font=FONT, fill=INK)
    y += 32
    for label, path, tile_paths in BACKGROUNDS:
        sprite = load(path)
        draw.rectangle((20, y, 3580, y + 340), fill=PANEL)
        draw.text((30, y + 8), f"{label}  {sprite.width}x{sprite.height}", font=FONT, fill=INK)
        canvas.alpha_composite(sprite, (30, y + 30))

        wrap = Image.new("RGBA", (640, 128))
        wrap.alpha_composite(sprite.crop((448, 0, 512, 128)), (0, 0))
        wrap.alpha_composite(sprite, (64, 0))
        wrap.alpha_composite(sprite.crop((0, 0, 64, 128)), (576, 0))
        canvas.alpha_composite(wrap, (570, y + 30))
        draw.line((634, y + 30, 634, y + 158), fill=(255, 241, 166, 255), width=1)

        tile_x = 1240
        draw.text((tile_x, y + 18), "TILE REFERENCES 4x", font=FONT, fill=INK)
        for tile_path in tile_paths:
            tile = load(tile_path).crop((0, 0, 16, 16)).resize((64, 64), Image.Resampling.NEAREST)
            canvas.alpha_composite(tile, (tile_x, y + 34))
            draw.text((tile_x, y + 102), Path(tile_path).stem[:18], font=FONT, fill=INK)
            tile_x += 84

        detail = sprite.crop((176, 64, 240, 96))
        x = 30
        for scale in (1, 2, 3, 4):
            zoom = detail.resize((detail.width * scale, detail.height * scale), Image.Resampling.NEAREST)
            canvas.alpha_composite(zoom, (x, y + 190))
            draw.text((x, y + 174), f"{scale}x", font=FONT, fill=INK)
            x += zoom.width + 18
        y += 352

    used = canvas.crop((0, 0, canvas.width, y + 20))
    used.save(OUTPUT, format="PNG", optimize=False, compress_level=9)


if __name__ == "__main__":
    main()
