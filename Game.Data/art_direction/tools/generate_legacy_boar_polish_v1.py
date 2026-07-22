"""Generate the native 32x32 Forest Boar legacy-polish sprite family."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
SPRITES = ROOT / "sprites" / "entities" / "enemies"
ART_DIRECTION = ROOT / "art_direction"
WAVE_ID = "legacy_boar_polish_v1"
GENERATED_ON = "2026-07-22"
GENERATOR_VERSION = "1.0.0"

C = {
    "clear": (0, 0, 0, 0),
    "outline": (27, 24, 38, 255),
    "neutral_dark": (42, 38, 53, 255),
    "neutral_mid": (74, 69, 85, 255),
    "pale": (244, 233, 216, 255),
    "wood_dark": (53, 35, 25, 255),
    "wood_shadow": (90, 56, 37, 255),
    "wood_mid": (139, 90, 52, 255),
    "wood_light": (197, 139, 82, 255),
    "gold": (240, 195, 90, 255),
    "leaf_dark": (38, 63, 56, 255),
    "leaf_shadow": (53, 97, 74, 255),
    "leaf_mid": (79, 138, 91, 255),
    "leaf_light": (127, 186, 104, 255),
    "leaf_tip": (184, 212, 122, 255),
}

FRAME_NAMES = ("idle", "windup", "charge", "attack")
ASSETS = (
    ("entities/enemies/forest_boar", "forest_boar.png", False),
    ("entities/enemies/forest_boar_elite", "forest_boar_elite.png", True),
)


def _polygon(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], color: str) -> None:
    draw.polygon(points, fill=C[color])


def _rectangle(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: str) -> None:
    draw.rectangle(box, fill=C[color])


def _draw_idle(draw: ImageDraw.ImageDraw, ox: int) -> None:
    # Bristled back, weight-forward body, alert head and two planted hoof groups.
    _polygon(draw, [(ox+3,15),(ox+5,11),(ox+7,10),(ox+8,7),(ox+10,10),(ox+13,8),(ox+15,10),(ox+19,9),(ox+23,12),(ox+25,17),(ox+24,23),(ox+20,25),(ox+7,25),(ox+3,21)], "outline")
    _polygon(draw, [(ox+5,15),(ox+7,12),(ox+11,11),(ox+18,11),(ox+22,13),(ox+23,17),(ox+22,22),(ox+19,23),(ox+7,23),(ox+5,20)], "wood_shadow")
    _polygon(draw, [(ox+7,13),(ox+11,11),(ox+18,11),(ox+21,13),(ox+20,16),(ox+8,17)], "wood_mid")
    _rectangle(draw, (ox+8,13,ox+15,14), "wood_light")
    _polygon(draw, [(ox+4,15),(ox+2,13),(ox+1,15),(ox+3,17)], "outline")
    _draw_head(draw, ox, head_x=20, head_y=12, lowered=False, thrust=False)
    _draw_leg(draw, ox+7, 23, 29, 0)
    _draw_leg(draw, ox+18, 23, 29, 0)
    _rectangle(draw, (ox+6,29,ox+11,30), "outline")
    _rectangle(draw, (ox+18,29,ox+24,30), "outline")


def _draw_windup(draw: ImageDraw.ImageDraw, ox: int) -> None:
    # Shoulders bunch while the lowered head and lifted forehoof telegraph the charge.
    _polygon(draw, [(ox+3,17),(ox+5,12),(ox+8,11),(ox+9,8),(ox+12,11),(ox+16,9),(ox+18,11),(ox+22,12),(ox+25,17),(ox+24,24),(ox+20,26),(ox+7,26),(ox+3,22)], "outline")
    _polygon(draw, [(ox+5,17),(ox+7,13),(ox+12,12),(ox+19,12),(ox+22,14),(ox+23,18),(ox+22,23),(ox+19,24),(ox+7,24),(ox+5,21)], "wood_shadow")
    _polygon(draw, [(ox+7,14),(ox+12,12),(ox+19,12),(ox+21,15),(ox+20,18),(ox+8,18)], "wood_mid")
    _rectangle(draw, (ox+8,14,ox+15,15), "wood_light")
    _polygon(draw, [(ox+4,17),(ox+1,16),(ox+2,19)], "outline")
    _draw_head(draw, ox, head_x=20, head_y=15, lowered=True, thrust=False)
    _draw_leg(draw, ox+7, 24, 29, 0)
    _rectangle(draw, (ox+6,29,ox+11,30), "outline")
    _polygon(draw, [(ox+18,24),(ox+22,24),(ox+24,27),(ox+22,29),(ox+18,28)], "outline")
    _rectangle(draw, (ox+20,27,ox+25,28), "neutral_dark")


def _draw_charge(draw: ImageDraw.ImageDraw, ox: int) -> None:
    # Long low line, swept bristles and separated hoof silhouettes read at speed.
    _polygon(draw, [(ox+1,17),(ox+4,12),(ox+8,12),(ox+10,9),(ox+13,12),(ox+17,10),(ox+19,12),(ox+24,13),(ox+27,17),(ox+26,23),(ox+22,25),(ox+6,25),(ox+2,22)], "outline")
    _polygon(draw, [(ox+4,16),(ox+7,13),(ox+18,13),(ox+23,14),(ox+25,17),(ox+24,21),(ox+21,23),(ox+7,23),(ox+4,21)], "wood_shadow")
    _polygon(draw, [(ox+7,14),(ox+18,13),(ox+22,15),(ox+20,18),(ox+6,18)], "wood_mid")
    _rectangle(draw, (ox+7,14,ox+14,15), "wood_light")
    _polygon(draw, [(ox+3,16),(ox+0,14),(ox+1,18)], "outline")
    _draw_head(draw, ox, head_x=22, head_y=13, lowered=True, thrust=True)
    _polygon(draw, [(ox+5,23),(ox+11,23),(ox+9,27),(ox+3,29),(ox+1,28),(ox+6,25)], "outline")
    _polygon(draw, [(ox+18,23),(ox+23,23),(ox+27,27),(ox+31,28),(ox+30,30),(ox+25,29),(ox+20,26)], "outline")
    _rectangle(draw, (ox+2,28,ox+7,29), "neutral_dark")
    _rectangle(draw, (ox+26,29,ox+31,30), "neutral_dark")


def _draw_attack(draw: ImageDraw.ImageDraw, ox: int) -> None:
    # Braced rear weight and an upward head thrust make the tusk strike unmistakable.
    _polygon(draw, [(ox+2,17),(ox+4,12),(ox+7,11),(ox+8,8),(ox+11,11),(ox+15,9),(ox+17,12),(ox+22,13),(ox+24,18),(ox+22,24),(ox+18,26),(ox+6,25),(ox+2,22)], "outline")
    _polygon(draw, [(ox+4,17),(ox+6,13),(ox+11,12),(ox+18,13),(ox+21,15),(ox+22,18),(ox+20,22),(ox+17,24),(ox+7,23),(ox+4,21)], "wood_shadow")
    _polygon(draw, [(ox+6,14),(ox+12,12),(ox+18,13),(ox+20,15),(ox+18,18),(ox+6,18)], "wood_mid")
    _rectangle(draw, (ox+7,14,ox+14,15), "wood_light")
    _polygon(draw, [(ox+3,17),(ox+0,18),(ox+2,20)], "outline")
    _draw_head(draw, ox, head_x=21, head_y=10, lowered=False, thrust=True)
    _polygon(draw, [(ox+6,23),(ox+11,23),(ox+10,28),(ox+7,30),(ox+3,29),(ox+5,27)], "outline")
    _polygon(draw, [(ox+17,23),(ox+21,22),(ox+24,27),(ox+22,30),(ox+17,29)], "outline")
    _rectangle(draw, (ox+4,29,ox+10,30), "neutral_dark")
    _rectangle(draw, (ox+18,29,ox+24,30), "neutral_dark")


def _draw_head(draw: ImageDraw.ImageDraw, ox: int, head_x: int, head_y: int, lowered: bool, thrust: bool) -> None:
    hx = ox + head_x
    hy = head_y
    _polygon(draw, [(hx,hy+2),(hx+3,hy),(hx+7,hy+1),(hx+9,hy+4),(hx+8,hy+9),(hx+4,hy+11),(hx,hy+8)], "outline")
    _polygon(draw, [(hx+2,hy+3),(hx+4,hy+2),(hx+6,hy+3),(hx+7,hy+5),(hx+6,hy+8),(hx+3,hy+9),(hx+2,hy+7)], "wood_mid")
    _rectangle(draw, (hx+2,hy+3,hx+4,hy+4), "wood_light")
    _polygon(draw, [(hx+1,hy+2),(hx+2,hy-2),(hx+5,hy+1)], "outline")
    _polygon(draw, [(hx+2,hy+1),(hx+3,hy-1),(hx+4,hy+1)], "wood_shadow")
    _rectangle(draw, (hx+6,hy+4,hx+9,hy+7), "outline")
    _rectangle(draw, (hx+7,hy+5,hx+9,hy+6), "wood_shadow")
    _rectangle(draw, (hx+5,hy+3,hx+5,hy+3), "gold")
    _rectangle(draw, (hx+9,hy+5,hx+9,hy+5), "pale")
    tusk_y = hy + (8 if lowered else 7)
    tusk_reach = 3 if thrust else 2
    _polygon(draw, [(hx+7,tusk_y),(hx+9+tusk_reach,tusk_y+2),(hx+9,tusk_y-1)], "outline")
    _polygon(draw, [(hx+9,tusk_y),(hx+8+tusk_reach,tusk_y+1),(hx+9,tusk_y-1)], "pale")


def _draw_leg(draw: ImageDraw.ImageDraw, x: int, y0: int, y1: int, stride: int) -> None:
    _polygon(draw, [(x,y0),(x+4,y0),(x+4+stride,y1-1),(x+3+stride,y1),(x+stride,y1),(x+1,y0+2)], "outline")
    if y1 - y0 >= 5:
        _rectangle(draw, (x+1,y0+1,x+2,y1-2), "wood_dark")


def _add_elite_armor(draw: ImageDraw.ImageDraw, ox: int, frame: int) -> None:
    # Leaf plates follow the body motion without obscuring eye, hooves or attack line.
    body_y = (10, 11, 11, 11)[frame]
    _polygon(draw, [(ox+7,body_y+3),(ox+10,body_y-2),(ox+13,body_y+3),(ox+17,body_y-1),(ox+21,body_y+4),(ox+19,body_y+9),(ox+9,body_y+8)], "outline")
    _polygon(draw, [(ox+9,body_y+3),(ox+11,body_y),(ox+13,body_y+4),(ox+17,body_y+1),(ox+19,body_y+4),(ox+18,body_y+7),(ox+10,body_y+6)], "leaf_shadow")
    _polygon(draw, [(ox+10,body_y+3),(ox+12,body_y+1),(ox+13,body_y+4),(ox+16,body_y+2),(ox+18,body_y+4),(ox+16,body_y+5),(ox+11,body_y+5)], "leaf_mid")
    _rectangle(draw, (ox+11,body_y+2,ox+13,body_y+2), "leaf_light")
    _polygon(draw, [(ox+7,body_y+3),(ox+8,body_y-3),(ox+11,body_y+2)], "outline")
    _polygon(draw, [(ox+8,body_y+1),(ox+8,body_y-2),(ox+10,body_y+2)], "leaf_tip")
    _polygon(draw, [(ox+15,body_y+1),(ox+18,body_y-5),(ox+20,body_y+2)], "outline")
    _polygon(draw, [(ox+17,body_y),(ox+18,body_y-3),(ox+19,body_y+1)], "leaf_light")


def build_sheet(elite: bool) -> Image.Image:
    sheet = Image.new("RGBA", (128, 32), C["clear"])
    draw = ImageDraw.Draw(sheet)
    frame_drawers = (_draw_idle, _draw_windup, _draw_charge, _draw_attack)
    for frame, drawer in enumerate(frame_drawers):
        ox = frame * 32
        drawer(draw, ox)
        if elite:
            _add_elite_armor(draw, ox, frame)

    alpha = sheet.getchannel("A").point(lambda value: 255 if value else 0)
    sheet.putalpha(alpha)
    return sheet


def _save_png(path: Path, source: Image.Image) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    source.save(path, format="PNG", optimize=False, compress_level=9)


def _build_preview(normal: Image.Image, elite: Image.Image) -> Image.Image:
    width, height = 720, 336
    preview = Image.new("RGBA", (width, height), (237, 233, 224, 255))
    draw = ImageDraw.Draw(preview)
    draw.text((20, 14), "YjsE LEGACY BOAR POLISH V1 - NATIVE FRAME CONTRACT", fill=(27, 24, 38, 255))
    draw.text((20, 32), "idle / windup / charge / attack | 32x32 frames | nearest-neighbor", fill=(74, 69, 85, 255))
    for row, (label, source) in enumerate((("FOREST BOAR", normal), ("FOREST BOAR ELITE", elite))):
        y = 62 + row * 132
        draw.text((20, y), label, fill=(27, 24, 38, 255))
        for index, name in enumerate(FRAME_NAMES):
            x = 20 + index * 168
            draw.rectangle((x, y+18, x+143, y+115), fill=(201, 196, 187, 255))
            tile = source.crop((index*32, 0, index*32+32, 32)).resize((96, 96), Image.Resampling.NEAREST)
            checker = Image.new("RGBA", (96, 96), (244, 241, 234, 255))
            checker_draw = ImageDraw.Draw(checker)
            for cy in range(0, 96, 12):
                for cx in range(0, 96, 12):
                    if (cx // 12 + cy // 12) % 2:
                        checker_draw.rectangle((cx, cy, cx+11, cy+11), fill=(226, 222, 214, 255))
            checker.alpha_composite(tile)
            preview.alpha_composite(checker, (x+24, y+18))
            draw.text((x+4, y+119), name, fill=(42, 38, 53, 255))
    return preview


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _frame_audit(source: Image.Image, frame_index: int) -> dict[str, object]:
    frame = source.crop((frame_index * 32, 0, frame_index * 32 + 32, 32))
    alpha = frame.getchannel("A")
    bounds = alpha.getbbox()
    return {
        "id": FRAME_NAMES[frame_index],
        "visibleBounds": list(bounds) if bounds is not None else None,
        "opaquePixelCount": sum(1 for value in alpha.get_flattened_data() if value == 255),
        "contentSha256": hashlib.sha256(frame.tobytes()).hexdigest(),
    }


def main() -> None:
    rendered: dict[str, Image.Image] = {}
    for sprite_id, filename, elite in ASSETS:
        sheet = build_sheet(elite)
        _save_png(SPRITES / filename, sheet)
        rendered[sprite_id] = sheet

    preview_path = ART_DIRECTION / "legacy_boar_polish_v1_contact_sheet.png"
    _save_png(
        preview_path,
        _build_preview(rendered[ASSETS[0][0]], rendered[ASSETS[1][0]]),
    )

    generator_path = Path(__file__).resolve()
    generator_hash = _sha256(generator_path)
    records = []
    for sprite_id, filename, _elite in ASSETS:
        asset_path = SPRITES / filename
        records.append({
            "spriteId": sprite_id,
            "entityIds": ["forest_boar"] if not _elite else ["forest_boar_elite"],
            "path": f"sprites/entities/enemies/{filename}",
            "sha256": _sha256(asset_path),
            "dimensions": [128, 32],
            "frameDimensions": [32, 32],
            "frames": list(FRAME_NAMES),
            "origin": [16, 31],
            "alphaValues": [0, 255],
            "generator": "art_direction/tools/generate_legacy_boar_polish_v1.py",
            "method": "deterministic native-size Pillow pixel primitives",
            "license": "YjsE-Project-Owned",
            "runtimeConsumer": "EntityDefinition texture plus RuntimeEntityAnimationProfile source-rectangle animation through ClientTextureRegistry",
        })

    provenance = {
        "schemaVersion": 1,
        "waveId": WAVE_ID,
        "generatedOn": GENERATED_ON,
        "sourceType": "checked-in deterministic native-pixel generator",
        "externalDownloads": False,
        "modelGeneratedSource": False,
        "generator": {
            "path": "art_direction/tools/generate_legacy_boar_polish_v1.py",
            "version": GENERATOR_VERSION,
            "sha256": generator_hash,
            "runtime": "Python 3 plus Pillow from art_direction/requirements.txt",
            "method": "Direct final-size pixel primitives constrained to yjse-pixel-v1; no hidden source art or manual raster edits.",
        },
        "manifest": "assets/sprites.json",
        "briefs": [
            "asset_briefs/base_sprite_generation_briefs.json",
            "asset_briefs/production_wave_03_briefs.json",
        ],
        "preview": "art_direction/legacy_boar_polish_v1_contact_sheet.png",
        "assets": records,
    }
    (ART_DIRECTION / "legacy_boar_polish_v1_provenance.json").write_text(
        json.dumps(provenance, indent=2) + "\n",
        encoding="utf-8",
    )

    preview_summary = {
        "schemaVersion": 1,
        "waveId": WAVE_ID,
        "preview": "art_direction/legacy_boar_polish_v1_contact_sheet.png",
        "previewDimensions": [720, 336],
        "nativeSheetDimensions": [128, 32],
        "nativeFrameDimensions": [32, 32],
        "frameOrder": list(FRAME_NAMES),
        "scale": 3,
        "resampling": "nearest-neighbor",
        "spriteIds": [asset[0] for asset in ASSETS],
        "generatorSha256": generator_hash,
    }
    (ART_DIRECTION / "legacy_boar_polish_v1_preview_summary.json").write_text(
        json.dumps(preview_summary, indent=2) + "\n",
        encoding="utf-8",
    )

    allowed_colors = set(C.values())
    audit_assets = []
    for sprite_id, filename, _elite in ASSETS:
        source = rendered[sprite_id]
        alpha_values = sorted(set(source.getchannel("A").get_flattened_data()))
        opaque_colors = {
            pixel
            for pixel in source.get_flattened_data()
            if pixel[3] == 255
        }
        frames = [_frame_audit(source, index) for index in range(len(FRAME_NAMES))]
        frame_hashes = [frame["contentSha256"] for frame in frames]
        audit_assets.append({
            "spriteId": sprite_id,
            "path": f"sprites/entities/enemies/{filename}",
            "dimensionsPass": source.size == (128, 32),
            "frameContractPass": len(frames) == 4,
            "frameOrder": list(FRAME_NAMES),
            "origin": [16, 31],
            "binaryAlphaPass": alpha_values == [0, 255],
            "transparentCornersPass": all(source.getpixel(point)[3] == 0 for point in ((0, 0), (127, 0), (0, 31), (127, 31))),
            "palettePass": opaque_colors.issubset(allowed_colors),
            "uniqueOpaqueColors": len(opaque_colors),
            "distinctFrameContentPass": len(set(frame_hashes)) == len(FRAME_NAMES),
            "frames": frames,
        })

    audit = {
        "schemaVersion": 1,
        "scope": WAVE_ID,
        "passed": all(
            asset[gate]
            for asset in audit_assets
            for gate in (
                "dimensionsPass",
                "frameContractPass",
                "binaryAlphaPass",
                "transparentCornersPass",
                "palettePass",
                "distinctFrameContentPass",
            )
        ),
        "nativeSheetDimensions": [128, 32],
        "nativeFrameDimensions": [32, 32],
        "sharedOrigin": [16, 31],
        "frameOrder": list(FRAME_NAMES),
        "assets": audit_assets,
    }
    (ART_DIRECTION / "legacy_boar_polish_v1_asset_audit.json").write_text(
        json.dumps(audit, indent=2) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
