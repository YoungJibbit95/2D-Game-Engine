#!/usr/bin/env python3
"""Generate the original, sparse LooseFoliageV2 runtime autotile pack."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets" / "LooseFoliageV2"
PREVIEW = ROOT / "art_direction" / "loose_foliage_v2_contact_sheet.png"
PROVENANCE = ROOT / "art_direction" / "loose_foliage_v2_provenance.json"

SPECS = (
    {
        "sprite_prefix": "tiles/loose_oak_leaves",
        "filename_prefix": "loose_oak_leaves",
        "palette": ((28, 57, 34, 255), (49, 98, 48, 255), (82, 143, 68, 255), (142, 183, 84, 255)),
        "shape": "round oak clusters with occasional pointed leaf tips",
    },
    {
        "sprite_prefix": "tiles/loose_autumn_leaves",
        "filename_prefix": "loose_autumn_leaves",
        "palette": ((78, 42, 25, 255), (142, 65, 31, 255), (207, 109, 40, 255), (246, 171, 60, 255)),
        "shape": "asymmetric amber clusters with bright sunward tips",
    },
    {
        "sprite_prefix": "tiles/loose_marsh_leaves",
        "filename_prefix": "loose_marsh_leaves",
        "palette": ((19, 51, 46, 255), (34, 84, 69, 255), (59, 124, 89, 255), (113, 159, 96, 255)),
        "shape": "narrow teal-green clusters with slightly hanging lower tips",
    },
)

VARIANT_NAMES = ("a", "b", "c")

FRAME_SIZE = 16
VERTICAL_BRIDGE_LOW = 6
VERTICAL_BRIDGE_HIGH = 8
HORIZONTAL_BRIDGE_LOW = 8
HORIZONTAL_BRIDGE_HIGH = 9


def stable_hash(*values: int) -> int:
    value = 0x9E3779B9
    for item in values:
        value ^= (item + 0x85EBCA6B + (value << 6) + (value >> 2)) & 0xFFFFFFFF
        value = (value * 0x7FEB352D) & 0xFFFFFFFF
        value ^= value >> 15
    return value & 0xFFFFFFFF


def add_irregular_cluster(
    cells: set[tuple[int, int]],
    center_x: int,
    center_y: int,
    radius_x: int,
    radius_y: int,
    seed: int,
) -> None:
    """Add a compact, non-circular leaf lobe without touching arbitrary sides."""
    for y in range(max(1, center_y - radius_y), min(15, center_y + radius_y + 1)):
        for x in range(max(1, center_x - radius_x), min(15, center_x + radius_x + 1)):
            dx = x - center_x
            dy = y - center_y
            distance = (dx * dx) / ((radius_x + 0.35) ** 2) + (dy * dy) / ((radius_y + 0.35) ** 2)
            if distance > 1.0:
                continue

            # Removing a few outer pixels breaks the procedural-disc silhouette,
            # while the lobe core remains solid and legible at native resolution.
            edge_pixel = distance > 0.56
            if edge_pixel and stable_hash(x, y, seed) % 7 == 0:
                continue
            cells.add((x, y))

    # One deterministic tip gives every lobe a leafy, non-circular contour.
    tip_direction = stable_hash(seed, center_x, center_y) % 4
    tips = ((center_x, center_y - radius_y), (center_x + radius_x, center_y),
            (center_x, center_y + radius_y), (center_x - radius_x, center_y))
    tip_x, tip_y = tips[tip_direction]
    if 1 <= tip_x <= 14 and 1 <= tip_y <= 14:
        cells.add((tip_x, tip_y))


def add_path(cells: set[tuple[int, int]], points: tuple[tuple[int, int], ...], horizontal: bool) -> None:
    """Rasterize a two-pixel-wide stepped organic bridge between leaf lobes."""
    for index in range(len(points) - 1):
        x, y = points[index]
        target_x, target_y = points[index + 1]
        while (x, y) != (target_x, target_y):
            cells.add((x, y))
            if horizontal:
                cells.add((x, min(15, y + 1)))
            else:
                cells.add((min(15, x + 1), y))
            if x != target_x:
                x += 1 if target_x > x else -1
            elif y != target_y:
                y += 1 if target_y > y else -1
        cells.add((target_x, target_y))
        if horizontal:
            cells.add((target_x, min(15, target_y + 1)))
        else:
            cells.add((min(15, target_x + 1), target_y))


SIDE_CENTERS = (
    ((1, (7, 4)), (2, (11, 8)), (4, (8, 11)), (8, (4, 8))),
    ((1, (8, 4)), (2, (11, 9)), (4, (7, 11)), (8, (4, 7))),
    ((1, (7, 5)), (2, (10, 8)), (4, (8, 10)), (8, (5, 9))),
)


def add_declared_bridges(cells: set[tuple[int, int]], mask: int, variant: int) -> None:
    """Join declared sides through narrow, aligned, seam-safe leaf bridges."""
    side_centers = dict(SIDE_CENTERS[variant])
    if mask & 1:  # top
        add_path(cells, ((VERTICAL_BRIDGE_LOW, 0), (VERTICAL_BRIDGE_LOW, 2), side_centers[1]), horizontal=False)
    if mask & 2:  # right
        add_path(cells, ((15, HORIZONTAL_BRIDGE_LOW), (13, HORIZONTAL_BRIDGE_LOW), side_centers[2]), horizontal=True)
    if mask & 4:  # bottom
        add_path(cells, ((VERTICAL_BRIDGE_LOW, 15), (VERTICAL_BRIDGE_LOW, 13), side_centers[4]), horizontal=False)
    if mask & 8:  # left
        add_path(cells, ((0, HORIZONTAL_BRIDGE_LOW), (2, HORIZONTAL_BRIDGE_LOW), side_centers[8]), horizontal=True)

    # Every declared side exposes the same socket as its opposite side, so masks
    # meet without stretching, overlap, or a one-pixel dark grid seam. Vertical
    # joins use a three-pixel pad while horizontal joins use a two-pixel pad;
    # this breaks the repeated diamond rhythm without compromising adjacency.
    if mask & 1:
        cells.update((x, 0) for x in range(VERTICAL_BRIDGE_LOW, VERTICAL_BRIDGE_HIGH + 1))
    if mask & 2:
        cells.update((15, y) for y in range(HORIZONTAL_BRIDGE_LOW, HORIZONTAL_BRIDGE_HIGH + 1))
    if mask & 4:
        cells.update((x, 15) for x in range(VERTICAL_BRIDGE_LOW, VERTICAL_BRIDGE_HIGH + 1))
    if mask & 8:
        cells.update((0, y) for y in range(HORIZONTAL_BRIDGE_LOW, HORIZONTAL_BRIDGE_HIGH + 1))


def select_cluster_centers(mask: int, species: int, variant: int) -> tuple[tuple[int, int], ...]:
    """Choose two to four separated lobes while retaining generous air holes."""
    central_positions = ((6, 7), (9, 8), (7, 9))
    center = central_positions[variant]
    side_centers = [side_center for bit, side_center in SIDE_CENTERS[variant] if mask & bit]
    if len(side_centers) == 4:
        # Mask 15 still retains one asymmetric open quadrant; its fourth side
        # reaches the central lobe through a narrow connector instead of adding
        # a fifth leaf pad or filling the square.
        del side_centers[stable_hash(mask, species, variant, 5) % 4]
    selected = [center, *side_centers]
    candidates = [
        (5, 5), (10, 5), (5, 10), (10, 10),
        (7, 5), (10, 8), (6, 9), (8, 11),
    ]
    offset = stable_hash(mask, species, variant, 3) % len(candidates)
    stride = (3, 5, 7)[variant]
    desired = max(len(selected), 3 + (mask.bit_count() >= 3))
    desired = min(4, desired)
    cursor = offset
    while len(selected) < desired:
        candidate = candidates[cursor % len(candidates)]
        cursor += stride
        if candidate in selected:
            continue
        if all(abs(candidate[0] - other[0]) + abs(candidate[1] - other[1]) >= 4 for other in selected):
            selected.append(candidate)
    return tuple(selected)


def connection_continues(mask: int, x: int, y: int) -> bool:
    if y < 0:
        return bool(mask & 1) and VERTICAL_BRIDGE_LOW <= x <= VERTICAL_BRIDGE_HIGH
    if x >= FRAME_SIZE:
        return bool(mask & 2) and HORIZONTAL_BRIDGE_LOW <= y <= HORIZONTAL_BRIDGE_HIGH
    if y >= FRAME_SIZE:
        return bool(mask & 4) and VERTICAL_BRIDGE_LOW <= x <= VERTICAL_BRIDGE_HIGH
    if x < 0:
        return bool(mask & 8) and HORIZONTAL_BRIDGE_LOW <= y <= HORIZONTAL_BRIDGE_HIGH
    return False


def build_mask(mask: int, species: int, variant: int) -> Image.Image:
    centers = select_cluster_centers(mask, species, variant)
    cells: set[tuple[int, int]] = set()

    for index, (center_x, center_y) in enumerate(centers):
        seed = stable_hash(mask, species, variant, index, 17)
        if species == 0:
            radius_x, radius_y = (4, 4) if index == 0 else ((4, 3) if seed % 3 == 0 else (3, 3))
        elif species == 1:
            radius_x, radius_y = (4, 4) if index == 0 else (4, 3)
        else:
            radius_x, radius_y = (4, 3) if index == 0 else ((3, 3) if seed % 3 else (3, 4))
        add_irregular_cluster(cells, center_x, center_y, radius_x, radius_y, seed)

    # A compact hub joins the crown pads underneath their silhouettes. The
    # connector is never expanded into a rectangular interior fill.
    hub = centers[0]
    for index, cluster in enumerate(centers[1:], start=1):
        bend = (cluster[0], hub[1]) if (index + mask + species) % 2 else (hub[0], cluster[1])
        add_path(cells, (cluster, bend, hub), horizontal=abs(cluster[0] - hub[0]) >= abs(cluster[1] - hub[1]))

    add_declared_bridges(cells, mask, variant)

    # Exposed sides retain at least a full transparent pixel boundary. Corners
    # are always empty, including mask 15, preventing rectangular canopy slabs.
    cells = {
        (x, y)
        for x, y in cells
        if not (y == 0 and not mask & 1)
        and not (x == 15 and not mask & 2)
        and not (y == 15 and not mask & 4)
        and not (x == 0 and not mask & 8)
        and (x, y) not in ((0, 0), (15, 0), (0, 15), (15, 15))
    }

    palette = SPECS[species]["palette"]
    image = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
    pixels = image.load()
    for x, y in cells:
        connection_socket = (
            (y == 0 and mask & 1 and VERTICAL_BRIDGE_LOW <= x <= VERTICAL_BRIDGE_HIGH)
            or (x == 15 and mask & 2 and HORIZONTAL_BRIDGE_LOW <= y <= HORIZONTAL_BRIDGE_HIGH)
            or (y == 15 and mask & 4 and VERTICAL_BRIDGE_LOW <= x <= VERTICAL_BRIDGE_HIGH)
            or (x == 0 and mask & 8 and HORIZONTAL_BRIDGE_LOW <= y <= HORIZONTAL_BRIDGE_HIGH)
        )
        edge = False
        for offset_x, offset_y in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            neighbor_x = x + offset_x
            neighbor_y = y + offset_y
            if (neighbor_x, neighbor_y) in cells:
                continue
            if connection_continues(mask, neighbor_x, neighbor_y):
                continue
            edge = True
            break

        if connection_socket:
            color_index = 2
        elif edge:
            color_index = 0
        else:
            noise = stable_hash(x, y, mask, species, variant, 29)
            sunward = x + y <= 14
            color_index = 3 if sunward and noise % 5 == 0 else 2 if noise % 4 else 1
        pixels[x, y] = palette[color_index]
    return image


def sample_canopy(species: int, variant: int) -> Image.Image:
    """Compose separated four-tile crown pads matching the runtime silhouette."""
    rows = (
        "..#.....#...",
        ".##.....##..",
        "..#.....#...",
        "............",
        "....#.....#.",
        "...##....##.",
        "....#.....#.",
    )
    occupied = {(x, y) for y, row in enumerate(rows) for x, value in enumerate(row) if value == "#"}
    image = Image.new("RGBA", (len(rows[0]) * 16, len(rows) * 16), (0, 0, 0, 0))
    for x, y in sorted(occupied, key=lambda point: (point[1], point[0])):
        mask = 0
        if (x, y - 1) in occupied:
            mask |= 1
        if (x + 1, y) in occupied:
            mask |= 2
        if (x, y + 1) in occupied:
            mask |= 4
        if (x - 1, y) in occupied:
            mask |= 8
        image.alpha_composite(build_mask(mask, species, variant), (x * 16, y * 16))
    return image


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    sheets: list[tuple[int, int, Image.Image]] = []
    assets = []
    for species, spec in enumerate(SPECS):
        for variant, variant_name in enumerate(VARIANT_NAMES):
            sheet = Image.new("RGBA", (256, 16), (0, 0, 0, 0))
            for mask in range(16):
                sheet.alpha_composite(build_mask(mask, species, variant), (mask * 16, 0))
            filename = f"{spec['filename_prefix']}_v2{variant_name}_autotile.png"
            sprite_id = f"{spec['sprite_prefix']}_v2{variant_name}_autotile"
            path = OUTPUT / filename
            sheet.save(path, optimize=False)
            sheets.append((species, variant, sheet))
            palette = spec["palette"]
            assets.append({
                "spriteId": sprite_id,
                "path": str(path.relative_to(ROOT)).replace("\\", "/"),
                "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                "dimensions": [256, 16],
                "frames": 16,
                "frameSize": [16, 16],
                "variant": variant_name,
                "alpha": "binary",
                "generator": "art_direction/tools/generate_loose_foliage_v2.py",
                "license": "YjsE-Project-Owned",
                "design": spec["shape"],
                "palette": ["#%02x%02x%02x" % color[:3] for color in palette],
            })

    preview = Image.new("RGBA", (1280, 1360), (17, 20, 27, 255))
    for species in range(len(SPECS)):
        section_y = 8 + species * 448
        for variant in range(len(VARIANT_NAMES)):
            sheet = sheets[species * 3 + variant][2]
            preview.alpha_composite(sheet.resize((1024, 64), Image.Resampling.NEAREST), (128, section_y + variant * 72))
            canopy = sample_canopy(species, variant).resize((384, 224), Image.Resampling.NEAREST)
            preview.alpha_composite(canopy, (32 + variant * 416, section_y + 224))
    preview.save(PREVIEW, optimize=False)

    PROVENANCE.write_text(json.dumps({
        "version": 2,
        "generator": "deterministic Pillow pixel primitives",
        "sourceBrief": "asset_briefs/loose_foliage_v2_briefs.json",
        "visualDirectionReference": "art_direction/generated_sources/tree_v3_concept_source.png",
        "referenceUse": "Composition reference only: separated crown pads, visible branch air, and species silhouette. Runtime pixels are original deterministic native-size output and are not sampled or resized from the reference.",
        "preview": str(PREVIEW.relative_to(ROOT)).replace("\\", "/"),
        "designIntent": "Sparse original side-view foliage; two-to-four irregular lobes per frame, narrow declared bridges, large transparent air pockets, no full-square interior mask.",
        "assets": assets,
    }, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
