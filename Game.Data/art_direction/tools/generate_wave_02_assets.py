#!/usr/bin/env python3
"""Generate the deterministic creature, biome, character, and prop wave."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
SPRITES = ROOT / "sprites"
PROVENANCE_PATH = ROOT / "art_direction" / "wave_02_provenance.json"
WAVE_ID = "wave_02_creatures_biomes_character_props"
WAVE_DATE = "2026-07-12"

C = {
    "clear": (0, 0, 0, 0),
    "outline": (27, 24, 38, 255),
    "dark": (42, 38, 53, 255),
    "mid": (74, 69, 85, 255),
    "soft": (113, 106, 123, 255),
    "pale": (244, 233, 216, 255),
    "wood0": (53, 35, 25, 255),
    "wood1": (90, 56, 37, 255),
    "wood2": (139, 90, 52, 255),
    "wood3": (197, 139, 82, 255),
    "copper0": (91, 43, 34, 255),
    "copper1": (143, 69, 49, 255),
    "copper2": (197, 109, 62, 255),
    "copper3": (240, 163, 91, 255),
    "red0": (91, 31, 50, 255),
    "red1": (165, 43, 70, 255),
    "red2": (229, 72, 77, 255),
    "red3": (255, 138, 103, 255),
    "blue0": (36, 59, 120, 255),
    "blue1": (53, 92, 181, 255),
    "blue2": (75, 145, 222, 255),
    "blue3": (145, 215, 255, 255),
    "gold0": (88, 65, 42, 255),
    "gold1": (185, 119, 50, 255),
    "gold2": (240, 195, 90, 255),
    "gold3": (255, 241, 166, 255),
    "teal0": (31, 92, 98, 255),
    "teal1": (61, 141, 132, 255),
    "teal2": (114, 195, 174, 255),
    "leaf0": (38, 63, 56, 255),
    "leaf1": (53, 97, 74, 255),
    "leaf2": (79, 138, 91, 255),
    "leaf3": (127, 186, 104, 255),
    "leaf4": (184, 212, 122, 255),
    "earth0": (48, 35, 61, 255),
    "earth1": (75, 49, 85, 255),
    "earth2": (105, 64, 96, 255),
    "earth3": (149, 96, 107, 255),
    "mush0": (122, 63, 101, 255),
    "mush1": (182, 90, 120, 255),
    "mush2": (229, 139, 145, 255),
    "mush3": (246, 198, 168, 255),
    "crystal0": (32, 36, 79, 255),
    "crystal1": (53, 60, 134, 255),
    "crystal2": (83, 103, 200, 255),
    "crystal3": (120, 183, 227, 255),
    "crystal4": (185, 239, 255, 255),
    "cloth_blue": (45, 71, 119, 255),
    "cloth_blue_hi": (68, 116, 166, 255),
    "cloth_green": (71, 107, 79, 255),
    "cloth_green_hi": (111, 155, 99, 255),
    "cloth_violet": (89, 67, 110, 255),
    "cloth_violet_hi": (139, 102, 161, 255),
    "cloth_grey": (98, 93, 104, 255),
    "cloth_grey_hi": (154, 146, 127, 255),
}


def image(width: int, height: int, fill=C["clear"]) -> Image.Image:
    return Image.new("RGBA", (width, height), fill)


def poly(draw: ImageDraw.ImageDraw, points, fill: str) -> None:
    draw.polygon(points, fill=C[fill])


def rect(draw: ImageDraw.ImageDraw, box, fill: str) -> None:
    draw.rectangle(box, fill=C[fill])


def save(relative_path: str, source: Image.Image) -> None:
    path = ROOT / relative_path
    path.parent.mkdir(parents=True, exist_ok=True)
    alpha = source.getchannel("A").point(lambda value: 255 if value else 0)
    source.putalpha(alpha)
    source.save(path, format="PNG", optimize=False, compress_level=9)


def draw_squirrel() -> Image.Image:
    result = image(64, 16)
    poses = [(0, 0), (0, -1), (-1, 0), (1, 0)]
    for frame, (shift, lift) in enumerate(poses):
        x = frame * 16 + shift
        d = ImageDraw.Draw(result)
        poly(d, [(x+3,13+lift),(x+1,10+lift),(x+2,6+lift),(x+5,4+lift),(x+6,1+lift),(x+9,2+lift),(x+10,6+lift),(x+8,9+lift),(x+7,13+lift)], "outline")
        poly(d, [(x+3,11+lift),(x+2,9+lift),(x+3,6+lift),(x+6,5+lift),(x+7,3+lift),(x+8,3+lift),(x+9,6+lift),(x+7,9+lift),(x+6,12+lift)], "wood2")
        rect(d, (x+4,6+lift,x+7,10+lift), "wood1")
        poly(d, [(x+6,7+lift),(x+9,6+lift),(x+12,7+lift),(x+13,10+lift),(x+11,12+lift),(x+6,12+lift)], "outline")
        rect(d, (x+7,8+lift,x+11,11+lift), "wood2")
        rect(d, (x+9,7+lift,x+12,9+lift), "wood3")
        rect(d, (x+11,8+lift,x+11,8+lift), "outline")
        rect(d, (x+12,10+lift,x+13,10+lift), "pale")
        if frame < 2:
            rect(d, (x+7,12+lift,x+8,13+lift), "outline")
            rect(d, (x+11,12+lift,x+12,13+lift), "outline")
        else:
            rect(d, (x+5,13+lift,x+8,13+lift), "outline")
            rect(d, (x+10,12+lift,x+14,13+lift), "outline")
    return result


def draw_firefly() -> Image.Image:
    result = image(64, 16)
    wing_shapes = [((4,3),(7,7)), ((3,5),(7,7)), ((4,8),(7,7)), ((3,5),(7,7))]
    for frame, (wing, center) in enumerate(wing_shapes):
        ox = frame * 16
        d = ImageDraw.Draw(result)
        wx, wy = wing
        poly(d, [(ox+7,7),(ox+wx,wy),(ox+2,wy+1),(ox+6,9)], "outline")
        poly(d, [(ox+7,8),(ox+wx,wy+1),(ox+4,wy+1),(ox+6,9)], "teal2")
        poly(d, [(ox+8,7),(ox+12,wy),(ox+14,wy+1),(ox+10,9)], "outline")
        poly(d, [(ox+9,8),(ox+12,wy+1),(ox+12,wy+1),(ox+10,9)], "teal2")
        rect(d, (ox+6,6,ox+10,11), "outline")
        rect(d, (ox+7,7,ox+9,8), "gold0")
        rect(d, (ox+7,9,ox+9,10), "gold2" if frame in (0,3) else "gold3")
        rect(d, (ox+7,5,ox+9,6), "dark")
        rect(d, (ox+6,4,ox+6,5), "outline")
        rect(d, (ox+10,4,ox+10,5), "outline")
    return result


def draw_boar() -> Image.Image:
    result = image(128, 32)
    for frame in range(4):
        ox = frame * 32
        d = ImageDraw.Draw(result)
        body_y = 14 + (1 if frame == 1 else 0)
        poly(d, [(ox+4,body_y+7),(ox+5,body_y-2),(ox+9,body_y-6),(ox+20,body_y-7),(ox+25,body_y-3),(ox+28,body_y+1),(ox+27,body_y+7)], "outline")
        poly(d, [(ox+6,body_y+5),(ox+7,body_y-1),(ox+10,body_y-4),(ox+20,body_y-5),(ox+24,body_y-2),(ox+26,body_y+2),(ox+25,body_y+5)], "wood1")
        poly(d, [(ox+9,body_y-4),(ox+13,body_y-7),(ox+17,body_y-5),(ox+21,body_y-7),(ox+23,body_y-3)], "dark")
        rect(d, (ox+9,body_y-2,ox+18,body_y+2), "wood2")
        poly(d, [(ox+23,body_y-2),(ox+29,body_y),(ox+30,body_y+4),(ox+26,body_y+6),(ox+23,body_y+3)], "outline")
        rect(d, (ox+24,body_y,ox+28,body_y+3), "wood2")
        rect(d, (ox+27,body_y,ox+27,body_y), "gold3")
        poly(d, [(ox+28,body_y+4),(ox+31,body_y+6),(ox+29,body_y+2)], "pale")
        if frame == 1:
            rect(d, (ox+7,body_y+5,ox+9,body_y+10), "outline")
            rect(d, (ox+20,body_y+5,ox+24,body_y+7), "outline")
        elif frame == 2:
            rect(d, (ox+4,body_y+5,ox+10,body_y+7), "outline")
            rect(d, (ox+20,body_y+5,ox+29,body_y+7), "outline")
        else:
            rect(d, (ox+7,body_y+5,ox+9,body_y+9), "outline")
            rect(d, (ox+21,body_y+5,ox+24,body_y+9), "outline")
        rect(d, (ox+6,body_y+9,ox+10,body_y+10), "outline")
        rect(d, (ox+21,body_y+9,ox+26,body_y+10), "outline")
    return result


def draw_spider() -> Image.Image:
    result = image(128, 32)
    leg_offsets = [0, 1, -1, 0]
    for frame, step in enumerate(leg_offsets):
        ox = frame * 32
        d = ImageDraw.Draw(result)
        legs = [
            [(ox+14,16),(ox+8+step,12),(ox+3,9)],
            [(ox+14,18),(ox+7-step,17),(ox+2,15)],
            [(ox+15,20),(ox+8+step,23),(ox+3,27)],
            [(ox+17,20),(ox+23-step,24),(ox+29,27)],
            [(ox+18,18),(ox+25+step,18),(ox+30,16)],
            [(ox+18,16),(ox+24-step,12),(ox+29,9)],
        ]
        for points in legs:
            d.line(points, fill=C["outline"], width=2)
            d.line(points[1:], fill=C["mid"], width=1)
        poly(d, [(ox+9,13),(ox+12,9),(ox+20,9),(ox+24,13),(ox+23,21),(ox+18,24),(ox+11,22),(ox+8,18)], "outline")
        poly(d, [(ox+11,14),(ox+13,11),(ox+19,11),(ox+22,14),(ox+21,19),(ox+18,22),(ox+12,20),(ox+10,17)], "dark")
        poly(d, [(ox+13,13),(ox+18,12),(ox+21,15),(ox+18,19),(ox+13,18)], "teal1")
        rect(d, (ox+15,13,ox+18,15), "teal2")
        rect(d, (ox+10,14,ox+11,15), "gold3")
        rect(d, (ox+21,14,ox+22,15), "gold3")
        if frame == 3:
            poly(d, [(ox+12,21),(ox+14,26),(ox+16,22)], "pale")
            poly(d, [(ox+18,22),(ox+20,26),(ox+21,21)], "pale")
    return result


def stepped_height(x: int, base: int, amplitude: int, period: int) -> int:
    phase = x % period
    half = period // 2
    triangle = phase if phase <= half else period - phase
    return base - (triangle * amplitude // max(1, half))


def finish_seam(source: Image.Image) -> Image.Image:
    source.paste(source.crop((0, 0, 1, source.height)), (source.width - 1, 0))
    return source


def draw_meadow_background() -> Image.Image:
    result = image(512, 128, C["blue3"])
    p = result.load()
    for x in range(511):
        far = stepped_height(x, 77, 16, 128)
        mid = stepped_height(x + 29, 94, 12, 80)
        for y in range(far, 128): p[x, y] = C["leaf0"]
        for y in range(mid, 128): p[x, y] = C["leaf1"]
        for y in range(109 + ((x // 9) % 3), 128): p[x, y] = C["leaf2"]
    d = ImageDraw.Draw(result)
    for x in range(28, 511, 64):
        rect(d, (x,91,x+2,109), "wood1")
        poly(d, [(x-8,96),(x+1,78),(x+10,96)], "leaf0")
        poly(d, [(x-6,91),(x+1,82),(x+8,91)], "leaf2")
    for x in range(14, 511, 37):
        rect(d, (x,111,x,116), "leaf4")
        rect(d, (x-1,110,x+1,111), "gold2")
    return finish_seam(result)


def draw_mushroom_background() -> Image.Image:
    result = image(512, 128, C["earth0"])
    p = result.load()
    for x in range(511):
        ceiling = 12 + ((x // 11) % 5) * 2
        floor = stepped_height(x + 17, 107, 10, 96)
        for y in range(ceiling): p[x, y] = C["outline"]
        for y in range(floor, 128): p[x, y] = C["earth1"]
        if (x // 32) % 2 == 0:
            for y in range(32, 84):
                if abs(x % 64 - 32) < (y - 28) // 6: p[x, y] = C["earth1"]
    d = ImageDraw.Draw(result)
    for x, h, cap in [(24,26,"mush1"),(73,38,"mush2"),(132,22,"mush3"),(191,44,"mush1"),(260,30,"mush2"),(334,42,"mush3"),(411,25,"mush1"),(474,36,"mush2")]:
        rect(d, (x,106-h,x+3,107), "mush3")
        poly(d, [(x-7,107-h),(x+1,99-h),(x+11,107-h),(x+8,111-h),(x-5,111-h)], "outline")
        poly(d, [(x-5,106-h),(x+1,101-h),(x+9,106-h),(x+6,109-h),(x-3,109-h)], cap)
        rect(d, (x,104-h,x+1,104-h), "gold3")
    return finish_seam(result)


def draw_crystal_background() -> Image.Image:
    result = image(512, 128, C["crystal0"])
    p = result.load()
    for x in range(511):
        floor = stepped_height(x + 9, 111, 8, 72)
        for y in range(floor, 128): p[x, y] = C["earth0"]
        if (x // 24) % 3 == 0:
            for y in range(25, 96):
                if abs((x % 72) - 36) < 5 + (y // 22): p[x, y] = C["crystal1"]
    d = ImageDraw.Draw(result)
    clusters = [(18,108,22),(61,112,34),(126,109,18),(173,113,40),(246,110,26),(304,112,37),(378,109,21),(430,113,43),(490,110,24)]
    for x, base, h in clusters:
        poly(d, [(x,base),(x+4,base-h),(x+9,base),(x+6,base+3)], "outline")
        poly(d, [(x+2,base-1),(x+4,base-h+4),(x+7,base-1)], "crystal2")
        d.line([(x+4,base-h+5),(x+4,base-5)], fill=C["crystal4"], width=1)
        poly(d, [(x+7,base),(x+13,base-h//2),(x+16,base+1),(x+12,base+3)], "outline")
        poly(d, [(x+9,base),(x+13,base-h//2+3),(x+14,base)], "crystal3")
    return finish_seam(result)


def draw_hair() -> Image.Image:
    result = image(128, 32)
    styles = ["wood2","dark","gold2","copper2","wood1","outline","gold1","copper1"]
    for frame, color in enumerate(styles):
        ox = frame * 16
        d = ImageDraw.Draw(result)
        shapes = [
            [(ox+4,7),(ox+7,4),(ox+12,6),(ox+13,12),(ox+11,15),(ox+4,14),(ox+3,10)],
            [(ox+3,8),(ox+5,4),(ox+8,6),(ox+11,3),(ox+14,8),(ox+12,15),(ox+4,14)],
            [(ox+3,7),(ox+7,4),(ox+13,7),(ox+13,17),(ox+10,16),(ox+9,12),(ox+4,15)],
            [(ox+3,7),(ox+7,4),(ox+13,7),(ox+12,14),(ox+14,18),(ox+12,23),(ox+10,17),(ox+4,14)],
            [(ox+3,7),(ox+7,4),(ox+13,7),(ox+12,14),(ox+11,21),(ox+9,17),(ox+8,23),(ox+6,17),(ox+4,14)],
            [(ox+5,8),(ox+7,2),(ox+9,8),(ox+13,7),(ox+12,14),(ox+4,14)],
            [(ox+3,7),(ox+7,4),(ox+13,7),(ox+13,23),(ox+10,25),(ox+9,14),(ox+4,15)],
            [(ox+2,8),(ox+5,5),(ox+12,5),(ox+14,9),(ox+12,11),(ox+4,11)],
        ][frame]
        poly(d, shapes, "outline")
        inner = [(x + (1 if x < ox+8 else -1), y + 1) for x, y in shapes]
        poly(d, inner, color)
        rect(d, (ox+5,6,ox+8,7), "wood3" if color.startswith("wood") else ("gold3" if color.startswith("gold") else "copper3" if color.startswith("copper") else "mid"))
    return result


def draw_clothes() -> Image.Image:
    result = image(128, 32)
    palettes = [
        ("cloth_blue","cloth_blue_hi","dark"),("cloth_green","cloth_green_hi","wood1"),
        ("red1","red3","dark"),("cloth_violet","cloth_violet_hi","blue0"),
        ("wood2","wood3","dark"),("cloth_grey","cloth_grey_hi","dark"),
        ("teal0","teal2","blue0"),("gold0","gold2","wood1"),
    ]
    for frame, (shirt, highlight, pants) in enumerate(palettes):
        ox = frame * 16
        d = ImageDraw.Draw(result)
        poly(d, [(ox+4,14),(ox+6,12),(ox+10,12),(ox+13,15),(ox+12,22),(ox+10,22),(ox+10,29),(ox+7,29),(ox+6,22),(ox+3,22)], "outline")
        rect(d, (ox+5,14,ox+11,21), shirt)
        rect(d, (ox+5,14,ox+8,15), highlight)
        rect(d, (ox+3,16,ox+4,21), shirt)
        rect(d, (ox+12,16,ox+13,21), shirt)
        rect(d, (ox+6,22,ox+9,28), pants)
        rect(d, (ox+10,22,ox+11,28), pants)
        rect(d, (ox+5,28,ox+8,30), "outline")
        rect(d, (ox+10,28,ox+13,30), "outline")
        rect(d, (ox+7,21,ox+10,22), "gold2")
    return result


def draw_accessories() -> Image.Image:
    result = image(96, 32)
    for frame in range(6):
        ox = frame * 16
        d = ImageDraw.Draw(result)
        if frame == 0:
            poly(d, [(ox+2,9),(ox+5,5),(ox+11,5),(ox+14,9),(ox+12,11),(ox+4,11)], "outline")
            rect(d, (ox+4,8,ox+12,9), "gold2"); rect(d, (ox+6,6,ox+10,7), "wood3")
        elif frame == 1:
            poly(d, [(ox+3,10),(ox+5,5),(ox+11,5),(ox+13,10)], "outline")
            rect(d, (ox+5,7,ox+11,10), "mid"); rect(d, (ox+7,4,ox+9,6), "gold3")
        elif frame == 2:
            rect(d, (ox+3,10,ox+13,12), "outline"); rect(d, (ox+4,10,ox+11,11), "red2"); rect(d, (ox+12,12,ox+14,14), "red1")
        elif frame == 3:
            rect(d, (ox+3,10,ox+13,12), "outline"); rect(d, (ox+4,10,ox+7,11), "blue3"); rect(d, (ox+9,10,ox+12,11), "blue3")
        elif frame == 4:
            poly(d, [(ox+3,9),(ox+5,5),(ox+7,9),(ox+9,4),(ox+11,9),(ox+13,6),(ox+13,11),(ox+3,11)], "outline")
            rect(d, (ox+4,9,ox+12,10), "leaf3"); rect(d, (ox+8,6,ox+9,8), "leaf4")
        else:
            poly(d, [(ox+3,15),(ox+4,7),(ox+7,4),(ox+11,6),(ox+13,14),(ox+11,18),(ox+5,18)], "outline")
            poly(d, [(ox+5,14),(ox+6,8),(ox+8,6),(ox+11,8),(ox+11,15),(ox+10,16),(ox+6,16)], "cloth_violet")
    return result


def draw_mana_crystal() -> Image.Image:
    result = image(16, 16); d = ImageDraw.Draw(result)
    poly(d, [(8,1),(13,6),(11,13),(8,15),(4,12),(2,6)], "outline")
    poly(d, [(8,3),(11,6),(9,12),(7,13),(5,11),(4,7)], "blue1")
    poly(d, [(8,3),(9,11),(7,13),(6,7)], "blue2")
    rect(d, (7,4,8,7), "blue3"); rect(d, (3,12,5,14), "gold1"); rect(d, (10,12,12,14), "gold1")
    return result


def draw_mining_charm() -> Image.Image:
    result = image(16, 16); d = ImageDraw.Draw(result)
    d.line([(3,2),(8,5),(13,2)], fill=C["outline"], width=2)
    rect(d, (7,4,9,7), "gold2")
    poly(d, [(5,7),(8,5),(11,8),(10,13),(8,15),(5,12)], "outline")
    poly(d, [(7,8),(8,7),(9,8),(8,12),(7,11)], "copper2")
    rect(d, (4,9,11,10), "pale"); rect(d, (9,8,11,9), "mid")
    return result


def draw_chair() -> Image.Image:
    result = image(16, 32); d = ImageDraw.Draw(result)
    rect(d, (3,3,12,27), "outline"); rect(d, (5,5,10,17), "wood1")
    rect(d, (5,6,9,8), "wood3"); rect(d, (3,17,13,21), "outline"); rect(d, (5,18,11,19), "wood2")
    rect(d, (4,21,6,30), "outline"); rect(d, (10,21,12,30), "outline")
    return result


def draw_table() -> Image.Image:
    result = image(32, 16); d = ImageDraw.Draw(result)
    rect(d, (1,2,30,7), "outline"); rect(d, (3,3,28,5), "wood2"); rect(d, (4,3,13,3), "wood3")
    rect(d, (4,7,8,15), "outline"); rect(d, (24,7,28,15), "outline"); rect(d, (6,7,7,13), "wood1"); rect(d, (25,7,26,13), "wood1")
    return result


def draw_chest() -> Image.Image:
    result = image(32, 16); d = ImageDraw.Draw(result)
    poly(d, [(2,7),(4,2),(27,2),(30,7),(29,15),(2,15)], "outline")
    poly(d, [(4,7),(6,4),(25,4),(28,7)], "wood2"); rect(d, (4,8,27,13), "wood1")
    rect(d, (3,7,28,9), "copper1"); rect(d, (14,7,18,12), "outline"); rect(d, (15,8,17,10), "gold2")
    rect(d, (6,4,13,5), "wood3")
    return result


def draw_lantern() -> Image.Image:
    result = image(16, 16); d = ImageDraw.Draw(result)
    rect(d, (6,1,9,2), "outline"); rect(d, (4,3,11,14), "outline")
    rect(d, (6,5,9,11), "gold2"); rect(d, (7,6,8,9), "gold3")
    rect(d, (5,4,10,5), "copper1"); rect(d, (5,12,10,13), "copper1")
    return result


ASSETS = {
    "sprites/entities/critters/squirrel.png": draw_squirrel,
    "sprites/entities/critters/firefly.png": draw_firefly,
    # forest_boar.png is now owned by generate_legacy_boar_polish_v1.py.
    "sprites/entities/enemies/cave_spider.png": draw_spider,
    "sprites/world/backgrounds/meadow_parallax_layer.png": draw_meadow_background,
    "sprites/world/backgrounds/mushroom_cave_parallax_layer.png": draw_mushroom_background,
    "sprites/world/backgrounds/crystal_depths_parallax_layer.png": draw_crystal_background,
    "sprites/entities/player/player_hair_variants_v2.png": draw_hair,
    "sprites/entities/player/player_clothes_variants_v2.png": draw_clothes,
    "sprites/entities/player/player_accessories_hats.png": draw_accessories,
    "sprites/items/accessories/mana_crystal.png": draw_mana_crystal,
    "sprites/items/accessories/mining_charm.png": draw_mining_charm,
    "sprites/world/objects/chair.png": draw_chair,
    "sprites/world/objects/table.png": draw_table,
    "sprites/world/objects/chest.png": draw_chest,
    "sprites/world/objects/lantern.png": draw_lantern,
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
            "generator": "Game.Data/art_direction/tools/generate_wave_02_assets.py",
            "method": "deterministic hand-authored Pillow pixel primitives",
            "license": "YjsE-Project-Owned",
            "references": "No third-party art; YjsE yjse-pixel-v1 palette and contracts only",
        })

    payload = {
        "schemaVersion": 1,
        "waveId": WAVE_ID,
        "generatedOn": WAVE_DATE,
        "sourceAttempt": "Built-in image generation was attempted for creature concepts but aborted before integration; final assets do not derive from those outputs.",
        "assets": records,
    }
    PROVENANCE_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
