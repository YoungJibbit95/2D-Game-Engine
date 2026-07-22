#!/usr/bin/env python3
"""Generate the deterministic YjsE mobility-accessory production set."""

from __future__ import annotations

from dataclasses import dataclass
from hashlib import sha256
import json
from pathlib import Path
from typing import Callable

from PIL import Image, ImageDraw


DATA_ROOT = Path(__file__).resolve().parents[3]
WAVE_ROOT = DATA_ROOT / "assets" / "MobilityAccessories"
MANIFEST_PATH = DATA_ROOT / "assets" / "mobility_accessories.sprites.json"
BRIEF_PATH = DATA_ROOT / "asset_briefs" / "mobility_accessories_briefs.json"
PROVENANCE_PATH = DATA_ROOT / "art_direction" / "mobility_accessories_provenance.json"
PREVIEW_PATH = DATA_ROOT / "art_direction" / "mobility_accessories_contact_sheet.png"
PREVIEW_SUMMARY_PATH = DATA_ROOT / "art_direction" / "mobility_accessories_preview_summary.json"
GENERATOR_RELATIVE = "assets/MobilityAccessories/tools/generate_mobility_accessories.py"
WAVE_ID = "mobility_accessories_v1"
HUD_SPRITE_ID = "ui/mobility_abilities"
HUD_RELATIVE_PATH = "assets/MobilityAccessories/ui/mobility_abilities.png"

C = {
    "clear": (0, 0, 0, 0),
    "outline": (27, 24, 38, 255),
    "neutral0": (42, 38, 53, 255),
    "neutral1": (74, 69, 85, 255),
    "neutral2": (113, 106, 123, 255),
    "neutral3": (170, 162, 176, 255),
    "pale": (244, 233, 216, 255),
    "wood0": (53, 35, 25, 255),
    "wood1": (90, 56, 37, 255),
    "wood2": (139, 90, 52, 255),
    "wood3": (197, 139, 82, 255),
    "copper0": (91, 43, 34, 255),
    "copper1": (143, 69, 49, 255),
    "copper2": (197, 109, 62, 255),
    "copper3": (240, 163, 91, 255),
    "mana0": (36, 59, 120, 255),
    "mana1": (53, 92, 181, 255),
    "mana2": (75, 145, 222, 255),
    "mana3": (145, 215, 255, 255),
    "crystal0": (32, 36, 79, 255),
    "crystal1": (53, 60, 134, 255),
    "crystal2": (83, 103, 200, 255),
    "crystal3": (120, 183, 227, 255),
    "crystal4": (185, 239, 255, 255),
    "teal0": (31, 92, 98, 255),
    "teal1": (61, 141, 132, 255),
    "teal2": (114, 195, 174, 255),
}


@dataclass(frozen=True)
class AssetSpec:
    sprite_id: str
    file_name: str
    item_id: str
    subject: str
    ability: str
    draw: Callable[[], Image.Image]


def canvas() -> Image.Image:
    return Image.new("RGBA", (32, 32), C["clear"])


def draw_double_jump_boots() -> Image.Image:
    result = canvas()
    draw = ImageDraw.Draw(result)

    # Rear boot: compact blue-steel silhouette with a visible airborne heel vane.
    draw.polygon(((16, 4), (27, 4), (28, 17), (29, 21), (28, 27), (17, 27)), fill=C["outline"])
    draw.rectangle((18, 6, 25, 11), fill=C["mana0"])
    draw.polygon(((18, 12), (26, 12), (26, 18), (28, 21), (27, 24), (19, 24)), fill=C["mana1"])
    draw.rectangle((19, 7, 24, 8), fill=C["mana2"])
    draw.rectangle((24, 14, 26, 19), fill=C["neutral2"])
    draw.rectangle((18, 24, 27, 25), fill=C["crystal3"])

    # Front boot: warm leather keeps the pair readable from the flight items.
    draw.polygon(((4, 3), (15, 3), (16, 17), (22, 21), (22, 28), (4, 28)), fill=C["outline"])
    draw.rectangle((6, 5, 13, 10), fill=C["wood2"])
    draw.rectangle((7, 6, 12, 7), fill=C["wood3"])
    draw.polygon(((6, 11), (14, 11), (14, 18), (20, 22), (20, 25), (6, 25)), fill=C["copper1"])
    draw.rectangle((7, 12, 12, 20), fill=C["copper2"])
    draw.rectangle((8, 13, 11, 15), fill=C["copper3"])
    draw.rectangle((6, 25, 20, 26), fill=C["neutral1"])

    # Twin lift crystals communicate the second impulse without text or effects.
    draw.polygon(((3, 8), (6, 5), (8, 8), (6, 12)), fill=C["outline"])
    draw.polygon(((5, 8), (6, 7), (7, 8), (6, 10)), fill=C["mana3"])
    draw.polygon(((13, 4), (16, 2), (18, 5), (16, 8)), fill=C["outline"])
    draw.polygon(((15, 5), (16, 4), (17, 5), (16, 6)), fill=C["crystal4"])
    return result


