#!/usr/bin/env python3
"""Generate the deterministic YjsE Wave 05 living-world production slice."""

from __future__ import annotations

from dataclasses import dataclass, field
from hashlib import sha256
import json
from pathlib import Path
from typing import Callable

from PIL import Image, ImageDraw


DATA_ROOT = Path(__file__).resolve().parents[3]
WAVE_ROOT = DATA_ROOT / "Assets" / "Wave05"
MANIFEST_PATH = DATA_ROOT / "assets" / "wave05_living_world.sprites.json"
BRIEF_PATH = DATA_ROOT / "asset_briefs" / "production_wave_05_living_world_briefs.json"
PROVENANCE_PATH = DATA_ROOT / "art_direction" / "wave_05_living_world_provenance.json"
GENERATOR_RELATIVE = "Assets/Wave05/tools/generate_wave05_assets.py"
WAVE_ID = "wave_05_living_world_production"

C = {
    "clear": (0, 0, 0, 0),
    "outline": (27, 24, 38, 255),
    "neutral0": (42, 38, 53, 255),
    "neutral1": (74, 69, 85, 255),
    "neutral2": (113, 106, 123, 255),
    "neutral3": (170, 162, 176, 255),
    "pale": (244, 233, 216, 255),
    "wood0": (53, 35, 25, 255),
    "wood1": (90, 56, 37, 255),
    "wood2": (139, 90, 52, 255),
    "wood3": (197, 139, 82, 255),
    "copper0": (91, 43, 34, 255),
    "copper1": (143, 69, 49, 255),
    "copper2": (197, 109, 62, 255),
    "copper3": (240, 163, 91, 255),
    "health0": (91, 31, 50, 255),
    "health1": (165, 43, 70, 255),
    "health2": (229, 72, 77, 255),
    "health3": (255, 138, 103, 255),
    "mana0": (36, 59, 120, 255),
    "mana1": (53, 92, 181, 255),
    "mana2": (75, 145, 222, 255),
    "mana3": (145, 215, 255, 255),
    "spark0": (88, 65, 42, 255),
    "spark1": (185, 119, 50, 255),
    "spark2": (240, 195, 90, 255),
    "spark3": (255, 241, 166, 255),
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
}


@dataclass(frozen=True)
class AssetSpec:
    sprite_id: str
    relative_path: str
    category: str
    size: tuple[int, int]
    atlas: str
    origin: tuple[int, int]
    render_layer: str
    subject: str
    tags: tuple[str, ...]
    draw: Callable[[], Image.Image]
    frames: tuple[dict, ...] = field(default_factory=tuple)


def canvas(width: int, height: int, fill=C["clear"]) -> Image.Image:
    return Image.new("RGBA", (width, height), fill)


def frame(frame_id: str, x: int, y: int, width: int, height: int,
          origin_x: int, origin_y: int, duration_ms: int = 0,
          auto_tile_mask: int | None = None) -> dict:
    result = {
        "id": frame_id,
        "x": x,
        "y": y,
        "width": width,
        "height": height,
        "originX": origin_x,
        "originY": origin_y,
    }
    if duration_ms:
        result["durationMs"] = duration_ms
    if auto_tile_mask is not None:
        result["autoTileMask"] = auto_tile_mask
    return result


def animation_frames(count: int, width: int, height: int,
                     origin_x: int, origin_y: int, duration_ms: int = 110) -> tuple[dict, ...]:
    return tuple(
        frame(f"frame_{index}", index * width, 0, width, height, origin_x, origin_y, duration_ms)
        for index in range(count)
    )


def autotile_frames() -> tuple[dict, ...]:
    return tuple(frame(f"mask_{mask}", mask * 16, 0, 16, 16, 8, 8, auto_tile_mask=mask) for mask in range(16))


