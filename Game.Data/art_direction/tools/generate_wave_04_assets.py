#!/usr/bin/env python3
"""Generate Production Wave 04 character, parallax, effects, and UI assets."""

from __future__ import annotations

from collections import Counter
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw

from generate_wave_02_assets import C, image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art_direction" / "generated_sources" / "wave_04_character_source.png"
PROMPTSET_PATH = ROOT / "art_direction" / "wave_04_promptset.json"
BRIEF_PATH = ROOT / "asset_briefs" / "production_wave_04_briefs.json"
MANIFEST_FRAGMENT_PATH = ROOT / "art_direction" / "wave_04_manifest_entries.json"
PROVENANCE_PATH = ROOT / "art_direction" / "wave_04_provenance.json"
WAVE_ID = "wave_04_character_atmosphere_ui_production"
WAVE_DATE = "2026-07-14"
GENERATOR = "Game.Data/art_direction/tools/generate_wave_04_assets.py"

FRAME_IDS = [
    "idle_0", "idle_1",
    "run_0", "run_1", "run_2", "run_3", "run_4", "run_5",
    "jump", "fall",
    "tool_0", "tool_1", "tool_2",
    "block", "hurt_0", "hurt_1",
]

POSES = [
    {"bob": 0, "stride": 0, "arm": 0, "kind": "idle"},
    {"bob": -1, "stride": 0, "arm": 0, "kind": "idle"},
    {"bob": 0, "stride": -2, "arm": 2, "kind": "run"},
    {"bob": -1, "stride": -1, "arm": 1, "kind": "run"},
    {"bob": 0, "stride": 1, "arm": -1, "kind": "run"},
    {"bob": 0, "stride": 2, "arm": -2, "kind": "run"},
    {"bob": -1, "stride": 1, "arm": -1, "kind": "run"},
    {"bob": 0, "stride": -1, "arm": 1, "kind": "run"},
    {"bob": -3, "stride": -1, "arm": -2, "kind": "jump"},
    {"bob": -1, "stride": 1, "arm": 2, "kind": "fall"},
    {"bob": 0, "stride": 0, "arm": -3, "kind": "tool_0"},
    {"bob": 0, "stride": 0, "arm": -1, "kind": "tool_1"},
    {"bob": 1, "stride": 1, "arm": 2, "kind": "tool_2"},
    {"bob": 0, "stride": -1, "arm": -1, "kind": "block"},
    {"bob": 0, "stride": 1, "arm": 2, "kind": "hurt_0"},
    {"bob": 1, "stride": 2, "arm": 3, "kind": "hurt_1"},
]

CHARACTER_LAYERS = {
    "body": "sprites/entities/player/wave04/player_body_actions.png",
    "hair": "sprites/entities/player/wave04/player_hair_actions.png",
    "clothes": "sprites/entities/player/wave04/player_clothes_actions.png",
    "armor": "sprites/entities/player/wave04/player_armor_actions.png",
    "equipment": "sprites/entities/player/wave04/player_equipment_actions.png",
}

BACKGROUND_SPECS = {
    "meadow": {
        "path": "sprites/world/backgrounds/wave04/meadow_parallax_layer.png",
        "tiles": ["grass_autotile", "dirt_autotile"],
    },
    "forest": {
        "path": "sprites/world/backgrounds/wave04/forest_parallax_layer.png",
        "tiles": ["leaves_autotile", "oak_trunk_autotile"],
    },
    "cave": {
        "path": "sprites/world/backgrounds/wave04/cave_parallax_layer.png",
        "tiles": ["stone_autotile", "granite_autotile"],
    },
    "mushroom": {
        "path": "sprites/world/backgrounds/wave04/mushroom_cave_parallax_layer.png",
        "tiles": ["mud_autotile", "granite_autotile"],
    },
    "crystal": {
        "path": "sprites/world/backgrounds/wave04/crystal_depths_parallax_layer.png",
        "tiles": ["stone_autotile", "ice_autotile"],
    },
}

EFFECT_SPECS = {
    "ambient/meadow_seed_drift": ("sprites/effects/ambient/meadow_seed_drift.png", 16, "meadow", "ambient"),
    "ambient/forest_leaf_swirl": ("sprites/effects/ambient/forest_leaf_swirl.png", 16, "forest", "ambient"),
    "ambient/cave_motes": ("sprites/effects/ambient/cave_motes.png", 16, "cave", "ambient"),
    "ambient/mushroom_spores": ("sprites/effects/ambient/mushroom_spores.png", 16, "mushroom", "ambient"),
    "ambient/crystal_glints": ("sprites/effects/ambient/crystal_glints.png", 16, "crystal", "ambient"),
    "weather/rain_streaks": ("sprites/effects/weather/rain_streaks.png", 16, "rain", "weather"),
    "weather/snow_flurry": ("sprites/effects/weather/snow_flurry.png", 16, "snow", "weather"),
    "weather/wind_gust": ("sprites/effects/weather/wind_gust.png", 16, "wind", "weather"),
    "weather/storm_spark": ("sprites/effects/weather/storm_spark.png", 16, "storm", "weather"),
    "combat/sword_arc": ("sprites/effects/combat/sword_arc.png", 32, "sword", "combat"),
    "combat/block_impact": ("sprites/effects/combat/block_impact.png", 32, "block", "combat"),
    "combat/hurt_burst": ("sprites/effects/combat/hurt_burst.png", 32, "hurt", "combat"),
    "combat/tool_impact": ("sprites/effects/combat/tool_impact.png", 32, "tool", "combat"),
}

NINE_SLICE_SPECS = {
    "panel": "sprites/ui/wave04/panel_9slice.png",
    "tooltip": "sprites/ui/wave04/tooltip_9slice.png",
}