def draw_skyward_wings() -> Image.Image:
    result = canvas()
    draw = ImageDraw.Draw(result)

    # Broad mirrored silhouette remains legible as a 16-pixel inventory downscale.
    left_outline = ((15, 7), (11, 3), (5, 2), (7, 8), (2, 10), (6, 15), (3, 20), (10, 19), (11, 27), (16, 21))
    right_outline = tuple((31 - x, y) for x, y in left_outline)
    draw.polygon(left_outline, fill=C["outline"])
    draw.polygon(right_outline, fill=C["outline"])

    draw.polygon(((14, 8), (11, 5), (7, 4), (9, 9), (5, 11), (9, 14), (6, 18), (12, 17), (13, 23), (15, 20)), fill=C["crystal2"])
    draw.polygon(((17, 8), (20, 5), (24, 4), (22, 9), (26, 11), (22, 14), (25, 18), (19, 17), (18, 23), (16, 20)), fill=C["mana1"])

    draw.line((9, 6, 13, 16), fill=C["crystal3"], width=2)
    draw.line((22, 6, 18, 16), fill=C["mana2"], width=2)
    draw.line((7, 12, 12, 15), fill=C["crystal4"], width=1)
    draw.line((24, 12, 19, 15), fill=C["mana3"], width=1)
    draw.rectangle((13, 10, 18, 21), fill=C["outline"])
    draw.rectangle((15, 11, 16, 19), fill=C["neutral2"])
    draw.point((15, 10), fill=C["pale"])
    draw.rectangle((12, 20, 19, 23), fill=C["outline"])
    draw.rectangle((14, 20, 17, 21), fill=C["copper2"])
    return result


def draw_ether_glider() -> Image.Image:
    result = canvas()
    draw = ImageDraw.Draw(result)

    # A rigid triangular sail differentiates gliding from powered wing flight.
    draw.polygon(((2, 10), (16, 2), (29, 10), (26, 22), (18, 17), (18, 29), (13, 29), (13, 17), (6, 22)), fill=C["outline"])
    draw.polygon(((5, 10), (15, 5), (14, 15), (7, 19)), fill=C["teal1"])
    draw.polygon(((17, 5), (27, 10), (25, 19), (17, 15)), fill=C["mana1"])
    draw.polygon(((8, 10), (14, 7), (14, 13), (9, 16)), fill=C["teal2"])
    draw.polygon(((18, 7), (24, 10), (23, 16), (18, 13)), fill=C["mana2"])
    draw.line((4, 10, 28, 10), fill=C["neutral3"], width=1)
    draw.line((16, 4, 16, 26), fill=C["outline"], width=2)
    draw.rectangle((15, 5, 16, 14), fill=C["crystal4"])
    draw.rectangle((14, 16, 17, 27), fill=C["wood1"])
    draw.rectangle((15, 17, 16, 25), fill=C["wood3"])
    draw.rectangle((11, 23, 20, 26), fill=C["outline"])
    draw.rectangle((13, 23, 18, 24), fill=C["copper2"])
    return result


