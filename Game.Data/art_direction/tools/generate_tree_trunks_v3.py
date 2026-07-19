#!/usr/bin/env python3
"""Generate organic, transparent TreeTrunksV3 cardinal-mask runtime sheets.

The sheet keeps exact cardinal sockets, but draws every limb as a native-resolution
curved stroke.  One-neighbour frames finish in a tapered tip rather than a cut-off
bar, while junction frames share a compact, rounded heartwood knot.
"""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets" / "TreeTrunksV3" / "world" / "tiles"
MANIFEST = ROOT / "assets" / "tree_trunks_v3.sprites.json"
PREVIEW = ROOT / "art_direction" / "tree_trunks_v3_contact_sheet.png"
PROVENANCE = ROOT / "art_direction" / "tree_trunks_v3_provenance.json"
BRIEF = "asset_briefs/tree_trunks_v3_briefs.json"
FRAME_SIZE = 16

SPECS = (
    {
        "id": "tiles/oak_trunk_v3_autotile",
        "filename": "oak_trunk_v3_autotile.png",
        "palette": ((38, 24, 19, 255), (84, 48, 27, 255), (137, 78, 38, 255), (190, 122, 61, 255)),
        "tags": ["forest", "oak"],
    },
    {
        "id": "tiles/living_wood_v3_autotile",
        "filename": "living_wood_v3_autotile.png",
        "palette": ((42, 29, 20, 255), (91, 57, 30, 255), (151, 94, 42, 255), (213, 149, 67, 255)),
        "tags": ["amber-grove", "living-wood"],
    },
    {
        "id": "tiles/mangrove_trunk_v3_autotile",
        "filename": "mangrove_trunk_v3_autotile.png",
        "palette": ((29, 25, 24, 255), (66, 43, 31, 255), (111, 70, 40, 255), (160, 105, 54, 255)),
        "tags": ["twilight-marsh", "mangrove"],
    },
)


def stable_hash(x: int, y: int, mask: int, species: int) -> int:
    value = (x * 0x45D9F3B) ^ (y * 0x119DE1F3) ^ (mask * 0x9E3779B9) ^ (species * 0x85EBCA6B)
    value ^= value >> 16
    value = (value * 0x7FEB352D) & 0xFFFFFFFF
    value ^= value >> 15
    return value & 0xFFFFFFFF


PORTS = {
    1: (8.0, 0.0),
    2: (16.0, 8.0),
    4: (8.0, 16.0),
    8: (0.0, 8.0),
}


def cubic_point(
    p0: tuple[float, float],
    p1: tuple[float, float],
    p2: tuple[float, float],
    p3: tuple[float, float],
    t: float,
) -> tuple[float, float]:
    inverse = 1.0 - t
    return (
        inverse ** 3 * p0[0] + 3 * inverse * inverse * t * p1[0] + 3 * inverse * t * t * p2[0] + t ** 3 * p3[0],
        inverse ** 3 * p0[1] + 3 * inverse * inverse * t * p1[1] + 3 * inverse * t * t * p2[1] + t ** 3 * p3[1],
    )


def stamp_disc(cells: set[tuple[int, int]], center: tuple[float, float], width: float) -> None:
    """Raster a round native-pixel brush around a continuous pixel-edge center."""
    cx, cy = center
    radius = width * 0.5
    minimum_x = max(0, math.floor(cx - radius - 0.5))
    maximum_x = min(FRAME_SIZE - 1, math.ceil(cx + radius - 0.5))
    minimum_y = max(0, math.floor(cy - radius - 0.5))
    maximum_y = min(FRAME_SIZE - 1, math.ceil(cy + radius - 0.5))
    radius_squared = radius * radius
    for y in range(minimum_y, maximum_y + 1):
        for x in range(minimum_x, maximum_x + 1):
            dx = x + 0.5 - cx
            dy = y + 0.5 - cy
            if dx * dx + dy * dy <= radius_squared:
                cells.add((x, y))


def stroke_curve(
    cells: set[tuple[int, int]],
    p0: tuple[float, float],
    p1: tuple[float, float],
    p2: tuple[float, float],
    p3: tuple[float, float],
    start_width: float,
    end_width: float,
) -> list[tuple[float, float]]:
    points: list[tuple[float, float]] = []
    for step in range(33):
        t = step / 32.0
        point = cubic_point(p0, p1, p2, p3, t)
        # A slight middle swell reads as living wood without creating a square knot.
        width = start_width + (end_width - start_width) * t + math.sin(math.pi * t) * 0.35
        stamp_disc(cells, point, width)
        points.append(point)
    return points


def endpoint_curve(direction: int, species: int) -> tuple[tuple[float, float], ...]:
    """Return a port-to-taper cubic for a terminal trunk or branch tile."""
    sway = (-0.55, 0.35, -0.2)[species]
    if direction == 1:
        return PORTS[1], (8.0 + sway, 3.0), (6.4 - sway, 7.2), (9.2 + sway, 12.2)
    if direction == 2:
        return PORTS[2], (13.0, 7.5 + sway), (9.1, 9.8 - sway), (3.6, 6.6 + sway)
    if direction == 4:
        return PORTS[4], (8.0 - sway, 13.0), (9.7 + sway, 9.0), (6.2 - sway, 3.8)
    return PORTS[8], (3.0, 8.2 - sway), (7.2, 6.1 + sway), (12.4, 9.1 - sway)