ICON_SPECS = {
    "run": "sprites/ui/wave04/run.png",
    "block": "sprites/ui/wave04/block.png",
    "hurt": "sprites/ui/wave04/hurt.png",
    "tool": "sprites/ui/wave04/tool.png",
    "weather": "sprites/ui/wave04/weather.png",
    "armor": "sprites/ui/wave04/armor.png",
}


def save(relative_path: str, source: Image.Image) -> None:
    path = ROOT / relative_path
    path.parent.mkdir(parents=True, exist_ok=True)
    alpha = source.getchannel("A").point(lambda value: 255 if value else 0)
    source.putalpha(alpha)
    source.save(path, format="PNG", optimize=False, compress_level=9)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rgb_distance(left: tuple[int, int, int], right: tuple[int, int, int]) -> int:
    return sum((left[index] - right[index]) ** 2 for index in range(3))


def generated_source_palette() -> tuple[dict[str, str], list[str]]:
    """Quantize the generated source to the checked-in style palette deterministically."""
    palette = {name: value[:3] for name, value in C.items() if name != "clear"}
    counts: Counter[str] = Counter()
    with Image.open(SOURCE) as opened:
        source = opened.convert("RGBA").resize((256, 102), Image.Resampling.NEAREST)
    for red, green, blue, alpha in source.get_flattened_data():
        if alpha == 0 or (red > 220 and blue > 180 and green < 100):
            continue
        nearest = min(palette, key=lambda name: rgb_distance((red, green, blue), palette[name]))
        counts[nearest] += 1

    def pick(candidates: list[str], fallback: str) -> str:
        ranked = sorted(candidates, key=lambda name: (-counts[name], candidates.index(name)))
        return ranked[0] if counts[ranked[0]] else fallback

    selected = {
        "hair_base": pick(["wood1", "wood2", "wood3", "copper0"], "wood1"),
        "hair_light": pick(["wood2", "wood3", "copper1", "copper2"], "wood3"),
        "cloth_base": pick(["teal0", "teal1", "cloth_blue", "cloth_blue_hi"], "teal0"),
        "cloth_light": pick(["teal1", "teal2", "cloth_blue_hi"], "teal1"),
        "armor_base": pick(["copper0", "copper1", "copper2", "mid"], "copper1"),
        "armor_light": pick(["copper2", "copper3", "soft", "pale"], "copper3"),
    }
    return selected, [name for name, _ in counts.most_common(12)]


SOURCE_COLORS, SOURCE_QUANTIZED_TOP = generated_source_palette()


def frame_x(frame: int) -> int:
    return frame * 16


def character_geometry(frame: int) -> dict[str, int | str]:
    pose = POSES[frame]
    hurt_shift = -1 if pose["kind"] == "hurt_0" else (1 if pose["kind"] == "hurt_1" else 0)
    return {
        **pose,
        "x": frame_x(frame) + hurt_shift,
        "top": 3 + int(pose["bob"]),
    }


def draw_body_layer() -> Image.Image:
    result = image(256, 32)
    draw = ImageDraw.Draw(result)
    for frame in range(16):
        g = character_geometry(frame)
        x, top = int(g["x"]), int(g["top"])
        stride, arm, kind = int(g["stride"]), int(g["arm"]), str(g["kind"])

        draw.rectangle((x + 5, top, x + 11, top + 8), fill=C["outline"])
        draw.rectangle((x + 6, top + 1, x + 10, top + 7), fill=C["mush3"])
        draw.rectangle((x + 6, top + 2, x + 7, top + 4), fill=C["pale"])
        draw.point((x + 10, top + 4), fill=C["outline"])
        draw.rectangle((x + 7, top + 9, x + 9, top + 10), fill=C["outline"])

        shoulder = top + 11
        draw.rectangle((x + 4, shoulder, x + 11, shoulder + 10), fill=C["outline"])
        draw.rectangle((x + 5, shoulder + 1, x + 10, shoulder + 9), fill=C["mush3"])

        left_y = shoulder + max(-2, min(3, arm))
        right_y = shoulder - max(-2, min(3, arm))
        if kind == "block":
            right_y = shoulder - 2
        draw.rectangle((x + 2, left_y, x + 4, left_y + 8), fill=C["outline"])
        draw.rectangle((x + 3, left_y + 1, x + 3, left_y + 6), fill=C["mush3"])
        draw.rectangle((x + 11, right_y, x + 13, right_y + 8), fill=C["outline"])
        draw.rectangle((x + 12, right_y + 1, x + 12, right_y + 6), fill=C["mush3"])

        hip = top + 21
        if kind in {"jump", "fall"}:
            left_offset, right_offset = -1, 1
            leg_bottom = 29
        else:
            left_offset, right_offset = stride, -stride
            leg_bottom = 30
        draw.rectangle((x + 4 + left_offset, hip, x + 7 + left_offset, leg_bottom), fill=C["outline"])
        draw.rectangle((x + 5 + left_offset, hip + 1, x + 6 + left_offset, leg_bottom - 1), fill=C["mush3"])
        draw.rectangle((x + 8 + right_offset, hip, x + 11 + right_offset, leg_bottom), fill=C["outline"])
        draw.rectangle((x + 9 + right_offset, hip + 1, x + 10 + right_offset, leg_bottom - 1), fill=C["mush3"])
    return result


