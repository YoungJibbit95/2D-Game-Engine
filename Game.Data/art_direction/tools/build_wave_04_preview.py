#!/usr/bin/env python3
"""Build the deterministic Production Wave 04 visual QA sheet."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "art_direction" / "wave_04_production_preview.png"
SUMMARY = ROOT / "art_direction" / "wave_04_preview_summary.json"
SOURCE = ROOT / "art_direction" / "generated_sources" / "wave_04_character_source.png"
LIGHT = (236, 230, 218, 255)
DARK = (35, 32, 46, 255)
INK = (27, 24, 38, 255)
PANEL = (199, 194, 186, 255)
ACCENT = (61, 141, 132, 255)
FONT = ImageFont.load_default()

CHARACTER = [
    ("BODY", "sprites/entities/player/wave04/player_body_actions.png"),
    ("HAIR", "sprites/entities/player/wave04/player_hair_actions.png"),
    ("CLOTHES", "sprites/entities/player/wave04/player_clothes_actions.png"),
    ("ARMOR", "sprites/entities/player/wave04/player_armor_actions.png"),
    ("TOOLS / BLOCK / HURT", "sprites/entities/player/wave04/player_equipment_actions.png"),
]

BACKGROUNDS = [
    ("MEADOW", "sprites/world/backgrounds/wave04/meadow_parallax_layer.png", ["grass_autotile", "dirt_autotile"]),
    ("FOREST", "sprites/world/backgrounds/wave04/forest_parallax_layer.png", ["leaves_autotile", "oak_trunk_autotile"]),
    ("CAVE", "sprites/world/backgrounds/wave04/cave_parallax_layer.png", ["stone_autotile", "granite_autotile"]),
    ("MUSHROOM", "sprites/world/backgrounds/wave04/mushroom_cave_parallax_layer.png", ["mud_autotile", "granite_autotile"]),
    ("CRYSTAL", "sprites/world/backgrounds/wave04/crystal_depths_parallax_layer.png", ["stone_autotile", "ice_autotile"]),
]

EFFECTS = [
    ("MEADOW SEED DRIFT", "sprites/effects/ambient/meadow_seed_drift.png"),
    ("FOREST LEAF SWIRL", "sprites/effects/ambient/forest_leaf_swirl.png"),
    ("CAVE MOTES", "sprites/effects/ambient/cave_motes.png"),
    ("MUSHROOM SPORES", "sprites/effects/ambient/mushroom_spores.png"),
    ("CRYSTAL GLINTS", "sprites/effects/ambient/crystal_glints.png"),
    ("RAIN", "sprites/effects/weather/rain_streaks.png"),
    ("SNOW", "sprites/effects/weather/snow_flurry.png"),
    ("WIND", "sprites/effects/weather/wind_gust.png"),
    ("STORM", "sprites/effects/weather/storm_spark.png"),
    ("SWORD ARC", "sprites/effects/combat/sword_arc.png"),
    ("BLOCK IMPACT", "sprites/effects/combat/block_impact.png"),
    ("HURT BURST", "sprites/effects/combat/hurt_burst.png"),
    ("TOOL IMPACT", "sprites/effects/combat/tool_impact.png"),
]

UI = [
    ("PANEL 9-SLICE", "sprites/ui/wave04/panel_9slice.png"),
    ("TOOLTIP 9-SLICE", "sprites/ui/wave04/tooltip_9slice.png"),
    ("RUN", "sprites/ui/wave04/run.png"),
    ("BLOCK", "sprites/ui/wave04/block.png"),
    ("HURT", "sprites/ui/wave04/hurt.png"),
    ("TOOL", "sprites/ui/wave04/tool.png"),
    ("WEATHER", "sprites/ui/wave04/weather.png"),
    ("ARMOR", "sprites/ui/wave04/armor.png"),
]


def load(relative_path: str) -> Image.Image:
    with Image.open(ROOT / relative_path) as opened:
        return opened.convert("RGBA")


def checker(width: int, height: int, first, second) -> Image.Image:
    target = Image.new("RGBA", (width, height), first)
    draw = ImageDraw.Draw(target)
    size = 8
    for y in range(0, height, size):
        for x in range(0, width, size):
            if (x // size + y // size) % 2:
                draw.rectangle((x, y, x + size - 1, y + size - 1), fill=second)
    return target


def title(draw: ImageDraw.ImageDraw, y: int, value: str) -> None:
    draw.rectangle((20, y, 2380, y + 25), fill=INK)
    draw.text((30, y + 8), value, font=FONT, fill=LIGHT)


def composite_character() -> Image.Image:
    result = Image.new("RGBA", (256, 32))
    for _, path in CHARACTER:
        result.alpha_composite(load(path))
    return result


def paste_scaled(canvas: Image.Image, source: Image.Image, x: int, y: int, scale: int) -> None:
    canvas.alpha_composite(source.resize((source.width * scale, source.height * scale), Image.Resampling.NEAREST), (x, y))


def character_section(canvas: Image.Image, draw: ImageDraw.ImageDraw, y: int) -> int:
    title(draw, y, "CHARACTER V1 WAVE04 / 16x32 CELLS / 16 FRAMES / SHARED REGISTRATION")
    y += 35
    with Image.open(SOURCE) as opened:
        generated = opened.convert("RGBA")
    generated.thumbnail((800, 320), Image.Resampling.NEAREST)
    draw.rectangle((20, y, 2380, y + 350), fill=PANEL)
    canvas.alpha_composite(generated, (30, y + 26))
    draw.text((30, y + 8), "GENERATED SOURCE / BUILT-IN IMAGE_GEN / CHROMA EXCLUDED DURING PALETTE QUANTIZATION", font=FONT, fill=INK)
    composite = composite_character()
    draw.text((860, y + 8), "FINAL PIXEL-NATIVE COMPOSITE: IDLE / RUN / JUMP / FALL / TOOL / BLOCK / HURT", font=FONT, fill=INK)
    paste_scaled(canvas, composite, 860, y + 45, 4)
    y += 365

    all_rows = CHARACTER + [("COMPOSITE", "__composite__")]
    for label, path in all_rows:
        sprite = composite if path == "__composite__" else load(path)
        row_height = 190
        draw.rectangle((20, y, 2380, y + row_height), fill=PANEL)
        draw.text((30, y + 8), f"{label} / {sprite.width}x{sprite.height}", font=FONT, fill=INK)
        for index, background in enumerate((LIGHT, DARK)):
            shade = tuple(max(0, channel - 12) for channel in background[:3]) + (255,)
            field = checker(1080, 150, background, shade)
            x = 190 + index * 1090
            canvas.alpha_composite(field, (x, y + 25))
            paste_scaled(canvas, sprite, x + 10, y + 38, 4)
        y += row_height + 8
    return y


def background_section(canvas: Image.Image, draw: ImageDraw.ImageDraw, y: int) -> tuple[int, list[dict]]:
    title(draw, y, "PARALLAX / TILE PALETTE / NATIVE PERIOD / 64PX WRAP LEAD-IN / SEAM COLUMN")
    y += 35
    records = []
    for label, path, tile_names in BACKGROUNDS:
        sprite = load(path)
        draw.rectangle((20, y, 2380, y + 285), fill=PANEL)
        draw.text((30, y + 8), f"{label} / {sprite.width}x{sprite.height}", font=FONT, fill=INK)
        canvas.alpha_composite(sprite, (30, y + 28))
        wrap = Image.new("RGBA", (640, 128))
        wrap.alpha_composite(sprite.crop((448, 0, 512, 128)), (0, 0))
        wrap.alpha_composite(sprite, (64, 0))
        wrap.alpha_composite(sprite.crop((0, 0, 64, 128)), (576, 0))
        canvas.alpha_composite(wrap, (565, y + 28))
        draw.line((629, y + 28, 629, y + 155), fill=(255, 241, 166, 255), width=1)
        draw.text((1225, y + 12), "TILE REFERENCES / 4x", font=FONT, fill=INK)
        tile_x = 1225
        for tile_name in tile_names:
            tile = load(f"sprites/world/tiles/{tile_name}.png").crop((0, 0, 16, 16))
            paste_scaled(canvas, tile, tile_x, y + 35, 4)
            draw.text((tile_x, y + 104), tile_name, font=FONT, fill=INK)
            tile_x += 120
        detail = sprite.crop((176, 64, 240, 96))
        x = 30
        for scale in (1, 2, 3, 4):
            paste_scaled(canvas, detail, x, y + 195, scale)
            draw.text((x, y + 178), f"{scale}x", font=FONT, fill=INK)
            x += detail.width * scale + 18
        left = list(sprite.crop((0, 0, 1, 128)).get_flattened_data())
        right = list(sprite.crop((511, 0, 512, 128)).get_flattened_data())
        records.append({"label": label, "path": path, "horizontalSeamColumnsEqual": left == right})
        y += 293
    return y, records


def sprite_grid_section(canvas: Image.Image, draw: ImageDraw.ImageDraw, y: int, heading: str, rows: list[tuple[str, str]]) -> int:
    title(draw, y, heading)
    y += 35
    card_width, card_height = 460, 210
    for index, (label, path) in enumerate(rows):
        column, row = index % 5, index // 5
        x = 20 + column * 472
        card_y = y + row * 222
        sprite = load(path)
        draw.rectangle((x, card_y, x + card_width, card_y + card_height), fill=PANEL)
        draw.text((x + 10, card_y + 8), f"{label} / {sprite.width}x{sprite.height}", font=FONT, fill=INK)
        for field_index, background in enumerate((LIGHT, DARK)):
            shade = tuple(max(0, channel - 12) for channel in background[:3]) + (255,)
            field = checker(210, 165, background, shade)
            field_x = x + 10 + field_index * 220
            canvas.alpha_composite(field, (field_x, card_y + 30))
            scale = min(4, max(1, min(190 // sprite.width, 130 // sprite.height)))
            scaled = sprite.resize((sprite.width * scale, sprite.height * scale), Image.Resampling.NEAREST)
            canvas.alpha_composite(scaled, (field_x + (210 - scaled.width) // 2, card_y + 45 + (135 - scaled.height) // 2))
    rows_used = (len(rows) + 4) // 5
    return y + rows_used * 222


def main() -> None:
    canvas = Image.new("RGBA", (2400, 6000), (244, 240, 232, 255))
    draw = ImageDraw.Draw(canvas)
    draw.text((28, 18), "YjsE PRODUCTION WAVE 04 / GENERATED SOURCE TO DETERMINISTIC RUNTIME PIXEL ART", font=FONT, fill=INK)
    draw.text((28, 36), "Nearest-neighbor QA / binary alpha / exact frame grids / additive IDs / Wave-03 compatibility preserved", font=FONT, fill=INK)
    y = character_section(canvas, draw, 62)
    y, seam_records = background_section(canvas, draw, y + 10)
    y = sprite_grid_section(canvas, draw, y + 10, "AMBIENT / WEATHER / COMBAT EFFECTS / 1x-4x INTEGER QA", EFFECTS)
    y = sprite_grid_section(canvas, draw, y + 10, "UI / TWO NINE-SLICE SETS / ACTION ICONS / LIGHT-DARK QA", UI)
    used = canvas.crop((0, 0, canvas.width, y + 20))
    used.save(OUTPUT, format="PNG", optimize=False, compress_level=9)
    summary = {
        "waveId": "wave_04_character_atmosphere_ui_production",
        "preview": str(OUTPUT.relative_to(ROOT)).replace("\\", "/"),
        "dimensions": list(used.size),
        "sha256": hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
        "integerScales": [1, 2, 3, 4],
        "backgroundFields": ["light", "dark"],
        "characterLayerCount": 5,
        "characterFrameCount": 16,
        "parallax": seam_records,
        "effectCount": len(EFFECTS),
        "uiAssetCount": len(UI),
    }
    SUMMARY.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
