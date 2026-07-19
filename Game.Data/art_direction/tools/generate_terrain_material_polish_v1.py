#!/usr/bin/env python3
"""Generate the deterministic YjsE terrain-material polish V1 autotiles."""

from __future__ import annotations

from dataclasses import dataclass
from hashlib import sha256
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


DATA_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_ROOT = DATA_ROOT / "Assets" / "TerrainPolishV1" / "world" / "tiles"
MANIFEST_PATH = DATA_ROOT / "assets" / "terrain_material_polish_v1.sprites.json"
BRIEF_PATH = DATA_ROOT / "asset_briefs" / "terrain_material_polish_v1_briefs.json"
PROVENANCE_PATH = DATA_ROOT / "art_direction" / "terrain_material_polish_v1_provenance.json"
PREVIEW_PATH = DATA_ROOT / "art_direction" / "terrain_material_polish_v1_preview.png"
SOURCE_PATH = DATA_ROOT / "art_direction" / "generated_sources" / "terrain_material_polish_v1_source.png"
GENERATOR_RELATIVE = "art_direction/tools/generate_terrain_material_polish_v1.py"
SOURCE_PROMPT = (
    "Use case: stylized-concept. Asset type: game terrain-material concept board used as art-direction "
    "reference for deterministic 16x16 autotile production. Use the existing YjsE Wave 05 pixel-art "
    "materials and V5 biome palette as style references. Design six cohesive side-view pixel-art material "
    "families: forest grass-over-loam, deep forest loam, layered gray cave stone, glowing amberstone, wet "
    "twilight-marsh moss, and gnarled amberwood planks. Use crisp readable 1x clusters, restrained original "
    "Terraria-like looseness, top-left lighting, four to six intentional tones, and no antialiasing, blur, "
    "gradients or noisy dithering. Present a clean unlabeled 3x2 board where each cell demonstrates connected "
    "top, side, inner and bottom edges plus an isolated block. No text, logos, watermark, copied sprites, UI, "
    "characters, background scene, perspective cubes, 3D rendering or soft transparency."
)


@dataclass(frozen=True)
class Material:
    sprite_id: str
    filename: str
    display_name: str
    kind: str
    palette: tuple[str, ...]


MATERIALS = (
    Material(
        "world/tiles/polish_v1/forest_grass_loam_autotile",
        "forest_grass_loam_autotile.png",
        "Forest Grass And Loam",
        "grass",
        ("#1b1826", "#352319", "#5a3825", "#8b5a34", "#263f38", "#35614a", "#7fba68", "#b8d47a"),
    ),
    Material(
        "world/tiles/polish_v1/forest_loam_autotile",
        "forest_loam_autotile.png",
        "Deep Forest Loam",
        "loam",
        ("#1b1826", "#352319", "#5a3825", "#8b5a34", "#c58b52", "#f0a35b"),
    ),
    Material(
        "world/tiles/polish_v1/layered_stone_autotile",
        "layered_stone_autotile.png",
        "Layered Cave Stone",
        "stone",
        ("#1b1826", "#2a2635", "#4a4555", "#716a7b", "#aaa2b0", "#f4e9d8"),
    ),
    Material(
        "world/tiles/polish_v1/amberstone_autotile",
        "amberstone_autotile.png",
        "Veined Amberstone",
        "amber",
        ("#1b1826", "#352319", "#58412a", "#8b5a34", "#b97732", "#f0c35a", "#fff1a6"),
    ),
    Material(
        "world/tiles/polish_v1/marsh_moss_autotile",
        "marsh_moss_autotile.png",
        "Twilight Marsh Moss",
        "moss",
        ("#1b1826", "#20244f", "#30233d", "#263f38", "#35614a", "#4f8a5b", "#7fba68", "#8b66a1"),
    ),
    Material(
        "world/tiles/polish_v1/amberwood_plank_autotile",
        "amberwood_plank_autotile.png",
        "Gnarled Amberwood Planks",
        "plank",
        ("#1b1826", "#352319", "#5a3825", "#8b5a34", "#c58b52", "#f0a35b", "#f0c35a"),
    ),
)


def rgba(hex_color: str) -> tuple[int, int, int, int]:
    value = hex_color.removeprefix("#")
    return tuple(int(value[index:index + 2], 16) for index in (0, 2, 4)) + (255,)


