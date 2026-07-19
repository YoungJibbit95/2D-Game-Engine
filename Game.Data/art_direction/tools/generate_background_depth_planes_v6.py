#!/usr/bin/env python3
"""Build binary-alpha Far/Mid/Near depth planes from project-owned panoramas."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets" / "BackgroundDepthV6"
MANIFEST = ROOT / "assets" / "background_depth_v6.sprites.json"
BRIEFS = ROOT / "asset_briefs" / "background_depth_v6_briefs.json"
PROVENANCE = ROOT / "art_direction" / "background_depth_v6_provenance.json"
PREVIEW = ROOT / "art_direction" / "background_depth_v6_contact_sheet.png"
GENERATOR = "Game.Data/art_direction/tools/generate_background_depth_planes_v6.py"

SPECS = (
    ("forest_far", "world/backgrounds/depth_v6/forest_far", "sprites/world/backgrounds/forest_parallax_layer_v5.png", (1536, 384), 0.00, 0.58),
    ("forest_mid", "world/backgrounds/depth_v6/forest_mid", "sprites/world/backgrounds/forest_parallax_layer_v4.png", (1024, 256), 0.00, 0.78),
    ("forest_near", "world/backgrounds/depth_v6/forest_near", "sprites/world/backgrounds/forest_parallax_layer_v3.png", (512, 128), 0.00, 0.68),
    ("amber_far", "world/backgrounds/depth_v6/amber_far", "sprites/world/backgrounds/amber_grove_parallax_layer_v5.png", (1536, 384), 0.00, 0.58),
    ("amber_mid", "world/backgrounds/depth_v6/amber_mid", "sprites/world/backgrounds/amber_grove_parallax_layer_v4.png", (1024, 256), 0.00, 0.78),
    ("amber_near", "world/backgrounds/depth_v6/amber_near", "sprites/world/backgrounds/magical_grove_parallax_layer.png", (512, 128), 0.00, 0.68),
    ("twilight_far", "world/backgrounds/depth_v6/twilight_far", "sprites/world/backgrounds/twilight_marsh_parallax_layer_v5.png", (1536, 384), 0.00, 0.58),
    ("twilight_mid", "world/backgrounds/depth_v6/twilight_mid", "sprites/world/backgrounds/twilight_marsh_parallax_layer_v4.png", (1024, 256), 0.00, 0.78),
    ("twilight_near", "world/backgrounds/depth_v6/twilight_near", "sprites/world/backgrounds/twilight_marsh_parallax_layer_v4.png", (512, 128), 0.00, 0.68),
    ("crystal_far", "world/backgrounds/depth_v6/crystal_far", "sprites/world/backgrounds/crystal_depths_parallax_layer_v5.png", (1536, 384), 0.00, 0.58),
    ("crystal_mid", "world/backgrounds/depth_v6/crystal_mid", "sprites/world/backgrounds/crystal_depths_parallax_layer_v4.png", (1024, 256), 0.00, 0.78),
    ("crystal_near", "world/backgrounds/depth_v6/crystal_near", "sprites/world/backgrounds/wave04/crystal_depths_parallax_layer.png", (512, 128), 0.00, 0.68),
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def stable_threshold(x: int, y: int, seed: int) -> int:
    value = (x * 0x45D9F3B) ^ (y * 0x119DE1F3) ^ (seed * 0x9E3779B9)
    value ^= value >> 16
    value = (value * 0x7FEB352D) & 0xFFFFFFFF
    value ^= value >> 15
    value = (value * 0x846CA68B) & 0xFFFFFFFF
    return (value ^ (value >> 16)) & 0xFF


def pixel_luminance(red: int, green: int, blue: int) -> int:
    return (red * 54 + green * 183 + blue * 19) >> 8


def build_far_plane(source: Image.Image) -> Image.Image:
    """Remove authored sky while retaining mountains and canopy as solid silhouettes."""
    pixels = source.load()
    width, height = source.size
    for y in range(height):
        row_luminance = sorted(
            pixel_luminance(*pixels[x, y][:3])
            for x in range(width)
            if pixels[x, y][3] > 0
        )
        sky_luminance = row_luminance[min(len(row_luminance) - 1, round((len(row_luminance) - 1) * 0.9))]
        progress = y / max(1, height - 1)
        transition = min(1.0, max(0.0, (progress - 0.18) / (0.74 - 0.18)))
        transition = transition * transition * (3.0 - 2.0 * transition)
        required_separation = round(38 * (1.0 - transition))
        for x in range(width):
            red, green, blue, alpha = pixels[x, y]
            separation = sky_luminance - pixel_luminance(red, green, blue)
            keep = alpha > 0 and (required_separation <= 0 or separation >= required_separation)
            pixels[x, y] = (red, green, blue, 255 if keep else 0)
    return source


def build_plane(name: str, source_path: Path, size: tuple[int, int], fade_start: float, fade_end: float) -> Image.Image:
    with Image.open(source_path) as opened:
        source = opened.convert("RGBA")
    if source.size != size:
        source = source.resize(size, Image.Resampling.NEAREST)

    if name.endswith("_far"):
        return build_far_plane(source)

    pixels = source.load()
    width, height = source.size
    start_y = round((height - 1) * fade_start)
    end_y = max(start_y + 1, round((height - 1) * fade_end))
    seed = sum((index + 1) * ord(character) for index, character in enumerate(name))
    for y in range(height):
        if y < start_y:
            coverage = 0.0
        elif y >= end_y:
            coverage = 1.0
        else:
            t = (y - start_y) / (end_y - start_y)
            coverage = t * t * (3.0 - 2.0 * t)
        limit = round(coverage * 256)
        for x in range(width):
            red, green, blue, alpha = pixels[x, y]
            seam_x = 0 if x == width - 1 else x
            keep = alpha > 0 and stable_threshold(seam_x, y, seed) < limit
            pixels[x, y] = (red, green, blue, 255 if keep else 0)
    return source


def frame_entry(sprite_id: str, path: str, size: tuple[int, int]) -> dict:
    width, height = size
    plane = sprite_id.rsplit("_", 1)[-1]
    biome = sprite_id.split("/")[-1].removesuffix(f"_{plane}")
    return {
        "id": sprite_id,
        "path": path,
        "category": "Background",
        "width": width,
        "height": height,
        "atlasId": "backgrounds",
        "originX": 0,
        "originY": height - 1,
        "renderLayer": f"background.{plane}",
        "license": "YjsE-Project-Owned",
        "provenance": "background_depth_v6_provenance.json; deterministic binary-alpha extraction from project-owned panoramas",
        "tags": [
            "world", "background", "parallax", "depth-plane", plane, biome,
            "binary-alpha", "runtime-active", "version-6",
        ],
    }


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    entries = []
    records = []
    previews = []
    for name, sprite_id, source_relative, size, fade_start, fade_end in SPECS:
        source_path = ROOT / source_relative
        output_relative = f"Assets/BackgroundDepthV6/{name}.png"
        output_path = ROOT / output_relative
        plane = build_plane(name, source_path, size, fade_start, fade_end)
        plane.save(output_path, format="PNG", optimize=False, compress_level=9)
        previews.append((name, plane))
        entries.append(frame_entry(sprite_id, output_relative, size))
        with Image.open(output_path) as opened:
            rgba = opened.convert("RGBA")
            alpha_values = sorted(set(rgba.getchannel("A").get_flattened_data()))
            first_column = list(rgba.crop((0, 0, 1, rgba.height)).get_flattened_data())
            last_column = list(rgba.crop((rgba.width - 1, 0, rgba.width, rgba.height)).get_flattened_data())
        records.append({
            "spriteId": sprite_id,
            "path": output_relative,
            "source": source_relative,
            "sourceSha256": sha256(source_path),
            "sha256": sha256(output_path),
            "dimensions": list(size),
            "fadeRatios": [fade_start, fade_end],
            "alphaValues": alpha_values,
            "horizontalSeamColumnsEqual": first_column == last_column,
            "method": (
                "row-luminance sky removal with a smoothstep-decreasing binary silhouette threshold"
                if name.endswith("_far")
                else "nearest-neighbor size normalization plus smoothstep-shaped stable-noise binary-alpha dissolve"
            ),
            "generator": GENERATOR,
            "license": "YjsE-Project-Owned",
            "runtimeIdTarget": sprite_id,
        })

    MANIFEST.write_text(json.dumps({"sprites": entries}, indent=2) + "\n", encoding="utf-8")
    BRIEFS.write_text(json.dumps({
        "version": 1,
        "scope": "background_depth_v6",
        "globalStyle": "Project-owned authored panorama color with binary-alpha, seam-safe depth extraction and point sampling.",
        "globalRequirements": [
            "Keep exact declared dimensions and horizontal wrapping.",
            "Use only alpha 0 or 255 so strict pixel-art sampling remains deterministic.",
            "Dissolve authored sky pixels gradually so stacked Mid/Near planes have no rectangular top cutoff.",
            "Keep Far, Mid and Near runtime textures independent and source-rectangle based.",
        ],
        "briefs": [
            {
                "spriteId": entry["id"],
                "outputPath": entry["path"],
                "width": entry["width"],
                "height": entry["height"],
                "subject": entry["id"].split("/")[-1].replace("_", " "),
                "prompt": (
                    "Remove the authored sky from this project-owned panorama and retain a seam-safe binary-alpha mountain/canopy silhouette."
                    if entry["id"].endswith("_far")
                    else "Derive one seam-safe pixel-art depth plane from the declared project-owned panorama with a stable binary dissolve into transparent sky."
                ),
                "requirements": [
                    "Preserve the source panorama palette and horizontal seam.",
                    "Keep alpha binary and remove the hard rectangular sky edge.",
                    f"Activate through runtime ID {entry['id']}.",
                ],
                "tags": entry["tags"],
                "runtimeIdTarget": entry["id"],
            }
            for entry in entries
        ],
    }, indent=2) + "\n", encoding="utf-8")

    preview_width = 1024
    cell_height = 280
    preview = Image.new("RGBA", (preview_width * 3, cell_height * 4), (21, 25, 34, 255))
    draw = ImageDraw.Draw(preview)
    for index, (name, plane) in enumerate(previews):
        column = index % 3
        row = index // 3
        target_width = preview_width - 24
        target_height = 224
        shown = plane.resize((target_width, target_height), Image.Resampling.NEAREST)
        x = column * preview_width + 12
        y = row * cell_height + 32
        draw.text((x, row * cell_height + 10), name.upper(), fill=(220, 230, 236, 255))
        preview.alpha_composite(shown, (x, y))
    preview.save(PREVIEW, format="PNG", optimize=False, compress_level=9)

    PROVENANCE.write_text(json.dumps({
        "version": 1,
        "scope": "background_depth_v6",
        "generatedAt": "2026-07-19",
        "generator": GENERATOR,
        "generatorSha256": sha256(Path(__file__).resolve()),
        "sourceBrief": "asset_briefs/background_depth_v6_briefs.json",
        "manifest": "assets/background_depth_v6.sprites.json",
        "preview": "art_direction/background_depth_v6_contact_sheet.png",
        "previewSha256": sha256(PREVIEW),
        "records": records,
    }, indent=2) + "\n", encoding="utf-8")
    print(f"Generated {len(entries)} background depth planes.")


if __name__ == "__main__":
    main()
