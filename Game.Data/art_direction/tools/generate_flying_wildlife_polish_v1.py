"""Generate the native 16x16 flying-wildlife polish family."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
SPRITES = ROOT / "sprites" / "entities" / "critters"
ART_DIRECTION = ROOT / "art_direction"
WAVE_ID = "flying_wildlife_polish_v1"
GENERATED_ON = "2026-07-22"
GENERATOR_VERSION = "1.0.0"
FRAME_NAMES = ("flight_0", "flight_1", "flight_2", "flight_3")

C = {
    "clear": (0, 0, 0, 0),
    "outline": (27, 24, 38, 255),
    "neutral_dark": (42, 38, 53, 255),
    "neutral_mid": (74, 69, 85, 255),
    "pale": (244, 233, 216, 255),
    "wood_dark": (53, 35, 25, 255),
    "wood_mid": (139, 90, 52, 255),
    "copper": (197, 109, 62, 255),
    "gold_dark": (185, 119, 50, 255),
    "gold": (240, 195, 90, 255),
    "gold_light": (255, 241, 166, 255),
    "pink_dark": (122, 63, 101, 255),
    "pink": (182, 90, 120, 255),
    "pink_light": (229, 139, 145, 255),
    "leaf_dark": (38, 63, 56, 255),
    "leaf_shadow": (53, 97, 74, 255),
    "leaf": (79, 138, 91, 255),
    "leaf_light": (127, 186, 104, 255),
    "crystal_dark": (32, 36, 79, 255),
    "crystal": (83, 103, 200, 255),
    "crystal_light": (120, 183, 227, 255),
    "crystal_tip": (185, 239, 255, 255),
}

ASSETS = (
    ("entities/critters/meadow_butterfly", "meadow_butterfly.png", "meadow_butterfly", "butterfly"),
    ("entities/critters/forest_moth", "forest_moth.png", "forest_moth", "moth"),
    ("entities/critters/cave_glowbug", "cave_glowbug.png", "cave_glowbug", "glowbug"),
)


def _poly(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], color: str) -> None:
    draw.polygon(points, fill=C[color])


def _rect(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: str) -> None:
    draw.rectangle(box, fill=C[color])


def _line(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], color: str) -> None:
    draw.line(points, fill=C[color], width=1)


def _butterfly_wings(draw: ImageDraw.ImageDraw, ox: int, phase: int) -> None:
    if phase == 0:
        upper = [(ox+8,8),(ox+6,3),(ox+3,1),(ox+1,3),(ox+2,6),(ox+6,8)]
        upper_inner = [(ox+6,6),(ox+5,3),(ox+3,2),(ox+2,4),(ox+3,6)]
        lower = [(ox+8,8),(ox+5,8),(ox+2,11),(ox+4,13),(ox+7,12),(ox+9,9)]
        lower_inner = [(ox+6,9),(ox+3,11),(ox+4,12),(ox+6,11)]
    elif phase == 1:
        upper = [(ox+8,8),(ox+5,4),(ox+2,3),(ox+1,6),(ox+4,8)]
        upper_inner = [(ox+6,7),(ox+4,5),(ox+2,5),(ox+3,7)]
        lower = [(ox+8,8),(ox+4,8),(ox+1,10),(ox+3,12),(ox+7,11)]
        lower_inner = [(ox+6,9),(ox+3,9),(ox+2,10),(ox+4,11)]
    elif phase == 2:
        upper = [(ox+8,8),(ox+5,5),(ox+1,5),(ox+0,8),(ox+4,9)]
        upper_inner = [(ox+6,7),(ox+4,6),(ox+2,6),(ox+2,8),(ox+5,8)]
        lower = [(ox+8,8),(ox+4,9),(ox+1,9),(ox+2,12),(ox+6,11)]
        lower_inner = [(ox+6,9),(ox+3,10),(ox+4,11),(ox+6,10)]
    else:
        upper = [(ox+8,8),(ox+5,6),(ox+2,6),(ox+3,9),(ox+6,9)]
        upper_inner = [(ox+6,7),(ox+4,7),(ox+5,8)]
        lower = [(ox+8,8),(ox+5,9),(ox+2,10),(ox+1,13),(ox+4,14),(ox+7,11)]
        lower_inner = [(ox+6,10),(ox+3,11),(ox+2,13),(ox+4,13),(ox+6,11)]

    _poly(draw, upper, "outline")
    _poly(draw, upper_inner, "pink_light")
    _poly(draw, lower, "outline")
    _poly(draw, lower_inner, "pink")
    draw.point((ox+4, 5 if phase < 2 else 7), fill=C["gold_light"])
    draw.point((ox+4, 11 if phase != 3 else 12), fill=C["gold"])


def _draw_butterfly_frame(draw: ImageDraw.ImageDraw, ox: int, phase: int) -> None:
    _butterfly_wings(draw, ox, phase)
    _poly(draw, [(ox+7,7),(ox+11,6),(ox+14,7),(ox+14,9),(ox+11,10),(ox+7,9)], "outline")
    _rect(draw, (ox+8,7,ox+11,8), "gold_dark")
    _rect(draw, (ox+11,7,ox+12,8), "gold")
    draw.point((ox+13,7), fill=C["gold_light"])
    _line(draw, [(ox+12,6),(ox+14,4),(ox+15,4)], "outline")
    _line(draw, [(ox+12,6),(ox+14,5)], "outline")


def _moth_wings(draw: ImageDraw.ImageDraw, ox: int, phase: int) -> None:
    if phase == 0:
        upper = [(ox+8,8),(ox+6,2),(ox+3,2),(ox+1,5),(ox+3,8)]
        upper_inner = [(ox+6,6),(ox+5,3),(ox+3,3),(ox+2,5),(ox+4,7)]
        lower = [(ox+8,8),(ox+4,8),(ox+2,10),(ox+4,13),(ox+7,11)]
        lower_inner = [(ox+6,9),(ox+4,9),(ox+3,10),(ox+4,11),(ox+6,10)]
    elif phase == 1:
        upper = [(ox+8,8),(ox+5,4),(ox+2,4),(ox+1,7),(ox+5,9)]
        upper_inner = [(ox+6,7),(ox+4,5),(ox+2,6),(ox+4,8)]
        lower = [(ox+8,8),(ox+4,9),(ox+2,11),(ox+5,12),(ox+8,10)]
        lower_inner = [(ox+6,9),(ox+4,10),(ox+5,11),(ox+7,10)]
    elif phase == 2:
        upper = [(ox+8,8),(ox+5,5),(ox+1,6),(ox+2,9),(ox+6,10)]
        upper_inner = [(ox+6,7),(ox+4,6),(ox+2,7),(ox+4,9)]
        lower = [(ox+8,8),(ox+5,9),(ox+3,11),(ox+6,12),(ox+9,9)]
        lower_inner = [(ox+7,9),(ox+5,10),(ox+6,11)]
    else:
        upper = [(ox+8,8),(ox+5,6),(ox+2,7),(ox+4,10),(ox+7,10)]
        upper_inner = [(ox+6,8),(ox+4,7),(ox+5,9)]
        lower = [(ox+8,8),(ox+5,10),(ox+3,13),(ox+6,14),(ox+9,10)]
        lower_inner = [(ox+7,10),(ox+5,12),(ox+6,13),(ox+8,10)]

    _poly(draw, upper, "outline")
    _poly(draw, upper_inner, "leaf")
    _poly(draw, lower, "outline")
    _poly(draw, lower_inner, "leaf_shadow")
    draw.point((ox+4, 5 if phase == 0 else 7), fill=C["leaf_light"])
    draw.point((ox+5, 11 if phase < 3 else 13), fill=C["gold_dark"])


def _draw_moth_frame(draw: ImageDraw.ImageDraw, ox: int, phase: int) -> None:
    _moth_wings(draw, ox, phase)
    _poly(draw, [(ox+7,7),(ox+11,6),(ox+13,7),(ox+14,9),(ox+11,10),(ox+7,9)], "outline")
    _rect(draw, (ox+8,7,ox+11,8), "wood_dark")
    _rect(draw, (ox+11,7,ox+12,8), "gold_dark")
    draw.point((ox+13,7), fill=C["gold_light"])
    _line(draw, [(ox+12,6),(ox+14,4)], "outline")
    _line(draw, [(ox+12,6),(ox+15,6)], "outline")


def _glowbug_wings(draw: ImageDraw.ImageDraw, ox: int, phase: int) -> None:
    if phase == 0:
        top = [(ox+8,7),(ox+6,2),(ox+3,3),(ox+5,7)]
        bottom = [(ox+8,9),(ox+5,9),(ox+3,12),(ox+6,13)]
    elif phase == 1:
        top = [(ox+8,7),(ox+5,4),(ox+2,5),(ox+5,8)]
        bottom = [(ox+8,9),(ox+5,9),(ox+2,10),(ox+5,12)]
    elif phase == 2:
        top = [(ox+8,7),(ox+5,6),(ox+2,7),(ox+5,9)]
        bottom = [(ox+8,9),(ox+5,9),(ox+3,11),(ox+6,12)]
    else:
        top = [(ox+8,7),(ox+5,7),(ox+3,9),(ox+6,10)]
        bottom = [(ox+8,9),(ox+6,10),(ox+4,14),(ox+7,13)]

    _poly(draw, top, "outline")
    _poly(draw, bottom, "outline")
    top_inner = [(x + (1 if x < ox+8 else 0), y + (1 if y < 8 else 0)) for x, y in top[1:]]
    bottom_inner = [(x + (1 if x < ox+8 else 0), y - (1 if y > 9 else 0)) for x, y in bottom[1:]]
    if len(top_inner) >= 3:
        _poly(draw, top_inner, "crystal_light")
    if len(bottom_inner) >= 3:
        _poly(draw, bottom_inner, "crystal")


def _draw_glowbug_frame(draw: ImageDraw.ImageDraw, ox: int, phase: int) -> None:
    _glowbug_wings(draw, ox, phase)
    _poly(draw, [(ox+5,7),(ox+9,6),(ox+13,7),(ox+14,9),(ox+11,11),(ox+6,10)], "outline")
    abdomen_color = ("crystal_light", "crystal_tip", "crystal_light", "crystal_tip")[phase]
    _rect(draw, (ox+6,8,ox+9,9), abdomen_color)
    _rect(draw, (ox+9,7,ox+11,9), "crystal_dark")
    _rect(draw, (ox+12,8,ox+13,8), "crystal")
    draw.point((ox+13,7), fill=C["crystal_tip"])
    _line(draw, [(ox+12,7),(ox+14,5)], "outline")
    _line(draw, [(ox+12,7),(ox+15,7)], "outline")


BUILDERS = {
    "butterfly": _draw_butterfly_frame,
    "moth": _draw_moth_frame,
    "glowbug": _draw_glowbug_frame,
}


def build_sheet(kind: str) -> Image.Image:
    source = Image.new("RGBA", (64, 16), C["clear"])
    draw = ImageDraw.Draw(source)
    for phase in range(4):
        BUILDERS[kind](draw, phase * 16, phase)
    source.putalpha(source.getchannel("A").point(lambda value: 255 if value else 0))
    return source


def _save(path: Path, source: Image.Image) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    source.save(path, format="PNG", optimize=False, compress_level=9)


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _frame_audit(source: Image.Image, index: int) -> dict[str, object]:
    frame = source.crop((index * 16, 0, index * 16 + 16, 16))
    alpha = frame.getchannel("A")
    bounds = alpha.getbbox()
    return {
        "id": FRAME_NAMES[index],
        "visibleBounds": list(bounds) if bounds is not None else None,
        "opaquePixelCount": sum(1 for value in alpha.get_flattened_data() if value == 255),
        "contentSha256": hashlib.sha256(frame.tobytes()).hexdigest(),
    }


def _build_contact_sheet(rendered: dict[str, Image.Image]) -> Image.Image:
    preview = Image.new("RGBA", (720, 420), (237, 233, 224, 255))
    draw = ImageDraw.Draw(preview)
    draw.text((18, 14), "YjsE FLYING WILDLIFE POLISH V1 - NATIVE FLIGHT PHASES", fill=(27, 24, 38, 255))
    draw.text((18, 31), "64x16 sheets | 4 x 16x16 | pivot 8,8 | nearest-neighbor", fill=(74, 69, 85, 255))
    labels = ("MEADOW BUTTERFLY", "FOREST MOTH", "CAVE GLOWBUG")
    for row, ((sprite_id, _filename, _entity_id, _kind), label) in enumerate(zip(ASSETS, labels, strict=True)):
        y = 58 + row * 118
        draw.text((18, y), label, fill=(27, 24, 38, 255))
        source = rendered[sprite_id]
        for index, frame_name in enumerate(FRAME_NAMES):
            x = 18 + index * 172
            checker = Image.new("RGBA", (96, 96), (244, 241, 234, 255))
            checker_draw = ImageDraw.Draw(checker)
            for cy in range(0, 96, 12):
                for cx in range(0, 96, 12):
                    if (cx // 12 + cy // 12) % 2:
                        checker_draw.rectangle((cx, cy, cx+11, cy+11), fill=(226, 222, 214, 255))
            frame = source.crop((index*16, 0, index*16+16, 16)).resize((96, 96), Image.Resampling.NEAREST)
            checker.alpha_composite(frame)
            preview.alpha_composite(checker, (x, y+17))
            draw.text((x+102, y+50), frame_name, fill=(42, 38, 53, 255))
    return preview


def main() -> None:
    rendered: dict[str, Image.Image] = {}
    for sprite_id, filename, _entity_id, kind in ASSETS:
        sheet = build_sheet(kind)
        _save(SPRITES / filename, sheet)
        rendered[sprite_id] = sheet

    preview_path = ART_DIRECTION / "flying_wildlife_polish_v1_contact_sheet.png"
    _save(preview_path, _build_contact_sheet(rendered))

    generator_path = Path(__file__).resolve()
    generator_hash = _sha256(generator_path)
    records = []
    audit_assets = []
    allowed_colors = set(C.values())
    for sprite_id, filename, entity_id, _kind in ASSETS:
        asset_path = SPRITES / filename
        source = rendered[sprite_id]
        frames = [_frame_audit(source, index) for index in range(4)]
        alpha_values = sorted(set(source.getchannel("A").get_flattened_data()))
        opaque_colors = {pixel for pixel in source.get_flattened_data() if pixel[3] == 255}
        records.append({
            "spriteId": sprite_id,
            "entityId": entity_id,
            "path": f"sprites/entities/critters/{filename}",
            "sha256": _sha256(asset_path),
            "dimensions": [64, 16],
            "frameDimensions": [16, 16],
            "frames": list(FRAME_NAMES),
            "origin": [8, 8],
            "alphaValues": [0, 255],
            "generator": "art_direction/tools/generate_flying_wildlife_polish_v1.py",
            "method": "deterministic native-size Pillow pixel primitives",
            "license": "YjsE-Project-Owned",
            "runtimeConsumer": "EntityDefinition texture plus RuntimeEntityAnimationProfile source rectangles through ClientTextureRegistry",
        })
        audit_assets.append({
            "spriteId": sprite_id,
            "dimensionsPass": source.size == (64, 16),
            "frameContractPass": len(frames) == 4,
            "frameOrder": list(FRAME_NAMES),
            "origin": [8, 8],
            "binaryAlphaPass": alpha_values == [0, 255],
            "transparentCornersPass": all(source.getpixel(point)[3] == 0 for point in ((0, 0), (63, 0), (0, 15), (63, 15))),
            "palettePass": opaque_colors.issubset(allowed_colors),
            "uniqueOpaqueColors": len(opaque_colors),
            "distinctFrameContentPass": len({frame["contentSha256"] for frame in frames}) == 4,
            "frames": frames,
        })

    provenance = {
        "schemaVersion": 1,
        "waveId": WAVE_ID,
        "generatedOn": GENERATED_ON,
        "sourceType": "checked-in deterministic native-pixel generator",
        "externalDownloads": False,
        "modelGeneratedSource": False,
        "generator": {
            "path": "art_direction/tools/generate_flying_wildlife_polish_v1.py",
            "version": GENERATOR_VERSION,
            "sha256": generator_hash,
            "runtime": "Python 3 plus Pillow from art_direction/requirements.txt",
            "method": "Direct final-size pixel primitives constrained to yjse-pixel-v1; no hidden source art or manual raster edits.",
        },
        "manifest": "assets/sprites.json",
        "brief": "asset_briefs/production_wave_03_briefs.json",
        "preview": "art_direction/flying_wildlife_polish_v1_contact_sheet.png",
        "assets": records,
    }
    (ART_DIRECTION / "flying_wildlife_polish_v1_provenance.json").write_text(
        json.dumps(provenance, indent=2) + "\n",
        encoding="utf-8",
    )

    preview_summary = {
        "schemaVersion": 1,
        "waveId": WAVE_ID,
        "preview": "art_direction/flying_wildlife_polish_v1_contact_sheet.png",
        "previewDimensions": [720, 420],
        "nativeSheetDimensions": [64, 16],
        "nativeFrameDimensions": [16, 16],
        "frameOrder": list(FRAME_NAMES),
        "scale": 6,
        "resampling": "nearest-neighbor",
        "spriteIds": [asset[0] for asset in ASSETS],
        "generatorSha256": generator_hash,
    }
    (ART_DIRECTION / "flying_wildlife_polish_v1_preview_summary.json").write_text(
        json.dumps(preview_summary, indent=2) + "\n",
        encoding="utf-8",
    )

    gates = (
        "dimensionsPass",
        "frameContractPass",
        "binaryAlphaPass",
        "transparentCornersPass",
        "palettePass",
        "distinctFrameContentPass",
    )
    audit = {
        "schemaVersion": 1,
        "scope": WAVE_ID,
        "passed": all(asset[gate] for asset in audit_assets for gate in gates),
        "nativeSheetDimensions": [64, 16],
        "nativeFrameDimensions": [16, 16],
        "sharedOrigin": [8, 8],
        "frameOrder": list(FRAME_NAMES),
        "assets": audit_assets,
    }
    (ART_DIRECTION / "flying_wildlife_polish_v1_asset_audit.json").write_text(
        json.dumps(audit, indent=2) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
