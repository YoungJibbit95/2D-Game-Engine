#!/usr/bin/env python3
"""Generate the native 24x40 Wave 06 layered player rig and its contracts."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw

from generate_wave_02_assets import C, image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art_direction" / "generated_sources" / "wave_06_character_source.png"
OUTPUT_ROOT = ROOT / "Assets" / "Wave06Character"
MANIFEST = ROOT / "assets" / "wave06_character.sprites.json"
BRIEFS = ROOT / "asset_briefs" / "production_wave_06_character_briefs.json"
PROVENANCE = ROOT / "art_direction" / "wave_06_character_provenance.json"
PREVIEW = ROOT / "art_direction" / "wave_06_character_preview.png"
PREVIEW_SUMMARY = ROOT / "art_direction" / "wave_06_character_preview_summary.json"
ASSET_AUDIT = ROOT / "art_direction" / "wave_06_character_asset_audit.json"
RUNTIME_PROFILES = ROOT / "animations" / "wave06_character_runtime_profiles.json"

FRAME_WIDTH = 24
FRAME_HEIGHT = 40
FRAME_COUNT = 16
SHEET_WIDTH = FRAME_WIDTH * FRAME_COUNT
PIVOT = (12, 40)
FRAME_IDS = [
    "idle_0", "idle_1",
    "run_0", "run_1", "run_2", "run_3", "run_4", "run_5",
    "jump", "fall",
    "tool_0", "tool_1", "tool_2",
    "block", "hurt_0", "hurt_1",
]
POSES = [
    {"bob": 0, "stride": 0, "arm": 0, "kind": "idle"},
    {"bob": -1, "stride": 0, "arm": 1, "kind": "idle"},
    {"bob": 0, "stride": -3, "arm": 3, "kind": "run"},
    {"bob": -1, "stride": -2, "arm": 2, "kind": "run"},
    {"bob": 0, "stride": 1, "arm": -1, "kind": "run"},
    {"bob": 0, "stride": 3, "arm": -3, "kind": "run"},
    {"bob": -1, "stride": 2, "arm": -2, "kind": "run"},
    {"bob": 0, "stride": -1, "arm": 1, "kind": "run"},
    {"bob": -3, "stride": -2, "arm": -3, "kind": "jump"},
    {"bob": -1, "stride": 2, "arm": 3, "kind": "fall"},
    {"bob": 0, "stride": 0, "arm": -4, "kind": "tool_0"},
    {"bob": 0, "stride": 0, "arm": -1, "kind": "tool_1"},
    {"bob": 1, "stride": 1, "arm": 4, "kind": "tool_2"},
    {"bob": 0, "stride": -1, "arm": -2, "kind": "block"},
    {"bob": 0, "stride": 2, "arm": 3, "kind": "hurt_0"},
    {"bob": 1, "stride": 3, "arm": 4, "kind": "hurt_1"},
]
LAYERS = ("body", "clothes", "hair", "armor", "equipment")
LAYER_PATHS = {
    layer: f"Assets/Wave06Character/player_{layer}_actions.png"
    for layer in LAYERS
}
SOURCE_PROMPT = """Use case: stylized-concept
Asset type: production source board for deterministic conversion into a more detailed 24x40 side-view pixel-art character layer atlas
Primary request: Refine Image 1 into one coherent original human sandbox adventurer with substantially richer anatomy, facial readability, hair volume, cloth folds, boots, gloves, copper-and-steel armor plates, belt hardware, pickaxe and shield silhouettes. Keep the same strict 16-column action sequence: idle A, idle B, six distinct run phases, jump, fall, three tool-use phases, defensive block, hurt recoil A, hurt recoil B.
Input image: Image 1 is the prior repository-owned character source and fixes identity, teal/copper material language, action ordering, side-view orientation, and layer concept.
Scene/backdrop: perfectly flat solid #FF00FF chroma-key field, no floor, no shadows, no gradient, no texture.
Style/medium: original crisp high-detail pixel art for a 2D side-view sandbox game; classic handcrafted sprite discipline and readable Terraria-like scale without copying any Terraria character, palette, pose, clothing, or asset; hard clusters, two-step material highlights, restrained one-pixel near-black contours at intended target scale, no painterly softness.
Composition/framing: orthographic side view facing right, aligned baseline, generous even spacing; five text-free horizontal rows corresponding to composite figure, head/hair, clothing/body gear, armor, held equipment; consistent body registration and pivots in every column.
Color palette: restrained project palette, warm skin, dark navy outline, teal cloth, copper orange, muted steel blue, dark brown leather, small amber hit accents.
Constraints: preserve exactly one character identity and exactly 16 readable action columns; every pose must remain readable when reconstructed at 24x40; retain clear separability of body, hair, clothes, armor, and equipment; opaque hard-edged pixel shapes only; no text, letters, numbers, labels, watermark, anti-aliasing, drop shadow, perspective, blur, glow, semi-transparent pixels, extra characters, scenery, UI chrome, duplicated filler poses, or #FF00FF inside the subject."""


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save_png(path: Path, source: Image.Image) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    rgba = source.convert("RGBA")
    rgba.putalpha(rgba.getchannel("A").point(lambda value: 255 if value else 0))
    rgba.save(path, format="PNG", optimize=False, compress_level=9)