def draw_material_frame(material: Material, mask: int) -> Image.Image:
    colors = tuple(rgba(color) for color in material.palette)
    outline = colors[0]
    dark = colors[1]
    base = colors[2]
    mid = colors[min(3, len(colors) - 1)]
    light = colors[min(4, len(colors) - 1)]
    accent = colors[-1]
    image = Image.new("RGBA", (16, 16), base)
    draw = ImageDraw.Draw(image)

    # Broad material planes; no random single-pixel noise.
    draw.rectangle((0, 8, 15, 15), fill=dark)
    draw.rectangle((1, 7, 6, 11), fill=mid)
    draw.rectangle((9, 3, 15, 8), fill=mid)
    draw.rectangle((3, 12, 10, 15), fill=base)

    if material.kind in ("grass", "loam"):
        draw.line((1, 6, 5, 4, 9, 6, 14, 3), fill=light)
        draw.line((3, 10, 6, 8, 10, 10, 13, 8), fill=mid)
        draw.rectangle((4, 13, 7, 14), fill=mid)
        if material.kind == "grass" and not mask & 1:
            draw.rectangle((0, 1, 15, 3), fill=colors[5])
            draw.line((0, 2, 2, 0, 4, 3, 7, 0, 9, 3, 12, 0, 15, 2), fill=accent)
            draw.line((3, 3, 3, 8), fill=light)
            draw.line((11, 3, 10, 7), fill=light)
    elif material.kind == "stone":
        draw.line((0, 5, 5, 3, 10, 5, 15, 2), fill=light)
        draw.line((5, 3, 7, 8, 12, 10, 15, 9), fill=dark)
        draw.line((0, 12, 4, 9, 8, 11, 13, 8), fill=colors[3])
        draw.rectangle((2, 1, 6, 2), fill=accent)
    elif material.kind == "amber":
        draw.line((1, 12, 4, 8, 7, 10, 10, 5, 14, 3), fill=colors[4])
        draw.line((4, 8, 7, 10, 10, 5), fill=accent)
        draw.rectangle((11, 11, 13, 12), fill=colors[5])
        draw.point((12, 11), fill=accent)
    elif material.kind == "moss":
        draw.rectangle((0, 4, 15, 7), fill=colors[4])
        draw.line((1, 6, 1, 10, 4, 6, 5, 9, 8, 6, 10, 11, 13, 6, 14, 9), fill=colors[5])
        if not mask & 1:
            draw.line((0, 2, 3, 0, 5, 3, 8, 1, 11, 3, 14, 0, 15, 2), fill=colors[6])
        draw.rectangle((11, 12, 12, 13), fill=accent)
    elif material.kind == "plank":
        image.paste(mid, (0, 0, 16, 16))
        draw.rectangle((0, 0, 15, 3), fill=light)
        draw.rectangle((0, 8, 15, 9), fill=dark)
        draw.line((7, 0, 7, 8), fill=dark)
        draw.line((12, 9, 12, 15), fill=dark)
        draw.line((1, 5, 5, 5), fill=colors[4])
        draw.rectangle((9, 4, 11, 6), outline=dark)
        draw.point((10, 5), fill=accent)
        draw.line((1, 12, 8, 12), fill=colors[4])

    # Cardinal connectivity: up=1, right=2, down=4, left=8.
    if not mask & 1:
        draw.line((0, 0, 15, 0), fill=outline)
    if not mask & 2:
        draw.line((15, 0, 15, 15), fill=outline)
        draw.line((14, 2, 14, 13), fill=dark)
    if not mask & 4:
        draw.line((0, 15, 15, 15), fill=outline)
        draw.line((2, 14, 5, 13, 9, 14, 13, 12), fill=dark)
    if not mask & 8:
        draw.line((0, 0, 0, 15), fill=outline)
        draw.line((1, 2, 1, 13), fill=dark)

    return image


def draw_sheet(material: Material) -> Image.Image:
    sheet = Image.new("RGBA", (256, 16), (0, 0, 0, 0))
    for mask in range(16):
        sheet.paste(draw_material_frame(material, mask), (mask * 16, 0))
    return sheet


def frames() -> list[dict]:
    return [
        {
            "id": f"mask_{mask}",
            "x": mask * 16,
            "y": 0,
            "width": 16,
            "height": 16,
            "originX": 8,
            "originY": 8,
            "autoTileMask": mask,
        }
        for mask in range(16)
    ]


def write_preview(sheets: dict[str, Image.Image]) -> None:
    scale = 4
    row_height = 16 * scale + 28
    preview = Image.new("RGBA", (256 * scale + 32, row_height * len(MATERIALS) + 24), (238, 232, 218, 255))
    draw = ImageDraw.Draw(preview)
    font = ImageFont.load_default()
    for index, material in enumerate(MATERIALS):
        y = 12 + index * row_height
        draw.text((16, y), material.display_name.upper(), fill=(27, 24, 38, 255), font=font)
        scaled = sheets[material.sprite_id].resize((256 * scale, 16 * scale), Image.Resampling.NEAREST)
        preview.paste(scaled, (16, y + 18), scaled)
    preview.save(PREVIEW_PATH, "PNG", optimize=False, compress_level=9)


