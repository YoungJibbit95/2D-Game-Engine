#!/usr/bin/env python3
"""Convert built-in image_gen sources into seam-safe YjsE V4 panoramas."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
LOGICAL_SIZE = (512, 128)
DELIVERY_SIZE = (1024, 256)
SEAM_BAND = 64

ASSETS = {
    "forest": {
        "output": "sprites/world/backgrounds/forest_parallax_layer_v4.png",
        "palette_groups": ("outline", "neutral", "wood", "mana", "uiAccent", "foliage", "crystalDepths"),
        "denoise": True,
    },
    "amber_grove": {
        "output": "sprites/world/backgrounds/amber_grove_parallax_layer_v4.png",
        "palette_groups": ("outline", "neutral", "wood", "copper", "spark", "foliage", "caveEarth"),
        "denoise": True,
    },
    "twilight_marsh": {
        "output": "sprites/world/backgrounds/twilight_marsh_parallax_layer_v4.png",
        "palette_groups": ("outline", "neutral", "mana", "uiAccent", "foliage", "caveEarth", "mushroom", "crystalDepths"),
        "denoise": False,
    },
    "crystal_depths": {
        "output": "sprites/world/backgrounds/crystal_depths_parallax_layer_v4.png",
        "palette_groups": ("outline", "neutral", "mana", "caveEarth", "mushroom", "crystalDepths"),
        "denoise": False,
    },
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-root", type=Path, default=ROOT)
    for biome in ASSETS:
        parser.add_argument(f"--{biome.replace('_', '-')}-source", required=True, type=Path)
    return parser.parse_args()


def parse_hex(value: str) -> tuple[int, int, int]:
    raw = value.lstrip("#")
    return tuple(int(raw[index : index + 2], 16) for index in (0, 2, 4))


def load_palettes(data_root: Path) -> dict[str, list[tuple[int, int, int]]]:
    style_path = data_root / "art_direction" / "yjse_pixel_style.json"
    style = json.loads(style_path.read_text(encoding="utf-8"))
    return {
        group["group"]: [parse_hex(color) for color in group["colors"]]
        for group in style["palette"]
    }


def palette_image(colors: list[tuple[int, int, int]]) -> Image.Image:
    flattened = [channel for color in colors for channel in color]
    flattened.extend(flattened[-3:] * (256 - len(colors)))
    palette = Image.new("P", (1, 1))
    palette.putpalette(flattened)
    return palette


def seam_safe_edges(image: Image.Image, band: int) -> Image.Image:
    source = image.convert("RGB")
    pixels = source.load()
    width, height = source.size
    sample_width = 8
    seam_colors = []
    for y in range(height):
        samples = [pixels[x, y] for x in range(sample_width)]
        samples.extend(pixels[width - sample_width + x, y] for x in range(sample_width))
        seam_colors.append(
            tuple(round(sum(color[channel] for color in samples) / len(samples)) for channel in range(3))
        )

    result = source.copy()
    result_pixels = result.load()
    for y, seam_color in enumerate(seam_colors):
        for distance in range(band):
            edge_weight = (1.0 - distance / band) ** 2
            for x in (distance, width - 1 - distance):
                original = pixels[x, y]
                result_pixels[x, y] = tuple(
                    round(original[channel] * (1.0 - edge_weight) + seam_color[channel] * edge_weight)
                    for channel in range(3)
                )
    return result


def quantize(image: Image.Image, colors: list[tuple[int, int, int]]) -> Image.Image:
    indexed = image.quantize(
        palette=palette_image(colors),
        dither=Image.Dither.NONE,
    )
    return indexed.convert("RGBA")


def flattened_pixels(image: Image.Image):
    get_flattened_data = getattr(image, "get_flattened_data", None)
    return get_flattened_data() if get_flattened_data else image.getdata()


def process(
    source_path: Path,
    output_path: Path,
    colors: list[tuple[int, int, int]],
    denoise: bool,
) -> dict:
    source_bytes = source_path.read_bytes()
    with Image.open(source_path) as opened:
        source = opened.convert("RGB")
    logical = source.resize(LOGICAL_SIZE, Image.Resampling.LANCZOS)
    if denoise:
        logical = logical.filter(ImageFilter.MedianFilter(size=3))
    logical = seam_safe_edges(logical, SEAM_BAND)
    logical = quantize(logical, colors)
    logical_pixels = logical.load()
    for y in range(logical.height):
        logical_pixels[logical.width - 1, y] = logical_pixels[0, y]

    delivery = logical.resize(DELIVERY_SIZE, Image.Resampling.NEAREST).convert("RGBA")
    delivery_pixels = delivery.load()
    for y in range(delivery.height):
        delivery_pixels[delivery.width - 1, y] = delivery_pixels[0, y]
    output_path.parent.mkdir(parents=True, exist_ok=True)
    delivery.save(output_path, format="PNG", optimize=False, compress_level=9)

    unique_colors = sorted({pixel[:3] for pixel in flattened_pixels(delivery)})
    left = list(flattened_pixels(delivery.crop((0, 0, 1, delivery.height))))
    right = list(flattened_pixels(delivery.crop((delivery.width - 1, 0, delivery.width, delivery.height))))
    return {
        "source": str(source_path),
        "sourceSha256": hashlib.sha256(source_bytes).hexdigest(),
        "sourceDimensions": list(source.size),
        "output": str(output_path),
        "outputSha256": hashlib.sha256(output_path.read_bytes()).hexdigest(),
        "outputDimensions": list(delivery.size),
        "fileBytes": output_path.stat().st_size,
        "uniqueColorCount": len(unique_colors),
        "alphaValues": sorted(set(flattened_pixels(delivery.getchannel("A")))),
        "horizontalSeamColumnsEqual": left == right,
    }


def main() -> None:
    args = parse_args()
    data_root = args.data_root.resolve()
    palettes = load_palettes(data_root)
    records = []
    for biome, spec in ASSETS.items():
        source_path = getattr(args, f"{biome}_source").resolve()
        colors = [
            color
            for group_name in spec["palette_groups"]
            for color in palettes[group_name]
        ]
        output_path = data_root / spec["output"]
        record = process(source_path, output_path, colors, spec["denoise"])
        record["biome"] = biome
        record["paletteGroups"] = list(spec["palette_groups"])
        record["medianDenoise"] = spec["denoise"]
        records.append(record)
    print(json.dumps(records, indent=2))


if __name__ == "__main__":
    main()
