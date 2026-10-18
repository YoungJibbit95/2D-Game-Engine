"""Compare smoke PNGs with fixture identity and explicit renderer compatibility.

The default sidecar is the PNG path with its suffix replaced by .json. Portable
mode applies thresholds to unmodified decoded RGBA channels; it never aligns,
rescales, masks or blurs. Timing diagnostics are copied, not treated as GPU time.
"""

import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

from PIL import Image, ImageChops, ImageStat


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False)


def read_metadata(path):
    def reject_constant(value):
        raise ValueError(f"non-finite JSON value: {value}")

    with path.open(encoding="utf-8-sig") as stream:
        data = json.load(stream, parse_constant=reject_constant)
    if not isinstance(data, dict):
        raise ValueError(f"{path}: metadata must be an object")
    for field in ("GoldenScene", "CaptureEnvironment"):
        if not isinstance(data.get(field), dict) or not data[field]:
            raise ValueError(f"{path}: nonempty {field} object is required")
    return data


def differences(reference, candidate, prefix=""):
    """Keep environment changes inspectable, including added or removed fields."""
    result = []
    for key in sorted(set(reference) | set(candidate)):
        path = f"{prefix}.{key}" if prefix else key
        before, after = reference.get(key), candidate.get(key)
        if key not in reference or key not in candidate:
            result.append({"path": path, "reference": before, "candidate": after,
                           "reference_present": key in reference,
                           "candidate_present": key in candidate})
        elif isinstance(before, dict) and isinstance(after, dict):
            result.extend(differences(before, after, path))
        elif canonical(before) != canonical(after):
            result.append({"path": path, "reference": before, "candidate": after})
    return result