def nine_slice_frames() -> tuple[dict, ...]:
    names = ("top_left", "top", "top_right", "left", "center", "right", "bottom_left", "bottom", "bottom_right")
    return tuple(frame(name, (index % 3) * 16, (index // 3) * 16, 16, 16, 8, 8) for index, name in enumerate(names))


def force_horizontal_seam(image: Image.Image) -> Image.Image:
    image.paste(image.crop((0, 0, 1, image.height)), (image.width - 1, 0))
    return image


def draw_parallax(kind: str) -> Image.Image:
    marsh = kind == "twilight_marsh"
    colors = (
        (C["crystal0"], C["earth0"], C["teal0"], C["leaf0"], C["leaf1"], C["mana2"])
        if marsh else
        (C["earth1"], C["copper0"], C["spark0"], C["wood0"], C["leaf0"], C["spark2"])
    )
    sky, haze, far, trunk, canopy, accent = colors
    result = canvas(512, 128, sky)
    draw = ImageDraw.Draw(result)
    draw.rectangle((0, 28, 511, 127), fill=haze)
    draw.rectangle((0, 54, 511, 127), fill=far)
    for x in range(-32, 544, 32):
        ridge = 40 + ((x // 32) % 4) * 5
        draw.polygon(((x, 76), (x + 14, ridge), (x + 32, 76)), fill=trunk)
        draw.polygon(((x - 8, 67), (x + 10, ridge + 2), (x + 28, 67)), fill=canopy)
    draw.rectangle((0, 91, 511, 127), fill=trunk)
    for x in range(0, 512, 24):
        offset = (x // 24) % 3
        if marsh:
            draw.rectangle((x + 7, 66 + offset * 3, x + 10, 108), fill=C["outline"])
            draw.rectangle((x + 8, 68 + offset * 3, x + 9, 108), fill=C["leaf1"])
            draw.ellipse((x + 1, 58 + offset * 3, x + 16, 72 + offset * 3), fill=C["outline"])
            draw.rectangle((x + 3, 61 + offset * 3, x + 14, 69 + offset * 3), fill=C["leaf2"])
            draw.point((x + 13, 60 + offset * 3), fill=accent)
        else:
            draw.rectangle((x + 8, 60 + offset * 2, x + 13, 111), fill=C["outline"])
            draw.rectangle((x + 9, 62 + offset * 2, x + 12, 111), fill=C["wood1"])
            draw.ellipse((x - 2, 48 + offset * 2, x + 24, 71 + offset * 2), fill=C["outline"])
            draw.rectangle((x + 1, 52 + offset * 2, x + 21, 67 + offset * 2), fill=C["leaf2"])
            draw.rectangle((x + 5, 51 + offset * 2, x + 16, 55 + offset * 2), fill=C["leaf3"])
            draw.point((x + 18, 56 + offset * 2), fill=accent)
    water = C["mana0"] if marsh else C["wood1"]
    draw.rectangle((0, 111, 511, 127), fill=water)
    for x in range(0, 512, 19):
        draw.rectangle((x, 114 + (x % 3), min(511, x + 7), 115 + (x % 3)), fill=accent)
    return force_horizontal_seam(result)


def draw_autotile(base: str, mid: str, light: str, motif: str) -> Image.Image:
    result = canvas(256, 16)
    draw = ImageDraw.Draw(result)
    for mask in range(16):
        x = mask * 16
        draw.rectangle((x + 4, 4, x + 11, 11), fill=C["outline"])
        if mask & 1:
            draw.rectangle((x + 4, 0, x + 11, 7), fill=C["outline"])
        if mask & 2:
            draw.rectangle((x + 8, 4, x + 15, 11), fill=C["outline"])
        if mask & 4:
            draw.rectangle((x + 4, 8, x + 11, 15), fill=C["outline"])
        if mask & 8:
            draw.rectangle((x, 4, x + 7, 11), fill=C["outline"])
        draw.rectangle((x + 5, 5, x + 10, 10), fill=C[base])
        if mask & 1:
            draw.rectangle((x + 5, 0, x + 10, 6), fill=C[mid])
        if mask & 2:
            draw.rectangle((x + 9, 5, x + 15, 10), fill=C[base])
        if mask & 4:
            draw.rectangle((x + 5, 9, x + 10, 15), fill=C[base])
        if mask & 8:
            draw.rectangle((x, 5, x + 6, 10), fill=C[mid])
        draw.point((x + 6 + mask % 4, 6 + (mask // 4) % 4), fill=C[light])
        if motif == "root":
            draw.line((x + 6, 6, x + 9, 9), fill=C[light], width=1)
        elif motif == "moss":
            draw.rectangle((x + 5, 5, x + 10, 6), fill=C[light])
        elif motif == "plank":
            draw.line((x + 5, 8, x + 10, 8), fill=C[light], width=1)
        else:
            draw.point((x + 9, 7), fill=C[light])
    return result


def draw_chain_sheet() -> Image.Image:
    result = canvas(64, 32)
    draw = ImageDraw.Draw(result)
    for index in range(4):
        x = index * 16
        if index != 0:
            draw.rectangle((x + 7, 0, x + 8, 10), fill=C["outline"])
            draw.point((x + 8, 2), fill=C["copper3"])
        draw.rectangle((x + 3, 9, x + 12, 12), fill=C["outline"])
        draw.rectangle((x + 4, 10, x + 11, 11), fill=C["copper1"])
        if index in (0, 3):
            draw.polygon(((x + 4, 13), (x + 11, 13), (x + 13, 23), (x + 2, 23)), fill=C["outline"])
            draw.rectangle((x + 5, 14, x + 10, 21), fill=C["spark2"])
            draw.rectangle((x + 6, 14, x + 8, 16), fill=C["spark3"])
        if index == 2:
            draw.rectangle((x + 6, 13, x + 9, 30), fill=C["outline"])
            draw.rectangle((x + 7, 14, x + 8, 29), fill=C["copper2"])
    return result


def draw_workshop_set() -> Image.Image:
    result = canvas(128, 32)
    draw = ImageDraw.Draw(result)
    for index in range(4):
        x = index * 32
        if index == 0:  # bench
            draw.rectangle((x + 3, 13, x + 28, 18), fill=C["outline"])
            draw.rectangle((x + 4, 14, x + 27, 16), fill=C["wood2"])
            draw.rectangle((x + 6, 18, x + 9, 29), fill=C["outline"])
            draw.rectangle((x + 22, 18, x + 25, 29), fill=C["outline"])
        elif index == 1:  # shelf
            draw.rectangle((x + 5, 4, x + 26, 28), fill=C["outline"])
            draw.rectangle((x + 7, 6, x + 24, 26), fill=C["wood0"])
            for shelf_y in (11, 19):
                draw.rectangle((x + 6, shelf_y, x + 25, shelf_y + 2), fill=C["wood2"])
            draw.rectangle((x + 9, 7, x + 13, 10), fill=C["mana2"])
            draw.rectangle((x + 17, 14, x + 22, 18), fill=C["copper2"])
        elif index == 2:  # chair
            draw.rectangle((x + 8, 4, x + 20, 18), fill=C["outline"])
            draw.rectangle((x + 10, 6, x + 18, 15), fill=C["wood2"])
            draw.rectangle((x + 6, 17, x + 23, 21), fill=C["outline"])
            draw.rectangle((x + 8, 21, x + 11, 29), fill=C["outline"])
            draw.rectangle((x + 18, 21, x + 21, 29), fill=C["outline"])
        else:  # tool rack
            draw.rectangle((x + 3, 7, x + 28, 11), fill=C["outline"])
            draw.rectangle((x + 5, 8, x + 26, 9), fill=C["wood2"])
            draw.line((x + 9, 11, x + 7, 26), fill=C["outline"], width=3)
            draw.line((x + 18, 11, x + 23, 25), fill=C["outline"], width=3)
            draw.point((x + 7, 25), fill=C["copper3"])
            draw.point((x + 23, 24), fill=C["spark3"])
    return result


def outlined_line(draw: ImageDraw.ImageDraw, points: tuple[tuple[int, int], ...], fill, width: int = 2) -> None:
    draw.line(points, fill=C["outline"], width=width + 2)
    draw.line(points, fill=fill, width=width)


def draw_icon(kind: str) -> Image.Image:
    result = canvas(32, 32)
    draw = ImageDraw.Draw(result)
    if kind == "sunsteel_pickaxe":
        outlined_line(draw, ((8, 27), (21, 8)), C["wood2"], 3)
        draw.polygon(((8, 5), (25, 4), (29, 8), (27, 11), (22, 8), (11, 9)), fill=C["outline"])
        draw.polygon(((10, 6), (24, 6), (27, 8), (26, 9), (22, 7), (11, 8)), fill=C["spark2"])
    elif kind == "prism_axe":
        outlined_line(draw, ((9, 27), (19, 7)), C["wood2"], 3)
        draw.polygon(((17, 4), (28, 8), (24, 17), (18, 12)), fill=C["outline"])
        draw.polygon(((19, 6), (26, 9), (23, 14), (19, 11)), fill=C["crystal3"])
        draw.point((22, 8), fill=C["crystal4"])
    elif kind == "glimmer_rod":
        outlined_line(draw, ((9, 27), (21, 8)), C["wood2"], 3)
        draw.polygon(((20, 3), (26, 8), (22, 14), (17, 9)), fill=C["outline"])
        draw.polygon(((21, 5), (24, 8), (21, 12), (19, 9)), fill=C["mana3"])
        draw.point((25, 4), fill=C["spark3"])
    elif kind == "thornblade":
        draw.polygon(((6, 28), (8, 20), (22, 4), (27, 3), (25, 10), (12, 24)), fill=C["outline"])
        draw.polygon(((9, 23), (23, 6), (24, 6), (22, 10), (11, 24)), fill=C["leaf3"])
        draw.rectangle((6, 22, 15, 25), fill=C["outline"])
        draw.point((12, 20), fill=C["leaf4"])
    elif kind == "mirror_shield":
        draw.polygon(((5, 5), (27, 5), (27, 18), (16, 29), (5, 18)), fill=C["outline"])
        draw.polygon(((8, 8), (24, 8), (24, 17), (16, 25), (8, 17)), fill=C["neutral2"])
        draw.polygon(((10, 9), (21, 9), (10, 17)), fill=C["crystal4"])
        draw.line((16, 9, 16, 23), fill=C["pale"], width=1)
    elif kind == "flare_bow":
        draw.arc((6, 3, 27, 29), 255, 105, fill=C["outline"], width=5)
        draw.arc((7, 4, 26, 28), 255, 105, fill=C["copper2"], width=2)
        draw.line((12, 5, 12, 27), fill=C["pale"], width=1)
        draw.line((5, 16, 27, 16), fill=C["outline"], width=3)
        draw.line((7, 16, 25, 16), fill=C["spark2"], width=1)
    else:
        # UI symbols use a shared readable badge frame.
        draw.rectangle((4, 4, 27, 27), fill=C["outline"])
        draw.rectangle((6, 6, 25, 25), fill=C["teal0"])
        if kind == "quest":
            draw.rectangle((11, 8, 20, 11), fill=C["spark3"])
            draw.rectangle((17, 11, 20, 17), fill=C["spark3"])
            draw.rectangle((13, 16, 18, 19), fill=C["spark3"])
            draw.rectangle((13, 22, 17, 25), fill=C["spark3"])
        elif kind == "combat":
            outlined_line(draw, ((9, 22), (21, 10)), C["copper3"], 2)
            outlined_line(draw, ((10, 9), (22, 22)), C["crystal3"], 2)
        elif kind == "social":
            draw.ellipse((8, 8, 14, 14), fill=C["pale"])
            draw.ellipse((18, 8, 24, 14), fill=C["spark3"])
            draw.rectangle((7, 17, 15, 22), fill=C["pale"])
            draw.rectangle((17, 17, 25, 22), fill=C["spark3"])
        else:
            draw.rectangle((8, 13, 23, 24), fill=C["wood2"])
            draw.polygon(((7, 13), (16, 6), (24, 13)), fill=C["spark2"])
            draw.rectangle((14, 17, 18, 24), fill=C["outline"])
    return result


def draw_entity(kind: str) -> Image.Image:
    result = canvas(256, 32)
    draw = ImageDraw.Draw(result)
    for index in range(8):
        x = index * 32
        bob = (0, 1, 0, -1, 0, 1, 0, -1)[index]
        if kind == "marsh_frog":
            y = 16 + bob
            draw.ellipse((x + 5, y - 5, x + 26, y + 9), fill=C["outline"])
            draw.rectangle((x + 8, y - 3, x + 23, y + 6), fill=C["leaf2"])
            draw.rectangle((x + 4, y + 5, x + 11, y + 8), fill=C["outline"])
            draw.rectangle((x + 21, y + 5, x + 28, y + 8), fill=C["outline"])
            draw.rectangle((x + 9, y - 8, x + 13, y - 3), fill=C["outline"])
            draw.rectangle((x + 19, y - 8, x + 23, y - 3), fill=C["outline"])
            draw.point((x + 11, y - 6), fill=C["spark3"])
            draw.point((x + 21, y - 6), fill=C["spark3"])
        elif kind == "canopy_owl":
            y = 7 + bob
            wing = 2 + (index % 4)
            draw.ellipse((x + 7, y, x + 24, y + 20), fill=C["outline"])
            draw.rectangle((x + 9, y + 4, x + 22, y + 17), fill=C["wood2"])
            draw.polygon(((x + 8, y + 6), (x + wing, y + 12), (x + 9, y + 16)), fill=C["copper1"])
            draw.polygon(((x + 23, y + 6), (x + 31 - wing, y + 12), (x + 22, y + 16)), fill=C["copper1"])
            draw.rectangle((x + 10, y + 4, x + 14, y + 8), fill=C["pale"])
            draw.rectangle((x + 18, y + 4, x + 22, y + 8), fill=C["pale"])
            draw.point((x + 12, y + 6), fill=C["outline"])
            draw.point((x + 20, y + 6), fill=C["outline"])
            draw.polygon(((x + 15, y + 8), (x + 18, y + 8), (x + 16, y + 11)), fill=C["spark2"])
        elif kind == "amber_beetle":
            y = 11 + bob
            draw.ellipse((x + 6, y, x + 25, y + 15), fill=C["outline"])
            draw.ellipse((x + 9, y + 2, x + 23, y + 13), fill=C["copper1"])
            draw.line((x + 16, y + 2, x + 16, y + 13), fill=C["spark2"], width=1)
            draw.rectangle((x + 4, y + 4, x + 9, y + 10), fill=C["outline"])
            draw.line((x + 8, y + 13, x + 4, y + 18), fill=C["outline"], width=2)
            draw.line((x + 23, y + 13, x + 28, y + 18), fill=C["outline"], width=2)
            draw.point((x + 12, y + 4), fill=C["spark3"])
        else:  # prism wisp
            y = 7 + bob
            draw.polygon(((x + 16, y), (x + 25, y + 9), (x + 20, y + 21), (x + 16, y + 27), (x + 12, y + 21), (x + 7, y + 9)), fill=C["outline"])
            draw.polygon(((x + 16, y + 3), (x + 22, y + 10), (x + 18, y + 20), (x + 16, y + 24), (x + 14, y + 20), (x + 10, y + 10)), fill=C["crystal2"])
            draw.polygon(((x + 16, y + 5), (x + 20, y + 11), (x + 16, y + 16), (x + 12, y + 11)), fill=C["crystal4"])
            draw.point((x + 8 - index % 2, y + 18), fill=C["mana3"])
            draw.point((x + 25 + index % 2, y + 14), fill=C["spark3"])
    return result


def draw_nine_slice(kind: str) -> Image.Image:
    result = canvas(48, 48)
    draw = ImageDraw.Draw(result)
    base = C["crystal0"] if kind == "glass" else C["wood0"]
    edge = C["teal1"] if kind == "glass" else C["copper2"]
    shine = C["crystal3"] if kind == "glass" else C["spark2"]
    for row in range(3):
        for column in range(3):
            x, y = column * 16, row * 16
            draw.rectangle((x + 1, y + 1, x + 14, y + 14), fill=C["outline"])
            draw.rectangle((x + 2, y + 2, x + 13, y + 13), fill=base)
            if row == 0:
                draw.rectangle((x + 3, y + 2, x + 12, y + 3), fill=shine)
            if row == 2:
                draw.rectangle((x + 3, y + 12, x + 12, y + 13), fill=edge)
            if column == 0:
                draw.rectangle((x + 2, y + 3, x + 3, y + 12), fill=edge)
            if column == 2:
                draw.rectangle((x + 12, y + 3, x + 13, y + 12), fill=shine)
            if row == 1 and column == 1:
                draw.rectangle((x + 4, y + 4, x + 11, y + 11), fill=C["neutral0"] if kind == "glass" else C["wood1"])
    return result


def specs() -> tuple[AssetSpec, ...]:
    common = ("production-sample", "runtime-preloaded", "wave-05", "living-world")
    result = [
        AssetSpec("world/backgrounds/wave05/twilight_marsh", "backgrounds/twilight_marsh_parallax.png", "Background", (512, 128), "wave05.backgrounds", (0, 127), "background.mid", "Seam-safe twilight marsh parallax scene", common + ("background", "parallax", "twilight-marsh", "tileable"), lambda: draw_parallax("twilight_marsh")),
        AssetSpec("world/backgrounds/wave05/amber_grove", "backgrounds/amber_grove_parallax.png", "Background", (512, 128), "wave05.backgrounds", (0, 127), "background.mid", "Seam-safe amber grove parallax scene", common + ("background", "parallax", "amber-grove", "tileable"), lambda: draw_parallax("amber_grove")),
        AssetSpec("world/wave05/mangrove_root_autotile", "world/mangrove_root_autotile.png", "Tile", (256, 16), "wave05.world", (8, 8), "tiles.front", "Connectable mangrove root material", common + ("world", "autotile", "connectable", "root"), lambda: draw_autotile("wood1", "wood2", "wood3", "root"), autotile_frames()),
        AssetSpec("world/wave05/marsh_moss_autotile", "world/marsh_moss_autotile.png", "Tile", (256, 16), "wave05.world", (8, 8), "tiles.front", "Connectable marsh moss material", common + ("world", "autotile", "connectable", "moss"), lambda: draw_autotile("leaf1", "leaf2", "leaf4", "moss"), autotile_frames()),
        AssetSpec("world/wave05/amberwood_plank_autotile", "world/amberwood_plank_autotile.png", "Tile", (256, 16), "wave05.world", (8, 8), "tiles.front", "Connectable amberwood plank material", common + ("world", "autotile", "connectable", "plank"), lambda: draw_autotile("wood1", "wood2", "copper3", "plank"), autotile_frames()),
        AssetSpec("world/wave05/amberstone_autotile", "world/amberstone_autotile.png", "Tile", (256, 16), "wave05.world", (8, 8), "tiles.front", "Connectable amberstone material", common + ("world", "autotile", "connectable", "stone"), lambda: draw_autotile("earth1", "earth2", "spark2", "stone"), autotile_frames()),
        AssetSpec("world/wave05/hanging_lantern_chain", "world/hanging_lantern_chain.png", "WorldObject", (64, 32), "wave05.world", (8, 31), "world.decoration", "Four-piece connectable hanging lantern chain", common + ("world-object", "furniture", "connectable", "light-source"), draw_chain_sheet, tuple(frame(name, i * 16, 0, 16, 32, 8, 31) for i, name in enumerate(("cap", "chain_short", "chain_long", "lantern")))),
        AssetSpec("world/wave05/amber_workshop_set", "world/amber_workshop_set.png", "WorldObject", (128, 32), "wave05.world", (16, 31), "world.furniture", "Modular amberwood workshop furniture set", common + ("world-object", "furniture", "modular", "connectable"), draw_workshop_set, tuple(frame(name, i * 32, 0, 32, 32, 16, 31) for i, name in enumerate(("bench", "shelf", "chair", "tool_rack")))),
    ]
    icon_specs = (
        ("sunsteel_pickaxe", "Tool"), ("prism_axe", "Tool"), ("glimmer_rod", "Tool"),
        ("thornblade", "Weapon"), ("mirror_shield", "Weapon"), ("flare_bow", "Weapon"),
    )
    for name, category in icon_specs:
        result.append(AssetSpec(f"items/wave05/{name}", f"items/{name}.png", category, (32, 32), "wave05.items", (16, 16), "items", name.replace("_", " ").title(), common + ("item", "icon", "combat" if category == "Weapon" else "tool"), lambda name=name: draw_icon(name)))
    for name, faction in (("marsh_frog", "friendly"), ("canopy_owl", "friendly"), ("amber_beetle", "neutral"), ("prism_wisp", "hostile")):
        result.append(AssetSpec(f"entities/wave05/{name}", f"entities/{name}.png", "Entity", (256, 32), "wave05.entities", (16, 31), "entities.front", f"Eight-frame animated {name.replace('_', ' ')}", common + ("entity", "animated", faction, "eight-frame"), lambda name=name: draw_entity(name), animation_frames(8, 32, 32, 16, 31)))
    result.extend((
        AssetSpec("ui/wave05/glass_panel_9slice", "ui/glass_panel_9slice.png", "UI", (48, 48), "wave05.ui", (8, 8), "ui.frame", "Modern glass-and-teal pixel nine-slice", common + ("ui", "nine-slice", "panel"), lambda: draw_nine_slice("glass"), nine_slice_frames()),
        AssetSpec("ui/wave05/brass_modal_9slice", "ui/brass_modal_9slice.png", "UI", (48, 48), "wave05.ui", (8, 8), "ui.frame", "Modern brass-and-wood pixel nine-slice", common + ("ui", "nine-slice", "modal"), lambda: draw_nine_slice("brass"), nine_slice_frames()),
    ))
    for name in ("quest", "combat", "social", "building"):
        result.append(AssetSpec(f"ui/wave05/{name}", f"ui/{name}.png", "UI", (32, 32), "wave05.ui", (16, 16), "ui.icon", f"Readable {name} menu icon", common + ("ui", "icon", name), lambda name=name: draw_icon(name)))
    return tuple(result)


def manifest_entry(spec: AssetSpec) -> dict:
    entry = {
        "id": spec.sprite_id,
        "path": f"Assets/Wave05/{spec.relative_path}",
        "category": spec.category,
        "width": spec.size[0],
        "height": spec.size[1],
        "pixelsPerUnit": 16,
        "atlasId": spec.atlas,
        "originX": spec.origin[0],
        "originY": spec.origin[1],
        "renderLayer": spec.render_layer,
        "license": "YjsE-Project-Owned",
        "provenance": f"{WAVE_ID}; deterministic Pillow generator; wave_05_living_world_provenance.json",
        "tags": list(spec.tags),
    }
    if spec.frames:
        entry["frames"] = list(spec.frames)
    return entry


def brief_entry(spec: AssetSpec) -> dict:
    frame_note = f"Preserve {len(spec.frames)} declared source rectangles and origins." if spec.frames else "Render as one exact source rectangle."
    return {
        "spriteId": spec.sprite_id,
        "outputPath": f"Assets/Wave05/{spec.relative_path}",
        "width": spec.size[0],
        "height": spec.size[1],
        "subject": spec.subject,
        "prompt": f"YjsE Wave 05 production pixel art: {spec.subject}. Crisp side-view yjse-pixel-v1 clusters, strong native-scale silhouette, top-left lighting, exact runtime sheet layout.",
        "requirements": [
            f"Export exactly {spec.size[0]}x{spec.size[1]} RGBA.",
            "Use binary alpha only and nearest-neighbor pixel placement.",
            "Use only colors declared by yjse-pixel-v1.",
            frame_note,
            f"Runtime target: {spec.sprite_id}; activation is preload-ready.",
        ],
        "palette": sorted({"#%02x%02x%02x" % value[:3] for value in C.values() if value[3] > 0}),
        "tags": list(spec.tags),
    }


def flattened_alpha(image: Image.Image) -> list[int]:
    getter = getattr(image.getchannel("A"), "get_flattened_data", None)
    return list(getter() if getter else image.getchannel("A").getdata())


def main() -> None:
    asset_specs = specs()
    generated = []
    for spec in asset_specs:
        output = WAVE_ROOT / spec.relative_path
        output.parent.mkdir(parents=True, exist_ok=True)
        image = spec.draw()
        if image.size != spec.size:
            raise RuntimeError(f"{spec.sprite_id}: expected {spec.size}, got {image.size}")
        if image.mode != "RGBA":
            raise RuntimeError(f"{spec.sprite_id}: expected RGBA, got {image.mode}")
        alpha_values = sorted(set(flattened_alpha(image)))
        if any(value not in (0, 255) for value in alpha_values):
            raise RuntimeError(f"{spec.sprite_id}: partial alpha {alpha_values}")
        image.save(output, "PNG", optimize=False, compress_level=9)
        generated.append({
            "spriteId": spec.sprite_id,
            "path": f"Assets/Wave05/{spec.relative_path}",
            "sha256": sha256(output.read_bytes()).hexdigest(),
            "dimensions": list(spec.size),
            "frameCount": len(spec.frames),
            "alphaValues": alpha_values,
            "generator": GENERATOR_RELATIVE,
            "method": "deterministic Pillow pixel primitives at final runtime dimensions",
            "license": "YjsE-Project-Owned",
            "runtimeConsumer": "SpriteAssetJsonLoader and ClientTextureRegistry preload; gameplay activation remains content-owner controlled",
        })

    manifest = {"sprites": [manifest_entry(spec) for spec in asset_specs]}
    MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    brief = {
        "version": 1,
        "scope": WAVE_ID,
        "globalStyle": "YjsE yjse-pixel-v1 crisp side-view pixel art; binary alpha; top-left light; hard clusters; no antialiasing or gradients.",
        "globalNegativePrompt": "blur, partial alpha, antialiasing, noise dithering, watermark, logo, copied game art, text, frame labels",
        "globalRequirements": [
            "Export exact manifest dimensions and source rectangles.",
            "Use only colors declared in Game.Data/art_direction/yjse_pixel_style.json.",
            "Keep standalone silhouettes readable on light and dark fields.",
            "Keep runtime activation honest as preload-ready until referenced by game content.",
        ],
        "briefs": [brief_entry(spec) for spec in asset_specs],
    }
    BRIEF_PATH.parent.mkdir(parents=True, exist_ok=True)
    BRIEF_PATH.write_text(json.dumps(brief, indent=2) + "\n", encoding="utf-8")

    provenance = {
        "schemaVersion": 1,
        "waveId": WAVE_ID,
        "generatedOn": "2026-07-14",
        "sourceType": "checked-in deterministic pixel generator",
        "externalDownloads": False,
        "modelGeneratedSource": False,
        "generator": {
            "path": GENERATOR_RELATIVE,
            "sha256": sha256(Path(__file__).read_bytes()).hexdigest(),
            "runtime": "Python 3 plus Pillow from art_direction/requirements.txt",
            "method": "Direct final-size Pillow primitives constrained to yjse-pixel-v1; no hidden source art and no manual raster edits claimed.",
        },
        "manifest": "assets/wave05_living_world.sprites.json",
        "brief": "asset_briefs/production_wave_05_living_world_briefs.json",
        "assets": generated,
    }
    PROVENANCE_PATH.parent.mkdir(parents=True, exist_ok=True)
    PROVENANCE_PATH.write_text(json.dumps(provenance, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"waveId": WAVE_ID, "assetCount": len(generated), "manifest": str(MANIFEST_PATH)}, indent=2))


if __name__ == "__main__":
    main()
