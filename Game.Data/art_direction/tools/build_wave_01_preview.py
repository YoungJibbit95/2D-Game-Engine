#!/usr/bin/env python3
"""Build the deterministic contact sheet for the nine wave-01 sprites."""

from __future__ import annotations

import argparse
import hashlib
from io import BytesIO
import json
from pathlib import Path

from PIL import Image, ImageDraw


SPRITES = (
    ("items/healing_potion", "HEALING POTION"),
    ("items/mana_potion", "MANA POTION"),
    ("items/copper_pickaxe", "COPPER PICKAXE"),
    ("items/copper_sword", "COPPER SWORD"),
    ("items/spark_wand", "SPARK WAND"),
    ("items/workbench", "WORKBENCH ITEM"),
    ("tiles/workbench", "WORKBENCH WORLD"),
    ("ui/inventory_tab", "INVENTORY TAB"),
    ("ui/crafting_hammer", "CRAFTING HAMMER"),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-root", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument(
        "--check-existing",
        type=Path,
        help="Fail with exit code 2 when the generated PNG differs from this file.",
    )
    return parser.parse_args()


def load_assets(
    data_root: Path, manifest_path: Path
) -> list[tuple[str, str, Image.Image]]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    by_id = {entry["id"]: entry for entry in manifest["sprites"]}
    assets = []
    for sprite_id, label in SPRITES:
        entry = by_id[sprite_id]
        with Image.open(data_root / entry["path"]) as opened:
            image = opened.convert("RGBA")
        expected = (entry["width"], entry["height"])
        if image.size != expected:
            raise ValueError(f"{sprite_id}: expected {expected}, found {image.size}")
        alpha_values = set(image.getchannel("A").get_flattened_data())
        if not alpha_values <= {0, 255}:
            raise ValueError(f"{sprite_id}: expected binary alpha, found {sorted(alpha_values)}")
        assets.append((sprite_id, label, image))
    return assets


def draw_checkerboard(
    draw: ImageDraw.ImageDraw, bounds: tuple[int, int, int, int]
) -> None:
    left, top, right, bottom = bounds
    size = 12
    colors = ((39, 40, 51, 255), (50, 51, 64, 255))
    for y in range(top, bottom, size):
        for x in range(left, right, size):
            index = ((x - left) // size + (y - top) // size) % 2
            draw.rectangle(
                (x, y, min(x + size - 1, right - 1), min(y + size - 1, bottom - 1)),
                fill=colors[index],
            )


def build_preview(assets: list[tuple[str, str, Image.Image]]) -> Image.Image:
    margin = 18
    gutter = 10
    cell_width = 270
    cell_height = 210
    header_height = 52
    width = margin * 2 + cell_width * 3 + gutter * 2
    height = header_height + cell_height * 3 + gutter * 2 + margin
    sheet = Image.new("RGBA", (width, height), (13, 15, 22, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((margin, 13), "YjsE PIXEL WAVE 01", fill=(239, 232, 214, 255))
    draw.text(
        (margin, 30),
        "NATIVE SOURCES - NEAREST NEIGHBOR - BINARY ALPHA",
        fill=(114, 195, 174, 255),
    )

    for index, (sprite_id, label, source) in enumerate(assets):
        column = index % 3
        row = index // 3
        left = margin + column * (cell_width + gutter)
        top = header_height + row * (cell_height + gutter)
        right = left + cell_width
        bottom = top + cell_height
        draw.rounded_rectangle(
            (left, top, right - 1, bottom - 1),
            radius=4,
            fill=(26, 28, 39, 255),
            outline=(91, 98, 116, 255),
            width=1,
        )
        preview_bounds = (left + 8, top + 27, right - 8, bottom - 24)
        draw_checkerboard(draw, preview_bounds)

        max_width = preview_bounds[2] - preview_bounds[0] - 16
        max_height = preview_bounds[3] - preview_bounds[1] - 12
        scale = max(1, min(max_width // source.width, max_height // source.height))
        resized = source.resize(
            (source.width * scale, source.height * scale),
            Image.Resampling.NEAREST,
        )
        target_x = preview_bounds[0] + (max_width + 16 - resized.width) // 2
        target_y = preview_bounds[1] + (max_height + 12 - resized.height) // 2
        sheet.alpha_composite(resized, (target_x, target_y))

        draw.text((left + 9, top + 8), label, fill=(239, 232, 214, 255))
        draw.text(
            (left + 9, bottom - 17),
            f"{sprite_id}  {source.width}x{source.height}  {scale}x",
            fill=(181, 185, 198, 255),
        )

    return sheet


def main() -> None:
    args = parse_args()
    preview = build_preview(load_assets(args.data_root, args.manifest))
    encoded = BytesIO()
    preview.convert("RGB").save(encoded, format="PNG", optimize=True)
    generated_bytes = encoded.getvalue()

    existing_bytes = None
    matches_existing = True
    if args.check_existing is not None:
        if not args.check_existing.is_file():
            print(f"missing checked-in preview: {args.check_existing}")
            matches_existing = False
        else:
            existing_bytes = args.check_existing.read_bytes()
            matches_existing = existing_bytes == generated_bytes

    same_output_as_check = (
        args.check_existing is not None
        and args.output.resolve() == args.check_existing.resolve()
    )
    if matches_existing or not same_output_as_check:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_bytes(generated_bytes)
        print(f"wrote {args.output} ({preview.width}x{preview.height})")

    if not matches_existing:
        generated_hash = hashlib.sha256(generated_bytes).hexdigest()
        existing_hash = (
            hashlib.sha256(existing_bytes).hexdigest()
            if existing_bytes is not None
            else "missing"
        )
        print(f"generated sha256: {generated_hash}")
        print(f"existing sha256:  {existing_hash}")
        raise SystemExit(2)


if __name__ == "__main__":
    main()