def draw_hair_layer() -> Image.Image:
    result = image(256, 32)
    draw = ImageDraw.Draw(result)
    for frame in range(16):
        g = character_geometry(frame)
        x, top = int(g["x"]), int(g["top"])
        base, light = C[SOURCE_COLORS["hair_base"]], C[SOURCE_COLORS["hair_light"]]
        draw.polygon(
            [(x + 4, top + 1), (x + 5, top - 1), (x + 8, top), (x + 10, top - 1),
             (x + 12, top + 1), (x + 14, top), (x + 12, top + 4), (x + 12, top + 7),
             (x + 10, top + 5), (x + 10, top + 2), (x + 5, top + 3)],
            fill=C["outline"],
        )
        draw.polygon(
            [(x + 5, top + 1), (x + 6, top), (x + 8, top + 1), (x + 10, top),
             (x + 12, top + 1), (x + 11, top + 4), (x + 10, top + 4), (x + 10, top + 2),
             (x + 6, top + 3)],
            fill=base,
        )
        draw.rectangle((x + 6, top + 1, x + 9, top + 1), fill=light)
    return result


def draw_clothes_layer() -> Image.Image:
    result = image(256, 32)
    draw = ImageDraw.Draw(result)
    for frame in range(16):
        g = character_geometry(frame)
        x, top = int(g["x"]), int(g["top"])
        stride, arm, kind = int(g["stride"]), int(g["arm"]), str(g["kind"])
        base, light = C[SOURCE_COLORS["cloth_base"]], C[SOURCE_COLORS["cloth_light"]]
        shoulder = top + 11
        draw.polygon([(x + 4, shoulder), (x + 11, shoulder), (x + 12, shoulder + 9), (x + 3, shoulder + 9)], fill=C["outline"])
        draw.rectangle((x + 5, shoulder + 1, x + 10, shoulder + 8), fill=base)
        draw.rectangle((x + 5, shoulder + 1, x + 7, shoulder + 3), fill=light)
        draw.rectangle((x + 6, shoulder + 8, x + 10, shoulder + 9), fill=C["wood1"])
        draw.point((x + 8, shoulder + 8), fill=C["gold2"])

        left_y = shoulder + max(-2, min(3, arm))
        right_y = shoulder - max(-2, min(3, arm))
        if kind == "block":
            right_y = shoulder - 2
        draw.rectangle((x + 2, left_y, x + 4, left_y + 5), fill=C["outline"])
        draw.rectangle((x + 3, left_y + 1, x + 3, left_y + 4), fill=base)
        draw.rectangle((x + 11, right_y, x + 13, right_y + 5), fill=C["outline"])
        draw.rectangle((x + 12, right_y + 1, x + 12, right_y + 4), fill=light)

        hip = top + 21
        left_offset = -1 if kind in {"jump", "fall"} else stride
        right_offset = 1 if kind in {"jump", "fall"} else -stride
        bottom = 29 if kind in {"jump", "fall"} else 30
        for start, offset, color in ((4, left_offset, base), (8, right_offset, light)):
            draw.rectangle((x + start + offset, hip, x + start + 3 + offset, bottom), fill=C["outline"])
            draw.rectangle((x + start + 1 + offset, hip + 1, x + start + 2 + offset, bottom - 2), fill=color)
            draw.rectangle((x + start + offset, bottom - 2, x + start + 3 + offset, bottom), fill=C["wood0"])
    return result


def draw_armor_layer() -> Image.Image:
    result = image(256, 32)
    draw = ImageDraw.Draw(result)
    for frame in range(16):
        g = character_geometry(frame)
        x, top = int(g["x"]), int(g["top"])
        stride, kind = int(g["stride"]), str(g["kind"])
        base, light = C[SOURCE_COLORS["armor_base"]], C[SOURCE_COLORS["armor_light"]]

        draw.rectangle((x + 4, top + 11, x + 11, top + 20), fill=C["outline"])
        draw.polygon([(x + 5, top + 12), (x + 10, top + 12), (x + 9, top + 19), (x + 6, top + 19)], fill=base)
        draw.rectangle((x + 6, top + 12, x + 9, top + 13), fill=light)
        draw.rectangle((x + 3, top + 11, x + 5, top + 14), fill=C["outline"])
        draw.rectangle((x + 4, top + 12, x + 5, top + 13), fill=base)
        draw.rectangle((x + 10, top + 11, x + 12, top + 14), fill=C["outline"])
        draw.rectangle((x + 10, top + 12, x + 11, top + 13), fill=light)

        hip = top + 21
        left_offset = -1 if kind in {"jump", "fall"} else stride
        right_offset = 1 if kind in {"jump", "fall"} else -stride
        for start, offset in ((4, left_offset), (8, right_offset)):
            draw.rectangle((x + start + offset, hip + 1, x + start + 3 + offset, hip + 5), fill=C["outline"])
            draw.rectangle((x + start + 1 + offset, hip + 2, x + start + 2 + offset, hip + 4), fill=base)
        if kind == "block":
            draw.rectangle((x + 5, top - 1, x + 11, top + 2), fill=C["outline"])
            draw.rectangle((x + 6, top, x + 10, top + 1), fill=base)
    return result


def draw_pickaxe(draw: ImageDraw.ImageDraw, x: int, y: int, phase: int) -> None:
    if phase == 0:
        draw.line((x + 7, y + 16, x + 13, y + 4), fill=C["outline"], width=3)
        draw.line((x + 8, y + 15, x + 13, y + 5), fill=C["wood3"], width=1)
        draw.polygon([(x + 8, y + 3), (x + 15, y + 2), (x + 14, y + 6), (x + 10, y + 5)], fill=C["outline"])
        draw.line((x + 10, y + 4, x + 14, y + 3), fill=C["soft"], width=1)
    elif phase == 1:
        draw.line((x + 7, y + 15, x + 15, y + 10), fill=C["outline"], width=3)
        draw.line((x + 8, y + 14, x + 14, y + 10), fill=C["wood3"], width=1)
        draw.polygon([(x + 12, y + 7), (x + 15, y + 9), (x + 15, y + 12), (x + 12, y + 11)], fill=C["outline"])
    else:
        draw.line((x + 7, y + 14, x + 13, y + 24), fill=C["outline"], width=3)
        draw.line((x + 8, y + 15, x + 13, y + 23), fill=C["wood3"], width=1)
        draw.polygon([(x + 10, y + 22), (x + 15, y + 21), (x + 15, y + 25), (x + 11, y + 25)], fill=C["outline"])