def read_png(path):
    with Image.open(path) as source:
        if source.format != "PNG" or min(source.size) <= 0:
            raise ValueError(f"{path}: a nonempty PNG is required")
        if source.mode not in ("1", "L", "LA", "P", "RGB", "RGBA"):
            raise ValueError(f"{path}: unsupported screenshot mode {source.mode}; use 8-bit PNG channels")
        source.load()
        return source.mode, source.convert("RGBA")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def compare(reference, candidate, output_dir, mode="exact", reference_metadata=None,
            candidate_metadata=None, mean_threshold=2.0, pixel_threshold=0.01,
            channel_threshold=12):
    """Write artifacts even when metadata or pixel acceptance fails."""
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    result = {"schema_version": 1, "mode": mode, "accepted": False, "errors": [],
              "environment_differences": [], "thresholds": {}, "artifacts": {}}
    try:
        if mode not in ("exact", "portable"):
            raise ValueError("mode must be exact or portable")
        if (not math.isfinite(mean_threshold) or mean_threshold < 0
                or not math.isfinite(pixel_threshold) or not 0 <= pixel_threshold <= 1
                or not isinstance(channel_threshold, int) or not 0 <= channel_threshold <= 255):
            raise ValueError("thresholds must be finite; mean >= 0, fraction in [0,1], delta in [0,255]")
        result["thresholds"] = {"mean_absolute_channel_error": mean_threshold,
                                "over_threshold_pixel_fraction": pixel_threshold,
                                "channel_delta": channel_threshold}
        reference, candidate = Path(reference), Path(candidate)
        reference_mode, before = read_png(reference)
        candidate_mode, after = read_png(candidate)
        result.update({"reference_sha256": digest(reference.read_bytes()),
                       "candidate_sha256": digest(candidate.read_bytes()),
                       "reference_pixels_sha256": digest(before.tobytes()),
                       "candidate_pixels_sha256": digest(after.tobytes())})
        if before.size != after.size or reference_mode != candidate_mode:
            raise ValueError(f"image size/mode mismatch: {before.size}/{reference_mode} vs "
                             f"{after.size}/{candidate_mode}")
        delta = ImageChops.difference(before, after)
        channels = delta.split()
        maximum = ImageChops.lighter(ImageChops.lighter(channels[0], channels[1]),
                                     ImageChops.lighter(channels[2], channels[3]))
        histogram = maximum.histogram()
        count = before.width * before.height
        result.update({"width": before.width, "height": before.height,
                       "source_mode": reference_mode, "comparison_mode": "RGBA",
                       "maximum_absolute_channel_error": max(maximum.getextrema()),
                       "mean_absolute_channel_error": sum(ImageStat.Stat(delta).mean) / 4,
                       "differing_pixel_fraction": 1 - histogram[0] / count,
                       "differing_pixel_count": count - histogram[0],
                       "difference_bounds": maximum.getbbox(),
                       "over_threshold_pixel_fraction": sum(histogram[channel_threshold + 1:]) / count})
        # Alpha-only regressions must also be visible. RGB channels show amplified
        # absolute error, with alpha error raised into each channel when larger.
        visible = Image.merge("RGB", tuple(ImageChops.lighter(channel, channels[3])
                                          for channel in channels[:3]))
        visible = visible.point(lambda value: min(255, value * 8))
        visible.save(output_dir / "diff.png")
        preview = Image.new("RGB", (before.width * 3, before.height))
        for index, image in enumerate((before, after, visible)):
            preview.paste(image.convert("RGB"), (index * before.width, 0))
        preview.save(output_dir / "preview.png")
        result["artifacts"] = {"diff": "diff.png", "preview": "preview.png",
                               "preview_order": ["reference", "candidate", "diff"],
                               "diff_amplification": 8,
                               "diff_alpha": "maximum alpha error raised into each RGB channel"}
        first = read_metadata(Path(reference_metadata) if reference_metadata else reference.with_suffix(".json"))
        second = read_metadata(Path(candidate_metadata) if candidate_metadata else candidate.with_suffix(".json"))
        result["environment_differences"] = differences(first["CaptureEnvironment"], second["CaptureEnvironment"])
        result["fixture_differences"] = differences(first["GoldenScene"], second["GoldenScene"])
        result["golden_scene"] = first["GoldenScene"]
        result["reference_environment"] = first["CaptureEnvironment"]
        result["candidate_environment"] = second["CaptureEnvironment"]
        for label, metadata in (("reference", first), ("candidate", second)):
            result[f"{label}_diagnostics"] = {key: metadata[key] for key in
                ("FrameTiming", "PerformanceMetrics", "GraphicsAdapterDescription", "BenchmarkContext") if key in metadata}
        if result["fixture_differences"]:
            result["errors"].append("GoldenScene fixture mismatch; pixels are not comparable")
        if mode == "exact" and result["environment_differences"]:
            result["errors"].append("CaptureEnvironment mismatch in exact mode; use a matching controlled renderer")
        if mode == "exact" and histogram[0] != count:
            result["errors"].append("decoded pixels differ in exact mode")
        if mode == "portable" and (result["mean_absolute_channel_error"] > mean_threshold
                or result["over_threshold_pixel_fraction"] > pixel_threshold):
            result["errors"].append("portable pixel thresholds exceeded")
        result["accepted"] = not result["errors"]
    except (OSError, ValueError, TypeError, Image.DecompressionBombError) as error:
        result["errors"].append(str(error))
    (output_dir / "comparison.json").write_text(json.dumps(result, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference", required=True, type=Path)
    parser.add_argument("--candidate", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--mode", choices=("exact", "portable"), default="exact")
    parser.add_argument("--reference-metadata", type=Path)
    parser.add_argument("--candidate-metadata", type=Path)
    parser.add_argument("--mean-threshold", type=float, default=2.0,
                        help="portable maximum mean absolute RGBA channel error (default: 2.0)")
    parser.add_argument("--pixel-threshold", type=float, default=0.01,
                        help="portable maximum fraction of pixels above channel delta (default: 0.01)")
    parser.add_argument("--channel-threshold", type=int, default=12,
                        help="channel delta strictly above which a portable pixel counts (default: 12)")
    result = compare(**vars(parser.parse_args(argv)))
    print(json.dumps(result, indent=2))
    return 0 if result["accepted"] else 1


if __name__ == "__main__":
    sys.exit(main())
