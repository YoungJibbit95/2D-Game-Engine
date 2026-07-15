#!/usr/bin/env python3
"""Audit the sprite manifest and emit a machine-readable quality report."""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path

from PIL import Image


AUDIT_GENERATOR_VERSION = "2.0.0"
STANDALONE_CATEGORIES = {"Item", "Tool", "Weapon", "Projectile", "Particle", "Effect", "UI"}
SMALL_WORLD_CATEGORIES = {"Tile", "WorldObject"}
CONTRACT_HARD_ISSUES = {
    "missing_file",
    "dimension_mismatch",
    "empty_sprite",
    "missing_alpha_channel",
    "no_transparent_pixels",
    "partial_alpha_pixels",
    "missing_atlas_id",
    "missing_origin",
    "origin_out_of_bounds",
    "missing_render_layer",
    "missing_license",
    "missing_provenance",
    "source_alias_target_missing",
    "source_alias_target_is_alias",
    "source_alias_path_mismatch",
    "source_alias_dimension_mismatch",
    "duplicate_frame_id",
    "frame_out_of_bounds",
    "frame_origin_incomplete",
    "frame_origin_out_of_bounds",
    "missing_generation_brief",
    "brief_dimension_mismatch",
    "duplicate_generation_brief",
}
PRODUCTION_STYLE_HARD_ISSUES = {
    "style_palette_mismatch",
    "opaque_corner",
    "palette_over_recommended",
    "weak_dark_silhouette",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-root", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--style", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument(
        "--scan-root",
        type=Path,
        default=Path("sprites"),
        help="PNG root relative to --data-root. Defaults to sprites.",
    )
    parser.add_argument(
        "--fail-on-hard-issues",
        action="store_true",
        help="Exit with code 2 when packaging-contract issues are found.",
    )
    return parser.parse_args()


def parse_hex(value: str) -> tuple[int, int, int]:
    raw = value.lstrip("#")
    return tuple(int(raw[index : index + 2], 16) for index in (0, 2, 4))


def normalize_manifest_path(value: str) -> str:
    normalized = value.replace("\\", "/")
    while normalized.startswith("./"):
        normalized = normalized[2:]
    return normalized


def boundary_dark_ratio(image: Image.Image) -> float | None:
    pixels = image.load()
    opaque = {
        (x, y)
        for y in range(image.height)
        for x in range(image.width)
        if pixels[x, y][3] > 0
    }
    boundary = []
    for x, y in opaque:
        if any(
            (x + offset_x, y + offset_y) not in opaque
            for offset_x, offset_y in (
                (-1, -1),
                (0, -1),
                (1, -1),
                (-1, 0),
                (1, 0),
                (-1, 1),
                (0, 1),
                (1, 1),
            )
        ):
            boundary.append(pixels[x, y])
    if not boundary:
        return None
    dark = sum(
        1
        for red, green, blue, _ in boundary
        if 0.2126 * red + 0.7152 * green + 0.0722 * blue < 72
    )
    return round(dark / len(boundary), 4)


def flattened_pixels(image: Image.Image):
    get_flattened_data = getattr(image, "get_flattened_data", None)
    return get_flattened_data() if get_flattened_data else image.getdata()


def append_issue(result: dict, issue: str) -> None:
    if issue not in result["issues"]:
        result["issues"].append(issue)


def update_quality_tier(result: dict) -> None:
    score = result["readabilityScore"]
    if score >= 80:
        result["qualityTier"] = "pass"
    elif score >= 60:
        result["qualityTier"] = "review"
    else:
        result["qualityTier"] = "priority_regeneration"


def validate_production_metadata(entry: dict, result: dict) -> int:
    if "production-sample" not in entry.get("tags", []):
        return 0

    penalty = 0
    if not entry.get("atlasId"):
        append_issue(result, "missing_atlas_id")
        penalty += 20
    origin_x = entry.get("originX")
    origin_y = entry.get("originY")
    if origin_x is None or origin_y is None:
        append_issue(result, "missing_origin")
        penalty += 20
    elif not (0 <= origin_x <= entry["width"] and 0 <= origin_y <= entry["height"]):
        append_issue(result, "origin_out_of_bounds")
        penalty += 30
    if not entry.get("renderLayer"):
        append_issue(result, "missing_render_layer")
        penalty += 15
    if not entry.get("license"):
        append_issue(result, "missing_license")
        penalty += 15
    if not entry.get("provenance"):
        append_issue(result, "missing_provenance")
        penalty += 15
    return penalty


def validate_frames(entry: dict, result: dict) -> None:
    frames = entry.get("frames", [])
    frame_ids = set()
    frame_issue_names = {
        "duplicate_frame_id",
        "frame_out_of_bounds",
        "frame_origin_incomplete",
        "frame_origin_out_of_bounds",
    }
    for frame in frames:
        frame_id = frame.get("id")
        if frame_id in frame_ids:
            append_issue(result, "duplicate_frame_id")
        frame_ids.add(frame_id)

        x = frame.get("x")
        y = frame.get("y")
        width = frame.get("width")
        height = frame.get("height")
        if (
            not all(isinstance(value, int) for value in (x, y, width, height))
            or x < 0
            or y < 0
            or width <= 0
            or height <= 0
            or x + width > entry["width"]
            or y + height > entry["height"]
        ):
            append_issue(result, "frame_out_of_bounds")
            continue

        origin_x = frame.get("originX", entry.get("originX"))
        origin_y = frame.get("originY", entry.get("originY"))
        if (origin_x is None) != (origin_y is None):
            append_issue(result, "frame_origin_incomplete")
        elif origin_x is not None and not (
            0 <= origin_x <= width and 0 <= origin_y <= height
        ):
            append_issue(result, "frame_origin_out_of_bounds")

    result["frameCount"] = len(frames)
    result["frameContractValid"] = not any(
        issue in frame_issue_names for issue in result["issues"]
    )


def analyze_entry(
    data_root: Path,
    entry: dict,
    style_palette: set[tuple[int, int, int]],
    validation: dict,
) -> tuple[dict, str | None]:
    path = data_root / entry["path"]
    result = {
        "id": entry["id"],
        "path": normalize_manifest_path(entry["path"]),
        "sourceAliasOf": entry.get("sourceAliasOf"),
        "sourceAliasValid": None,
        "category": entry["category"],
        "declaredSize": [entry["width"], entry["height"]],
        "atlasId": entry.get("atlasId"),
        "origin": [entry.get("originX"), entry.get("originY")],
        "renderLayer": entry.get("renderLayer"),
        "license": entry.get("license"),
        "provenance": entry.get("provenance"),
        "tags": entry.get("tags", []),
        "issues": [],
    }
    metadata_penalty = validate_production_metadata(entry, result)
    validate_frames(entry, result)
    if not path.exists():
        result.update(
            {
                "exists": False,
                "readabilityScore": 0,
                "qualityTier": "priority_regeneration",
            }
        )
        append_issue(result, "missing_file")
        return result, None

    with Image.open(path) as opened:
        source_mode = opened.mode
        source_has_alpha = source_mode in {"RGBA", "LA"} or "transparency" in opened.info
        image = opened.convert("RGBA")

    alpha = image.getchannel("A")
    alpha_values = Counter(flattened_pixels(alpha))
    visible_bounds = alpha.getbbox()
    opaque_pixels = sum(count for value, count in alpha_values.items() if value > 0)
    partial_alpha_pixels = sum(
        count for value, count in alpha_values.items() if 0 < value < 255
    )
    color_counts = Counter(
        (red, green, blue)
        for red, green, blue, alpha_value in flattened_pixels(image)
        if alpha_value > 0
    )
    out_of_style_pixels = sum(
        count for color, count in color_counts.items() if color not in style_palette
    )
    total_pixels = image.width * image.height
    occupancy = opaque_pixels / total_pixels if total_pixels else 0
    dimensions_match = image.size == (entry["width"], entry["height"])
    digest = hashlib.sha256(
        image.tobytes() + f"{image.width}x{image.height}".encode("ascii")
    ).hexdigest()
    dark_ratio = boundary_dark_ratio(image)
    corner_alpha_values = [
        image.getpixel((0, 0))[3],
        image.getpixel((image.width - 1, 0))[3],
        image.getpixel((0, image.height - 1))[3],
        image.getpixel((image.width - 1, image.height - 1))[3],
    ]
    transparent_corners = all(value == 0 for value in corner_alpha_values)

    category = entry["category"]
    is_standalone = category in STANDALONE_CATEGORIES
    is_small_world = category in SMALL_WORLD_CATEGORIES and total_pixels <= 4096
    recommended_color_maximum = None
    if is_standalone:
        recommended_color_maximum = int(
            validation["standaloneRecommendedUniqueColorMaximum"]
        )
    elif is_small_world:
        recommended_color_maximum = int(
            validation["smallWorldSpriteRecommendedUniqueColorMaximum"]
        )
    transparent_corners_required = bool(
        validation.get("transparentCornersRequired", False)
    ) and (is_standalone or category == "WorldObject")

    result.update(
        {
            "exists": True,
            "actualSize": list(image.size),
            "dimensionsMatch": dimensions_match,
            "sourceMode": source_mode,
            "sourceHasAlpha": source_has_alpha,
            "hasTransparentPixels": 0 in alpha_values,
            "alphaExtrema": list(alpha.getextrema()),
            "partialAlphaPixels": partial_alpha_pixels,
            "cornerAlphaValues": corner_alpha_values,
            "transparentCorners": transparent_corners,
            "transparentCornersRequired": transparent_corners_required,
            "visibleBounds": list(visible_bounds) if visible_bounds else None,
            "opaquePixelCount": opaque_pixels,
            "occupancyRatio": round(occupancy, 4),
            "uniqueOpaqueColors": len(color_counts),
            "recommendedUniqueColorMaximum": recommended_color_maximum,
            "outOfStyleOpaquePixels": out_of_style_pixels,
            "stylePaletteComplianceRatio": round(
                1 - out_of_style_pixels / opaque_pixels, 4
            )
            if opaque_pixels
            else 0,
            "darkBoundaryRatio": dark_ratio,
            "contentSha256": digest,
        }
    )

    score = 100 - metadata_penalty
    allows_empty = "empty" in entry.get("tags", [])
    if not dimensions_match:
        append_issue(result, "dimension_mismatch")
        score -= 50
    if not visible_bounds and not allows_empty:
        append_issue(result, "empty_sprite")
        score = 0
    if is_standalone and not source_has_alpha:
        append_issue(result, "missing_alpha_channel")
        score -= 15
    if is_standalone and 0 not in alpha_values:
        append_issue(result, "no_transparent_pixels")
        score -= 10
    if partial_alpha_pixels:
        append_issue(result, "partial_alpha_pixels")
        score -= 10
    if out_of_style_pixels:
        append_issue(result, "style_palette_mismatch")
        score -= 15
    if transparent_corners_required and not transparent_corners:
        append_issue(result, "opaque_corner")
        score -= 10
    if (
        recommended_color_maximum is not None
        and len(color_counts) > recommended_color_maximum
    ):
        append_issue(result, "palette_over_recommended")
        score -= 15
    if is_standalone and occupancy < 0.08:
        append_issue(result, "low_canvas_occupancy")
        score -= 20
    if is_standalone and occupancy > 0.9:
        append_issue(result, "crowded_canvas")
        score -= 10
    if is_standalone and dark_ratio is not None and dark_ratio < 0.45:
        append_issue(result, "weak_dark_silhouette")
        score -= 15

    result["readabilityScore"] = max(0, score)
    update_quality_tier(result)
    return result, digest


def validate_source_aliases(
    manifest_entries: list[dict],
    analyzed_by_id: dict[str, dict],
) -> None:
    entries_by_id = {entry["id"]: entry for entry in manifest_entries}
    for entry in manifest_entries:
        alias_target_id = entry.get("sourceAliasOf")
        if not alias_target_id:
            continue

        result = analyzed_by_id[entry["id"]]
        target = entries_by_id.get(alias_target_id)
        valid = True
        if target is None:
            append_issue(result, "source_alias_target_missing")
            valid = False
        else:
            if target.get("sourceAliasOf"):
                append_issue(result, "source_alias_target_is_alias")
                valid = False
            if normalize_manifest_path(target["path"]) != normalize_manifest_path(entry["path"]):
                append_issue(result, "source_alias_path_mismatch")
                valid = False
            if (target["width"], target["height"]) != (
                entry["width"],
                entry["height"],
            ):
                append_issue(result, "source_alias_dimension_mismatch")
                valid = False
        result["sourceAliasValid"] = valid


def split_duplicate_and_alias_groups(
    hashes: dict[str, list[dict]],
    analyzed_by_id: dict[str, dict],
) -> tuple[list[dict], list[dict]]:
    duplicate_groups = []
    source_alias_groups = []
    for digest, entries in hashes.items():
        if len(entries) <= 1:
            continue

        paths = {normalize_manifest_path(entry["path"]) for entry in entries}
        canonical_entries = [entry for entry in entries if not entry.get("sourceAliasOf")]
        alias_entries = [entry for entry in entries if entry.get("sourceAliasOf")]
        is_valid_alias_group = (
            len(paths) == 1
            and len(canonical_entries) == 1
            and len(alias_entries) == len(entries) - 1
            and all(
                analyzed_by_id[entry["id"]]["sourceAliasValid"] is True
                and entry["sourceAliasOf"] == canonical_entries[0]["id"]
                for entry in alias_entries
            )
        )
        if is_valid_alias_group:
            source_alias_groups.append(
                {
                    "contentSha256": digest,
                    "path": next(iter(paths)),
                    "canonicalId": canonical_entries[0]["id"],
                    "aliases": [
                        {
                            "id": entry["id"],
                            "sourceAliasOf": entry["sourceAliasOf"],
                        }
                        for entry in alias_entries
                    ],
                }
            )
            continue

        duplicate_groups.append(
            {
                "contentSha256": digest,
                "ids": [entry["id"] for entry in entries],
                "paths": sorted(paths),
            }
        )
    return duplicate_groups, source_alias_groups


def validate_generation_briefs(
    data_root: Path,
    manifest_entries: list[dict],
    analyzed_by_id: dict[str, dict],
) -> dict:
    brief_root = data_root / "asset_briefs"
    briefs_by_id: dict[str, list[dict]] = defaultdict(list)
    for brief_path in sorted(brief_root.rglob("*.json")):
        payload = json.loads(brief_path.read_text(encoding="utf-8"))
        for brief in payload.get("briefs", []):
            sprite_id = brief.get("spriteId")
            if sprite_id:
                briefs_by_id[sprite_id].append(brief)

    missing = []
    dimension_mismatches = []
    duplicate_ids = sorted(
        sprite_id for sprite_id, briefs in briefs_by_id.items() if len(briefs) > 1
    )
    for entry in manifest_entries:
        sprite_id = entry["id"]
        briefs = briefs_by_id.get(sprite_id, [])
        if not briefs:
            append_issue(analyzed_by_id[sprite_id], "missing_generation_brief")
            missing.append(sprite_id)
            continue
        brief = briefs[0]
        if (brief.get("width"), brief.get("height")) != (
            entry["width"],
            entry["height"],
        ):
            append_issue(analyzed_by_id[sprite_id], "brief_dimension_mismatch")
            dimension_mismatches.append(sprite_id)
        if sprite_id in duplicate_ids:
            append_issue(analyzed_by_id[sprite_id], "duplicate_generation_brief")

    return {
        "briefFiles": len(list(brief_root.rglob("*.json"))),
        "uniqueBriefIds": len(briefs_by_id),
        "missingBriefIds": missing,
        "dimensionMismatchIds": dimension_mismatches,
        "duplicateBriefIds": duplicate_ids,
    }


def main() -> None:
    args = parse_args()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    style = json.loads(args.style.read_text(encoding="utf-8"))
    validation = style["validation"]
    style_palette = {
        parse_hex(color)
        for group in style["palette"]
        for color in group["colors"]
    }

    manifest_entries = manifest["sprites"]
    analyzed = []
    analyzed_by_id: dict[str, dict] = {}
    hashes: dict[str, list[dict]] = defaultdict(list)
    for entry in manifest_entries:
        result, digest = analyze_entry(
            args.data_root,
            entry,
            style_palette,
            validation,
        )
        analyzed.append(result)
        analyzed_by_id[entry["id"]] = result
        if digest:
            hashes[digest].append(entry)

    validate_source_aliases(manifest_entries, analyzed_by_id)
    brief_contract = validate_generation_briefs(
        args.data_root,
        manifest_entries,
        analyzed_by_id,
    )
    duplicate_groups, source_alias_groups = split_duplicate_and_alias_groups(
        hashes,
        analyzed_by_id,
    )
    duplicate_hashes_allowed = bool(
        validation.get("duplicateContentHashesAllowed", False)
    )
    if not duplicate_hashes_allowed:
        duplicate_ids = {
            sprite_id
            for group in duplicate_groups
            for sprite_id in group["ids"]
        }
        for sprite_id in duplicate_ids:
            asset = analyzed_by_id[sprite_id]
            append_issue(asset, "duplicate_content")
            asset["readabilityScore"] = max(0, asset["readabilityScore"] - 10)
            update_quality_tier(asset)

    manifest_paths = {
        normalize_manifest_path(entry["path"]) for entry in manifest_entries
    }
    scan_root = args.data_root / args.scan_root
    disk_paths = {
        path.relative_to(args.data_root).as_posix()
        for path in scan_root.rglob("*.png")
    }
    missing_paths = manifest_paths - disk_paths
    unmanifested_paths = disk_paths - manifest_paths

    for asset in analyzed:
        is_production_sample = "production-sample" in asset["tags"]
        asset["hardIssues"] = [
            issue
            for issue in asset["issues"]
            if issue in CONTRACT_HARD_ISSUES
            or (is_production_sample and issue in PRODUCTION_STYLE_HARD_ISSUES)
            or (not duplicate_hashes_allowed and issue == "duplicate_content")
        ]

    issue_counts = Counter(issue for asset in analyzed for issue in asset["issues"])
    hard_issue_count = sum(len(asset["hardIssues"]) for asset in analyzed)
    hard_issue_count += len(unmanifested_paths)
    hard_issue_asset_count = sum(bool(asset["hardIssues"]) for asset in analyzed)
    tier_counts = Counter(asset["qualityTier"] for asset in analyzed)
    report = {
        "schemaVersion": 2,
        "generatorVersion": AUDIT_GENERATOR_VERSION,
        "styleId": style["id"],
        "styleEffectiveDate": style["effectiveDate"],
        "auditTimestampUtc": datetime.now(timezone.utc).isoformat(),
        "validationPolicy": {
            "duplicateContentHashesAllowed": duplicate_hashes_allowed,
            "transparentCornersRequired": bool(
                validation.get("transparentCornersRequired", False)
            ),
            "standaloneRecommendedUniqueColorMaximum": int(
                validation["standaloneRecommendedUniqueColorMaximum"]
            ),
            "smallWorldSpriteRecommendedUniqueColorMaximum": int(
                validation["smallWorldSpriteRecommendedUniqueColorMaximum"]
            ),
            "productionStyleIssuesAreHard": sorted(PRODUCTION_STYLE_HARD_ISSUES),
        },
        "scope": {
            "scanRoot": args.scan_root.as_posix(),
            "manifestEntries": len(manifest_entries),
            "uniqueManifestPaths": len(manifest_paths),
            "pngFilesOnDisk": len(disk_paths),
            "manifestPathsMissingOnDisk": sorted(missing_paths),
            "unmanifestedPngPaths": sorted(unmanifested_paths),
            "generationBriefs": brief_contract,
        },
        "summary": {
            "qualityTiers": dict(sorted(tier_counts.items())),
            "issueCounts": dict(sorted(issue_counts.items())),
            "duplicateGroupCount": len(duplicate_groups),
            "sourceAliasGroupCount": len(source_alias_groups),
            "dimensionMismatchCount": issue_counts["dimension_mismatch"],
            "missingFileCount": issue_counts["missing_file"],
            "hardIssueAssetCount": hard_issue_asset_count,
            "hardIssueCount": hard_issue_count,
        },
        "hardScopeIssues": [
            {"issue": "unmanifested_png", "path": path}
            for path in sorted(unmanifested_paths)
        ],
        "duplicateGroups": duplicate_groups,
        "sourceAliasGroups": source_alias_groups,
        "assets": analyzed,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report["summary"], indent=2))
    if args.fail_on_hard_issues and hard_issue_count:
        raise SystemExit(2)


if __name__ == "__main__":
    main()
