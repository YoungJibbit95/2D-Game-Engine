#!/usr/bin/env python3
"""Build a deterministic visual QA board for YjsE Wave 05."""

from __future__ import annotations

from hashlib import sha256
from io import BytesIO
import argparse
import json
from pathlib import Path
import sys

from PIL import Image, ImageDraw, ImageFont


DATA_ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(DATA_ROOT / "art_direction" / "tools"))
from preview_integrity import preview_contract_matches

MANIFEST = DATA_ROOT / "assets" / "wave05_living_world.sprites.json"
OUTPUT = DATA_ROOT / "art_direction" / "wave_05_living_world_preview.png"
SUMMARY = DATA_ROOT / "art_direction" / "wave_05_living_world_preview_summary.json"
FONT = ImageFont.load_default()
LIGHT = (244, 233, 216, 255)
DARK = (42, 38, 53, 255)
INK = (27, 24, 38, 255)
PANEL = (170, 162, 176, 255)
ACCENT = (61, 141, 132, 255)


def load(path: str) -> Image.Image:
    with Image.open(DATA_ROOT / path) as opened:
        return opened.convert("RGBA")


def checker(width: int, height: int, first, second) -> Image.Image:
    result = Image.new("RGBA", (width, height), first)
    draw = ImageDraw.Draw(result)
    for y in range(0, height, 8):
        for x in range(0, width, 8):
            if (x // 8 + y // 8) % 2:
                draw.rectangle((x, y, x + 7, y + 7), fill=second)
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check-existing", action="store_true",
                        help="Check exact image pixels and all summary metadata without replacing references.")
    args = parser.parse_args()
    payload = json.loads(MANIFEST.read_text(encoding="utf-8"))
    sprites = payload["sprites"]
    backgrounds = [entry for entry in sprites if entry["category"] == "Background"]
    groups = [
        ("CONNECTABLE WORLD / FURNITURE", [entry for entry in sprites if entry["category"] in {"Tile", "WorldObject"}]),
        ("TOOLS / ITEMS / COMBAT", [entry for entry in sprites if entry["category"] in {"Tool", "Weapon"}]),
        ("LIVING ENTITIES / 8-FRAME SHEETS", [entry for entry in sprites if entry["category"] == "Entity"]),
        ("MODERN UI / NINE-SLICE / ICONS", [entry for entry in sprites if entry["category"] == "UI"]),
    ]
    canvas = Image.new("RGBA", (1800, 2600), (236, 230, 218, 255))
    draw = ImageDraw.Draw(canvas)
    draw.text((24, 18), "YjsE PRODUCTION WAVE 05 / LIVING WORLD", font=FONT, fill=INK)
    draw.text((24, 36), "Deterministic final-size pixels / exact sheets / binary alpha / yjse-pixel-v1", font=FONT, fill=INK)
    y = 62
    draw.rectangle((20, y, 1780, y + 25), fill=INK)
    draw.text((30, y + 8), "BIOME PARALLAX / NATIVE AND WRAP-SEAM QA", font=FONT, fill=LIGHT)
    y += 35
    seam_records = []
    for entry in backgrounds:
        sprite = load(entry["path"])
        draw.rectangle((20, y, 1780, y + 310), fill=PANEL)
        draw.text((30, y + 8), f"{entry['id']} / {sprite.width}x{sprite.height}", font=FONT, fill=INK)
        canvas.alpha_composite(sprite.resize((1024, 256), Image.Resampling.NEAREST), (30, y + 32))
        wrap = Image.new("RGBA", (640, 128))
        wrap.alpha_composite(sprite.crop((448, 0, 512, 128)), (0, 0))
        wrap.alpha_composite(sprite, (64, 0))
        wrap.alpha_composite(sprite.crop((0, 0, 64, 128)), (576, 0))
        canvas.alpha_composite(wrap, (1085, y + 48))
        draw.line((1149, y + 48, 1149, y + 175), fill=ACCENT, width=1)
        left = list(sprite.crop((0, 0, 1, sprite.height)).get_flattened_data())
        right = list(sprite.crop((sprite.width - 1, 0, sprite.width, sprite.height)).get_flattened_data())
        seam_records.append({"spriteId": entry["id"], "horizontalSeamColumnsEqual": left == right})
        y += 320

    for heading, entries in groups:
        draw.rectangle((20, y, 1780, y + 25), fill=INK)
        draw.text((30, y + 8), heading, font=FONT, fill=LIGHT)
        y += 35
        for index, entry in enumerate(entries):
            column, row = index % 4, index // 4
            x = 20 + column * 440
            card_y = y + row * 210
            sprite = load(entry["path"])
            draw.rectangle((x, card_y, x + 428, card_y + 198), fill=PANEL)
            draw.text((x + 8, card_y + 7), f"{entry['id']} / {sprite.width}x{sprite.height}", font=FONT, fill=INK)
            for side, background in enumerate((LIGHT, DARK)):
                shade = tuple(max(0, c - 12) for c in background[:3]) + (255,)
                field = checker(198, 155, background, shade)
                field_x = x + 8 + side * 207
                canvas.alpha_composite(field, (field_x, card_y + 32))
                scale = min(5, max(1, min(188 // sprite.width, 140 // sprite.height)))
                scaled = sprite.resize((sprite.width * scale, sprite.height * scale), Image.Resampling.NEAREST)
                field.alpha_composite(scaled, ((198 - scaled.width) // 2, (155 - scaled.height) // 2))
                canvas.alpha_composite(field, (field_x, card_y + 32))
        y += ((len(entries) + 3) // 4) * 210 + 10

    used = canvas.crop((0, 0, canvas.width, min(canvas.height, y + 20)))
    encoded = BytesIO()
    used.save(encoded, "PNG", optimize=False, compress_level=9)
    generated_bytes = encoded.getvalue()
    summary = {
        "waveId": "wave_05_living_world_production",
        "preview": "art_direction/wave_05_living_world_preview.png",
        "dimensions": list(used.size),
        "sha256": sha256(generated_bytes).hexdigest(),
        "assetCount": len(sprites),
        "categoryCounts": {category: sum(entry["category"] == category for entry in sprites) for category in sorted({entry["category"] for entry in sprites})},
        "integerScales": [1, 2, 3, 4, 5],
        "backgroundFields": ["light", "dark"],
        "parallax": seam_records,
    }
    if args.check_existing:
        if not preview_contract_matches(generated_bytes, summary, OUTPUT.read_bytes(),
                                        json.loads(SUMMARY.read_text(encoding="utf-8"))):
            raise SystemExit("Wave05 preview pixels or summary metadata differ from the checked-in reference.")
        print("Wave05 preview pixels and summary metadata match the checked-in reference.")
    else:
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT.write_bytes(generated_bytes)
        SUMMARY.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
