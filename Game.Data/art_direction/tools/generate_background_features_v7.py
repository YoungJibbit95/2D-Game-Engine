"""Generate deterministic, seam-safe V7 parallax feature planes.

The output is intentionally low-resolution binary-alpha pixel art. Runtime
projection keeps each plane fullscreen while horizontal composition supplies
world-seeded phase, flip, and small vertical variation without allocations.
"""

from __future__ import annotations

import argparse
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw


WIDTH = 1024
HEIGHT = 256
BIOMES = {
    "forest": {
        "seed": 0xF07E57,
        "mountain_far": (57, 80, 120, 255),
        "mountain_mid": (45, 72, 100, 255),
        "mountain_near": (36, 61, 79, 255),
        "rock": (38, 54, 62, 255),
        "rock_light": (59, 79, 78, 255),
        "growth": (59, 103, 70, 255),
        "growth_light": (111, 151, 73, 255),
        "water": (100, 201, 205, 255),
    },
    "amber": {
        "seed": 0xA8B3E2,
        "mountain_far": (120, 83, 91, 255),
        "mountain_mid": (95, 67, 70, 255),
        "mountain_near": (68, 52, 53, 255),
        "rock": (71, 50, 47, 255),
        "rock_light": (104, 70, 52, 255),
        "growth": (104, 101, 50, 255),
        "growth_light": (181, 151, 56, 255),
        "water": (229, 166, 76, 255),
    },
    "twilight": {
        "seed": 0x7A1197,
        "mountain_far": (75, 72, 125, 255),
        "mountain_mid": (56, 59, 102, 255),
        "mountain_near": (39, 47, 79, 255),
        "rock": (38, 43, 68, 255),
        "rock_light": (64, 66, 94, 255),
        "growth": (48, 91, 83, 255),
        "growth_light": (91, 139, 108, 255),
        "water": (99, 182, 202, 255),
    },
    "crystal": {
        "seed": 0xC2757A,
        "mountain_far": (48, 63, 119, 255),
        "mountain_mid": (36, 48, 93, 255),
        "mountain_near": (25, 35, 70, 255),
        "rock": (27, 32, 62, 255),
        "rock_light": (48, 52, 91, 255),
        "growth": (54, 87, 123, 255),
        "growth_light": (104, 129, 192, 255),
        "water": (100, 220, 226, 255),
    },
}


def periodic_height(x: int, base: float, amplitude: float, seed: int, octave: int) -> int:
    phase = ((seed >> (octave * 3)) & 255) / 255.0 * math.tau
    value = (
        math.sin((x / WIDTH) * math.tau * (2 + octave) + phase) * 0.56
        + math.sin((x / WIDTH) * math.tau * (5 + octave * 2) - phase * 0.7) * 0.28
        + math.sin((x / WIDTH) * math.tau * (11 + octave * 3) + phase * 1.3) * 0.16
    )
    return round(base - value * amplitude)


def draw_mountains(palette: dict[str, object]) -> Image.Image:
    image = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    seed = int(palette["seed"])
    planes = (
        ("mountain_far", 157, 30, 0),
        ("mountain_mid", 184, 43, 1),
        ("mountain_near", 214, 55, 2),
    )
    for color_key, baseline, amplitude, octave in planes:
        ridge = [(x, periodic_height(x, baseline, amplitude, seed, octave)) for x in range(WIDTH)]
        polygon = ridge + [(WIDTH - 1, HEIGHT - 1), (0, HEIGHT - 1)]
        draw.polygon(polygon, fill=palette[color_key])
        for x in range(20 + octave * 11, WIDTH, 73 + octave * 17):
            peak_y = ridge[x][1]
            flank = 7 + ((x * 13 + seed) & 15)
            draw.line((x, peak_y + 5, x - flank, peak_y + 22), fill=palette[color_key], width=2)
    draw.line((0, HEIGHT - 1, WIDTH - 1, HEIGHT - 1), fill=palette["mountain_near"], width=1)
    image.putpixel((WIDTH - 1, HEIGHT - 1), image.getpixel((0, HEIGHT - 1)))
    return image