def branch_curve(
    direction: int,
    center: tuple[float, float],
    mask: int,
    species: int,
) -> tuple[tuple[float, float], ...]:
    """Return a center-to-port curve whose endpoint stays socket-stable."""
    variation = ((stable_hash(direction, mask, mask, species) >> 5) % 3 - 1) * 0.55
    cx, cy = center
    if direction == 1:
        return center, (cx - 1.1 + variation, 6.0), (8.8 - variation * 0.35, 2.7), PORTS[1]
    if direction == 2:
        return center, (10.2, cy - 1.15 + variation), (13.3, 8.8 - variation * 0.35), PORTS[2]
    if direction == 4:
        return center, (cx + 1.0 - variation, 10.1), (7.1 + variation * 0.35, 13.2), PORTS[4]
    return center, (5.7, cy + 1.05 - variation), (2.8, 7.1 + variation * 0.35), PORTS[8]


def four_connected(cells: set[tuple[int, int]]) -> bool:
    if not cells:
        return False
    pending = [next(iter(cells))]
    visited = {pending[0]}
    while pending:
        x, y = pending.pop()
        for neighbour in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if neighbour in cells and neighbour not in visited:
                visited.add(neighbour)
                pending.append(neighbour)
    return visited == cells


def assert_frame_contract(mask: int, cells: set[tuple[int, int]]) -> None:
    """Keep topology, negative space, and sockets honest during regeneration."""
    if not four_connected(cells):
        raise ValueError(f"mask {mask} is not one four-connected silhouette")
    if len(cells) > 150:
        raise ValueError(f"mask {mask} is too dense ({len(cells)}/256 opaque pixels)")
    edges = {
        1: {(x, 0) for x in range(FRAME_SIZE)},
        2: {(15, y) for y in range(FRAME_SIZE)},
        4: {(x, 15) for x in range(FRAME_SIZE)},
        8: {(0, y) for y in range(FRAME_SIZE)},
    }
    for direction, edge in edges.items():
        count = len(cells & edge)
        if mask & direction:
            if not 4 <= count <= 6:
                raise ValueError(f"mask {mask} direction {direction} has {count} socket pixels")
        elif count:
            raise ValueError(f"mask {mask} leaks {count} pixels through absent direction {direction}")


def build_frame(mask: int, species: int) -> Image.Image:
    outer: set[tuple[int, int]] = set()
    strokes: list[list[tuple[float, float]]] = []
    directions = [direction for direction in (1, 2, 4, 8) if mask & direction]
    center = ((7.7, 8.15), (8.25, 7.7), (7.45, 8.3))[species]

    if not directions:
        # A small diagonal knot for isolated decorative placements; never a block.
        strokes.append(stroke_curve(outer, (5.0, 12.8), (6.1, 10.5), (9.2, 6.1), (10.3, 3.5), 4.8, 2.0))
    elif len(directions) == 1:
        curve = endpoint_curve(directions[0], species)
        socket_width = 5.4 if directions[0] in (1, 4) else 4.4
        strokes.append(stroke_curve(outer, *curve, socket_width, 1.4))
    elif mask == 5:
        # A continuous S-curve keeps the main trunk broad but avoids pipe elbows.
        sway = (-0.6, 0.45, -0.25)[species]
        curve = (PORTS[1], (6.2 + sway, 4.6), (9.8 - sway, 11.0), PORTS[4])
        strokes.append(stroke_curve(outer, *curve, 5.4, 5.4))
    elif mask == 10:
        lift = (-0.5, 0.4, -0.25)[species]
        curve = (PORTS[8], (4.0, 6.7 + lift), (11.8, 9.2 - lift), PORTS[2])
        strokes.append(stroke_curve(outer, *curve, 4.4, 4.4))
    elif len(directions) == 2:
        # Corner masks are a single diagonal arc, not two bars glued to a square.
        lean = (-0.35, 0.4, -0.15)[species]
        corner_curves = {
            3: (PORTS[1], (8.0 + lean, 4.2), (11.8, 7.2 - lean), PORTS[2]),
            6: (PORTS[2], (11.8, 8.0 + lean), (8.8 - lean, 11.8), PORTS[4]),
            12: (PORTS[4], (7.8 - lean, 11.7), (4.1, 8.8 + lean), PORTS[8]),
            9: (PORTS[8], (4.1, 7.8 - lean), (7.1 + lean, 4.1), PORTS[1]),
        }
        curve = corner_curves[mask]
        start_width = 5.4 if directions[0] in (1, 4) else 4.4
        end_width = 5.4 if directions[1] in (1, 4) else 4.4
        strokes.append(stroke_curve(outer, *curve, start_width, end_width))
    else:
        vertical_junction = any(direction in (1, 4) for direction in directions)
        stamp_disc(outer, center, 5.6 if vertical_junction else 4.8)
        for direction in directions:
            curve = branch_curve(direction, center, mask, species)
            socket_width = 5.4 if direction in (1, 4) else 4.4
            center_width = 5.6 if vertical_junction else 4.8
            strokes.append(stroke_curve(outer, *curve, center_width, socket_width))

    assert_frame_contract(mask, outer)

    palette = SPECS[species]["palette"]
    image = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
    pixels = image.load()
    for x, y in outer:
        missing_left = (x - 1, y) not in outer
        missing_top = (x, y - 1) not in outer
        missing_right = (x + 1, y) not in outer
        missing_bottom = (x, y + 1) not in outer
        if missing_right or missing_bottom:
            color_index = 0
        elif missing_left or missing_top:
            color_index = 2
        else:
            color_index = 1 if stable_hash(x, y, mask, species) % 4 == 0 else 2
        pixels[x, y] = palette[color_index]

    # Short top-left glints follow the curve; dark cuts cross it elsewhere.
    for stroke_index, points in enumerate(strokes):
        for sample in (11, 22):
            if sample >= len(points):
                continue
            x = math.floor(points[sample][0] - 0.7)
            y = math.floor(points[sample][1] - 0.7)
            if (x, y) in outer:
                pixels[x, y] = palette[3]
                if (x + 1, y) in outer and (sample + stroke_index + species) % 2 == 0:
                    pixels[x + 1, y] = palette[3]
        cut = points[16]
        cut_x = math.floor(cut[0] + 0.7)
        cut_y = math.floor(cut[1] + 0.7)
        if (cut_x, cut_y) in outer and stable_hash(cut_x, cut_y, mask + stroke_index, species) % 3:
            pixels[cut_x, cut_y] = palette[0]

    return image


