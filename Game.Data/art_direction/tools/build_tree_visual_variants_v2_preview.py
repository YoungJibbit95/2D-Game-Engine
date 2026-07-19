#!/usr/bin/env python3
"""Build the focused tree variant preview, summary and manifest audit."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "assets" / "sprites.json"
PREVIEW = ROOT / "art_direction" / "tree_visual_variants_v2_preview.png"
SUMMARY = ROOT / "art_direction" / "tree_visual_variants_v2_preview_summary.json"
AUDIT = ROOT / "art_direction" / "tree_visual_variants_v2_asset_audit.json"
FONT = ImageFont.load_default()
INK = (27, 31, 36, 255)
PANEL = (220, 224, 213, 255)
SKY = (120, 177, 205, 255)
GROUND = (92, 67, 43, 255)
SPECS = [
    ("OAK", "tiles/oak_trunk_autotile", "sprites/world/tiles/oak_trunk_autotile.png",
     "tiles/oak_leaves_autotile", "sprites/world/tiles/oak_leaves_autotile.png", 1),
    ("PINE", "tiles/pine_trunk_autotile", "sprites/world/tiles/pine_trunk_autotile.png",
     "tiles/pine_leaves_tree_autotile", "sprites/world/tiles/pine_leaves_tree_autotile.png", 4),
    ("BIRCH", "tiles/birch_trunk_autotile", "sprites/world/tiles/birch_trunk_autotile.png",
     "tiles/birch_leaves_autotile", "sprites/world/tiles/birch_leaves_autotile.png", 8),
]


def load(path: str) -> Image.Image:
    with Image.open(ROOT / path) as opened:
        return opened.convert("RGBA")


def branch(dx: int, dy: int, start_y: int, direction: int, length: int) -> bool:
    if dx == 0 or (1 if dx > 0 else -1) != direction:
        return False
    step = abs(dx)
    if step > length:
        return False
    branch_y = start_y - step // 2
    return dy == branch_y or (step > 1 and step % 2 == 0 and dy == branch_y + 1)


def cluster(dx: int, dy: int, radius_x: int, radius_y: int) -> bool:
    if abs(dx) > radius_x or abs(dy) > radius_y:
        return False
    return (dx * dx * radius_y * radius_y + dy * dy * radius_x * radius_x <=
            radius_x * radius_x * radius_y * radius_y)


def classify(dx: int, dy: int, height: int, variation: int) -> str | None:
    variation %= 12
    if dx == 0 and 0 <= dy < height:
        return "trunk"
    if height >= 5 and dy == height - 1 and abs(dx) == 1:
        return "trunk"
    direction = -1 if variation % 2 == 0 else 1
    upper = min(height - 2, 2 + ((variation >> 1) & 1))
    if height >= 5 and branch(dx, dy, upper, direction, 3 + (1 if variation % 3 == 0 else 0)):
        return "trunk"
    middle = min(height - 2, 4 + ((variation >> 2) & 1))
    if height >= 7 and branch(dx, dy, middle, -direction, 2 + ((variation + 1) % 3)):
        return "trunk"
    lower = min(height - 2, 6 + (variation & 1))
    if height >= 9 and branch(dx, dy, lower, -1 if variation % 4 < 2 else 1, 2):
        return "trunk"

    crown_shift = variation % 3 - 1
    crown_top = -2 - ((variation // 3) & 1)
    primary_x = direction * (3 + ((variation >> 2) & 1))
    secondary_x = -direction * (3 + ((variation >> 1) & 1))
    lower_x = direction * (1 + ((variation >> 1) & 1))
    leaf = (
        cluster(dx - crown_shift, dy - crown_top, 3, 2)
        or cluster(dx, dy - 1, 3 + (1 if variation % 4 == 0 else 0), 2)
        or cluster(dx - primary_x, dy - variation % 2, 3, 2)
        or cluster(dx - secondary_x, dy - (2 + ((variation >> 2) & 1)), 3, 2)
        or (variation % 3 != 1 and cluster(dx - lower_x, dy - 4, 2, 2))
    )
    if not leaf:
        return None
    if abs(dx) > 1 and dy >= -1 and (dx * 17 + dy * 31 + variation * 13) % 9 == 0:
        return None
    return "leaves"


def tree_cells(variation: int) -> dict[tuple[int, int], str]:
    return {
        (dx, dy): cell
        for dy in range(-4, 9)
        for dx in range(-7, 8)
        if (cell := classify(dx, dy, 9, variation)) is not None
    }


def autotile_frame(sheet: Image.Image, cells: dict[tuple[int, int], str], x: int, y: int, kind: str) -> Image.Image:
    mask = 0
    mask |= 1 if cells.get((x, y - 1)) == kind else 0
    mask |= 2 if cells.get((x + 1, y)) == kind else 0
    mask |= 4 if cells.get((x, y + 1)) == kind else 0
    mask |= 8 if cells.get((x - 1, y)) == kind else 0
    return sheet.crop((mask * 16, 0, mask * 16 + 16, 16))


def render_tree(trunk: Image.Image, leaves: Image.Image, variation: int) -> Image.Image:
    cells = tree_cells(variation)
    native = Image.new("RGBA", (15 * 16, 14 * 16))
    for (x, y), kind in cells.items():
        sheet = trunk if kind == "trunk" else leaves
        native.alpha_composite(autotile_frame(sheet, cells, x, y, kind), ((x + 7) * 16, (y + 4) * 16))
    return native.resize((native.width * 2, native.height * 2), Image.Resampling.NEAREST)


def validate_asset(entries: dict[str, dict], sprite_id: str, path: str) -> dict:
    source_path = ROOT / path
    sprite = load(path)
    entry = entries.get(sprite_id)
    masks = sorted(frame.get("autoTileMask") for frame in entry.get("frames", [])) if entry else []
    checks = {
        "fileExists": source_path.exists(),
        "dimensionsExact": sprite.size == (256, 16),
        "manifestEntryPresent": entry is not None,
        "manifestPathMatches": entry is not None and entry.get("path") == path,
        "manifestDimensionsMatch": entry is not None and entry.get("width") == 256 and entry.get("height") == 16,
        "sixteenExactFrames": entry is not None and len(entry.get("frames", [])) == 16 and masks == list(range(16)),
    }
    return {
        "spriteId": sprite_id,
        "path": path,
        "sha256": hashlib.sha256(source_path.read_bytes()).hexdigest(),
        "dimensions": list(sprite.size),
        "alphaValues": sorted(set(sprite.getchannel("A").get_flattened_data())),
        "checks": checks,
        "passed": all(checks.values()),
    }


def main() -> None:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    entries = {entry["id"]: entry for entry in manifest["sprites"]}
    records = []
    for _, trunk_id, trunk_path, leaves_id, leaves_path, _ in SPECS:
        records.append(validate_asset(entries, trunk_id, trunk_path))
        records.append(validate_asset(entries, leaves_id, leaves_path))

    canvas = Image.new("RGBA", (1580, 820), (239, 236, 224, 255))
    draw = ImageDraw.Draw(canvas)
    draw.text((24, 18), "YjsE TREE VISUAL VARIANTS V2 / ORIGINAL PROJECT ASSETS / RUNTIME AUTOTILE COMPOSITION", font=FONT, fill=INK)
    draw.text((24, 38), "12 deterministic silhouettes / root flare / stepped branches / crown openings / 3 stable palettes", font=FONT, fill=INK)
    for index, (label, _, trunk_path, _, leaves_path, variation) in enumerate(SPECS):
        x = 20 + index * 520
        draw.rectangle((x, 70, x + 500, 800), fill=PANEL)
        draw.text((x + 12, 82), f"{label} / SILHOUETTE {variation:02d} / 2x NEAREST", font=FONT, fill=INK)
        trunk = load(trunk_path)
        leaves = load(leaves_path)
        canvas.alpha_composite(trunk, (x + 12, 106))
        canvas.alpha_composite(leaves, (x + 12, 128))
        draw.text((x + 278, 110), "TRUNK 256x16 / MASKS 0-15", font=FONT, fill=INK)
        draw.text((x + 278, 132), "LEAVES 256x16 / MASKS 0-15", font=FONT, fill=INK)
        field = Image.new("RGBA", (480, 586), SKY)
        field_draw = ImageDraw.Draw(field)
        field_draw.rectangle((0, 548, 479, 585), fill=GROUND)
        tree = render_tree(trunk, leaves, variation)
        field.alpha_composite(tree, (0, 100))
        canvas.alpha_composite(field, (x + 10, 170))

    canvas.save(PREVIEW, format="PNG", optimize=False, compress_level=9)
    audit = {
        "schemaVersion": 1,
        "sliceId": "tree_visual_variants_v2",
        "manifest": "assets/sprites.json",
        "assetCount": len(records),
        "passed": all(record["passed"] for record in records),
        "assets": records,
    }
    AUDIT.write_text(json.dumps(audit, indent=2) + "\n", encoding="utf-8")
    summary = {
        "sliceId": "tree_visual_variants_v2",
        "preview": str(PREVIEW.relative_to(ROOT)).replace("\\", "/"),
        "dimensions": list(canvas.size),
        "sha256": hashlib.sha256(PREVIEW.read_bytes()).hexdigest(),
        "paletteCount": len(SPECS),
        "silhouetteVariationCount": 12,
        "sampleVariations": [spec[-1] for spec in SPECS],
        "integerScale": 2,
        "assetAuditPassed": audit["passed"],
    }
    SUMMARY.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