def draw_island(
    draw: ImageDraw.ImageDraw,
    rng: random.Random,
    x: int,
    y: int,
    width: int,
    palette: dict[str, object],
    waterfall: bool,
) -> None:
    half = width // 2
    shelf_points = [
        (x - half, y + 4),
        (x - half + 5, y),
        (x - width // 5, y - 3),
        (x + width // 6, y - 2),
        (x + half - 5, y + 1),
        (x + half, y + 6),
        (x + half - 7, y + 11),
        (x - half + 4, y + 11),
    ]
    draw.polygon(shelf_points, fill=palette["growth"])
    draw.line((x - half + 5, y, x + half - 6, y + 1), fill=palette["growth_light"], width=3)

    depth = max(24, width * 3 // 5)
    rock_points = [(x - half + 7, y + 9), (x + half - 7, y + 9)]
    steps = 7
    for step in range(1, steps):
        ratio = step / steps
        inset = round((half - 3) * ratio * 0.78)
        jitter = rng.randint(-3, 3)
        rock_points.append((x + half - inset + jitter, y + 9 + round(depth * ratio)))
    rock_points.append((x + rng.randint(-3, 3), y + depth + 13))
    for step in range(steps - 1, 0, -1):
        ratio = step / steps
        inset = round((half - 3) * ratio * 0.78)
        jitter = rng.randint(-3, 3)
        rock_points.append((x - half + inset + jitter, y + 9 + round(depth * ratio)))
    draw.polygon(rock_points, fill=palette["rock"])

    for stripe in range(4):
        sx = x - half // 2 + stripe * max(5, width // 8)
        sy = y + 14 + stripe * 5
        draw.line((sx, sy, x + (stripe - 2) * 2, y + depth - 4), fill=palette["rock_light"], width=2)

    root_count = max(3, width // 24)
    for root in range(root_count):
        rx = x - half + 11 + root * max(8, (width - 22) // max(1, root_count - 1))
        length = 8 + rng.randrange(max(9, depth // 2))
        draw.line((rx, y + 8, rx + rng.randint(-3, 3), y + 8 + length), fill=palette["growth"], width=2)

    tree_x = x - width // 6
    tree_height = max(13, width // 5)
    draw.rectangle((tree_x - 1, y - tree_height, tree_x + 2, y), fill=palette["rock"])
    crown = max(8, width // 7)
    draw.ellipse((tree_x - crown, y - tree_height - crown // 2, tree_x + crown, y - tree_height + crown // 2), fill=palette["growth"])
    draw.rectangle((tree_x - crown + 3, y - tree_height - 2, tree_x + crown - 2, y - tree_height + 2), fill=palette["growth_light"])

    if waterfall:
        wx = x + width // 5
        length = max(26, depth * 2 // 3)
        draw.rectangle((wx, y + 6, wx + 2, y + 6 + length), fill=palette["water"])
        draw.point((wx - 1, y + 9 + length), fill=palette["water"])
        draw.point((wx + 3, y + 12 + length), fill=palette["water"])


def draw_floating_islands(palette: dict[str, object]) -> Image.Image:
    image = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    rng = random.Random(int(palette["seed"]) ^ 0x51A9D)
    islands = (
        (136, 88, 82, False),
        (365, 61, 112, True),
        (641, 104, 68, False),
        (855, 73, 126, True),
    )
    for x, y, width, waterfall in islands:
        draw_island(draw, rng, x, y, width, palette, waterfall)
    return image


def build_contact_sheet(outputs: list[tuple[str, Image.Image]]) -> Image.Image:
    scale = 1
    label_height = 18
    sheet = Image.new("RGBA", (WIDTH * 2, (HEIGHT + label_height) * 4), (18, 20, 31, 255))
    draw = ImageDraw.Draw(sheet)
    for index, (name, image) in enumerate(outputs):
        row, column = divmod(index, 2)
        x = column * WIDTH
        y = row * (HEIGHT + label_height)
        checker = (32, 35, 49, 255) if row % 2 == 0 else (26, 30, 43, 255)
        draw.rectangle((x, y + label_height, x + WIDTH - 1, y + label_height + HEIGHT - 1), fill=checker)
        sheet.alpha_composite(image, (x, y + label_height))
        draw.text((x + 5, y + 3), name, fill=(225, 229, 239, 255))
    return sheet.resize((sheet.width * scale, sheet.height * scale), Image.Resampling.NEAREST)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[3])
    args = parser.parse_args()
    repo_root = args.repo_root.resolve()
    output_dir = repo_root / "Game.Data" / "assets" / "BackgroundFeaturesV7"
    preview_path = repo_root / "Game.Data" / "art_direction" / "background_features_v7_contact_sheet.png"
    output_dir.mkdir(parents=True, exist_ok=True)
    outputs: list[tuple[str, Image.Image]] = []
    for biome, palette in BIOMES.items():
        mountains = draw_mountains(palette)
        islands = draw_floating_islands(palette)
        for suffix, image in (("mountains", mountains), ("floating_islands", islands)):
            name = f"{biome}_{suffix}"
            image.save(output_dir / f"{name}.png", optimize=True)
            outputs.append((name, image))
    build_contact_sheet(outputs).save(preview_path, optimize=True)


if __name__ == "__main__":
    main()
