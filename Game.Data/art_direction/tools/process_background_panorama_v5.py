#!/usr/bin/env python3
"""Create native-resolution, seam-safe YjsE V5 biome panoramas."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
DELIVERY_SIZE = (1536, 384)
SEAM_BAND = 128
PALETTE_COLORS = 256

ASSETS = {
    "forest": {
        "output": "sprites/world/backgrounds/forest_parallax_layer_v5.png",
        "crop_y": 235,
    },
    "amber_grove": {
        "output": "sprites/world/backgrounds/amber_grove_parallax_layer_v5.png",
        "crop_y": 195,
    },
    "twilight_marsh": {
        "output": "sprites/world/backgrounds/twilight_marsh_parallax_layer_v5.png",
        "crop_y": 170,
    },
    "crystal_depths": {
        "output": "sprites/world/backgrounds/crystal_depths_parallax_layer_v5.png",
        "crop_y": 80,
    },
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-root", type=Path, default=ROOT)
    for biome in ASSETS:
        parser.add_argument(f"--{biome.replace('_', '-')}-source", required=True, type=Path)
    return parser.parse_args()


def flattened_pixels(image: Image.Image):
    get_flattened_data = getattr(image, "get_flattened_data", None)
    return get_flattened_data() if get_flattened_data else image.getdata()


def seam_score(source: Image.Image, start_x: int, sample_width: int = 6) -> int:
    pixels = source.load()
    right_x = start_x + DELIVERY_SIZE[0] - 1
    score = 0
    for y in range(0, DELIVERY_SIZE[1], 6):
        for offset in range(sample_width):
            left = pixels[start_x + offset, y]
            right = pixels[right_x - offset, y]
            score += sum(abs(left[channel] - right[channel]) for channel in range(3))
    return score


def select_horizontal_crop(source: Image.Image) -> tuple[int, int]:
    maximum = source.width - DELIVERY_SIZE[0]
    scores = ((seam_score(source, start_x), start_x) for start_x in range(maximum + 1))
    score, start_x = min(scores)
    return start_x, score


def smoothstep(value: float) -> float:
    return value * value * (3.0 - 2.0 * value)


def seam_safe_edges(image: Image.Image) -> Image.Image:
    source = image.convert("RGB")
    result = source.copy()
    source_pixels = source.load()
    result_pixels = result.load()
    width, height = source.size

    for y in range(height):
        for distance in range(SEAM_BAND):
            normalized = distance / (SEAM_BAND - 1)
            edge_weight = 1.0 - smoothstep(normalized)
            left_x = distance
            right_x = width - 1 - distance
            left = source_pixels[left_x, y]
            right = source_pixels[right_x, y]
            shared = tuple(round((left[channel] + right[channel]) * 0.5) for channel in range(3))
            result_pixels[left_x, y] = tuple(
                round(left[channel] * (1.0 - edge_weight) + shared[channel] * edge_weight)
                for channel in range(3)
            )
            result_pixels[right_x, y] = tuple(
                round(right[channel] * (1.0 - edge_weight) + shared[channel] * edge_weight)
                for channel in range(3)
            )

    return result


def quantize_detail(image: Image.Image) -> Image.Image:
    return image.quantize(
        colors=PALETTE_COLORS,
        method=Image.Quantize.MEDIANCUT,
        dither=Image.Dither.NONE,
    ).convert("RGBA")


def process(source_path: Path, output_path: Path, crop_y: int) -> dict:
    source_bytes = source_path.read_bytes()
    with Image.open(source_path) as opened:
        source = opened.convert("RGB")

    if source.width < DELIVERY_SIZE[0] or source.height < crop_y + DELIVERY_SIZE[1]:
        raise ValueError(
            f"{source_path} is {source.size}; native crop requires at least "
            f"{DELIVERY_SIZE[0]}x{crop_y + DELIVERY_SIZE[1]}"
        )

    vertical_crop = source.crop((0, crop_y, source.width, crop_y + DELIVERY_SIZE[1]))
    crop_x, candidate_score = select_horizontal_crop(vertical_crop)
    cropped = vertical_crop.crop((crop_x, 0, crop_x + DELIVERY_SIZE[0], DELIVERY_SIZE[1]))
    delivery = quantize_detail(seam_safe_edges(cropped))
    delivery_pixels = delivery.load()
    for y in range(delivery.height):
        delivery_pixels[delivery.width - 1, y] = delivery_pixels[0, y]

    output_path.parent.mkdir(parents=True, exist_ok=True)
    delivery.save(output_path, format="PNG", optimize=False, compress_level=9)

    left = list(flattened_pixels(delivery.crop((0, 0, 1, delivery.height))))
    right = list(flattened_pixels(delivery.crop((delivery.width - 1, 0, delivery.width, delivery.height))))
    return {
        "source": str(source_path),
        "sourceSha256": hashlib.sha256(source_bytes).hexdigest(),
        "sourceDimensions": list(source.size),
        "nativeCrop": [crop_x, crop_y, DELIVERY_SIZE[0], DELIVERY_SIZE[1]],
        "candidateEdgeScore": candidate_score,
        "output": str(output_path),
        "outputSha256": hashlib.sha256(output_path.read_bytes()).hexdigest(),
        "outputDimensions": list(delivery.size),
        "fileBytes": output_path.stat().st_size,
        "uniqueColorCount": len({pixel[:3] for pixel in flattened_pixels(delivery)}),
        "alphaValues": sorted(set(flattened_pixels(delivery.getchannel("A")))),
        "horizontalSeamColumnsEqual": left == right,
        "resampled": False,
    }


def main() -> None:
    args = parse_args()
    data_root = args.data_root.resolve()
    records = []
    for biome, spec in ASSETS.items():
        source_path = getattr(args, f"{biome}_source").resolve()
        output_path = data_root / spec["output"]
        record = process(source_path, output_path, spec["crop_y"])
        record["biome"] = biome
        records.append(record)
    print(json.dumps(records, indent=2))


if __name__ == "__main__":
    main()
