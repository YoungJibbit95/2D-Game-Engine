#!/usr/bin/env python3
"""Generate the bounded Wave 03 player, biome, and regeneration asset set."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw

from generate_wave_02_assets import C, draw_boar, draw_spider, image


ROOT = Path(__file__).resolve().parents[2]
PROVENANCE_PATH = ROOT / "art_direction" / "wave_03_provenance.json"
WAVE_ID = "wave_03_player_biome_production"
WAVE_DATE = "2026-07-12"


def save(relative_path: str, source: Image.Image) -> None:
    path = ROOT / relative_path
    path.parent.mkdir(parents=True, exist_ok=True)
    alpha = source.getchannel("A").point(lambda value: 255 if value else 0)
    source.putalpha(alpha)
    source.save(path, format="PNG", optimize=False, compress_level=9)


def finish_seam(source: Image.Image) -> Image.Image:
    source.paste(source.crop((0, 0, 1, source.height)), (source.width - 1, 0))
    return source


def draw_priority_hoe(blade: str) -> Image.Image:
    source = image(16, 16)
    draw = ImageDraw.Draw(source)
    draw.line((3, 13, 10, 6), fill=C["outline"], width=4)
    draw.line((3, 13, 10, 6), fill=C["wood2"], width=2)
    draw.point((5, 10), fill=C["wood3"])
    draw.polygon([(8, 4), (13, 4), (14, 6), (11, 7), (9, 7)], fill=C["outline"])
    draw.polygon([(9, 5), (13, 5), (11, 6), (9, 6)], fill=C[blade])
    draw.point((12, 5), fill=C["pale"] if blade == "mid" else C["copper3"])
    return source


def draw_priority_arrow() -> Image.Image:
    source = image(16, 16)
    draw = ImageDraw.Draw(source)
    draw.line((2, 12, 12, 2), fill=C["outline"], width=3)
    draw.line((3, 11, 12, 2), fill=C["wood3"], width=1)
    draw.polygon([(10, 2), (14, 1), (13, 5), (12, 4)], fill=C["outline"])
    draw.polygon([(12, 3), (13, 2), (13, 4)], fill=C["pale"])
    draw.polygon([(1, 10), (1, 15), (4, 12)], fill=C["outline"])
    draw.point((2, 12), fill=C["red2"])
    return source


def draw_priority_spark() -> Image.Image:
    source = image(16, 16)
    draw = ImageDraw.Draw(source)
    draw.polygon([(8, 1), (10, 6), (15, 8), (10, 10), (8, 15), (6, 10), (1, 8), (6, 6)], fill=C["outline"])
    draw.polygon([(8, 3), (9, 7), (13, 8), (9, 9), (8, 13), (7, 9), (3, 8), (7, 7)], fill=C["gold1"])
    draw.rectangle((7, 6, 9, 9), fill=C["gold3"])
    draw.point((8, 7), fill=C["pale"])
    return source


POSES = [
    (0, 0, 0), (0, -1, 0), (-1, 0, 1), (0, 0, -1),
    (1, 0, 1), (0, 0, -1), (0, -1, 1), (0, 1, -1),
    (-1, 0, 0), (1, 0, 0), (-1, 0, 1), (1, 0, -1),
]


def draw_human_base_frame(draw: ImageDraw.ImageDraw, x: int, pose: tuple[int, int, int]) -> None:
    bob, arm, stride = pose
    top = 3 + bob
    draw.rectangle((x + 5, top, x + 11, top + 8), fill=C["outline"])
    draw.rectangle((x + 6, top + 1, x + 10, top + 7), fill=C["pale"])
    draw.point((x + 10, top + 4), fill=C["outline"])
    draw.rectangle((x + 7, top + 9, x + 9, top + 10), fill=C["outline"])
    draw.polygon([(x + 4, top + 10), (x + 11, top + 10), (x + 12, top + 19), (x + 3, top + 19)], fill=C["outline"])
    draw.rectangle((x + 5, top + 11, x + 10, top + 18), fill=C["cloth_blue"])
    left_arm_y = top + 12 + arm
    right_arm_y = top + 12 - arm
    draw.rectangle((x + 2, left_arm_y, x + 4, left_arm_y + 7), fill=C["outline"])
    draw.rectangle((x + 3, left_arm_y + 1, x + 3, left_arm_y + 5), fill=C["pale"])
    draw.rectangle((x + 11, right_arm_y, x + 13, right_arm_y + 7), fill=C["outline"])
    draw.rectangle((x + 12, right_arm_y + 1, x + 12, right_arm_y + 5), fill=C["pale"])
    hip = top + 19
    draw.rectangle((x + 4, hip, x + 11, hip + 3), fill=C["outline"])
    draw.rectangle((x + 5, hip, x + 10, hip + 2), fill=C["cloth_grey"])
    draw.rectangle((x + 4 + stride, hip + 3, x + 7 + stride, 30), fill=C["outline"])
    draw.rectangle((x + 8 - stride, hip + 3, x + 11 - stride, 30), fill=C["outline"])
    draw.rectangle((x + 5 + stride, hip + 3, x + 6 + stride, 28), fill=C["cloth_grey_hi"])
    draw.rectangle((x + 9 - stride, hip + 3, x + 10 - stride, 28), fill=C["cloth_grey_hi"])


def draw_player_base() -> Image.Image:
    source = image(192, 32)
    draw = ImageDraw.Draw(source)
    for index, pose in enumerate(POSES):
        draw_human_base_frame(draw, index * 16, pose)
    return source


def draw_body_variants() -> Image.Image:
    source = image(64, 32)
    draw = ImageDraw.Draw(source)
    tones = [("pale", "soft"), ("wood3", "wood2"), ("copper3", "copper2"), ("wood2", "wood1")]
    for index, (light, shade) in enumerate(tones):
        x = index * 16
        draw.rectangle((x + 5, 3, x + 11, 11), fill=C["outline"])
        draw.rectangle((x + 6, 4, x + 10, 10), fill=C[light])
        draw.rectangle((x + 9, 8, x + 10, 10), fill=C[shade])
        draw.point((x + 10, 7), fill=C["outline"])
        draw.rectangle((x + 7, 12, x + 9, 14), fill=C[light])
        draw.rectangle((x + 2, 15, x + 4, 22), fill=C["outline"])
        draw.rectangle((x + 3, 16, x + 3, 21), fill=C[light])
        draw.rectangle((x + 11, 15, x + 13, 22), fill=C["outline"])
        draw.rectangle((x + 12, 16, x + 12, 21), fill=C[shade])
    return source


def draw_hair_variants() -> Image.Image:
    source = image(128, 32)
    draw = ImageDraw.Draw(source)
    styles = ["short", "messy", "bob", "pony", "braids", "mohawk", "long", "cap"]
    tones = [("wood2", "wood3"), ("dark", "mid"), ("gold1", "gold2"), ("copper1", "copper2")]
    for index, style in enumerate(styles):
        x = index * 16
        dark, light = tones[index % len(tones)]
        draw.rectangle((x + 4, 2, x + 11, 8), fill=C["outline"])
        draw.rectangle((x + 5, 3, x + 10, 6), fill=C[dark])
        draw.rectangle((x + 5, 3, x + 8, 3), fill=C[light])
        if style in {"messy", "mohawk"}:
            draw.polygon([(x + 5, 2), (x + 6, 0), (x + 7, 2), (x + 9, 0), (x + 10, 2)], fill=C["outline"])
            draw.point((x + 6, 1), fill=C[light])
        if style in {"bob", "long", "braids"}:
            draw.rectangle((x + 4, 7, x + 5, 14 if style == "bob" else 20), fill=C["outline"])
            draw.rectangle((x + 10, 7, x + 11, 14 if style == "bob" else 20), fill=C["outline"])
        if style == "pony":
            draw.rectangle((x + 11, 7, x + 13, 16), fill=C["outline"])
            draw.rectangle((x + 12, 8, x + 12, 14), fill=C[light])
        if style == "braids":
            draw.point((x + 4, 22), fill=C["outline"])
            draw.point((x + 11, 22), fill=C["outline"])
        if style == "long":
            draw.rectangle((x + 5, 9, x + 10, 18), fill=C[dark])
        if style == "cap":
            draw.rectangle((x + 3, 4, x + 12, 6), fill=C["outline"])
            draw.rectangle((x + 5, 2, x + 10, 5), fill=C["copper2"])
    return source


def draw_clothes_variants() -> Image.Image:
    source = image(128, 32)
    draw = ImageDraw.Draw(source)
    palettes = [
        ("cloth_blue", "cloth_blue_hi"), ("cloth_green", "cloth_green_hi"),
        ("red1", "red2"), ("cloth_violet", "cloth_violet_hi"),
        ("wood2", "wood3"), ("cloth_grey", "cloth_grey_hi"),
        ("teal0", "teal2"), ("gold0", "gold2"),
    ]
    for index, (base, light) in enumerate(palettes):
        x = index * 16
        draw.polygon([(x + 4, 13), (x + 11, 13), (x + 12, 22), (x + 3, 22)], fill=C["outline"])
        draw.rectangle((x + 5, 14, x + 10, 20), fill=C[base])
        draw.rectangle((x + 5, 14, x + 8, 15), fill=C[light])
        draw.rectangle((x + 2, 14, x + 4, 20), fill=C["outline"])
        draw.rectangle((x + 11, 14, x + 13, 20), fill=C["outline"])
        draw.rectangle((x + 4, 21, x + 11, 24), fill=C["outline"])
        draw.rectangle((x + 5, 22, x + 10, 23), fill=C["dark"])
        draw.rectangle((x + 4, 24, x + 7, 30), fill=C["outline"])
        draw.rectangle((x + 8, 24, x + 11, 30), fill=C["outline"])
        draw.rectangle((x + 5, 24, x + 6, 28), fill=C[base])
        draw.rectangle((x + 9, 24, x + 10, 28), fill=C[light])
    return source


def draw_accessories() -> Image.Image:
    source = image(96, 32)
    draw = ImageDraw.Draw(source)
    # Straw hat, miner helmet, bandana, glasses, leaf crown, hood.
    for index in range(6):
        x = index * 16
        if index == 0:
            draw.rectangle((x + 2, 3, x + 13, 6), fill=C["outline"]); draw.rectangle((x + 4, 1, x + 11, 4), fill=C["gold1"]); draw.rectangle((x + 3, 4, x + 12, 5), fill=C["gold2"])
        elif index == 1:
            draw.rectangle((x + 4, 1, x + 11, 7), fill=C["outline"]); draw.rectangle((x + 5, 2, x + 10, 6), fill=C["mid"]); draw.rectangle((x + 7, 1, x + 9, 3), fill=C["gold3"])
        elif index == 2:
            draw.rectangle((x + 4, 5, x + 12, 8), fill=C["outline"]); draw.rectangle((x + 5, 6, x + 11, 7), fill=C["red2"]); draw.polygon([(x + 11, 8), (x + 14, 11), (x + 11, 11)], fill=C["red1"])
        elif index == 3:
            draw.rectangle((x + 4, 6, x + 11, 9), fill=C["outline"]); draw.rectangle((x + 5, 7, x + 6, 8), fill=C["blue3"]); draw.rectangle((x + 9, 7, x + 10, 8), fill=C["blue3"])
        elif index == 4:
            draw.line((x + 3, 5, x + 12, 5), fill=C["outline"], width=2); draw.polygon([(x + 4, 4), (x + 6, 1), (x + 7, 5)], fill=C["leaf3"]); draw.polygon([(x + 8, 5), (x + 10, 1), (x + 12, 5)], fill=C["leaf4"])
        else:
            draw.polygon([(x + 4, 2), (x + 11, 2), (x + 13, 9), (x + 11, 15), (x + 4, 15), (x + 2, 9)], fill=C["outline"]); draw.polygon([(x + 5, 3), (x + 10, 3), (x + 11, 8), (x + 10, 12), (x + 5, 12), (x + 4, 8)], fill=C["cloth_violet"])
    return source


def stepped_height(x: int, base: int, amplitude: int, period: int) -> int:
    phase = x % period
    half = period // 2
    distance = phase if phase <= half else period - phase
    return base - (distance * amplitude // max(1, half))


def fill_below(draw: ImageDraw.ImageDraw, width: int, base: int, amplitude: int, period: int, color: str) -> None:
    for x in range(width):
        y = stepped_height(x, base, amplitude, period)
        draw.line((x, y, x, 127), fill=C[color])


def draw_meadow_background() -> Image.Image:
    source = image(512, 128, C["blue3"])
    draw = ImageDraw.Draw(source)
    fill_below(draw, 512, 84, 14, 128, "leaf3")
    fill_below(draw, 512, 101, 9, 80, "leaf2")
    fill_below(draw, 512, 116, 5, 48, "leaf1")
    for x in range(24, 512, 64):
        draw.rectangle((x, 102, x + 2, 116), fill=C["wood1"])
        draw.polygon([(x - 5, 106), (x + 1, 96), (x + 7, 106)], fill=C["leaf3"])
    return finish_seam(source)


def draw_forest_background() -> Image.Image:
    source = image(512, 128, C["teal2"])
    draw = ImageDraw.Draw(source)
    fill_below(draw, 512, 80, 12, 128, "leaf2")
    fill_below(draw, 512, 103, 8, 72, "leaf1")
    for x in range(16, 512, 48):
        draw.rectangle((x + 5, 61, x + 9, 118), fill=C["wood1"])
        draw.rectangle((x + 6, 62, x + 7, 116), fill=C["wood2"])
        draw.polygon([(x - 8, 72), (x + 7, 48), (x + 22, 72)], fill=C["leaf0"])
        draw.polygon([(x - 5, 64), (x + 7, 43), (x + 19, 64)], fill=C["leaf2"])
    fill_below(draw, 512, 120, 4, 40, "leaf0")
    return finish_seam(source)


def draw_cave_background() -> Image.Image:
    source = image(512, 128, C["earth0"])
    draw = ImageDraw.Draw(source)
    for x in range(0, 512, 32):
        depth = 12 + (x // 32 % 4) * 5
        draw.polygon([(x, 0), (x + 31, 0), (x + 17, depth), (x + 12, depth + 10)], fill=C["dark"])
    fill_below(draw, 512, 99, 11, 96, "earth1")
    fill_below(draw, 512, 116, 6, 48, "earth2")
    for x in range(20, 512, 72):
        draw.rectangle((x, 84, x + 18, 88), fill=C["mid"])
        draw.rectangle((x + 3, 89, x + 15, 91), fill=C["earth3"])
    return finish_seam(source)


def draw_flying_ambient(kind: str) -> Image.Image:
    source = image(64, 16)
    draw = ImageDraw.Draw(source)
    colors = {
        "meadow": ("mush2", "gold3"),
        "forest": ("leaf3", "gold2"),
        "cave": ("crystal3", "crystal4"),
    }
    wing, core = colors[kind]
    for frame in range(4):
        x = frame * 16
        lift = (0, -1, 0, 1)[frame]
        draw.rectangle((x + 6, 6, x + 9, 10), fill=C["outline"])
        draw.rectangle((x + 7, 7, x + 8, 9), fill=C[core])
        if kind == "meadow" and frame % 2 == 0:
            draw.polygon([(x + 6, 7), (x + 2, 3 + lift), (x + 3, 8)], fill=C["outline"])
            draw.polygon([(x + 9, 7), (x + 13, 3 + lift), (x + 12, 8)], fill=C["outline"])
        elif kind == "meadow":
            draw.polygon([(x + 6, 8), (x + 2, 11), (x + 5, 11)], fill=C["outline"])
            draw.polygon([(x + 9, 8), (x + 13, 11), (x + 10, 11)], fill=C["outline"])
        elif kind == "forest":
            wing_y = 3 + lift if frame % 2 == 0 else 10
            draw.polygon([(x + 6, 7), (x + 2, wing_y), (x + 4, 9)], fill=C["outline"])
            draw.polygon([(x + 9, 7), (x + 13, wing_y), (x + 11, 9)], fill=C["outline"])
        else:
            wing_y = 5 + (frame % 2) * 4
            draw.rectangle((x + 3, wing_y, x + 6, wing_y + 2), fill=C["outline"])
            draw.rectangle((x + 9, wing_y, x + 12, wing_y + 2), fill=C["outline"])
            draw.rectangle((x + 7, 10, x + 8, 12), fill=C[core])
        draw.point((x + 3, 5 + lift if frame % 2 == 0 else 10), fill=C[wing])
        draw.point((x + 12, 5 + lift if frame % 2 == 0 else 10), fill=C[wing])
    return source


def draw_particle_sheet(kind: str) -> Image.Image:
    source = image(64, 16)
    draw = ImageDraw.Draw(source)
    colors = {
        "meadow": ("gold1", "gold3"),
        "forest": ("leaf1", "leaf4"),
        "cave": ("earth2", "crystal4"),
    }
    base, light = colors[kind]
    for frame in range(4):
        x = frame * 16
        points = [(4 + frame % 2, 5), (9, 3 + frame), (12 - frame % 2, 9), (6, 12 - frame)]
        for px, py in points:
            draw.rectangle((x + px - 1, py - 1, x + px + 1, py + 1), fill=C["outline"])
            draw.point((x + px, py), fill=C[light if (px + py) % 2 else base])
    return source


def draw_meadow_elite() -> Image.Image:
    source = image(128, 32)
    draw = ImageDraw.Draw(source)
    for frame in range(4):
        x = frame * 32
        squash = (0, 2, -1, 1)[frame]
        draw.ellipse((x + 4, 8 + squash, x + 27, 28), fill=C["outline"])
        draw.ellipse((x + 6, 10 + squash, x + 25, 26), fill=C["leaf2"])
        draw.rectangle((x + 9, 21, x + 22, 26), fill=C["leaf3"])
        draw.rectangle((x + 10, 15 + squash, x + 12, 17 + squash), fill=C["gold3"])
        draw.rectangle((x + 19, 15 + squash, x + 21, 17 + squash), fill=C["gold3"])
        draw.polygon([(x + 9, 9 + squash), (x + 12, 3 + squash), (x + 15, 10 + squash)], fill=C["outline"])
        draw.polygon([(x + 18, 9 + squash), (x + 21, 3 + squash), (x + 24, 10 + squash)], fill=C["outline"])
    return source


def draw_forest_elite() -> Image.Image:
    source = draw_boar()
    draw = ImageDraw.Draw(source)
    for frame in range(4):
        x = frame * 32
        draw.polygon([(x + 9, 14), (x + 12, 8), (x + 15, 14)], fill=C["leaf3"])
        draw.polygon([(x + 16, 13), (x + 20, 7), (x + 22, 14)], fill=C["leaf4"])
        draw.rectangle((x + 13, 16, x + 20, 18), fill=C["leaf1"])
    return source


def draw_cave_elite() -> Image.Image:
    source = draw_spider()
    draw = ImageDraw.Draw(source)
    for frame in range(4):
        x = frame * 32
        draw.polygon([(x + 12, 13), (x + 15, 5), (x + 17, 13)], fill=C["crystal3"])
        draw.polygon([(x + 17, 13), (x + 20, 7), (x + 22, 14)], fill=C["crystal4"])
        draw.rectangle((x + 13, 14, x + 20, 16), fill=C["crystal1"])
    return source


def draw_biome_icon(kind: str) -> Image.Image:
    source = image(32, 32)
    draw = ImageDraw.Draw(source)
    draw.rectangle((4, 4, 27, 27), fill=C["outline"])
    draw.rectangle((6, 6, 25, 25), fill=C["dark"])
    if kind == "meadow":
        draw.rectangle((7, 17, 24, 24), fill=C["leaf2"])
        draw.polygon([(7, 18), (13, 10), (18, 18)], fill=C["leaf3"])
        draw.rectangle((19, 11, 20, 21), fill=C["gold2"])
        draw.rectangle((17, 9, 22, 13), fill=C["mush2"])
    elif kind == "forest":
        draw.rectangle((14, 14, 17, 24), fill=C["wood2"])
        draw.polygon([(7, 17), (16, 7), (25, 17)], fill=C["leaf1"])
        draw.polygon([(9, 14), (16, 5), (23, 14)], fill=C["leaf3"])
    else:
        draw.polygon([(7, 8), (25, 8), (21, 14), (18, 12), (15, 18), (11, 13)], fill=C["earth2"])
        draw.polygon([(11, 24), (15, 13), (18, 24)], fill=C["crystal3"])
        draw.polygon([(17, 24), (21, 17), (24, 24)], fill=C["crystal4"])
    return source


ASSETS = {
    "sprites/tools/copper_hoe.png": lambda: draw_priority_hoe("copper2"),
    "sprites/tools/iron_hoe.png": lambda: draw_priority_hoe("mid"),
    "sprites/projectiles/wooden_arrow.png": draw_priority_arrow,
    "sprites/projectiles/magic_spark_particles.png": draw_priority_spark,
    "sprites/entities/player/player_base_actions.png": draw_player_base,
    "sprites/entities/player/player_body_variants.png": draw_body_variants,
    "sprites/entities/player/player_hair_variants_v2.png": draw_hair_variants,
    "sprites/entities/player/player_clothes_variants_v2.png": draw_clothes_variants,
    "sprites/entities/player/player_accessories_hats.png": draw_accessories,
    "sprites/world/backgrounds/forest_parallax_layer.png": draw_forest_background,
    "sprites/world/backgrounds/cave_parallax_layer.png": draw_cave_background,
    "sprites/world/backgrounds/meadow_parallax_layer.png": draw_meadow_background,
    "sprites/entities/critters/meadow_butterfly.png": lambda: draw_flying_ambient("meadow"),
    "sprites/entities/critters/forest_moth.png": lambda: draw_flying_ambient("forest"),
    "sprites/entities/critters/cave_glowbug.png": lambda: draw_flying_ambient("cave"),
    "sprites/particles/meadow_pollen.png": lambda: draw_particle_sheet("meadow"),
    "sprites/particles/forest_leaf_drift.png": lambda: draw_particle_sheet("forest"),
    "sprites/particles/cave_dust.png": lambda: draw_particle_sheet("cave"),
    "sprites/entities/enemies/meadow_slime_elite.png": draw_meadow_elite,
    "sprites/entities/enemies/forest_boar_elite.png": draw_forest_elite,
    "sprites/entities/enemies/cave_spider_elite.png": draw_cave_elite,
    "sprites/ui/biomes/meadow.png": lambda: draw_biome_icon("meadow"),
    "sprites/ui/biomes/forest.png": lambda: draw_biome_icon("forest"),
    "sprites/ui/biomes/cave.png": lambda: draw_biome_icon("cave"),
}


def main() -> None:
    for relative_path, build in ASSETS.items():
        save(relative_path, build())

    records = []
    for relative_path in ASSETS:
        path = ROOT / relative_path
        with Image.open(path) as opened:
            dimensions = list(opened.size)
        records.append({
            "path": relative_path,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "dimensions": dimensions,
            "generator": "Game.Data/art_direction/tools/generate_wave_03_assets.py",
            "method": "deterministic hand-authored Pillow pixel primitives",
            "license": "YjsE-Project-Owned",
            "references": "No third-party art; YjsE yjse-pixel-v1 palette and checked-in tile silhouettes only",
            "runtimeConsumer": "ClientTextureRegistry.PreloadAll; scene selection is content-owner follow-up where tagged runtime-preloaded",
        })

    payload = {
        "schemaVersion": 1,
        "waveId": WAVE_ID,
        "generatedOn": WAVE_DATE,
        "source": "Deterministic checked-in generator; no image-generation output or third-party source was used.",
        "assets": records,
    }
    PROVENANCE_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
