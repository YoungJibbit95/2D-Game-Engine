#!/usr/bin/env python3
"""Generate exact, binary-alpha loose foliage autotile sheets and preview."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets" / "LooseFoliageV1"
PREVIEW = ROOT / "art_direction" / "loose_foliage_v1_contact_sheet.png"
PROVENANCE = ROOT / "art_direction" / "loose_foliage_v1_provenance.json"

SPECS = (
    ("tiles/loose_oak_leaves_v1_autotile", "loose_oak_leaves_v1_autotile.png", ((25, 50, 30, 255), (47, 96, 46, 255), (80, 143, 66, 255), (137, 181, 82, 255))),
    ("tiles/loose_autumn_leaves_v1_autotile", "loose_autumn_leaves_v1_autotile.png", ((68, 36, 24, 255), (137, 61, 29, 255), (205, 106, 38, 255), (244, 165, 57, 255))),
    ("tiles/loose_marsh_leaves_v1_autotile", "loose_marsh_leaves_v1_autotile.png", ((18, 47, 43, 255), (33, 83, 66, 255), (57, 124, 87, 255), (112, 158, 93, 255))),
)


def stable_hash(x: int, y: int, mask: int, species: int) -> int:
    value = (x * 0x45D9F3B) ^ (y * 0x119DE1F3) ^ (mask * 0x9E3779B9) ^ (species * 0x85EBCA6B)
    value ^= value >> 16
    value = (value * 0x7FEB352D) & 0xFFFFFFFF
    value ^= value >> 15
    return value & 0xFFFFFFFF


def add_disc(cells: set[tuple[int, int]], cx: int, cy: int, radius_x: int, radius_y: int) -> None:
    for y in range(max(0, cy - radius_y), min(16, cy + radius_y + 1)):
        for x in range(max(0, cx - radius_x), min(16, cx + radius_x + 1)):
            nx = (x - cx) / max(1, radius_x)
            ny = (y - cy) / max(1, radius_y)
            if nx * nx + ny * ny <= 1.05:
                cells.add((x, y))


def build_mask(mask: int, species: int) -> Image.Image:
    cells: set[tuple[int, int]] = set()
    # Broad overlapping leaf masses keep a connected canopy organic at world scale.
    # The earlier small islands repeated one empty square per tile and exposed the
    # 16-pixel grid; dense interiors plus only tiny irregular air pockets read as
    # foliage while the exposed edges can still break into individual clusters.
    add_disc(cells, 6 + ((species + mask) & 1), 7, 6, 5)
    add_disc(cells, 11, 8 + ((mask >> 2) & 1), 5, 4)
    add_disc(cells, 7, 12, 5, 3)
    if mask.bit_count() >= 3:
        add_disc(cells, 8, 8, 7, 7)

    # Interior autotiles must read as one canopy mass instead of a checkerboard
    # of round leaf pom-poms. Connected sides therefore carry broad, jagged fill;
    # only exposed sides retain the smaller individual clusters.
    connected_sides = mask.bit_count()
    if connected_sides == 4:
        cells.update((x, y) for y in range(16) for x in range(16))
    elif connected_sides == 3:
        cells.update((x, y) for y in range(2, 14) for x in range(2, 14))
    elif connected_sides == 2:
        cells.update((x, y) for y in range(4, 13) for x in range(3, 14))

    if mask & 1:  # top
        add_disc(cells, 8, 3, 3, 4)
        cells.update((x, 0) for x in range(6, 11))
    if mask & 2:  # right
        add_disc(cells, 12, 8, 4, 3)
        cells.update((15, y) for y in range(6, 11))
    if mask & 4:  # bottom
        add_disc(cells, 8, 12, 3, 4)
        cells.update((x, 15) for x in range(6, 11))
    if mask & 8:  # left
        add_disc(cells, 3, 8, 4, 3)
        cells.update((0, y) for y in range(6, 11))

    # Exposed boundaries stay transparent and repeated interiors get sparse,
    # pixel-sized negative space rather than one obvious hole per tile.
    cells = {
        (x, y)
        for x, y in cells
        if not (x == 0 and not mask & 8)
        and not (x == 15 and not mask & 2)
        and not (y == 0 and not mask & 1)
        and not (y == 15 and not mask & 4)
        and not (
            x in (0, 1)
            and y in (0, 1)
            and (not mask & 8 or not mask & 1)
        )
        and not (
            x in (14, 15)
            and y in (0, 1)
            and (not mask & 2 or not mask & 1)
        )
        and not (
            x in (0, 1)
            and y in (14, 15)
            and (not mask & 8 or not mask & 4)
        )
        and not (
            x in (14, 15)
            and y in (14, 15)
            and (not mask & 2 or not mask & 4)
        )
        and not (
            3 <= x <= 13
            and 3 <= y <= 13
            and stable_hash(x, y, mask, species) % (83 if connected_sides >= 3 else 53) == 0
        )
    }

    # Preserve the sheet-level transparent-corner audit without reopening a
    # visible gap across the connected interior frame.
    if mask == 15:
        cells.discard((15, 0))
        cells.discard((15, 15))

    palette = SPECS[species][2]
    image = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    pixels = image.load()

    def continues_into_neighbor(x: int, y: int) -> bool:
        """Treat an authored connection as filled beyond this source rectangle.

        Without this, every opaque boundary pixel is shaded as a silhouette edge
        even when the adjacent autotile continues the canopy. The result is a
        very visible 16-pixel checkerboard. Only declared connections suppress
        the edge; exposed crown boundaries retain their dark outline.
        """
        if x < 0:
            return bool(mask & 8)
        if x >= 16:
            return bool(mask & 2)
        if y < 0:
            return bool(mask & 1)
        if y >= 16:
            return bool(mask & 4)
        return (x, y) in cells

    for x, y in cells:
        edge = any(
            not continues_into_neighbor(x + ox, y + oy)
            for ox, oy in ((0, -1), (1, 0), (0, 1), (-1, 0))
        )
        if edge:
            color_index = 0
        else:
            noise = stable_hash(x, y, mask, species)
            color_index = 3 if (x + y < 13 and noise % 7 == 0) else 2 if noise % 3 else 1
        pixels[x, y] = palette[color_index]
    return image


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    sheets: list[Image.Image] = []
    assets = []
    for species, (sprite_id, filename, palette) in enumerate(SPECS):
        sheet = Image.new("RGBA", (256, 16), (0, 0, 0, 0))
        for mask in range(16):
            sheet.alpha_composite(build_mask(mask, species), (mask * 16, 0))
        path = OUTPUT / filename
        sheet.save(path, optimize=False)
        sheets.append(sheet)
        assets.append({
            "spriteId": sprite_id,
            "path": str(path.relative_to(ROOT)).replace("\\", "/"),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "dimensions": [256, 16],
            "alpha": "binary",
            "generator": "art_direction/tools/generate_loose_foliage_v1.py",
            "license": "YjsE-Project-Owned",
            "palette": ["#%02x%02x%02x" % color[:3] for color in palette],
        })

    preview = Image.new("RGBA", (768, 176), (18, 20, 28, 255))
    for row, sheet in enumerate(sheets):
        enlarged = sheet.resize((768, 48), Image.Resampling.NEAREST)
        preview.alpha_composite(enlarged, (0, 8 + row * 56))
    preview.save(PREVIEW, optimize=False)
    PROVENANCE.write_text(json.dumps({
        "version": 1,
        "generator": "deterministic Pillow pixel primitives",
        "sourceBrief": "asset_briefs/loose_foliage_v1_briefs.json",
        "preview": str(PREVIEW.relative_to(ROOT)).replace("\\", "/"),
        "assets": assets,
    }, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