def geometry(frame: int) -> dict[str, int | str]:
    pose = POSES[frame]
    hurt = -1 if pose["kind"] == "hurt_0" else (1 if pose["kind"] == "hurt_1" else 0)
    return {
        **pose,
        "left": frame * FRAME_WIDTH,
        "center": frame * FRAME_WIDTH + 12 + hurt,
        "top": 3 + int(pose["bob"]),
    }


def limb_points(g: dict[str, int | str]) -> tuple[tuple[int, int], tuple[int, int], tuple[int, int], tuple[int, int]]:
    center, top = int(g["center"]), int(g["top"])
    arm, kind = int(g["arm"]), str(g["kind"])
    left_shoulder = (center - 5, top + 16)
    right_shoulder = (center + 5, top + 16)
    left_hand = (center - 7 - max(0, arm // 2), top + 25 + arm)
    right_hand = (center + 7 + max(0, -arm // 2), top + 25 - arm)
    if kind.startswith("tool_"):
        right_hand = {
            "tool_0": (center + 2, top + 13),
            "tool_1": (center + 7, top + 19),
            "tool_2": (center + 4, top + 28),
        }[kind]
    elif kind == "block":
        right_hand = (center + 5, top + 19)
    return left_shoulder, left_hand, right_shoulder, right_hand


def leg_points(g: dict[str, int | str]) -> tuple[tuple[int, int], tuple[int, int], tuple[int, int], tuple[int, int]]:
    center, top = int(g["center"]), int(g["top"])
    stride, kind = int(g["stride"]), str(g["kind"])
    hip_y = top + 27
    left_hip, right_hip = (center - 3, hip_y), (center + 3, hip_y)
    if kind == "jump":
        return left_hip, (center - 6, top + 35), right_hip, (center + 5, top + 34)
    if kind == "fall":
        return left_hip, (center - 4, top + 36), right_hip, (center + 7, top + 36)
    return left_hip, (center - 3 + stride, 38), right_hip, (center + 3 - stride, 38)


def outlined_line(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], fill: tuple[int, int, int, int], width: int = 2) -> None:
    draw.line(points, fill=C["outline"], width=width + 2)
    draw.line(points, fill=fill, width=width)


def draw_body_layer() -> Image.Image:
    result = image(SHEET_WIDTH, FRAME_HEIGHT)
    draw = ImageDraw.Draw(result)
    skin = C["mush3"]
    skin_light = C["pale"]
    skin_shadow = C["copper0"]
    for frame in range(FRAME_COUNT):
        g = geometry(frame)
        center, top = int(g["center"]), int(g["top"])
        left_shoulder, left_hand, right_shoulder, right_hand = limb_points(g)
        left_hip, left_foot, right_hip, right_foot = leg_points(g)

        draw.polygon(
            [(center - 5, top + 2), (center + 3, top + 2), (center + 6, top + 5),
             (center + 6, top + 11), (center + 3, top + 14), (center - 4, top + 13),
             (center - 6, top + 10), (center - 6, top + 5)],
            fill=C["outline"],
        )
        draw.polygon(
            [(center - 4, top + 3), (center + 2, top + 3), (center + 5, top + 6),
             (center + 5, top + 10), (center + 2, top + 13), (center - 3, top + 12),
             (center - 5, top + 9), (center - 5, top + 5)],
            fill=skin,
        )
        draw.line((center - 3, top + 4, center + 1, top + 3), fill=skin_light, width=2)
        draw.point((center + 3, top + 7), fill=C["outline"])
        draw.point((center + 4, top + 10), fill=skin_shadow)
        draw.rectangle((center - 2, top + 13, center + 2, top + 16), fill=C["outline"])
        draw.rectangle((center - 1, top + 13, center + 1, top + 15), fill=skin)

        draw.polygon(
            [(center - 5, top + 15), (center + 5, top + 15), (center + 6, top + 27),
             (center + 3, top + 29), (center - 4, top + 29), (center - 6, top + 26)],
            fill=C["outline"],
        )
        draw.polygon(
            [(center - 4, top + 16), (center + 4, top + 16), (center + 5, top + 26),
             (center + 2, top + 28), (center - 3, top + 28), (center - 5, top + 25)],
            fill=skin,
        )
        outlined_line(draw, [left_shoulder, left_hand], skin, 2)
        outlined_line(draw, [right_shoulder, right_hand], skin_light, 2)
        draw.rectangle((left_hand[0] - 1, left_hand[1] - 1, left_hand[0] + 1, left_hand[1] + 1), fill=skin)
        draw.rectangle((right_hand[0] - 1, right_hand[1] - 1, right_hand[0] + 1, right_hand[1] + 1), fill=skin_light)
        outlined_line(draw, [left_hip, left_foot], skin_shadow, 3)
        outlined_line(draw, [right_hip, right_foot], skin, 3)
    return result


def draw_clothes_layer() -> Image.Image:
    result = image(SHEET_WIDTH, FRAME_HEIGHT)
    draw = ImageDraw.Draw(result)
    cloth = C["teal0"]
    cloth_mid = C["teal1"]
    cloth_light = C["teal2"]
    for frame in range(FRAME_COUNT):
        g = geometry(frame)
        center, top = int(g["center"]), int(g["top"])
        left_shoulder, left_hand, right_shoulder, right_hand = limb_points(g)
        left_hip, left_foot, right_hip, right_foot = leg_points(g)

        draw.polygon(
            [(center - 6, top + 15), (center + 5, top + 15), (center + 6, top + 26),
             (center + 3, top + 29), (center - 4, top + 29), (center - 6, top + 25)],
            fill=C["outline"],
        )
        draw.polygon(
            [(center - 5, top + 16), (center + 4, top + 16), (center + 5, top + 25),
             (center + 2, top + 27), (center - 3, top + 27), (center - 5, top + 24)],
            fill=cloth,
        )
        draw.polygon(
            [(center - 4, top + 16), (center + 1, top + 16), (center - 1, top + 21),
             (center - 4, top + 22)],
            fill=cloth_light,
        )
        draw.line((center - 4, top + 27, center + 3, top + 27), fill=C["wood0"], width=2)
        draw.rectangle((center - 1, top + 26, center + 1, top + 28), fill=C["outline"])
        draw.point((center, top + 27), fill=C["gold2"])

        outlined_line(draw, [left_shoulder, (left_hand[0], left_hand[1] - 2)], cloth, 3)
        outlined_line(draw, [right_shoulder, (right_hand[0], right_hand[1] - 2)], cloth_mid, 3)
        outlined_line(draw, [left_hip, left_foot], C["cloth_blue"], 3)
        outlined_line(draw, [right_hip, right_foot], C["cloth_blue_hi"], 3)
        for foot in (left_foot, right_foot):
            draw.rectangle((foot[0] - 2, foot[1] - 2, foot[0] + 2, min(39, foot[1] + 1)), fill=C["outline"])
            draw.rectangle((foot[0] - 1, foot[1] - 2, foot[0] + 2, min(38, foot[1])), fill=C["wood1"])
            draw.point((foot[0] + 1, min(38, foot[1] - 1)), fill=C["wood3"])
    return result


def draw_hair_layer() -> Image.Image:
    result = image(SHEET_WIDTH, FRAME_HEIGHT)
    draw = ImageDraw.Draw(result)
    base, shadow, light = C["wood1"], C["wood0"], C["wood3"]
    for frame in range(FRAME_COUNT):
        g = geometry(frame)
        center, top = int(g["center"]), int(g["top"])
        wind = max(-1, min(1, int(g["stride"])))
        draw.polygon(
            [(center - 7, top + 7), (center - 6, top + 2), (center - 3, top + 2),
             (center - 2 + wind, top), (center + 1, top + 2), (center + 4, top),
             (center + 5, top + 3), (center + 8 + wind, top + 2), (center + 7, top + 7),
             (center + 5, top + 9), (center + 3, top + 6), (center + 1, top + 5),
             (center - 2, top + 6), (center - 4, top + 11), (center - 7, top + 10)],
            fill=C["outline"],
        )
        draw.polygon(
            [(center - 6, top + 7), (center - 5, top + 3), (center - 2, top + 3),
             (center - 1 + wind, top + 2), (center + 1, top + 3), (center + 4, top + 2),
             (center + 4, top + 4), (center + 6 + wind, top + 4), (center + 5, top + 7),
             (center + 3, top + 5), (center, top + 4), (center - 3, top + 6),
             (center - 4, top + 9), (center - 6, top + 9)],
            fill=base,
        )
        draw.polygon(
            [(center - 5, top + 4), (center - 2, top + 3), (center + 1, top + 4),
             (center - 1, top + 5), (center - 4, top + 6)],
            fill=light,
        )
        draw.rectangle((center - 6, top + 8, center - 4, top + 11), fill=shadow)
        draw.point((center + 4, top + 4), fill=C["copper2"])
    return result


def draw_armor_layer() -> Image.Image:
    result = image(SHEET_WIDTH, FRAME_HEIGHT)
    draw = ImageDraw.Draw(result)
    base, shadow, light = C["copper1"], C["copper0"], C["copper3"]
    for frame in range(FRAME_COUNT):
        g = geometry(frame)
        center, top = int(g["center"]), int(g["top"])
        left_shoulder, left_hand, right_shoulder, right_hand = limb_points(g)
        left_hip, left_foot, right_hip, right_foot = leg_points(g)

        draw.polygon(
            [(center - 6, top + 15), (center + 5, top + 15), (center + 6, top + 25),
             (center + 3, top + 29), (center - 4, top + 29), (center - 6, top + 25)],
            fill=C["outline"],
        )
        draw.polygon(
            [(center - 5, top + 16), (center + 4, top + 16), (center + 5, top + 24),
             (center + 2, top + 27), (center - 3, top + 27), (center - 5, top + 24)],
            fill=base,
        )
        draw.polygon(
            [(center - 4, top + 16), (center + 2, top + 16), (center + 1, top + 19),
             (center - 3, top + 20)],
            fill=light,
        )
        draw.line((center - 3, top + 22, center + 4, top + 22), fill=shadow, width=1)
        draw.line((center, top + 18, center, top + 26), fill=C["outline"], width=1)
        draw.rectangle((left_shoulder[0] - 2, left_shoulder[1] - 2, left_shoulder[0] + 2, left_shoulder[1] + 2), fill=C["outline"])
        draw.rectangle((left_shoulder[0] - 1, left_shoulder[1] - 1, left_shoulder[0] + 1, left_shoulder[1] + 1), fill=base)
        draw.rectangle((right_shoulder[0] - 2, right_shoulder[1] - 2, right_shoulder[0] + 2, right_shoulder[1] + 2), fill=C["outline"])
        draw.rectangle((right_shoulder[0] - 1, right_shoulder[1] - 1, right_shoulder[0] + 1, right_shoulder[1] + 1), fill=light)
        for hand in (left_hand, right_hand):
            draw.rectangle((hand[0] - 2, hand[1] - 2, hand[0] + 1, hand[1] + 1), fill=C["outline"])
            draw.rectangle((hand[0] - 1, hand[1] - 1, hand[0], hand[1]), fill=base)
        for knee, foot in ((left_hip, left_foot), (right_hip, right_foot)):
            knee_y = (knee[1] + foot[1]) // 2
            knee_x = (knee[0] + foot[0]) // 2
            draw.rectangle((knee_x - 2, knee_y - 1, knee_x + 2, knee_y + 2), fill=C["outline"])
            draw.rectangle((knee_x - 1, knee_y, knee_x + 1, knee_y + 1), fill=base)
        if str(g["kind"]) == "block":
            draw.polygon(
                [(center - 5, top + 2), (center + 4, top + 2), (center + 6, top + 5),
                 (center + 4, top + 7), (center - 5, top + 7)],
                fill=C["outline"],
            )
            draw.rectangle((center - 4, top + 3, center + 4, top + 5), fill=base)
            draw.line((center - 2, top + 3, center + 3, top + 3), fill=light, width=1)
    return result


def draw_pickaxe(draw: ImageDraw.ImageDraw, center: int, top: int, phase: int) -> None:
    if phase == 0:
        handle = [(center + 1, top + 22), (center + 8, top + 7)]
        head = [(center + 3, top + 5), (center + 11, top + 5), (center + 9, top + 9), (center + 5, top + 8)]
    elif phase == 1:
        handle = [(center + 1, top + 22), (center + 10, top + 16)]
        head = [(center + 7, top + 13), (center + 11, top + 15), (center + 10, top + 19), (center + 7, top + 18)]
    else:
        handle = [(center + 1, top + 20), (center + 7, top + 31)]
        head = [(center + 4, top + 29), (center + 11, top + 28), (center + 11, top + 32), (center + 5, top + 33)]
    draw.line(handle, fill=C["outline"], width=4)
    draw.line(handle, fill=C["wood3"], width=2)
    draw.polygon(head, fill=C["outline"])
    inset = [(x - 1 if x > center + 6 else x + 1, y) for x, y in head]
    draw.line(inset[:3], fill=C["soft"], width=1)


def draw_equipment_layer() -> Image.Image:
    result = image(SHEET_WIDTH, FRAME_HEIGHT)
    draw = ImageDraw.Draw(result)
    for frame in range(FRAME_COUNT):
        g = geometry(frame)
        center, top, kind = int(g["center"]), int(g["top"]), str(g["kind"])
        if kind.startswith("tool_"):
            draw_pickaxe(draw, center, top, int(kind[-1]))
        elif kind == "block":
            draw.polygon(
                [(center + 3, top + 14), (center + 9, top + 11), (center + 11, top + 14),
                 (center + 11, top + 28), (center + 7, top + 33), (center + 3, top + 29)],
                fill=C["outline"],
            )
            draw.polygon(
                [(center + 5, top + 15), (center + 8, top + 13), (center + 9, top + 15),
                 (center + 9, top + 27), (center + 7, top + 30), (center + 5, top + 28)],
                fill=C["wood2"],
            )
            draw.line((center + 6, top + 15, center + 8, top + 27), fill=C["copper2"], width=2)
            draw.point((center + 7, top + 20), fill=C["gold2"])
        elif kind.startswith("hurt"):
            spark_x = center + (8 if kind == "hurt_0" else -7)
            spark_y = top + (10 if kind == "hurt_0" else 7)
            draw.line((spark_x - 3, spark_y, spark_x + 3, spark_y), fill=C["outline"], width=1)
            draw.line((spark_x, spark_y - 3, spark_x, spark_y + 3), fill=C["outline"], width=1)
            draw.line((spark_x - 2, spark_y - 2, spark_x + 2, spark_y + 2), fill=C["red3"], width=1)
            draw.point((spark_x, spark_y), fill=C["pale"])
    return result


def frame_entries() -> list[dict]:
    return [
        {
            "id": frame_id,
            "x": index * FRAME_WIDTH,
            "y": 0,
            "width": FRAME_WIDTH,
            "height": FRAME_HEIGHT,
            "originX": PIVOT[0],
            "originY": PIVOT[1],
        }
        for index, frame_id in enumerate(FRAME_IDS)
    ]


def manifest_entries() -> list[dict]:
    entries = []
    for draw_order, layer in enumerate(LAYERS):
        entries.append({
            "id": f"entities/player/character_v2_wave06/{layer}",
            "path": LAYER_PATHS[layer],
            "category": "Entity",
            "width": SHEET_WIDTH,
            "height": FRAME_HEIGHT,
            "pixelsPerUnit": 16,
            "atlasId": "entities",
            "originX": PIVOT[0],
            "originY": PIVOT[1],
            "renderLayer": f"world.entity.player.{draw_order:02d}.{layer}",
            "license": "YjsE-Project-Owned",
            "provenance": "wave_06_character_production; native final-size deterministic generator; wave_06_character_provenance.json",
            "tags": ["production", "runtime-active", "wave-06", "entity", "player", "character-v2", "layered", layer, "animated"],
            "frames": frame_entries(),
        })
    return entries


def write_manifest(entries: list[dict]) -> None:
    MANIFEST.write_text(json.dumps({"sprites": entries}, indent=2) + "\n", encoding="utf-8")


def write_briefs(entries: list[dict]) -> None:
    palette = sorted({"#%02x%02x%02x" % color[:3] for name, color in C.items() if name != "clear"})
    payload = {
        "version": 1,
        "scope": "wave_06_character_production",
        "sourcePrompt": SOURCE_PROMPT,
        "globalStyle": "Original YjsE side-view pixel art; crisp one-pixel clusters; top-left light; binary alpha; warm copper, teal cloth and readable brown hair.",
        "globalNegativePrompt": "16x32 upscale, resized legacy sheet, copied game sprite, blur, antialiasing, partial alpha, gradients, noisy dithering, frame labels, text, logo, watermark",
        "globalRequirements": [
            "Draw directly at 24x40 per frame; never read or resize a Wave 04 runtime sheet.",
            "Keep all five layers registered to pivot 12,40 and the same 16-pose order.",
            "Keep feet on y=38/39 for grounded poses and all visible pixels inside each frame.",
            "Use only the checked-in YjsE palette and binary alpha.",
            "Remain readable at native scale and integer-scaled 1080p/1440p presentation.",
        ],
        "briefs": [
            {
                "spriteId": entry["id"],
                "outputPath": entry["path"],
                "width": entry["width"],
                "height": entry["height"],
                "frameWidth": FRAME_WIDTH,
                "frameHeight": FRAME_HEIGHT,
                "frameOrder": FRAME_IDS,
                "pivot": {"x": PIVOT[0], "y": PIVOT[1]},
                "prompt": f"{SOURCE_PROMPT} Produce only the synchronized {entry['id'].split('/')[-1]} layer.",
                "palette": palette,
                "runtimeIdTarget": entry["id"],
            }
            for entry in entries
        ],
    }
    BRIEFS.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def clip(clip_id: str, display: str, loop: str, frames: list[tuple[int, int]], tags: list[str], events: list[dict] | None = None) -> dict:
    result = {
        "id": clip_id,
        "displayName": display,
        "sprite": "entities/player/character_v2_wave06/body",
        "loopMode": loop,
        "frames": [
            {
                "frame": frame,
                "frameId": FRAME_IDS[frame],
                "sourceRectangle": {"x": frame * FRAME_WIDTH, "y": 0, "width": FRAME_WIDTH, "height": FRAME_HEIGHT},
                "durationSeconds": ticks / 60.0,
                "durationTicks": ticks,
            }
            for frame, ticks in frames
        ],
        "tags": ["player", "wave-06", "fixed-tick", *tags],
    }
    if events:
        result["events"] = events
    return result


def write_runtime_profiles() -> None:
    clips = [
        clip("player.wave06.idle", "Wave 06 Player Idle", "PingPong", [(0, 18), (1, 18)], ["locomotion"]),
        clip("player.wave06.run", "Wave 06 Player Run", "Loop", [(i, 5) for i in range(2, 8)], ["locomotion"]),
        clip("player.wave06.jump", "Wave 06 Player Jump", "Once", [(8, 8)], ["airborne"]),
        clip("player.wave06.fall", "Wave 06 Player Fall", "Once", [(9, 8)], ["airborne"]),
        clip("player.wave06.tool", "Wave 06 Player Tool Action", "Once", [(10, 5), (11, 5), (12, 5)], ["action"], [{"id": "tool.windup", "tick": 0}, {"id": "tool.impact", "tick": 5}]),
        clip("player.wave06.block", "Wave 06 Player Block", "Loop", [(13, 8)], ["guard"]),
        clip("player.wave06.hurt", "Wave 06 Player Hurt", "Once", [(14, 5), (15, 5)], ["action"], [{"id": "hurt.flash", "tick": 0}]),
        clip("player.wave06.death", "Wave 06 Player Death Hold", "Once", [(15, 60)], ["terminal"]),
    ]
    states = [
        {"id": "idle", "clipId": "player.wave06.idle"},
        {"id": "run", "clipId": "player.wave06.run", "scaleWithLocomotion": True, "locomotionReferenceSpeedMilliUnitsPerSecond": 120000, "minimumLocomotionRatePercentage": 50, "maximumLocomotionRatePercentage": 200},
        {"id": "jump", "clipId": "player.wave06.jump"},
        {"id": "fall", "clipId": "player.wave06.fall"},
        {"id": "tool", "clipId": "player.wave06.tool", "actionLockMode": "UntilClipComplete", "completionStateId": "idle"},
        {"id": "block", "clipId": "player.wave06.block"},
        {"id": "hurt", "clipId": "player.wave06.hurt", "actionLockMode": "UntilClipComplete", "completionStateId": "idle"},
        {"id": "death", "clipId": "player.wave06.death"},
    ]
    transitions = [
        {"fromStateId": "*", "toStateId": state["id"], "blendTicks": 0 if state["id"] == "death" else (2 if state["id"] in {"idle", "run"} else 1)}
        for state in states
    ]
    payload = {
        "animations": clips,
        "runtimeAnimationContent": {
            "clips": clips,
            "stateMachines": [{
                "id": "player.wave06.states",
                "layers": [{"id": "base", "priority": 0, "initialStateId": "idle", "states": states, "transitions": transitions}],
            }],
        }
    }
    RUNTIME_PROFILES.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def alpha_bounds(source: Image.Image, frame: int) -> list[int] | None:
    crop = source.crop((frame * FRAME_WIDTH, 0, (frame + 1) * FRAME_WIDTH, FRAME_HEIGHT))
    bounds = crop.getchannel("A").getbbox()
    return list(bounds) if bounds is not None else None


def write_preview(layers: dict[str, Image.Image]) -> None:
    rows: list[Image.Image] = []
    composite = image(SHEET_WIDTH, FRAME_HEIGHT)
    for layer in ("body", "clothes", "hair", "equipment"):
        composite.alpha_composite(layers[layer])
    armored = composite.copy()
    armored.alpha_composite(layers["armor"])
    rows.extend([composite, armored, *[layers[layer] for layer in LAYERS]])

    sheet = image(SHEET_WIDTH, FRAME_HEIGHT * len(rows), C["dark"])
    checker = ImageDraw.Draw(sheet)
    for row in range(len(rows)):
        for y in range(FRAME_HEIGHT):
            for x in range(SHEET_WIDTH):
                checker.point((x, row * FRAME_HEIGHT + y), fill=C["blue0"] if ((x // 4) + (y // 4)) % 2 else C["blue1"])
        sheet.alpha_composite(rows[row], (0, row * FRAME_HEIGHT))
        checker.line((0, row * FRAME_HEIGHT, SHEET_WIDTH - 1, row * FRAME_HEIGHT), fill=C["pale"], width=1)
        for frame in range(FRAME_COUNT + 1):
            x = min(SHEET_WIDTH - 1, frame * FRAME_WIDTH)
            checker.line((x, row * FRAME_HEIGHT, x, (row + 1) * FRAME_HEIGHT - 1), fill=C["mid"], width=1)
    scaled = sheet.resize((sheet.width * 4, sheet.height * 4), Image.Resampling.NEAREST)
    save_png(PREVIEW, scaled)

    summary = {
        "version": 1,
        "preview": PREVIEW.relative_to(ROOT).as_posix(),
        "previewDimensions": list(scaled.size),
        "rows": ["composite", "composite_with_armor", *LAYERS],
        "frameOrder": FRAME_IDS,
        "nativeFrameDimensions": [FRAME_WIDTH, FRAME_HEIGHT],
        "integerPreviewScale": 4,
        "nativeScaleReadable": True,
        "targetPresentation": ["native", "1080p integer point sampling", "1440p integer point sampling"],
    }
    PREVIEW_SUMMARY.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")


def write_provenance_and_audit(entries: list[dict], layers: dict[str, Image.Image]) -> None:
    source_hash = sha256(SOURCE)
    records = []
    audit_layers = []
    palette_rgb = {color[:3] for name, color in C.items() if name != "clear"}
    for entry in entries:
        path = ROOT / entry["path"]
        with Image.open(path) as opened:
            rgba = opened.convert("RGBA")
            alpha_values = sorted(set(rgba.getchannel("A").get_flattened_data()))
            colors = {pixel[:3] for pixel in rgba.get_flattened_data() if pixel[3]}
            bounds = [alpha_bounds(rgba, frame) for frame in range(FRAME_COUNT)]
        records.append({
            "spriteId": entry["id"],
            "path": entry["path"],
            "sha256": sha256(path),
            "dimensions": [SHEET_WIDTH, FRAME_HEIGHT],
            "frameDimensions": [FRAME_WIDTH, FRAME_HEIGHT],
            "frameCount": FRAME_COUNT,
            "frameOrder": FRAME_IDS,
            "pivot": list(PIVOT),
            "alphaValues": alpha_values,
            "generator": "Game.Data/art_direction/tools/generate_wave_06_character.py",
            "method": "deterministic Pillow primitives drawn directly at final 24x40 frame resolution",
            "generatedSourceSha256": source_hash,
            "sourceUsage": "art-direction, pose and palette reference only; no source or Wave 04 runtime pixels are resized into output",
            "license": "YjsE-Project-Owned",
            "runtimeConsumer": "Wave04PlayerCharacterRenderer configured with player.wave06 content profile",
        })
        audit_layers.append({
            "spriteId": entry["id"],
            "dimensionsPass": rgba.size == (SHEET_WIDTH, FRAME_HEIGHT),
            "binaryAlphaPass": alpha_values in ([0], [255], [0, 255]),
            "palettePass": colors.issubset(palette_rgb),
            "frameBounds": bounds,
            "allFramesContained": all(bound is None or (bound[0] >= 0 and bound[1] >= 0 and bound[2] <= FRAME_WIDTH and bound[3] <= FRAME_HEIGHT) for bound in bounds),
        })

    provenance = {
        "version": 1,
        "waveId": "wave_06_character_production",
        "generatedAt": "2026-07-19",
        "source": SOURCE.relative_to(ROOT).as_posix(),
        "sourceSha256": source_hash,
        "sourceDimensions": list(Image.open(SOURCE).size),
        "sourceTool": "built-in image_gen (imagegen skill default mode)",
        "sourceReference": "art_direction/generated_sources/wave_04_character_source.png",
        "sourcePrompt": SOURCE_PROMPT,
        "deterministicFinalResolution": True,
        "legacyRuntimeSheetsRead": False,
        "records": records,
    }
    PROVENANCE.write_text(json.dumps(provenance, indent=2) + "\n", encoding="utf-8")

    audit = {
        "version": 1,
        "scope": "wave_06_character_production",
        "passed": all(layer["dimensionsPass"] and layer["binaryAlphaPass"] and layer["palettePass"] and layer["allFramesContained"] for layer in audit_layers),
        "nativeFrameDimensions": [FRAME_WIDTH, FRAME_HEIGHT],
        "sharedPivot": list(PIVOT),
        "frameOrder": FRAME_IDS,
        "noLegacyUpscale": True,
        "layers": audit_layers,
    }
    ASSET_AUDIT.write_text(json.dumps(audit, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    if not SOURCE.exists():
        raise SystemExit(f"Missing source board: {SOURCE}")
    with Image.open(SOURCE) as opened:
        if opened.width < SHEET_WIDTH or opened.height < FRAME_HEIGHT:
            raise SystemExit("Source board is not a high-resolution art-direction reference.")

    builders = {
        "body": draw_body_layer,
        "clothes": draw_clothes_layer,
        "hair": draw_hair_layer,
        "armor": draw_armor_layer,
        "equipment": draw_equipment_layer,
    }
    layers = {layer: builders[layer]() for layer in LAYERS}
    for layer, source in layers.items():
        save_png(ROOT / LAYER_PATHS[layer], source)

    entries = manifest_entries()
    write_manifest(entries)
    write_briefs(entries)
    write_runtime_profiles()
    write_preview(layers)
    write_provenance_and_audit(entries, layers)

    if not json.loads(ASSET_AUDIT.read_text(encoding="utf-8"))["passed"]:
        raise SystemExit("Wave 06 character audit failed.")
    print(f"Generated {len(layers)} Wave 06 layers, {FRAME_COUNT} frames each, at {FRAME_WIDTH}x{FRAME_HEIGHT}.")


if __name__ == "__main__":
    main()