def draw_equipment_layer() -> Image.Image:
    result = image(256, 32)
    draw = ImageDraw.Draw(result)
    for frame in range(16):
        g = character_geometry(frame)
        x, top, kind = int(g["x"]), int(g["top"]), str(g["kind"])
        if kind.startswith("tool_"):
            draw_pickaxe(draw, x, max(0, top - 2), int(kind[-1]))
        elif kind == "block":
            draw.polygon([(x + 11, top + 9), (x + 15, top + 11), (x + 15, top + 23), (x + 11, top + 26), (x + 8, top + 22), (x + 8, top + 12)], fill=C["outline"])
            draw.polygon([(x + 11, top + 11), (x + 14, top + 12), (x + 14, top + 22), (x + 11, top + 24), (x + 9, top + 21), (x + 9, top + 13)], fill=C["wood2"])
            draw.line((x + 10, top + 13, x + 13, top + 21), fill=C["copper2"], width=1)
        elif kind.startswith("hurt"):
            cx, cy = x + (13 if kind == "hurt_0" else 4), top + 8
            draw.line((cx - 2, cy, cx + 2, cy), fill=C["outline"], width=1)
            draw.line((cx, cy - 2, cx, cy + 2), fill=C["outline"], width=1)
            draw.point((cx, cy), fill=C["red3"])
    return result


def finish_seam(source: Image.Image) -> Image.Image:
    source.paste(source.crop((0, 0, 1, source.height)), (source.width - 1, 0))
    return source


def stepped_height(x: int, base: int, amplitude: int, period: int, phase: int = 0) -> int:
    local = (x + phase) % period
    half = period // 2
    distance = local if local <= half else period - local
    return base - distance * amplitude // max(1, half)


def fill_below(draw: ImageDraw.ImageDraw, base: int, amplitude: int, period: int, color: str, phase: int = 0) -> None:
    for x in range(512):
        y = stepped_height(x, base, amplitude, period, phase)
        draw.line((x, y, x, 127), fill=C[color])


def draw_meadow_background() -> Image.Image:
    result = image(512, 128, C["blue3"])
    draw = ImageDraw.Draw(result)
    for x in range(36, 512, 128):
        draw.rectangle((x, 22, x + 18, 24), fill=C["pale"])
        draw.rectangle((x + 4, 19, x + 13, 24), fill=C["pale"])
    fill_below(draw, 78, 16, 160, "leaf4", 24)
    fill_below(draw, 94, 13, 112, "leaf3", 12)
    fill_below(draw, 108, 9, 72, "leaf2", 31)
    for x in range(20, 512, 58):
        draw.rectangle((x, 92, x + 2, 116), fill=C["wood1"])
        draw.polygon([(x - 7, 99), (x + 1, 85), (x + 9, 99)], fill=C["leaf1"])
        draw.polygon([(x - 5, 94), (x + 1, 82), (x + 7, 94)], fill=C["leaf3"])
    fill_below(draw, 121, 4, 48, "leaf0", 7)
    return finish_seam(result)