def draw_mobility_hud_sheet() -> Image.Image:
    """Build three native 16x16 HUD glyphs in one shared texture resource."""
    result = Image.new("RGBA", (48, 16), C["clear"])
    draw = ImageDraw.Draw(result)

    # Frame 0: impulse boot plus two rising chevrons.
    draw.polygon(((2, 6), (7, 6), (8, 10), (12, 11), (12, 14), (2, 14)), fill=C["outline"])
    draw.polygon(((3, 7), (6, 7), (7, 11), (10, 12), (10, 13), (3, 13)), fill=C["copper2"])
    draw.rectangle((3, 8, 5, 10), fill=C["copper3"])
    draw.line((4, 5, 6, 3, 8, 5), fill=C["outline"], width=1)
    draw.point((6, 3), fill=C["crystal4"])
    draw.line((8, 7, 11, 4, 14, 7), fill=C["outline"], width=1)
    draw.line((10, 5, 12, 5), fill=C["mana3"], width=1)

    # Frame 1: powered mirrored wings and a compact harness.
    offset = 16
    draw.polygon(
        tuple((offset + x, y) for x, y in ((7, 5), (4, 2), (1, 3), (3, 7), (1, 10), (6, 9), (7, 14))),
        fill=C["outline"],
    )
    draw.polygon(
        tuple((offset + x, y) for x, y in ((8, 5), (11, 2), (14, 3), (12, 7), (14, 10), (9, 9), (8, 14))),
        fill=C["outline"],
    )
    draw.polygon(
        tuple((offset + x, y) for x, y in ((6, 6), (4, 4), (3, 4), (5, 7), (3, 9), (6, 8), (7, 11))),
        fill=C["crystal2"],
    )
    draw.polygon(
        tuple((offset + x, y) for x, y in ((9, 6), (11, 4), (12, 4), (10, 7), (12, 9), (9, 8), (8, 11))),
        fill=C["mana2"],
    )
    draw.rectangle((offset + 7, 6, offset + 8, 12), fill=C["neutral2"])
    draw.rectangle((offset + 6, 12, offset + 9, 14), fill=C["copper2"])

    # Frame 2: rigid canopy, central spar and grip for controlled glide.
    offset = 32
    draw.polygon(
        tuple((offset + x, y) for x, y in ((1, 6), (7, 2), (14, 6), (12, 10), (8, 8), (8, 14), (6, 14), (6, 8), (3, 10))),
        fill=C["outline"],
    )
    draw.polygon(
        tuple((offset + x, y) for x, y in ((3, 6), (6, 4), (6, 7), (4, 8))),
        fill=C["teal2"],
    )
    draw.polygon(
        tuple((offset + x, y) for x, y in ((8, 4), (12, 6), (11, 8), (8, 7))),
        fill=C["mana2"],
    )
    draw.rectangle((offset + 7, 3, offset + 7, 12), fill=C["crystal4"])
    draw.rectangle((offset + 6, 12, offset + 8, 13), fill=C["wood3"])
    return result


def specs() -> tuple[AssetSpec, ...]:
    return (
        AssetSpec(
            "items/double_jump_boots",
            "double_jump_boots.png",
            "double_jump_boots",
            "Paired leather and blue-steel impulse boots with twin lift crystals",
            "double-jump",
            draw_double_jump_boots,
        ),
        AssetSpec(
            "items/skyward_wings",
            "skyward_wings.png",
            "skyward_wings",
            "Compact mirrored sky-crystal wings on a wearable harness",
            "flight",
            draw_skyward_wings,
        ),
        AssetSpec(
            "items/ether_glider",
            "ether_glider.png",
            "ether_glider",
            "Rigid teal-and-mana ether glider with a central grip harness",
            "glide",
            draw_ether_glider,
        ),
    )


def manifest_entry(spec: AssetSpec) -> dict:
    return {
        "id": spec.sprite_id,
        "path": f"assets/MobilityAccessories/items/{spec.file_name}",
        "category": "Item",
        "width": 32,
        "height": 32,
        "pixelsPerUnit": 16,
        "atlasId": "mobility.accessories",
        "originX": 16,
        "originY": 16,
        "renderLayer": "items",
        "license": "YjsE-Project-Owned",
        "provenance": f"{WAVE_ID}; deterministic Pillow generator; mobility_accessories_provenance.json",
        "tags": [
            "production-sample",
            "runtime-preloaded",
            "item",
            "accessory",
            "mobility",
            spec.ability,
        ],
    }


