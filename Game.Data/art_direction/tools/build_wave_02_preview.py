#!/usr/bin/env python3
"""Build the deterministic visual QA sheet for asset wave 02."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "art_direction" / "wave_02_asset_preview.png"
LIGHT = (232, 226, 214, 255)
DARK = (35, 32, 46, 255)
INK = (27, 24, 38, 255)
PANEL = (198, 192, 184, 255)
FONT = ImageFont.load_default()

FOREGROUND = [
    ("SQUIRREL", "sprites/entities/critters/squirrel.png"),
    ("FIREFLY", "sprites/entities/critters/firefly.png"),
    ("FOREST BOAR", "sprites/entities/enemies/forest_boar.png"),
    ("CAVE SPIDER", "sprites/entities/enemies/cave_spider.png"),
    ("HAIR V2", "sprites/entities/player/player_hair_variants_v2.png"),
    ("CLOTHES V2", "sprites/entities/player/player_clothes_variants_v2.png"),
    ("ACCESSORIES", "sprites/entities/player/player_accessories_hats.png"),
    ("MANA CRYSTAL", "sprites/items/accessories/mana_crystal.png"),
    ("MINING CHARM", "sprites/items/accessories/mining_charm.png"),
    ("CHAIR", "sprites/world/objects/chair.png"),
    ("TABLE", "sprites/world/objects/table.png"),
    ("CHEST", "sprites/world/objects/chest.png"),
    ("LANTERN", "sprites/world/objects/lantern.png"),
]

BACKGROUNDS = [
    ("MEADOW", "sprites/world/backgrounds/meadow_parallax_layer.png"),
    ("MUSHROOM CAVES", "sprites/world/backgrounds/mushroom_cave_parallax_layer.png"),
    ("CRYSTAL DEPTHS", "sprites/world/backgrounds/crystal_depths_parallax_layer.png"),
]


def load(relative_path: str) -> Image.Image:
    return Image.open(ROOT / relative_path).convert("RGBA")


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
    px = x + (width - scaled.width) // 2
    py = y + (height - scaled.height) // 2
    canvas.alpha_composite(scaled, (px, py))


def main() -> None:
    canvas = Image.new("RGBA", (3000, 3600), (244, 240, 232, 255))
    draw = ImageDraw.Draw(canvas)
    draw.text((28, 20), "YjsE WAVE 02 - CREATURES / BIOMES / CHARACTER / PROPS", font=FONT, fill=INK)
    draw.text((28, 38), "Nearest-neighbor QA: 1x / 2x / 3x / 4x on light and dark fields", font=FONT, fill=INK)

    y = 70
    for label, path in FOREGROUND:
        sprite = load(path)
        row_height = max(108, sprite.height * 4 + 28)
        draw.rectangle((20, y, 2980, y + row_height), fill=PANEL)
        draw.text((30, y + 8), f"{label}  {sprite.width}x{sprite.height}", font=FONT, fill=INK)
        x = 180
        for background in (LIGHT, DARK):
            for scale in (1, 2, 3, 4):
                cell_w = max(80, sprite.width * scale + 16)
                field = checker(cell_w, row_height - 16, background, tuple(max(0, channel - 12) for channel in background[:3]) + (255,))
                canvas.alpha_composite(field, (x, y + 8))
                draw.text((x + 4, y + 10), f"{scale}x", font=FONT, fill=INK if background == LIGHT else LIGHT)
                paste_center(canvas, sprite, (x, y + 16, cell_w, row_height - 20), scale)
                x += cell_w + 8
        y += row_height + 10

    draw.text((28, y + 8), "PARALLAX BACKGROUNDS - NATIVE PERIOD + WRAP SEAM + DETAIL ZOOMS", font=FONT, fill=INK)
    y += 30
    for label, path in BACKGROUNDS:
        sprite = load(path)
        draw.rectangle((20, y, 2980, y + 282), fill=PANEL)
        draw.text((30, y + 8), f"{label}  {sprite.width}x{sprite.height}", font=FONT, fill=INK)
        canvas.alpha_composite(sprite, (30, y + 28))
        wrap = Image.new("RGBA", (640, 128))
        wrap.alpha_composite(sprite.crop((448, 0, 512, 128)), (0, 0))
        wrap.alpha_composite(sprite, (64, 0))
        wrap.alpha_composite(sprite.crop((0, 0, 64, 128)), (576, 0))
        canvas.alpha_composite(wrap, (570, y + 28))
        draw.line((634, y + 28, 634, y + 156), fill=(255, 241, 166, 255), width=1)
        detail = sprite.crop((192, 64, 256, 96))
        x = 30
        for scale in (1, 2, 3, 4):
            zoom = detail.resize((detail.width * scale, detail.height * scale), Image.Resampling.NEAREST)
            canvas.alpha_composite(zoom, (x, y + 174))
            draw.text((x, y + 160), f"{scale}x", font=FONT, fill=INK)
            x += zoom.width + 18
        y += 294

    used = canvas.crop((0, 0, canvas.width, min(canvas.height, y + 20)))
    used.save(OUTPUT, format="PNG", optimize=False, compress_level=9)


if __name__ == "__main__":
    main()