def main() -> None:
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    sheets: dict[str, Image.Image] = {}
    assets = []
    manifest = {"sprites": []}
    brief = {
        "version": 1,
        "scope": "terrain_material_polish_v1",
        "globalStyle": "YjsE yjse-pixel-v1 side-view terrain with broad clusters, top-left light and binary alpha.",
        "globalNegativePrompt": "blur, antialiasing, gradients, random pixel noise, copied game art, watermark, text",
        "sourceConcept": "art_direction/generated_sources/terrain_material_polish_v1_source.png",
        "sourcePrompt": SOURCE_PROMPT,
        "briefs": [],
    }

    for material in MATERIALS:
        image = draw_sheet(material)
        output = OUTPUT_ROOT / material.filename
        image.save(output, "PNG", optimize=False, compress_level=9)
        sheets[material.sprite_id] = image
        relative = output.relative_to(DATA_ROOT).as_posix()
        manifest["sprites"].append({
            "id": material.sprite_id,
            "path": relative,
            "category": "Tile",
            "width": 256,
            "height": 16,
            "pixelsPerUnit": 16,
            "atlasId": "terrain.polish.v1",
            "originX": 8,
            "originY": 8,
            "renderLayer": "tiles.front",
            "license": "YjsE-Project-Owned",
            "provenance": "terrain_material_polish_v1_provenance.json; built-in imagegen concept plus deterministic final-size generator",
            "tags": ["production", "runtime-active", "terrain-polish", "autotile", material.kind],
            "frames": frames(),
        })
        brief["briefs"].append({
            "spriteId": material.sprite_id,
            "outputPath": relative,
            "width": 256,
            "height": 16,
            "subject": material.display_name,
            "prompt": f"Translate the checked-in terrain concept into an original {material.display_name} 16-mask YjsE autotile sheet.",
            "requirements": [
                "Export exactly sixteen 16x16 frames in cardinal mask order 0-15.",
                "Keep connected sides seamless and emphasize only exposed edges.",
                "Use binary alpha, nearest-neighbor pixels and broad readable clusters.",
                "Keep every runtime frame at native density; no source-image raster scaling.",
            ],
            "palette": list(material.palette),
            "tags": ["production", "runtime-active", "terrain-polish", "autotile", material.kind],
        })
        assets.append({
            "spriteId": material.sprite_id,
            "path": relative,
            "sha256": sha256(output.read_bytes()).hexdigest(),
            "dimensions": [256, 16],
            "frameCount": 16,
            "generator": GENERATOR_RELATIVE,
            "method": "deterministic Pillow pixel primitives at exact final dimensions",
            "runtimeConsumer": "TileDefinition texture -> TilemapRenderer autotile mask frame",
        })

    write_preview(sheets)
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    BRIEF_PATH.write_text(json.dumps(brief, indent=2) + "\n", encoding="utf-8")
    provenance = {
        "schemaVersion": 1,
        "waveId": "terrain_material_polish_v1",
        "generatedOn": "2026-07-19",
        "sourceType": "built-in imagegen art-direction source plus checked-in deterministic pixel generator",
        "externalDownloads": False,
        "modelGeneratedSource": True,
        "source": {
            "path": SOURCE_PATH.relative_to(DATA_ROOT).as_posix(),
            "sha256": sha256(SOURCE_PATH.read_bytes()).hexdigest(),
            "role": "visual design reference only; production pixels are emitted at final size",
            "prompt": SOURCE_PROMPT,
        },
        "generator": {
            "path": GENERATOR_RELATIVE,
            "sha256": sha256(Path(__file__).read_bytes()).hexdigest(),
            "method": "direct final-size binary-alpha pixel primitives; no raster resize of the concept source",
        },
        "manifest": MANIFEST_PATH.relative_to(DATA_ROOT).as_posix(),
        "brief": BRIEF_PATH.relative_to(DATA_ROOT).as_posix(),
        "preview": PREVIEW_PATH.relative_to(DATA_ROOT).as_posix(),
        "assets": assets,
    }
    PROVENANCE_PATH.write_text(json.dumps(provenance, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"assets": len(assets), "preview": str(PREVIEW_PATH)}, indent=2))


if __name__ == "__main__":
    main()