def hud_manifest_entry() -> dict:
    return {
        "id": HUD_SPRITE_ID,
        "path": HUD_RELATIVE_PATH,
        "category": "UI",
        "width": 48,
        "height": 16,
        "pixelsPerUnit": 16,
        "atlasId": "ui.mobility",
        "originX": 8,
        "originY": 8,
        "renderLayer": "ui.icon.ability",
        "license": "YjsE-Project-Owned",
        "provenance": f"{WAVE_ID}; deterministic Pillow generator; mobility_accessories_provenance.json",
        "frames": [
            {"id": "double_jump", "x": 0, "y": 0, "width": 16, "height": 16, "originX": 8, "originY": 8},
            {"id": "flight", "x": 16, "y": 0, "width": 16, "height": 16, "originX": 8, "originY": 8},
            {"id": "glide", "x": 32, "y": 0, "width": 16, "height": 16, "originX": 8, "originY": 8},
        ],
        "tags": [
            "production-sample",
            "runtime-preloaded",
            "ui",
            "hud",
            "ability",
            "mobility",
            "sprite-sheet",
        ],
    }


def brief_entry(spec: AssetSpec) -> dict:
    return {
        "spriteId": spec.sprite_id,
        "outputPath": f"assets/MobilityAccessories/items/{spec.file_name}",
        "width": 32,
        "height": 32,
        "subject": spec.subject,
        "prompt": (
            f"YjsE yjse-pixel-v1 inventory accessory icon: {spec.subject}. "
            "Crisp native pixel clusters, continuous one-pixel dark silhouette, top-left lighting, "
            "restrained three-quarter item view, immediately readable mobility function."
        ),
        "negativePrompt": "blur, partial alpha, antialiasing, soft glow, gradients, text, logo, watermark, copied game art",
        "requirements": [
            "Export exactly 32x32 RGBA with at least two transparent pixels on every canvas edge.",
            "Use binary alpha only and nearest-neighbor pixel placement.",
            "Use only colors declared by yjse-pixel-v1.",
            "Keep the outer boundary predominantly #1b1826 and readable on light and dark UI fields.",
            "Render as one exact source rectangle with no animation frames.",
            f"Runtime target: {spec.sprite_id}; item definition: items/{spec.item_id}.json.",
        ],
        "palette": sorted({"#%02x%02x%02x" % value[:3] for value in C.values() if value[3] > 0}),
        "tags": ["item", "accessory", "mobility", spec.ability, "production-sample"],
    }


def hud_brief_entry() -> dict:
    return {
        "spriteId": HUD_SPRITE_ID,
        "outputPath": HUD_RELATIVE_PATH,
        "width": 48,
        "height": 16,
        "subject": "Three-frame mobility HUD sheet: impulse jump boot, powered wings, rigid glider",
        "prompt": (
            "YjsE yjse-pixel-v1 HUD ability sheet with three native 16x16 frames in strict order: "
            "double-jump boot and rising chevrons, powered mirrored wings, rigid glider canopy. "
            "Crisp binary-alpha clusters, continuous dark silhouettes and top-left highlights."
        ),
        "negativePrompt": "blur, partial alpha, antialiasing, gradients, text, logo, watermark, duplicate textures",
        "requirements": [
            "Export exactly 48x16 RGBA as one shared texture with three 16x16 source rectangles.",
            "Frame order is double_jump, flight, glide; every frame keeps a transparent one-pixel canvas margin.",
            "Use binary alpha, nearest-neighbor pixel placement and only yjse-pixel-v1 colors.",
            "Keep each glyph readable inside compact, regular and expanded HUD slots.",
            "Runtime target: GameplayFeedbackOverlay through MobilityAbilityDockPlanner and ClientTextureRegistry.",
        ],
        "palette": sorted({"#%02x%02x%02x" % value[:3] for value in C.values() if value[3] > 0}),
        "tags": ["ui", "hud", "ability", "mobility", "sprite-sheet", "production-sample"],
    }


def flattened_alpha(image: Image.Image) -> list[int]:
    alpha = image.getchannel("A")
    getter = getattr(alpha, "get_flattened_data", None)
    return list(getter() if getter else alpha.getdata())


