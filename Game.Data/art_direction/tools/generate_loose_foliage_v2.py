#!/usr/bin/env python3
"""Generate connected, seam-safe LooseFoliageV2 runtime autotiles.

The world planner already creates separated crown pads.  These frames therefore
describe the *surface inside one pad*: a connected leafy mass, not a collection
of miniature leaf balls repeated once per tile.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets" / "LooseFoliageV2"
PREVIEW = ROOT / "art_direction" / "loose_foliage_v2_contact_sheet.png"
PROVENANCE = ROOT / "art_direction" / "loose_foliage_v2_provenance.json"

SPECS = (
    {
        "sprite_prefix": "tiles/loose_oak_leaves",
        "filename_prefix": "loose_oak_leaves",
        "palette": ((28, 57, 34, 255), (49, 98, 48, 255), (82, 143, 68, 255), (142, 183, 84, 255)),
        "shape": "broad oak foliage masses with round, lightly pointed outer lobes",
    },
    {
        "sprite_prefix": "tiles/loose_autumn_leaves",
        "filename_prefix": "loose_autumn_leaves",
        "palette": ((78, 42, 25, 255), (142, 65, 31, 255), (207, 109, 40, 255), (246, 171, 60, 255)),
        "shape": "asymmetric amber foliage masses with warm sunward highlights",
    },
    {
        "sprite_prefix": "tiles/loose_marsh_leaves",
        "filename_prefix": "loose_marsh_leaves",
        "palette": ((19, 51, 46, 255), (34, 84, 69, 255), (59, 124, 89, 255), (113, 159, 96, 255)),
        "shape": "lower, hanging teal-green foliage masses with tapered tips",
    },
)

VARIANT_NAMES = ("a", "b", "c")
FRAME_SIZE = 16

# Every declared connection owns the complete shared edge.  Narrow sockets
# create ring holes at 2x2 junctions and alpha mismatches between otherwise
# valid neighboring masks; full edges make all sixteen mask combinations seam
# compatible while exposed sides still receive the organic contour trimming.
VERTICAL_SOCKET = range(0, 16)
HORIZONTAL_SOCKET = range(0, 16)


def stable_hash(*values: int) -> int:
    value = 0x9E3779B9
    for item in values:
        value ^= (item + 0x85EBCA6B + (value << 6) + (value >> 2)) & 0xFFFFFFFF
        value = (value * 0x7FEB352D) & 0xFFFFFFFF
        value ^= value >> 15
    return value & 0xFFFFFFFF


def base_mass(species: int, variant: int) -> set[tuple[int, int]]:
    """Create one solid, irregular leaf body that leaves a soft outer margin."""
    cells: set[tuple[int, int]] = set()
    center_x = (7.0, 8.0, 7.4)[variant]
    center_y = (7.2, 7.8, 8.2)[variant]
    radius_x = (7.8, 7.7, 7.9)[variant]
    radius_y = (7.5, 7.7, 7.4)[variant]
    if species == 1:
        center_x += 0.35
        radius_y += 0.2
    elif species == 2:
        center_y += 0.65
        radius_y -= 0.15

    for y in range(FRAME_SIZE):
        for x in range(FRAME_SIZE):
            dx = (x - center_x) / radius_x
            dy = (y - center_y) / radius_y
            # Low-frequency, deterministic contour wobble.  It changes only
            # the outside contour; the interior remains one readable mass.
            contour = 1.0
            contour += (((stable_hash(x // 2, y // 2, species, variant, 11) >> 5) % 5) - 2) * 0.022
            if dx * dx + dy * dy <= contour:
                cells.add((x, y))

    # Hand-sized contour bites and leaf tips distinguish the variants without
    # punching repeated holes through the middle of the canopy.
    bites = (
        ((0, 2), (1, 2), (14, 13)),
        ((14, 1), (15, 2), (1, 13)),
        ((0, 11), (14, 3), (15, 3)),
    )[variant]
    cells.difference_update(bites)
    tips = (
        ((2, 3), (13, 5), (11, 13)),
        ((3, 2), (14, 8), (4, 13)),
        ((1, 7), (12, 3), (9, 14)),
    )[variant]
    cells.update(tips)

    if species == 2:
        cells.update(((4, 14), (10, 14), (12, 13)))
        cells.discard((2, 3))
    return cells


def add_connected_side(cells: set[tuple[int, int]], bit: int) -> None:
    """Extend the mass through a broad taper to one exact seam socket."""
    if bit == 1:  # top
        for y, extent in ((0, (0, 15)), (1, (1, 14)), (2, (2, 13))):
            cells.update((x, y) for x in range(extent[0], extent[1] + 1))
    elif bit == 2:  # right
        for x, extent in ((15, (0, 15)), (14, (1, 14)), (13, (2, 13))):
            cells.update((x, y) for y in range(extent[0], extent[1] + 1))
    elif bit == 4:  # bottom
        for y, extent in ((15, (0, 15)), (14, (1, 14)), (13, (2, 13))):
            cells.update((x, y) for x in range(extent[0], extent[1] + 1))
    elif bit == 8:  # left
        for x, extent in ((0, (0, 15)), (1, (1, 14)), (2, (2, 13))):
            cells.update((x, y) for y in range(extent[0], extent[1] + 1))


def add_connected_corners(cells: set[tuple[int, int]], mask: int) -> None:
    """Fill the quadrant where two connected sides meet.

    Without this corner context, four otherwise valid side sockets leave a
    regular ring-shaped hole at every 2x2 tile junction.  These stepped wedges
    make multi-tile pads read as one mass while retaining an irregular contour
    at concave L-shaped pad corners.
    """
    if mask & 1 and mask & 2:  # north-east
        for y, start_x in ((0, 13), (1, 12), (2, 11), (3, 10), (4, 10), (5, 11)):
            cells.update((x, y) for x in range(start_x, 16))
    if mask & 2 and mask & 4:  # south-east
        for y, start_x in ((10, 11), (11, 10), (12, 10), (13, 11), (14, 12), (15, 13)):
            cells.update((x, y) for x in range(start_x, 16))
    if mask & 4 and mask & 8:  # south-west
        for y, end_x in ((10, 4), (11, 5), (12, 5), (13, 4), (14, 3), (15, 2)):
            cells.update((x, y) for x in range(0, end_x + 1))
    if mask & 8 and mask & 1:  # north-west
        for y, end_x in ((0, 2), (1, 3), (2, 4), (3, 5), (4, 5), (5, 4)):
            cells.update((x, y) for x in range(0, end_x + 1))


def socket_contains(mask: int, bit: int, coordinate: int) -> bool:
    """Return the mask-aware seam span, including adjacent corner fills."""
    if not mask & bit:
        return False
    if bit in (1, 4):
        return (
            coordinate in VERTICAL_SOCKET
            or (mask & 8 and coordinate <= 5)
            or (mask & 2 and coordinate >= 10)
        )
    return (
        coordinate in HORIZONTAL_SOCKET
        or (mask & 1 and coordinate <= 5)
        or (mask & 4 and coordinate >= 10)
    )


def contour_margins(
    start_connected: bool,
    end_connected: bool,
    species: int,
    variant: int,
    side: int,
) -> tuple[int, ...]:
    """Build one pad-scale edge profile instead of a repeated tile pillow.

    A deep 6-8px pullback is used only at a *global* pad corner, where the
    neighboring side is absent too.  At a junction to another tile on the same
    outer edge, the endpoint stays within 1-3px of the edge.  Consequently a
    3-5 tile crown has one rounded/asymmetric silhouette rather than one round
    blob per tile.
    """
    center = (
        (1, 1, 1, 2),
        (2, 1, 1, 1),
        (1, 2, 1, 1),
    )[variant][(side.bit_length() - 1) % 4]
    if species == 2 and side == 4:
        center = 1

    start = 1 if start_connected else (7, 6, 7)[variant]
    end = 1 if end_connected else (6, 7, 6)[variant]
    if species == 1:
        # Amber crowns are a touch more asymmetric without changing sockets.
        start = min(8, start + (not start_connected and variant == 0))
        end = max(2, end - (not end_connected and variant == 0))

    values: list[int] = []
    for coordinate in range(16):
        if coordinate <= 7:
            weight = coordinate / 7.0
            value = round(start * (1.0 - weight) + center * weight)
        else:
            weight = (coordinate - 8) / 7.0
            value = round(center * (1.0 - weight) + end * weight)
        values.append(max(1, min(8, value)))

    # Several restrained one-pixel steps create a leafy fringe without turning
    # the outer edge into repeating circular tile pillows.
    for salt, delta in ((73, 1), (97, -1), (131, 1)):
        wobble_at = 2 + stable_hash(species, variant, side, salt) % 12
        values[wobble_at] = max(1, min(8, values[wobble_at] + delta))
    return tuple(values)


def trim_exposed_side(
    cells: set[tuple[int, int]],
    bit: int,
    mask: int,
    species: int,
    variant: int,
) -> None:
    """Trim an exposed side according to its global pad-corner context."""
    if bit in (1, 4):
        margins = contour_margins(
            start_connected=bool(mask & 8),
            end_connected=bool(mask & 2),
            species=species,
            variant=variant,
            side=bit,
        )
        if bit == 1:
            cells.intersection_update((x, y) for y in range(16) for x in range(16) if y >= margins[x])
        else:
            cells.intersection_update((x, y) for y in range(16) for x in range(16) if y <= 15 - margins[x])
        return

    margins = contour_margins(
        start_connected=bool(mask & 1),
        end_connected=bool(mask & 4),
        species=species,
        variant=variant,
        side=bit,
    )
    if bit == 8:
        cells.intersection_update((x, y) for y in range(16) for x in range(16) if x >= margins[y])
    else:
        cells.intersection_update((x, y) for y in range(16) for x in range(16) if x <= 15 - margins[y])


def connected_beyond(mask: int, x: int, y: int) -> bool:
    if y < 0:
        return socket_contains(mask, 1, x)
    if x >= FRAME_SIZE:
        return socket_contains(mask, 2, y)
    if y >= FRAME_SIZE:
        return socket_contains(mask, 4, x)
    if x < 0:
        return socket_contains(mask, 8, y)
    return False


def boundary_allowed(mask: int, x: int, y: int) -> bool:
    """Keep a boundary pixel when either touching side is connected."""
    if (x, y) == (0, 0):
        return bool(mask & (1 | 8))
    if (x, y) == (15, 0):
        return bool(mask & (1 | 2))
    if (x, y) == (15, 15):
        return bool(mask & (2 | 4))
    if (x, y) == (0, 15):
        return bool(mask & (4 | 8))
    if y == 0:
        return bool(mask & 1)
    if x == 15:
        return bool(mask & 2)
    if y == 15:
        return bool(mask & 4)
    if x == 0:
        return bool(mask & 8)
    return True


def build_cells(mask: int, species: int, variant: int) -> set[tuple[int, int]]:
    cells = base_mass(species, variant)
    for bit in (1, 2, 4, 8):
        if not mask & bit:
            trim_exposed_side(cells, bit, mask, species, variant)
    # Add sockets after exposed-side trimming.  A top socket, for example,
    # must not be narrowed by the left/right contour margins of the same tile.
    for bit in (1, 2, 4, 8):
        if mask & bit:
            add_connected_side(cells, bit)
    add_connected_corners(cells, mask)

    # Exact sockets are a contract.  Remove all other boundary pixels so a
    # neighbor sees a continuous organic edge instead of a square tile seam.
    cells = {
        (x, y) for x, y in cells
        if boundary_allowed(mask, x, y)
    }
    carve_controlled_gaps(cells, mask, species, variant)
    return cells


def carve_controlled_gaps(
    cells: set[tuple[int, int]],
    mask: int,
    species: int,
    variant: int,
) -> None:
    """Open one or two tiny interior glints, never a seam or junction hole."""
    if mask.bit_count() != 3:
        return
    candidates = (
        ((5, 5), (10, 9), (7, 11), (11, 5)),
        ((10, 5), (5, 9), (8, 11), (4, 6)),
        ((6, 10), (10, 6), (5, 5), (11, 10)),
    )[variant]
    # A crown pad can contain many tiles, so holes are reserved for a small
    # subset of boundary T-junctions.  The repeated mask-15 interior frame is
    # deliberately hole-free; otherwise every tile would stamp the same dot.
    if stable_hash(mask, species, variant, 223) % 3:
        return
    desired = 1
    offset = stable_hash(mask, species, variant, 211) % len(candidates)
    carved = 0
    for index in range(len(candidates)):
        x, y = candidates[(offset + index) % len(candidates)]
        if all((x + dx, y + dy) in cells for dx, dy in ((0, 0), (0, -1), (1, 0), (0, 1), (-1, 0))):
            cells.remove((x, y))
            carved += 1
            if carved == desired:
                break


def volume_color_index(x: int, y: int, variant: int) -> int:
    """Author three radically different, calm macro-volume compositions.

    These are deliberately broad, connected masses rather than one guaranteed
    spot at the same location in every frame.  Per-tile A/B/C selection and
    horizontal mirroring then create canopy-scale variation without dithering.
    """
    if variant == 0:
        # A: one asymmetrical shadow bank rising from the lower-left, plus a
        # short broken highlight along the upper rim.
        shadow_right = (4, 5, 6, 8, 9, 10, 10, 9, 8)[y - 7] if y >= 7 else -1
        if y >= 7 and x <= shadow_right and not (y in (8, 12) and x == shadow_right):
            return 1
        highlight_ranges = ((4, 9), (3, 11), (5, 10))
        if y <= 2 and highlight_ranges[y][0] <= x <= highlight_ranges[y][1]:
            return 3
        return 2

    if variant == 1:
        # B: two overlapping right/upper shadow shelves.  There is no centered
        # highlight motif; the midtone remains open through the lower-left.
        upper_left = (9, 7, 6, 6, 7, 8, 10)
        in_upper = y <= 6 and x >= upper_left[y]
        middle_left = (11, 9, 8, 8, 9, 10, 11, 12)
        in_middle = 4 <= y <= 11 and x >= middle_left[y - 4]
        if (in_upper or in_middle) and not (x == 14 and y in (2, 9)):
            return 1
        return 2

    # C: mostly quiet midtone, a broad upper-left light mass, and one small
    # lower shadow.  Its balance is intentionally the inverse of A and B.
    highlight_right = (8, 10, 11, 11, 10, 9, 7, 5)
    if y <= 7 and x <= highlight_right[y] and not (x == highlight_right[y] and y in (2, 6)):
        return 3
    shadow_ranges = ((9, 11), (8, 12), (8, 13), (10, 12))
    if y >= 12 and shadow_ranges[y - 12][0] <= x <= shadow_ranges[y - 12][1]:
        return 1
    return 2


def build_mask(mask: int, species: int, variant: int) -> Image.Image:
    cells = build_cells(mask, species, variant)
    palette = SPECS[species]["palette"]
    image = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
    pixels = image.load()
    internal_gaps = {
        (x, y)
        for y in range(2, 14)
        for x in range(2, 14)
        if (x, y) not in cells
        and all((x + dx, y + dy) in cells for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)))
    }

    for x, y in cells:
        edge = False
        borders_gap = False
        for offset_x, offset_y in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            neighbor_x, neighbor_y = x + offset_x, y + offset_y
            if (neighbor_x, neighbor_y) in cells or connected_beyond(mask, neighbor_x, neighbor_y):
                continue
            edge = True
            borders_gap |= (neighbor_x, neighbor_y) in internal_gaps
            break

        if edge:
            color_index = 1 if borders_gap else 0
        else:
            color_index = volume_color_index(x, y, variant)
        pixels[x, y] = palette[color_index]
    return image


V5_SAMPLE_VARIATIONS = (0, 5, 10)


def v5_branch_y(start_y: int, step: int) -> int:
    return start_y - step // 2


def in_v5_branch_crown(dx: int, dy: int, variation: int, salt: int) -> bool:
    if dy < -2 or dy > 1 or abs(dx) > 2:
        return False
    direction = -1 if (variation + salt) & 1 == 0 else 1
    if dy == -2:
        return direction - 1 <= dx <= direction + 1
    if dy == -1:
        return dx != -direction * 2
    if dy == 0:
        return dx != direction * 2
    return abs(dx) <= 1


def in_v5_main_crown(dx: int, dy: int, variation: int) -> bool:
    if dy < -3 or dy > 1 or abs(dx) > 3:
        return False
    lean = -1 if variation & 1 == 0 else 1
    if dy == -3:
        return lean - 1 <= dx <= lean + 1
    if dy == -2:
        return abs(dx) <= 2 or dx == lean * 3
    if dy == -1:
        return dx != -lean * 3
    if dy == 0:
        bitten_side = -lean if (variation >> 1) & 1 == 0 else lean
        return dx != bitten_side * 3
    return abs(dx) <= 1 or dx == lean * 2


def v5_leaf_positions(variation: int, height: int = 9) -> set[tuple[int, int]]:
    """Mirror the current V5 planner crown geometry for contact-sheet QA."""
    primary_direction = -1 if variation & 1 == 0 else 1
    upper_start = min(height - 2, 3 + ((variation >> 1) & 1))
    primary_tip_y = v5_branch_y(upper_start, 3)
    secondary_length = 2 + ((variation >> 3) & 1)
    middle_start = min(height - 2, 4 + ((variation >> 2) & 1))
    secondary_tip_y = v5_branch_y(middle_start, secondary_length)
    crown_lean = 0 if (variation >> 2) & 1 == 0 else -primary_direction

    occupied: set[tuple[int, int]] = set()
    for dy in range(-3, height - 1):
        for dx in range(-6, 7):
            in_main = in_v5_main_crown(dx - crown_lean, dy, variation)
            in_primary = in_v5_branch_crown(
                dx - primary_direction * 3,
                dy - (primary_tip_y - 1),
                variation,
                4,
            )
            in_secondary = in_v5_branch_crown(
                dx + primary_direction * secondary_length,
                dy - (secondary_tip_y - 1),
                variation,
                8,
            )
            if in_main or in_primary or in_secondary:
                occupied.add((dx, dy))
    return occupied


def sample_canopy(species: int, variant: int) -> Image.Image:
    raw_occupied = v5_leaf_positions(V5_SAMPLE_VARIATIONS[variant])
    min_x = min(x for x, _ in raw_occupied)
    max_x = max(x for x, _ in raw_occupied)
    min_y = min(y for _, y in raw_occupied)
    max_y = max(y for _, y in raw_occupied)
    occupied = {(x - min_x, y - min_y) for x, y in raw_occupied}
    image = Image.new("RGBA", ((max_x - min_x + 1) * 16, (max_y - min_y + 1) * 16), (0, 0, 0, 0))
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
        band = y // 2
        lobe_width = 2 + stable_hash(variant, band, 307) % 2
        lobe = (x + (band & 1)) // lobe_width
        asset_variant = stable_hash(variant, band, lobe, 311) % 3
        flip = stable_hash(species, variant, x, y, 313) & 1
        source_mask = mask
        if flip:
            source_mask = mask & ~(2 | 8)
            if mask & 2:
                source_mask |= 8
            if mask & 8:
                source_mask |= 2
        tile = build_mask(source_mask, species, asset_variant)
        if flip:
            tile = tile.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        image.alpha_composite(tile, (x * 16, y * 16))
    return image


def validate_contract() -> dict[str, object]:
    occupancy: list[float] = []
    for species in range(3):
        for variant in range(3):
            for mask in range(16):
                cells = build_cells(mask, species, variant)
                boundaries = {
                    1: {x for x, y in cells if y == 0},
                    2: {y for x, y in cells if x == 15},
                    4: {x for x, y in cells if y == 15},
                    8: {y for x, y in cells if x == 0},
                }
                for bit, boundary in boundaries.items():
                    if mask & bit:
                        assert boundary == set(range(16))
                    else:
                        assert boundary <= {0, 15}
                alpha = build_mask(mask, species, variant).getchannel("A")
                flattened = getattr(alpha, "get_flattened_data", alpha.getdata)()
                assert set(flattened) <= {0, 255}
                if mask.bit_count() >= 2:
                    occupancy.append(len(cells) / 256.0)
    return {
        "binaryAlpha": True,
        "sharedSockets": {"topBottom": [0, 15], "leftRight": [0, 15]},
        "exposedOuterContour": {
            "globalCornerInsetPixels": [6, 8],
            "sideMidpointInsetPixels": [1, 3],
            "sameEdgeTileJunctionInsetPixels": [1, 3],
        },
        "multiNeighborOccupancy": [round(min(occupancy), 4), round(max(occupancy), 4)],
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--skip-provenance",
        action="store_true",
        help="Regenerate runtime PNGs/contact sheet without rewriting provenance metadata.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    contract = validate_contract()
    sheets: list[Image.Image] = []
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
            sheets.append(sheet)
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

    preview = Image.new("RGBA", (2240, 1160), (17, 20, 27, 255))
    draw = ImageDraw.Draw(preview)
    for species, spec in enumerate(SPECS):
        section_y = 24 + species * 376
        draw.text((24, section_y), spec["shape"].upper(), fill=(226, 231, 222, 255))
        for variant in range(3):
            row_y = section_y + 28 + variant * 110
            draw.text((24, row_y + 4), f"VARIANT {VARIANT_NAMES[variant].upper()}  16 MASKS", fill=(150, 160, 151, 255))
            sheet = sheets[species * 3 + variant]
            preview.alpha_composite(sheet.resize((768, 48), Image.Resampling.NEAREST), (180, row_y))
        for variant in range(3):
            canopy = sample_canopy(species, variant)
            sample_x = 980 + variant * 412
            draw.text((sample_x, section_y + 30), f"V5 RUNTIME MIX {V5_SAMPLE_VARIATIONS[variant]}", fill=(150, 160, 151, 255))
            preview.alpha_composite(
                canopy.resize((canopy.width * 2, canopy.height * 2), Image.Resampling.NEAREST),
                (sample_x, section_y + 52),
            )
    preview.save(PREVIEW, optimize=False)

    if not args.skip_provenance:
        PROVENANCE.write_text(json.dumps({
            "version": 4,
            "generator": "deterministic Pillow pixel primitives",
            "sourceBrief": "asset_briefs/loose_foliage_v2_briefs.json",
            "visualDirectionReference": "art_direction/generated_sources/tree_v3_concept_source.png",
            "referenceUse": "Composition reference only. Runtime pixels are original deterministic native-size output and are not sampled, traced, or resized from the reference.",
            "preview": str(PREVIEW.relative_to(ROOT)).replace("\\", "/"),
            "designIntent": "Dense Terraria-inspired side-view foliage with one overlapping macro canopy, species-specific A/B/C light and shadow volumes, no periodic tile motifs, broad compatible sockets, seamless internal junctions, and a globally irregular outer contour.",
            "contract": contract,
            "assets": assets,
        }, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
