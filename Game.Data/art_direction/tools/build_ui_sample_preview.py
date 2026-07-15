#!/usr/bin/env python3
"""Build the deterministic light/dark preview sheet for the UI sample wave."""

from __future__ import annotations

import argparse
from io import BytesIO
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw


SPRITE_IDS = (
    "ui/mana_star",
    "ui/inventory_tab",
    "ui/crafting_hammer",
)
SCALES = (1, 2, 3, 4)
BACKGROUNDS = (
    ("LIGHT", (232, 226, 211, 255)),
    ("DARK", (22, 24, 35, 255)),
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


def load_assets(data_root: Path, manifest_path: Path) -> list[tuple[str, Image.Image]]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    by_id = {entry["id"]: entry for entry in manifest["sprites"]}
    assets: list[tuple[str, Image.Image]] = []
    for sprite_id in SPRITE_IDS:
        entry = by_id[sprite_id]
        path = data_root / entry["path"]
        with Image.open(path) as opened:
            image = opened.convert("RGBA")
        expected = (entry["width"], entry["height"])
        if image.size != expected:
            raise ValueError(f"{sprite_id}: expected {expected}, found {image.size}")
        assets.append((sprite_id, image))
    return assets


def build_preview(assets: list[tuple[str, Image.Image]]) -> Image.Image:
    margin = 20
    label_height = 18
    cell_width = 152
    cell_height = 154
    header_height = 48
    width = margin * 2 + cell_width * len(SCALES)
    height = header_height + len(assets) * len(BACKGROUNDS) * cell_height + margin
    sheet = Image.new("RGBA", (width, height), (12, 14, 20, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((margin, 14), "YjsE UI SAMPLE - NATIVE PIXELS, NEAREST NEIGHBOR", fill=(229, 232, 239, 255))

    for column, scale in enumerate(SCALES):
        x = margin + column * cell_width
        draw.text((x + 6, 31), f"{scale}x", fill=(117, 197, 176, 255))

    row = 0
    for sprite_id, source in assets:
        for background_name, background_color in BACKGROUNDS:
            y = header_height + row * cell_height
            for column, scale in enumerate(SCALES):
                x = margin + column * cell_width
                cell = (x, y, x + cell_width - 8, y + cell_height - 8)
                draw.rectangle(cell, fill=background_color, outline=(91, 98, 116, 255), width=1)
                resized = source.resize(
                    (source.width * scale, source.height * scale),
                    Image.Resampling.NEAREST,
                )
                target_x = x + (cell_width - 8 - resized.width) // 2
                target_y = y + label_height + (cell_height - label_height - 8 - resized.height) // 2
                sheet.alpha_composite(resized, (target_x, target_y))
                draw.text(
                    (x + 5, y + 4),
                    f"{sprite_id}  {background_name}",
                    fill=(24, 26, 34, 255) if background_name == "LIGHT" else (226, 229, 236, 255),
                )
            row += 1

    return sheet


def main() -> None:
    args = parse_args()
    assets = load_assets(args.data_root, args.manifest)
    preview = build_preview(assets)
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
        print(
            "preview mismatch: "
            f"generated sha256={generated_hash}, existing sha256={existing_hash}"
        )
        raise SystemExit(2)

    if args.check_existing is not None:
        print(f"preview matches {args.check_existing}")


if __name__ == "__main__":
    main()
