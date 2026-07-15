#!/usr/bin/env python3
"""Turn a chroma-keyed imagegen cutout into a strict YjsE sprite."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--style", required=True, type=Path)
    parser.add_argument("--width", required=True, type=int)
    parser.add_argument("--height", required=True, type=int)
    parser.add_argument("--padding", type=int, default=2)
    parser.add_argument("--alpha-threshold", type=int, default=96)
    parser.add_argument("--anchor", choices=("center", "bottom"), default="center")
    parser.add_argument(
        "--groups",
        nargs="+",
        help="Palette groups to use. Defaults to every group in the style file.",
    )
    return parser.parse_args()


def parse_hex(value: str) -> tuple[int, int, int]:
    raw = value.lstrip("#")
    return tuple(int(raw[index : index + 2], 16) for index in (0, 2, 4))


def nearest_palette_color(
    source: tuple[int, int, int], palette: list[tuple[int, int, int]]
) -> tuple[int, int, int]:
    red, green, blue = source
    return min(
        palette,
        key=lambda candidate: (
            3 * (red - candidate[0]) ** 2
            + 4 * (green - candidate[1]) ** 2
            + 2 * (blue - candidate[2]) ** 2
        ),
    )


def enforce_inner_outline(image: Image.Image, color: tuple[int, int, int]) -> None:
    pixels = image.load()
    opaque = {
        (x, y)
        for y in range(image.height)
        for x in range(image.width)
        if pixels[x, y][3] == 255
    }
    boundary = set()
    for x, y in opaque:
        for offset_x, offset_y in (
            (-1, -1),
            (0, -1),
            (1, -1),
            (-1, 0),
            (1, 0),
            (-1, 1),
            (0, 1),
            (1, 1),
        ):
            if (x + offset_x, y + offset_y) not in opaque:
                boundary.add((x, y))
                break
    for x, y in boundary:
        pixels[x, y] = (*color, 255)


def main() -> None:
    args = parse_args()
    if args.width <= 0 or args.height <= 0:
        raise SystemExit("Output dimensions must be positive.")
    if args.padding < 0 or args.padding * 2 >= min(args.width, args.height):
        raise SystemExit("Padding leaves no drawable area.")

    style = json.loads(args.style.read_text(encoding="utf-8"))
    palette_groups = {group["group"]: group["colors"] for group in style["palette"]}
    selected_groups = args.groups or list(palette_groups)
    unknown_groups = sorted(set(selected_groups) - set(palette_groups))
    if unknown_groups:
        raise SystemExit(f"Unknown palette groups: {', '.join(unknown_groups)}")
    palette = [
        parse_hex(color)
        for group_name in selected_groups
        for color in palette_groups[group_name]
    ]
    outline = parse_hex(style["silhouette"]["outlineColor"])

    source = Image.open(args.input).convert("RGBA")
    hard_alpha = source.getchannel("A").point(
        lambda alpha: 255 if alpha >= args.alpha_threshold else 0
    )
    source.putalpha(hard_alpha)
    bounds = hard_alpha.getbbox()
    if bounds is None:
        raise SystemExit(f"No visible pixels after alpha threshold: {args.input}")

    cropped = source.crop(bounds)
    inner_width = args.width - 2 * args.padding
    inner_height = args.height - 2 * args.padding
    scale = min(inner_width / cropped.width, inner_height / cropped.height)
    target_width = max(1, min(inner_width, round(cropped.width * scale)))
    target_height = max(1, min(inner_height, round(cropped.height * scale)))
    resized = cropped.resize(
        (target_width, target_height), resample=Image.Resampling.NEAREST
    )

    canvas = Image.new("RGBA", (args.width, args.height), (0, 0, 0, 0))
    offset_x = (args.width - target_width) // 2
    if args.anchor == "bottom":
        offset_y = args.height - args.padding - target_height
    else:
        offset_y = (args.height - target_height) // 2
    canvas.alpha_composite(resized, (offset_x, offset_y))

    pixels = canvas.load()
    color_cache: dict[tuple[int, int, int], tuple[int, int, int]] = {}
    for y in range(canvas.height):
        for x in range(canvas.width):
            red, green, blue, alpha = pixels[x, y]
            if alpha < args.alpha_threshold:
                pixels[x, y] = (0, 0, 0, 0)
                continue
            source_color = (red, green, blue)
            mapped = color_cache.setdefault(
                source_color, nearest_palette_color(source_color, palette)
            )
            pixels[x, y] = (*mapped, 255)

    enforce_inner_outline(canvas, outline)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(args.output, format="PNG", optimize=True)

    alpha_values = sorted(set(canvas.getchannel("A").get_flattened_data()))
    used_colors = sorted(
        {
            "#{:02x}{:02x}{:02x}".format(red, green, blue)
            for red, green, blue, alpha in canvas.get_flattened_data()
            if alpha == 255
        }
    )
    result = {
        "output": str(args.output),
        "size": list(canvas.size),
        "sourceBounds": list(bounds),
        "fittedSize": [target_width, target_height],
        "alphaValues": alpha_values,
        "paletteGroups": selected_groups,
        "uniqueOpaqueColors": len(used_colors),
        "colors": used_colors,
    }
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