def validate_image(sprite_id: str, image: Image.Image, expected_size: tuple[int, int]) -> None:
    if image.mode != "RGBA" or image.size != expected_size:
        raise RuntimeError(f"{sprite_id}: expected {expected_size} RGBA, got {image.size} {image.mode}")
    alpha_values = sorted(set(flattened_alpha(image)))
    if any(value not in (0, 255) for value in alpha_values):
        raise RuntimeError(f"{sprite_id}: partial alpha values {alpha_values}")
    if image.getchannel("A").getbbox() is None:
        raise RuntimeError(f"{sprite_id}: generated sprite is empty")
    width, height = expected_size
    corners = ((0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1))
    if any(image.getpixel(point)[3] != 0 for point in corners):
        raise RuntimeError(f"{sprite_id}: transparent corners are required")


def validate_sprite(spec: AssetSpec, image: Image.Image) -> None:
    validate_image(spec.sprite_id, image, (32, 32))


def validate_hud_sheet(image: Image.Image) -> None:
    validate_image(HUD_SPRITE_ID, image, (48, 16))
    for frame_index, frame_id in enumerate(("double_jump", "flight", "glide")):
        frame = image.crop((frame_index * 16, 0, frame_index * 16 + 16, 16))
        bounds = frame.getchannel("A").getbbox()
        if bounds is None:
            raise RuntimeError(f"{HUD_SPRITE_ID}/{frame_id}: frame is empty")
        if bounds[0] < 1 or bounds[1] < 1 or bounds[2] > 15 or bounds[3] > 15:
            raise RuntimeError(f"{HUD_SPRITE_ID}/{frame_id}: expected a one-pixel transparent frame margin, got {bounds}")


def build_preview(
    asset_specs: tuple[AssetSpec, ...],
    images: dict[str, Image.Image],
    hud_sheet: Image.Image,
) -> None:
    cell_width = 112
    result = Image.new("RGB", (cell_width * (len(asset_specs) + 1), 128), (27, 24, 38))
    draw = ImageDraw.Draw(result)
    swatches = ((53, 92, 181), (83, 103, 200), (61, 141, 132))
    for index, spec in enumerate(asset_specs):
        left = index * cell_width
        draw.rectangle((left + 6, 6, left + 105, 121), fill=(42, 38, 53), outline=swatches[index], width=2)
        draw.rectangle((left + 11, 11, left + 100, 100), fill=(20, 20, 29))
        enlarged = images[spec.sprite_id].resize((80, 80), Image.Resampling.NEAREST)
        result.paste(enlarged, (left + 16, 16), enlarged)
        draw.rectangle((left + 12, 108, left + 99, 115), fill=swatches[index])
        draw.rectangle((left + 12, 117, left + 69, 119), fill=(170, 162, 176))

    left = len(asset_specs) * cell_width
    draw.rectangle((left + 6, 6, left + 105, 121), fill=(42, 38, 53), outline=(145, 215, 255), width=2)
    draw.rectangle((left + 11, 11, left + 100, 100), fill=(20, 20, 29))
    enlarged_hud = hud_sheet.resize((96, 32), Image.Resampling.NEAREST)
    result.paste(enlarged_hud, (left + 8, 40), enlarged_hud)
    draw.rectangle((left + 12, 108, left + 99, 115), fill=(145, 215, 255))
    draw.rectangle((left + 12, 117, left + 79, 119), fill=(170, 162, 176))
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    result.save(PREVIEW_PATH, "PNG", optimize=False, compress_level=9)