def frame_entries() -> list[dict[str, int | str]]:
    return [
        {
            "id": f"mask_{mask}",
            "x": mask * FRAME_SIZE,
            "y": 0,
            "width": FRAME_SIZE,
            "height": FRAME_SIZE,
            "autoTileMask": mask,
        }
        for mask in range(16)
    ]


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    sheets: list[Image.Image] = []
    assets: list[dict[str, object]] = []
    sprites: list[dict[str, object]] = []
    for species, spec in enumerate(SPECS):
        sheet = Image.new("RGBA", (256, 16), (0, 0, 0, 0))
        for mask in range(16):
            sheet.alpha_composite(build_frame(mask, species), (mask * FRAME_SIZE, 0))
        path = OUTPUT / str(spec["filename"])
        sheet.save(path, optimize=False)
        sheets.append(sheet)
        relative_path = str(path.relative_to(ROOT)).replace("\\", "/")
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        assets.append({
            "spriteId": spec["id"],
            "path": relative_path,
            "sha256": digest,
            "dimensions": [256, 16],
            "alpha": "binary",
            "generator": "art_direction/tools/generate_tree_trunks_v3.py",
            "license": "YjsE-Project-Owned",
        })
        sprites.append({
            "id": spec["id"],
            "path": relative_path,
            "category": "Tile",
            "width": 256,
            "height": 16,
            "atlasId": "tiles",
            "license": "YjsE-Project-Owned",
            "provenance": "tree_trunks_v3_provenance.json; deterministic Pillow pixel primitives",
            "frames": frame_entries(),
            "tags": ["world", "tile", "autotile", "tree", "trunk", "binary-alpha", "runtime-active", "version-3", *spec["tags"]],
        })

    scale = 4
    frame_pitch = FRAME_SIZE * scale + 4
    panel_pitch = frame_pitch * 4 + 12
    preview = Image.new("RGBA", (panel_pitch * len(sheets) - 4, frame_pitch * 4 + 12), (18, 20, 28, 255))
    for species, sheet in enumerate(sheets):
        panel_x = species * panel_pitch
        for mask in range(16):
            source = sheet.crop((mask * FRAME_SIZE, 0, (mask + 1) * FRAME_SIZE, FRAME_SIZE))
            enlarged = source.resize((FRAME_SIZE * scale, FRAME_SIZE * scale), Image.Resampling.NEAREST)
            x = panel_x + (mask % 4) * frame_pitch
            y = 6 + (mask // 4) * frame_pitch
            preview.alpha_composite(enlarged, (x, y))
    preview.save(PREVIEW, optimize=False)
    MANIFEST.write_text(json.dumps({"sprites": sprites}, indent=2) + "\n", encoding="utf-8")
    PROVENANCE.write_text(json.dumps({
        "version": 2,
        "generator": "deterministic native-pixel cubic strokes",
        "sourceBrief": BRIEF,
        "sourceConcept": "art_direction/generated_sources/tree_v3_concept_source.png",
        "preview": str(PREVIEW.relative_to(ROOT)).replace("\\", "/"),
        "qaContracts": {
            "frameSize": [16, 16],
            "sheetSize": [256, 16],
            "alpha": "binary",
            "topology": "one four-connected component per frame",
            "socketWidthPixels": [4, 6],
            "maximumOpaquePixelsPerFrame": 150,
        },
        "assets": assets,
    }, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
