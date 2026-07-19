#!/usr/bin/env python3
"""Build the deterministic V4 panorama contact sheet and QA summary."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "art_direction" / "background_panorama_v4_contact_sheet.png"
SUMMARY = ROOT / "art_direction" / "background_panorama_v4_preview_summary.json"
FONT = ImageFont.load_default()
INK = (27, 24, 38, 255)
PANEL = (74, 69, 85, 255)
PAPER = (170, 162, 176, 255)
LIGHT = (244, 233, 216, 255)
ACCENT = (240, 195, 90, 255)

ASSETS = [
    ("FOREST", "sprites/world/backgrounds/forest_parallax_layer_v4.png"),
    ("AMBER GROVE", "sprites/world/backgrounds/amber_grove_parallax_layer_v4.png"),
    ("TWILIGHT MARSH", "sprites/world/backgrounds/twilight_marsh_parallax_layer_v4.png"),
    ("CRYSTAL DEPTHS", "sprites/world/backgrounds/crystal_depths_parallax_layer_v4.png"),
]


def flattened_pixels(image: Image.Image):
    get_flattened_data = getattr(image, "get_flattened_data", None)
    return get_flattened_data() if get_flattened_data else image.getdata()


def load(relative_path: str) -> Image.Image:
    with Image.open(ROOT / relative_path) as opened:
        return opened.convert("RGBA")


def wrap_view(source: Image.Image, lead_in: int = 256) -> Image.Image:
    target = Image.new("RGBA", source.size)
    target.alpha_composite(source.crop((source.width - lead_in, 0, source.width, source.height)), (0, 0))
    target.alpha_composite(source.crop((0, 0, source.width - lead_in, source.height)), (lead_in, 0))
    return target


def asset_record(label: str, relative_path: str, source: Image.Image) -> dict:
    path = ROOT / relative_path
    left = list(flattened_pixels(source.crop((0, 0, 1, source.height))))
    right = list(flattened_pixels(source.crop((source.width - 1, 0, source.width, source.height))))
    seam_delta = max(
        sum(abs(left_pixel[channel] - right_pixel[channel]) for channel in range(4))
        for left_pixel, right_pixel in zip(left, right)
    )
    return {
        "label": label,
        "path": relative_path,
        "dimensions": list(source.size),
        "mode": source.mode,
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "fileBytes": path.stat().st_size,
        "uniqueColorCount": len({pixel[:3] for pixel in flattened_pixels(source)}),
        "alphaValues": sorted(set(flattened_pixels(source.getchannel("A")))),
        "horizontalSeamColumnsEqual": left == right,
        "maximumWrapSeamChannelDelta": seam_delta,
    }


def main() -> None:
    width = 2160
    header_height = 58
    row_height = 290
    canvas = Image.new("RGBA", (width, header_height + row_height * len(ASSETS) + 10), PAPER)
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((0, 0, width, header_height - 1), fill=INK)
    draw.text((20, 14), "YjsE BACKGROUND PANORAMA V4 / NATIVE 1024x256 + HORIZONTAL WRAP QA", font=FONT, fill=LIGHT)
    draw.text((20, 32), "Palette-only RGBA / 512x128 logical raster / nearest-neighbor 2x delivery / quiet upper field", font=FONT, fill=LIGHT)

    records = []
    for index, (label, relative_path) in enumerate(ASSETS):
        y = header_height + index * row_height
        source = load(relative_path)
        wrapped = wrap_view(source)
        draw.rectangle((10, y + 4, width - 10, y + row_height - 4), fill=PANEL)
        draw.text((20, y + 10), f"{label} / NATIVE PERIOD", font=FONT, fill=LIGHT)
        draw.text((1116, y + 10), "WRAP VIEW / LAST 256 PX + FIRST 768 PX", font=FONT, fill=LIGHT)
        draw.text((1364, y + 10), "SEAM", font=FONT, fill=ACCENT)
        canvas.alpha_composite(source, (20, y + 28))
        canvas.alpha_composite(wrapped, (1116, y + 28))
        draw.line((1372, y + 25, 1372, y + 27), fill=ACCENT, width=1)
        records.append(asset_record(label, relative_path, source))

    canvas.save(OUTPUT, format="PNG", optimize=False, compress_level=9)
    summary = {
        "schemaVersion": 1,
        "waveId": "background_panorama_v4_imagegen",
        "preview": str(OUTPUT.relative_to(ROOT)).replace("\\", "/"),
        "dimensions": list(canvas.size),
        "sha256": hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
        "views": ["native-period", "horizontal-wrap-last-256-plus-first-768"],
        "logicalRaster": [512, 128],
        "deliveryRaster": [1024, 256],
        "quietUpperFieldTargetPercent": [45, 55],
        "assets": records,
    }
    SUMMARY.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