def main() -> None:
    asset_specs = specs()
    images: dict[str, Image.Image] = {}
    generated = []

    for spec in asset_specs:
        image = spec.draw()
        validate_sprite(spec, image)
        output = WAVE_ROOT / "items" / spec.file_name
        output.parent.mkdir(parents=True, exist_ok=True)
        image.save(output, "PNG", optimize=False, compress_level=9)
        images[spec.sprite_id] = image
        alpha_values = sorted(set(flattened_alpha(image)))
        generated.append(
            {
                "spriteId": spec.sprite_id,
                "itemId": spec.item_id,
                "path": f"assets/MobilityAccessories/items/{spec.file_name}",
                "sha256": sha256(output.read_bytes()).hexdigest(),
                "dimensions": [32, 32],
                "alphaValues": alpha_values,
                "generator": GENERATOR_RELATIVE,
                "method": "deterministic Pillow pixel primitives at final runtime dimensions",
                "license": "YjsE-Project-Owned",
                "runtimeConsumer": "ItemDefinition texture reference through SpriteAssetRegistry and ClientTextureRegistry",
            }
        )

    hud_sheet = draw_mobility_hud_sheet()
    validate_hud_sheet(hud_sheet)
    hud_output = WAVE_ROOT / "ui" / "mobility_abilities.png"
    hud_output.parent.mkdir(parents=True, exist_ok=True)
    hud_sheet.save(hud_output, "PNG", optimize=False, compress_level=9)
    generated.append(
        {
            "spriteId": HUD_SPRITE_ID,
            "path": HUD_RELATIVE_PATH,
            "sha256": sha256(hud_output.read_bytes()).hexdigest(),
            "dimensions": [48, 16],
            "frameDimensions": [16, 16],
            "frames": ["double_jump", "flight", "glide"],
            "alphaValues": sorted(set(flattened_alpha(hud_sheet))),
            "generator": GENERATOR_RELATIVE,
            "method": "deterministic Pillow pixel primitives at final runtime dimensions; one shared texture",
            "license": "YjsE-Project-Owned",
            "runtimeConsumer": "GameplayFeedbackOverlay through MobilityAbilityDockPlanner and ClientTextureRegistry",
        }
    )

    MANIFEST_PATH.write_text(
        json.dumps({"sprites": [*(manifest_entry(spec) for spec in asset_specs), hud_manifest_entry()]}, indent=2) + "\n",
        encoding="utf-8",
    )
    BRIEF_PATH.write_text(
        json.dumps(
            {
                "version": 1,
                "scope": WAVE_ID,
                "globalStyle": "YjsE yjse-pixel-v1 crisp inventory pixel art; binary alpha; top-left light; hard clusters.",
                "globalNegativePrompt": "blur, partial alpha, antialiasing, gradients, noise dithering, watermark, logo, text",
                "globalRequirements": [
                    "Export exact 32x32 manifest dimensions.",
                    "Use only colors declared in Game.Data/art_direction/yjse_pixel_style.json.",
                    "Keep silhouettes readable at native scale and when reduced to a 16-pixel slot icon.",
                    "Keep each mobility category visually distinct: boots, powered wings, rigid glider.",
                ],
                "briefs": [*(brief_entry(spec) for spec in asset_specs), hud_brief_entry()],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    build_preview(asset_specs, images, hud_sheet)
    generator_hash = sha256(Path(__file__).read_bytes()).hexdigest()
    provenance = {
        "schemaVersion": 1,
        "waveId": WAVE_ID,
        "generatedOn": "2026-07-22",
        "sourceType": "checked-in deterministic pixel generator",
        "externalDownloads": False,
        "modelGeneratedSource": False,
        "generator": {
            "path": GENERATOR_RELATIVE,
            "sha256": generator_hash,
            "runtime": "Python 3 plus Pillow from art_direction/requirements.txt",
            "method": "Direct final-size Pillow primitives constrained to yjse-pixel-v1; no hidden source art or manual raster edits.",
        },
        "manifest": "assets/mobility_accessories.sprites.json",
        "brief": "asset_briefs/mobility_accessories_briefs.json",
        "preview": "art_direction/mobility_accessories_contact_sheet.png",
        "assets": generated,
    }
    PROVENANCE_PATH.write_text(json.dumps(provenance, indent=2) + "\n", encoding="utf-8")
    PREVIEW_SUMMARY_PATH.write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "waveId": WAVE_ID,
                "preview": "art_direction/mobility_accessories_contact_sheet.png",
                "previewDimensions": [112 * (len(asset_specs) + 1), 128],
                "nativeSpriteDimensions": [32, 32],
                "hudSheetDimensions": [48, 16],
                "hudFrameDimensions": [16, 16],
                "scale": 2.5,
                "resampling": "nearest-neighbor",
                "spriteIds": [*(spec.sprite_id for spec in asset_specs), HUD_SPRITE_ID],
                "generatorSha256": generator_hash,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    print(json.dumps({"waveId": WAVE_ID, "assetCount": len(generated), "manifest": str(MANIFEST_PATH)}, indent=2))


if __name__ == "__main__":
    main()