def draw_forest_background() -> Image.Image:
    result = image(512, 128, C["teal2"])
    draw = ImageDraw.Draw(result)
    fill_below(draw, 76, 14, 144, "leaf3", 19)
    fill_below(draw, 99, 10, 96, "leaf2", 7)
    for x in range(10, 512, 42):
        trunk_height = 46 + (x // 42 % 3) * 8
        draw.rectangle((x + 6, trunk_height, x + 10, 121), fill=C["wood1"])
        draw.rectangle((x + 7, trunk_height, x + 8, 119), fill=C["wood2"])
        draw.polygon([(x - 8, trunk_height + 20), (x + 8, trunk_height - 10), (x + 24, trunk_height + 20)], fill=C["leaf0"])
        draw.polygon([(x - 4, trunk_height + 9), (x + 8, trunk_height - 15), (x + 20, trunk_height + 9)], fill=C["leaf1"])
        draw.polygon([(x, trunk_height), (x + 8, trunk_height - 19), (x + 16, trunk_height)], fill=C["leaf3"])
    fill_below(draw, 122, 5, 52, "leaf0", 17)
    return finish_seam(result)


def draw_cave_background() -> Image.Image:
    result = image(512, 128, C["earth0"])
    draw = ImageDraw.Draw(result)
    for x in range(0, 512, 32):
        depth = 14 + (x // 32 % 4) * 5
        draw.polygon([(x, 0), (x + 31, 0), (x + 24, depth), (x + 17, depth + 12), (x + 11, depth)], fill=C["dark"])
        draw.line((x + 2, 4, x + 20, depth - 2), fill=C["earth1"], width=2)
    fill_below(draw, 91, 13, 128, "earth1", 21)
    fill_below(draw, 111, 9, 72, "earth2", 8)
    for x in range(24, 512, 76):
        draw.rectangle((x, 76, x + 22, 80), fill=C["mid"])
        draw.rectangle((x + 4, 81, x + 18, 84), fill=C["earth3"])
    fill_below(draw, 123, 4, 44, "dark", 3)
    return finish_seam(result)


def draw_mushroom_background() -> Image.Image:
    result = image(512, 128, C["crystal0"])
    draw = ImageDraw.Draw(result)
    for x in range(0, 512, 48):
        draw.polygon([(x, 0), (x + 47, 0), (x + 34, 18), (x + 26, 12), (x + 18, 27)], fill=C["earth0"])
    fill_below(draw, 101, 12, 104, "earth1", 31)
    for index, x in enumerate(range(20, 512, 54)):
        height = 18 + (index % 3) * 7
        draw.rectangle((x + 5, 102 - height, x + 8, 111), fill=C["mush3"])
        draw.rectangle((x + 6, 102 - height, x + 7, 110), fill=C["pale"])
        draw.polygon([(x - 3, 103 - height), (x + 7, 94 - height), (x + 17, 103 - height), (x + 13, 107 - height), (x + 1, 107 - height)], fill=C["outline"])
        draw.polygon([(x - 1, 102 - height), (x + 7, 96 - height), (x + 15, 102 - height), (x + 12, 104 - height), (x + 2, 104 - height)], fill=C["mush1" if index % 2 else "mush2"])
        draw.point((x + 4, 100 - height), fill=C["mush3"])
        draw.point((x + 10, 101 - height), fill=C["crystal4"])
    fill_below(draw, 120, 5, 56, "earth0", 9)
    return finish_seam(result)


def draw_crystal_background() -> Image.Image:
    result = image(512, 128, C["crystal0"])
    draw = ImageDraw.Draw(result)
    for x in range(0, 512, 40):
        depth = 12 + (x // 40 % 4) * 5
        draw.polygon([(x, 0), (x + 39, 0), (x + 30, depth), (x + 23, depth + 13), (x + 16, depth)], fill=C["dark"])
    fill_below(draw, 103, 13, 120, "earth0", 14)
    for index, x in enumerate(range(18, 512, 62)):
        height = 18 + (index % 4) * 6
        draw.polygon([(x, 116), (x + 5, 116 - height), (x + 10, 116)], fill=C["outline"])
        draw.polygon([(x + 2, 114), (x + 5, 119 - height), (x + 8, 114)], fill=C["crystal3"])
        draw.line((x + 5, 120 - height, x + 5, 110), fill=C["crystal4"], width=1)
        draw.polygon([(x + 9, 117), (x + 14, 103 - height // 3), (x + 19, 117)], fill=C["outline"])
        draw.polygon([(x + 11, 115), (x + 14, 106 - height // 3), (x + 17, 115)], fill=C["crystal2"])
    fill_below(draw, 123, 4, 48, "dark", 5)
    return finish_seam(result)


BACKGROUND_BUILDERS = {
    "meadow": draw_meadow_background,
    "forest": draw_forest_background,
    "cave": draw_cave_background,
    "mushroom": draw_mushroom_background,
    "crystal": draw_crystal_background,
}


def outlined_pixel(draw: ImageDraw.ImageDraw, x: int, y: int, color: str) -> None:
    draw.rectangle((x - 1, y - 1, x + 1, y + 1), fill=C["outline"])
    draw.point((x, y), fill=C[color])


def draw_small_effect(kind: str) -> Image.Image:
    result = image(96, 16)
    draw = ImageDraw.Draw(result)
    for frame in range(6):
        x = frame * 16
        if kind == "meadow":
            for index, (px, py) in enumerate(((3, 5), (8, 9), (12, 4))):
                outlined_pixel(draw, x + px + frame % 2, py + (frame + index) % 3, "gold3" if index == 1 else "leaf4")
        elif kind == "forest":
            points = [(3 + frame, 4), (8, 7 + frame % 3), (12 - frame // 2, 11)]
            for index, (px, py) in enumerate(points):
                draw.polygon([(x + px - 2, py), (x + px, py - 1), (x + px + 2, py), (x + px, py + 2)], fill=C["outline"])
                draw.point((x + px, py), fill=C["leaf3" if index % 2 else "leaf4"])
        elif kind == "cave":
            for index, (px, py) in enumerate(((4, 4), (10, 8), (6, 12))):
                outlined_pixel(draw, x + px + (frame + index) % 2, py - frame % 2, "soft")
        elif kind == "mushroom":
            for index, (px, py) in enumerate(((3, 10), (8, 5), (12, 11))):
                outlined_pixel(draw, x + px, py - (frame + index) % 4, "mush2" if index != 1 else "mush3")
        elif kind == "crystal":
            cx, cy = x + 8, 8
            radius = 2 + frame % 3
            draw.line((cx - radius, cy, cx + radius, cy), fill=C["outline"], width=3)
            draw.line((cx, cy - radius, cx, cy + radius), fill=C["outline"], width=3)
            draw.line((cx - 2, cy - 2, cx + 2, cy + 2), fill=C["crystal2"], width=1)
            draw.line((cx + 2, cy - 2, cx - 2, cy + 2), fill=C["crystal2"], width=1)
            draw.point((cx, cy), fill=C["crystal4"])
        elif kind == "rain":
            for index in range(3):
                px = x + 3 + index * 5 + frame % 2
                py = 2 + (frame * 2 + index * 4) % 8
                draw.line((px, py, px - 2, py + 5), fill=C["outline"], width=2)
                draw.line((px, py + 1, px - 1, py + 4), fill=C["blue3"], width=1)
        elif kind == "snow":
            for index, (px, py) in enumerate(((3, 4), (8, 10), (13, 6))):
                outlined_pixel(draw, x + px + (frame + index) % 2, py + frame % 2, "pale")
        elif kind == "wind":
            y = 4 + frame % 3
            draw.line((x + 2, y, x + 12, y), fill=C["outline"], width=2)
            draw.line((x + 4, y + 5, x + 14, y + 5), fill=C["outline"], width=2)
            draw.line((x + 3, y, x + 11, y), fill=C["teal2"], width=1)
            draw.line((x + 5, y + 5, x + 13, y + 5), fill=C["teal1"], width=1)
        else:
            cx, cy = x + 8, 8
            radius = 2 + frame % 3
            draw.line((cx - radius, cy, cx + radius, cy), fill=C["outline"], width=2)
            draw.line((cx, cy - radius, cx, cy + radius), fill=C["outline"], width=2)
            draw.point((cx, cy), fill=C["gold3"])
            draw.point((cx + 1, cy), fill=C["red3"])
    return result


def draw_combat_effect(kind: str) -> Image.Image:
    result = image(192, 32)
    draw = ImageDraw.Draw(result)
    for frame in range(6):
        x = frame * 32
        progress = frame + 1
        if kind == "sword":
            draw.arc((x + 3, 3, x + 28, 28), start=195, end=195 + progress * 22, fill=C["outline"], width=6)
            draw.arc((x + 5, 5, x + 26, 26), start=195, end=195 + progress * 22, fill=C["pale"], width=3)
            draw.arc((x + 8, 8, x + 23, 23), start=195, end=195 + progress * 22, fill=C["gold1"], width=2)
            draw.point((x + 7 + progress * 2, 23 - progress), fill=C["gold3"])
        elif kind == "block":
            cx, cy = x + 16, 16
            radius = 3 + progress * 2
            draw.line((cx - radius, cy, cx + radius, cy), fill=C["outline"], width=3)
            draw.line((cx, cy - radius, cx, cy + radius), fill=C["outline"], width=3)
            draw.line((cx - radius // 2, cy - radius // 2, cx + radius // 2, cy + radius // 2), fill=C["copper3"], width=2)
            draw.point((cx, cy), fill=C["pale"])
        elif kind == "hurt":
            cx, cy = x + 16, 16
            radius = 2 + progress * 2
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1)):
                draw.line((cx + dx * 3, cy + dy * 3, cx + dx * radius, cy + dy * radius), fill=C["outline"], width=3)
                draw.line((cx + dx * 4, cy + dy * 4, cx + dx * max(4, radius - 1), cy + dy * max(4, radius - 1)), fill=C["red3"], width=1)
            draw.rectangle((cx - 2, cy - 2, cx + 2, cy + 2), fill=C["red2"])
        else:
            cx, cy = x + 16, 21
            radius = 3 + progress
            for dx, dy in ((-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0)):
                draw.line((cx, cy, cx + dx * radius, cy + dy * radius), fill=C["outline"], width=3)
                draw.rectangle((cx + dx * radius - 1, cy + dy * radius - 1, cx + dx * radius + 1, cy + dy * radius + 1), fill=C["outline"])
                draw.point((cx + dx * radius, cy + dy * radius), fill=C["gold2"] if dx else C["soft"])
            draw.rectangle((cx - 5, cy, cx + 5, cy + 4), fill=C["outline"])
            draw.rectangle((cx - 4, cy + 1, cx + 4, cy + 3), fill=C["earth2"])
    return result


def draw_nine_slice(kind: str) -> Image.Image:
    result = image(48, 48)
    draw = ImageDraw.Draw(result)
    edge = "teal1" if kind == "panel" else "cloth_violet"
    light = "teal2" if kind == "panel" else "cloth_violet_hi"
    center = "dark" if kind == "panel" else "earth0"
    for row in range(3):
        for column in range(3):
            x, y = column * 16, row * 16
            draw.rectangle((x + 1, y + 1, x + 14, y + 14), fill=C["outline"])
            draw.rectangle((x + 3, y + 3, x + 12, y + 12), fill=C[center])
            if row == 0:
                draw.rectangle((x + 3, y + 2, x + 12, y + 4), fill=C[light])
            if row == 2:
                draw.rectangle((x + 3, y + 11, x + 12, y + 13), fill=C[edge])
            if column == 0:
                draw.rectangle((x + 2, y + 3, x + 4, y + 12), fill=C[edge])
            if column == 2:
                draw.rectangle((x + 11, y + 3, x + 13, y + 12), fill=C[edge])
    return result


def draw_icon(kind: str) -> Image.Image:
    result = image(32, 32)
    draw = ImageDraw.Draw(result)
    draw.rectangle((3, 3, 28, 28), fill=C["outline"])
    draw.rectangle((5, 5, 26, 26), fill=C["dark"])
    if kind == "run":
        draw.ellipse((11, 6, 17, 12), fill=C["pale"])
        draw.line((14, 12, 12, 20), fill=C["teal1"], width=4)
        draw.line((13, 15, 20, 12), fill=C["outline"], width=3)
        draw.line((12, 20, 7, 25), fill=C["outline"], width=4)
        draw.line((13, 20, 21, 24), fill=C["outline"], width=4)
    elif kind == "block":
        draw.polygon([(9, 8), (22, 8), (24, 13), (22, 23), (16, 27), (9, 23), (7, 13)], fill=C["outline"])
        draw.polygon([(10, 10), (21, 10), (22, 14), (20, 22), (16, 24), (10, 22), (9, 14)], fill=C["copper1"])
        draw.line((12, 12, 20, 20), fill=C["copper3"], width=2)
    elif kind == "hurt":
        draw.polygon([(16, 6), (19, 12), (26, 10), (22, 16), (27, 21), (19, 20), (16, 27), (13, 20), (5, 21), (10, 16), (6, 10), (13, 12)], fill=C["outline"])
        draw.polygon([(16, 9), (18, 14), (23, 13), (19, 16), (23, 19), (18, 18), (16, 23), (14, 18), (9, 19), (13, 16), (9, 13), (14, 14)], fill=C["red2"])
    elif kind == "tool":
        draw.line((9, 24, 20, 9), fill=C["outline"], width=5)
        draw.line((10, 23, 20, 10), fill=C["wood3"], width=2)
        draw.polygon([(15, 8), (25, 7), (27, 11), (22, 14), (18, 12)], fill=C["outline"])
        draw.line((18, 10, 24, 9), fill=C["soft"], width=2)
    elif kind == "weather":
        draw.rectangle((8, 10, 23, 15), fill=C["outline"])
        draw.rectangle((11, 7, 19, 16), fill=C["outline"])
        draw.rectangle((10, 11, 21, 14), fill=C["soft"])
        for offset in (0, 6, 12):
            draw.line((10 + offset, 18, 8 + offset, 24), fill=C["blue3"], width=2)
    else:
        draw.polygon([(9, 7), (15, 5), (22, 8), (24, 15), (21, 25), (11, 25), (7, 15)], fill=C["outline"])
        draw.polygon([(11, 9), (15, 7), (20, 10), (21, 15), (19, 22), (12, 22), (10, 15)], fill=C["copper1"])
        draw.rectangle((13, 10, 18, 12), fill=C["copper3"])
    return result


def frame_entries(width: int, height: int, ids: list[str], origin: tuple[int, int]) -> list[dict]:
    return [
        {"id": frame_id, "x": index * width, "y": 0, "width": width, "height": height, "originX": origin[0], "originY": origin[1]}
        for index, frame_id in enumerate(ids)
    ]


def nine_slice_frames() -> list[dict]:
    names = ["top_left", "top", "top_right", "left", "center", "right", "bottom_left", "bottom", "bottom_right"]
    return [
        {"id": name, "x": (index % 3) * 16, "y": (index // 3) * 16, "width": 16, "height": 16, "originX": 8, "originY": 8}
        for index, name in enumerate(names)
    ]


def manifest_entry(
    sprite_id: str,
    path: str,
    category: str,
    width: int,
    height: int,
    atlas: str,
    origin: tuple[int, int],
    layer: str,
    tags: list[str],
    frames: list[dict] | None = None,
) -> dict:
    entry = {
        "id": sprite_id,
        "path": path,
        "category": category,
        "width": width,
        "height": height,
        "atlasId": atlas,
        "originX": origin[0],
        "originY": origin[1],
        "renderLayer": layer,
        "license": "YjsE-Project-Owned",
        "provenance": f"{WAVE_ID}; generated source plus deterministic Pillow conversion; wave_04_provenance.json",
    }
    if frames:
        entry["frames"] = frames
    entry["tags"] = tags + ["production-sample", "runtime-preloaded", "wave-04"]
    return entry


def build_manifest_entries() -> list[dict]:
    entries: list[dict] = []
    character_frames = frame_entries(16, 32, FRAME_IDS, (8, 16))
    layer_order = ["body", "hair", "clothes", "armor", "equipment"]
    for index, layer_name in enumerate(layer_order):
        entries.append(manifest_entry(
            f"entities/player/character_v1_wave04/{layer_name}",
            CHARACTER_LAYERS[layer_name], "Entity", 256, 32, "entities", (8, 16),
            f"world.entity.player.{index:02d}.{layer_name}",
            ["entity", "player", "character-v1", "layered", layer_name, "animated"],
            character_frames,
        ))
    for biome, spec in BACKGROUND_SPECS.items():
        entries.append(manifest_entry(
            f"world/backgrounds/wave04/{biome}_parallax_layer", spec["path"], "Background",
            512, 128, "backgrounds", (0, 127), "background.mid",
            ["world", "background", biome, "parallax", "tileable", "tile-palette-matched"],
        ))
    for effect_id, (path, cell, kind, role) in EFFECT_SPECS.items():
        entries.append(manifest_entry(
            f"effects/{effect_id}", path, "Effect", cell * 6, cell, "effects", (cell // 2, cell // 2),
            f"world.effect.{role}", ["effect", role, kind, "animated"],
            frame_entries(cell, cell, [f"frame_{index}" for index in range(6)], (cell // 2, cell // 2)),
        ))
    for kind, path in NINE_SLICE_SPECS.items():
        entries.append(manifest_entry(
            f"ui/wave04/{kind}_9slice", path, "UI", 48, 48, "ui", (8, 8), "ui.frame",
            ["ui", "nine-slice", kind], nine_slice_frames(),
        ))
    for kind, path in ICON_SPECS.items():
        entries.append(manifest_entry(
            f"ui/wave04/{kind}", path, "UI", 32, 32, "ui", (16, 16), "ui.icon.action",
            ["ui", "icon", "action", kind],
        ))
    return entries


def brief_for(entry: dict) -> dict:
    sprite_id = entry["id"]
    requirements = [
        f"Export exactly {entry['width']}x{entry['height']} RGBA with binary alpha.",
        f"Runtime ID target: {sprite_id}; current activation is preload-only.",
        "Use only yjse-pixel-v1 colors and hard nearest-neighbor clusters.",
    ]
    if "frames" in entry:
        requirements.append(f"Preserve {len(entry['frames'])} declared frame rectangles and per-frame origins.")
    if "parallax" in entry["tags"]:
        requirements.append("Left and right columns must be byte-identical; horizons must wrap without a visible step.")
    if "layered" in entry["tags"]:
        requirements.append("All five character layers share the same 16x32 registration, frame order, origin, and baseline.")
    if "nine-slice" in entry["tags"]:
        requirements.append("Nine 16x16 slices use stable row-major corner, edge, and center IDs.")
    subject = sprite_id.replace("/", " ").replace("_", " ")
    return {
        "spriteId": sprite_id,
        "outputPath": entry["path"],
        "width": entry["width"],
        "height": entry["height"],
        "subject": subject,
        "prompt": f"Production Wave 04 pixel-art asset for {subject}; readable at native scale and coherent with the generated adventurer source board.",
        "requirements": requirements,
        "palette": sorted({"#%02x%02x%02x" % color[:3] for name, color in C.items() if name != "clear"}),
        "tags": entry["tags"],
        "runtimeIdTarget": sprite_id,
        "sourcePromptId": "wave04_character_source_v1" if "character_v1_wave04" in sprite_id else "wave04_deterministic_extension_v1",
    }


def write_contract_artifacts(entries: list[dict]) -> None:
    brief_payload = {
        "version": 1,
        "scope": WAVE_ID,
        "globalStyle": "YjsE yjse-pixel-v1 crisp side-view pixel art; binary alpha; top-left light; hard clusters; no antialiasing or gradients.",
        "globalNegativePrompt": "blur, partial alpha, soft glow, noise dithering, watermark, logo, copied game art, text, frame labels",
        "globalRequirements": [
            "Export exact manifest dimensions and source rectangles.",
            "Use only colors declared in Game.Data/art_direction/yjse_pixel_style.json.",
            "Keep every standalone sprite outlined and readable on light and dark fields.",
            "Keep scene activation honest as runtime-preloaded until a runtime owner wires the IDs.",
        ],
        "briefs": [brief_for(entry) for entry in entries],
    }
    BRIEF_PATH.write_text(json.dumps(brief_payload, indent=2) + "\n", encoding="utf-8")
    MANIFEST_FRAGMENT_PATH.write_text(json.dumps(entries, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    if not SOURCE.exists():
        raise SystemExit(f"Missing generated source: {SOURCE}")
    if not PROMPTSET_PATH.exists():
        raise SystemExit(f"Missing prompt set: {PROMPTSET_PATH}")

    character_builders = {
        "body": draw_body_layer,
        "hair": draw_hair_layer,
        "clothes": draw_clothes_layer,
        "armor": draw_armor_layer,
        "equipment": draw_equipment_layer,
    }
    for layer, path in CHARACTER_LAYERS.items():
        save(path, character_builders[layer]())
    for biome, spec in BACKGROUND_SPECS.items():
        save(spec["path"], BACKGROUND_BUILDERS[biome]())
    for _, (path, cell, kind, _) in EFFECT_SPECS.items():
        save(path, draw_combat_effect(kind) if cell == 32 else draw_small_effect(kind))
    for kind, path in NINE_SLICE_SPECS.items():
        save(path, draw_nine_slice(kind))
    for kind, path in ICON_SPECS.items():
        save(path, draw_icon(kind))

    entries = build_manifest_entries()
    write_contract_artifacts(entries)

    script_path = Path(__file__).resolve()
    source_hash = sha256(SOURCE)
    records = []
    for entry in entries:
        path = ROOT / entry["path"]
        with Image.open(path) as opened:
            dimensions = list(opened.size)
            alpha_values = sorted(set(opened.convert("RGBA").getchannel("A").get_flattened_data()))
        record = {
            "spriteId": entry["id"],
            "path": entry["path"],
            "sha256": sha256(path),
            "dimensions": dimensions,
            "frameCount": len(entry.get("frames", [])),
            "generator": GENERATOR,
            "method": "deterministic Pillow pixel primitives with nearest-neighbor generated-source palette quantization",
            "sourcePromptId": "wave04_character_source_v1" if "character_v1_wave04" in entry["id"] else "wave04_deterministic_extension_v1",
            "generatedSourceSha256": source_hash,
            "alphaValues": alpha_values,
            "license": "YjsE-Project-Owned",
            "runtimeIdTarget": entry["id"],
            "runtimeConsumer": "ClientTextureRegistry.PreloadAll; scene selection remains content/runtime-owner follow-up",
        }
        if "/backgrounds/" in entry["id"]:
            biome = entry["id"].split("/")[-1].removesuffix("_parallax_layer")
            record["tilePaletteReferences"] = BACKGROUND_SPECS[biome]["tiles"]
            with Image.open(path) as opened:
                rgba = opened.convert("RGBA")
                record["horizontalSeamColumnsEqual"] = list(rgba.crop((0, 0, 1, rgba.height)).get_flattened_data()) == list(rgba.crop((rgba.width - 1, 0, rgba.width, rgba.height)).get_flattened_data())
        records.append(record)

    payload = {
        "schemaVersion": 1,
        "waveId": WAVE_ID,
        "generatedOn": WAVE_DATE,
        "generatedSource": {
            "path": "art_direction/generated_sources/wave_04_character_source.png",
            "sha256": source_hash,
            "dimensions": [1983, 793],
            "toolPath": "built-in image_gen (imagegen skill default mode)",
            "promptId": "wave04_character_source_v1",
            "chromaKey": "#FF00FF",
            "postProcessing": "Nearest-neighbor sample, chroma exclusion, quantization to yjse-pixel-v1, deterministic pixel-native reconstruction at final frame grids.",
            "quantizedTopPaletteNames": SOURCE_QUANTIZED_TOP,
            "selectedCharacterPaletteNames": SOURCE_COLORS,
        },
        "generatorSha256": sha256(script_path),
        "promptSet": {
            "path": "art_direction/wave_04_promptset.json",
            "sha256": sha256(PROMPTSET_PATH),
        },
        "assets": records,
    }
    PROVENANCE_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
