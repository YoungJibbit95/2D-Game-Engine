"""Generate the four native 32x32 Adventure Arsenal V1 item icons."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "assets" / "AdventureArsenalV1" / "items"
ART_DIRECTION = ROOT / "art_direction"
WAVE_ID = "adventure_arsenal_v1"
GENERATED_ON = "2026-07-22"
GENERATOR_VERSION = "1.0.0"

C = {
    "clear": (0, 0, 0, 0),
    "outline": (27, 24, 38, 255),
    "neutral_dark": (42, 38, 53, 255),
    "neutral": (74, 69, 85, 255),
    "steel": (113, 106, 123, 255),
    "steel_light": (170, 162, 176, 255),
    "pale": (244, 233, 216, 255),
    "wood_dark": (53, 35, 25, 255),
    "wood": (90, 56, 37, 255),
    "wood_light": (139, 90, 52, 255),
    "copper_dark": (91, 43, 34, 255),
    "copper": (143, 69, 49, 255),
    "copper_light": (197, 109, 62, 255),
    "red_dark": (91, 31, 50, 255),
    "red": (165, 43, 70, 255),
    "red_light": (229, 72, 77, 255),
    "amber_dark": (88, 65, 42, 255),
    "amber": (185, 119, 50, 255),
    "amber_light": (240, 195, 90, 255),
    "amber_tip": (255, 241, 166, 255),
    "ice_dark": (32, 36, 79, 255),
    "ice": (83, 103, 200, 255),
    "ice_light": (120, 183, 227, 255),
    "ice_tip": (185, 239, 255, 255),
    "teal_dark": (31, 92, 98, 255),
    "teal": (61, 141, 132, 255),
    "teal_light": (114, 195, 174, 255),
}

ASSETS = (
    ("items/adventure_v1/cinderbloom_staff", "cinderbloom_staff.png", "cinderbloom_staff", "cinder"),
    ("items/adventure_v1/frostglass_scepter", "frostglass_scepter.png", "frostglass_scepter", "frost"),
    ("items/adventure_v1/ambercore_pickaxe", "ambercore_pickaxe.png", "ambercore_pickaxe", "pickaxe"),
    ("items/adventure_v1/wayfinder_charm", "wayfinder_charm.png", "wayfinder_charm", "charm"),
)


def _line(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], color: str, width: int) -> None:
    draw.line(points, fill=C[color], width=width, joint="curve")


def _poly(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], color: str) -> None:
    draw.polygon(points, fill=C[color])


def _rect(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: str) -> None:
    draw.rectangle(box, fill=C[color])


def _cinder_staff(draw: ImageDraw.ImageDraw) -> None:
    # A rising diagonal makes the silhouette unmistakably staff-like.
    _line(draw, [(7, 28), (19, 13)], "outline", 6)
    _line(draw, [(7, 27), (19, 13)], "wood", 4)
    _line(draw, [(8, 26), (18, 14)], "wood_light", 1)
    _poly(draw, [(4, 28), (8, 24), (11, 27), (8, 30)], "outline")
    _poly(draw, [(6, 28), (8, 26), (9, 27), (8, 29)], "copper")
    _poly(draw, [(15, 17), (19, 11), (22, 14), (18, 20)], "outline")
    _poly(draw, [(17, 17), (19, 13), (20, 14), (18, 18)], "copper_light")
    # Dark-contoured cinder blossom: no alpha aura.
    _poly(draw, [(18, 13), (16, 9), (18, 5), (21, 7), (23, 3), (26, 7), (25, 11), (22, 14)], "outline")
    _poly(draw, [(19, 11), (18, 9), (19, 7), (21, 9), (23, 5), (24, 8), (23, 11), (21, 12)], "red")
    _poly(draw, [(20, 10), (21, 8), (22, 9), (23, 7), (23, 10), (22, 12)], "red_light")
    _rect(draw, (21, 10, 22, 11), "amber_light")
    draw.point((22, 9), fill=C["amber_tip"])


def _frost_scepter(draw: ImageDraw.ImageDraw) -> None:
    # More upright and symmetrical than the fire staff.
    _line(draw, [(13, 26), (16, 11)], "outline", 6)
    _line(draw, [(13, 26), (16, 12)], "steel", 4)
    _line(draw, [(14, 24), (16, 13)], "steel_light", 1)
    _poly(draw, [(10, 27), (13, 23), (17, 26), (15, 29)], "outline")
    _poly(draw, [(12, 27), (14, 25), (15, 26), (14, 28)], "ice")
    _poly(draw, [(12, 16), (16, 11), (20, 15), (18, 19), (14, 19)], "outline")
    _poly(draw, [(14, 16), (16, 13), (18, 16), (17, 18), (15, 18)], "ice")
    # Forked crystalline crown.
    _poly(draw, [(14, 13), (9, 8), (10, 3), (14, 7), (16, 2), (19, 7), (23, 4), (22, 10), (18, 14)], "outline")
    _poly(draw, [(13, 10), (11, 8), (11, 5), (15, 9), (16, 4), (18, 9), (21, 6), (20, 9), (17, 12)], "ice")
    _poly(draw, [(15, 9), (16, 5), (17, 9), (19, 8), (17, 11)], "ice_light")
    draw.point((16, 4), fill=C["ice_tip"])
    draw.point((20, 7), fill=C["ice_tip"])


def _ambercore_pickaxe(draw: ImageDraw.ImageDraw) -> None:
    # Descending handle contrasts both magic silhouettes.
    _line(draw, [(22, 8), (9, 29)], "outline", 7)
    _line(draw, [(21, 9), (10, 28)], "wood", 5)
    _line(draw, [(20, 10), (11, 26)], "wood_light", 2)
    _poly(draw, [(6, 28), (10, 25), (13, 28), (10, 30)], "outline")
    _poly(draw, [(8, 28), (10, 27), (11, 28), (10, 29)], "copper_light")
    # Broad two-ended pick head with an asymmetrical mining beak.
    _poly(draw, [(4, 7), (12, 4), (22, 5), (28, 9), (27, 13), (22, 10), (14, 9), (8, 13), (3, 12)], "outline")
    _poly(draw, [(6, 8), (12, 6), (21, 7), (26, 9), (26, 11), (21, 9), (14, 8), (8, 11), (5, 10)], "neutral")
    _poly(draw, [(7, 8), (13, 6), (20, 7), (20, 8), (13, 8), (8, 10)], "steel_light")
    _poly(draw, [(17, 6), (22, 7), (24, 10), (21, 13), (17, 11), (15, 8)], "outline")
    _poly(draw, [(18, 8), (21, 8), (22, 10), (20, 11), (18, 10)], "amber")
    _rect(draw, (19, 8, 20, 9), "amber_light")
    draw.point((20, 8), fill=C["amber_tip"])


def _wayfinder_charm(draw: ImageDraw.ImageDraw) -> None:
    # Hanging loop and centered round compass provide the accessory silhouette.
    _poly(draw, [(12, 6), (13, 2), (19, 2), (20, 6), (18, 9), (14, 9)], "outline")
    _rect(draw, (15, 4, 17, 7), "copper_light")
    draw.ellipse((5, 6, 27, 28), fill=C["outline"])
    draw.ellipse((7, 8, 25, 26), fill=C["copper_dark"])
    draw.ellipse((9, 10, 23, 24), fill=C["neutral_dark"])
    _poly(draw, [(16, 10), (19, 17), (16, 22), (13, 17)], "outline")
    _poly(draw, [(16, 11), (18, 17), (16, 16)], "teal_light")
    _poly(draw, [(16, 23), (14, 17), (16, 18)], "amber_light")
    _rect(draw, (15, 16, 17, 18), "pale")
    _rect(draw, (15, 9, 16, 10), "steel_light")
    _rect(draw, (22, 16, 23, 17), "copper_light")
    _rect(draw, (15, 24, 16, 25), "copper")
    _rect(draw, (8, 16, 9, 17), "teal")
    draw.point((10, 12), fill=C["steel_light"])
    draw.point((21, 12), fill=C["copper_light"])


BUILDERS = {
    "cinder": _cinder_staff,
    "frost": _frost_scepter,
    "pickaxe": _ambercore_pickaxe,
    "charm": _wayfinder_charm,
}


def build_icon(kind: str) -> Image.Image:
    raw = Image.new("RGBA", (32, 32), C["clear"])
    BUILDERS[kind](ImageDraw.Draw(raw))
    vertical_offset = {"cinder": -1, "pickaxe": -2}.get(kind, 0)
    source = Image.new("RGBA", (32, 32), C["clear"])
    source.alpha_composite(raw, (0, vertical_offset))
    source.putalpha(source.getchannel("A").point(lambda value: 255 if value else 0))
    return source


def _save(path: Path, source: Image.Image) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    source.save(path, format="PNG", optimize=False, compress_level=9)


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _build_contact_sheet(rendered: dict[str, Image.Image]) -> Image.Image:
    preview = Image.new("RGBA", (760, 270), (237, 233, 224, 255))
    draw = ImageDraw.Draw(preview)
    draw.text((18, 14), "YjsE ADVENTURE ARSENAL V1 - NATIVE 32x32 INVENTORY SILHOUETTES", fill=C["outline"])
    draw.text((18, 32), "4 complete runtime items | nearest-neighbor 5x | binary alpha | yjse-pixel-v1", fill=C["neutral"])
    labels = ("CINDERBLOOM STAFF", "FROSTGLASS SCEPTER", "AMBERCORE PICKAXE", "WAYFINDER CHARM")
    for index, ((sprite_id, _filename, _item_id, _kind), label) in enumerate(zip(ASSETS, labels, strict=True)):
        x = 18 + index * 185
        y = 58
        checker = Image.new("RGBA", (160, 160), (244, 241, 234, 255))
        checker_draw = ImageDraw.Draw(checker)
        for cy in range(0, 160, 16):
            for cx in range(0, 160, 16):
                if (cx // 16 + cy // 16) % 2:
                    checker_draw.rectangle((cx, cy, cx + 15, cy + 15), fill=(226, 222, 214, 255))
        icon = rendered[sprite_id].resize((160, 160), Image.Resampling.NEAREST)
        checker.alpha_composite(icon)
        preview.alpha_composite(checker, (x, y))
        draw.text((x, 226), label, fill=C["outline"])
        draw.text((x, 242), "32x32 | pivot 16,16", fill=C["neutral"])
    return preview


def main() -> None:
    rendered: dict[str, Image.Image] = {}
    for sprite_id, filename, _item_id, kind in ASSETS:
        icon = build_icon(kind)
        _save(OUTPUT / filename, icon)
        rendered[sprite_id] = icon

    preview_path = ART_DIRECTION / "adventure_arsenal_v1_contact_sheet.png"
    _save(preview_path, _build_contact_sheet(rendered))

    generator_path = Path(__file__).resolve()
    generator_hash = _sha256(generator_path)
    allowed_colors = set(C.values())
    records = []
    audit_assets = []
    for sprite_id, filename, item_id, _kind in ASSETS:
        asset_path = OUTPUT / filename
        source = rendered[sprite_id]
        alpha_values = sorted(set(source.getchannel("A").get_flattened_data()))
        opaque_colors = {pixel for pixel in source.get_flattened_data() if pixel[3] == 255}
        bounds = source.getchannel("A").getbbox()
        edge_clear = all(
            source.getpixel((x, y))[3] == 0
            for x in range(32)
            for y in range(32)
            if x < 2 or x >= 30 or y < 2 or y >= 30
        )
        records.append({
            "spriteId": sprite_id,
            "itemId": item_id,
            "path": f"assets/AdventureArsenalV1/items/{filename}",
            "sha256": _sha256(asset_path),
            "dimensions": [32, 32],
            "origin": [16, 16],
            "alphaValues": [0, 255],
            "generator": "art_direction/tools/generate_adventure_arsenal_v1.py",
            "method": "deterministic native-size Pillow pixel primitives",
            "license": "YjsE-Project-Owned",
            "runtimeConsumer": "ItemDefinition texture through SpriteAssetRegistry and ClientTextureRegistry",
        })
        audit_assets.append({
            "spriteId": sprite_id,
            "dimensionsPass": source.size == (32, 32),
            "binaryAlphaPass": alpha_values == [0, 255],
            "twoPixelTransparentMarginPass": edge_clear,
            "transparentCornersPass": all(source.getpixel(point)[3] == 0 for point in ((0, 0), (31, 0), (0, 31), (31, 31))),
            "palettePass": opaque_colors.issubset(allowed_colors),
            "recommendedColorCountPass": len(opaque_colors) <= 16,
            "uniqueOpaqueColors": len(opaque_colors),
            "visibleBounds": list(bounds) if bounds is not None else None,
        })

    provenance = {
        "schemaVersion": 1,
        "waveId": WAVE_ID,
        "generatedOn": GENERATED_ON,
        "sourceType": "checked-in deterministic native-pixel generator",
        "externalDownloads": False,
        "modelGeneratedSource": False,
        "generator": {
            "path": "art_direction/tools/generate_adventure_arsenal_v1.py",
            "version": GENERATOR_VERSION,
            "sha256": generator_hash,
            "runtime": "Python 3 plus Pillow from art_direction/requirements.txt",
            "method": "Direct final-size pixel primitives constrained to yjse-pixel-v1; no hidden source art or manual raster edits.",
        },
        "manifest": "assets/adventure_arsenal_v1.sprites.json",
        "brief": "asset_briefs/adventure_arsenal_v1_briefs.json",
        "preview": "art_direction/adventure_arsenal_v1_contact_sheet.png",
        "assets": records,
    }
    (ART_DIRECTION / "adventure_arsenal_v1_provenance.json").write_text(json.dumps(provenance, indent=2) + "\n", encoding="utf-8")

    preview_summary = {
        "schemaVersion": 1,
        "waveId": WAVE_ID,
        "preview": "art_direction/adventure_arsenal_v1_contact_sheet.png",
        "previewDimensions": [760, 270],
        "nativeDimensions": [32, 32],
        "scale": 5,
        "resampling": "nearest-neighbor",
        "spriteIds": [asset[0] for asset in ASSETS],
        "generatorSha256": generator_hash,
    }
    (ART_DIRECTION / "adventure_arsenal_v1_preview_summary.json").write_text(json.dumps(preview_summary, indent=2) + "\n", encoding="utf-8")

    gates = (
        "dimensionsPass",
        "binaryAlphaPass",
        "twoPixelTransparentMarginPass",
        "transparentCornersPass",
        "palettePass",
        "recommendedColorCountPass",
    )
    audit = {
        "schemaVersion": 1,
        "scope": WAVE_ID,
        "passed": all(asset[gate] for asset in audit_assets for gate in gates),
        "nativeDimensions": [32, 32],
        "sharedOrigin": [16, 16],
        "assets": audit_assets,
    }
    (ART_DIRECTION / "adventure_arsenal_v1_asset_audit.json").write_text(json.dumps(audit, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
