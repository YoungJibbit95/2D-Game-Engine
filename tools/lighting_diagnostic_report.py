"""Summarize raw lighting diagnostics without changing captures or baselines.

Scope timings are CPU wall times, including driver work when SetData is called.
No CPU measurement is relabelled as GPU time. Image statistics use the full-width
middle half of the screenshot, avoiding the outer HUD bands. Display-encoded
RGB values are diagnostic signals, not linear radiance or a visual quality gate.
"""

import argparse
import hashlib
import json
import math
from pathlib import Path
import statistics
import sys

from PIL import Image


COUNTERS = ("PrepareExecutions", "UploadCalls", "UploadBytes", "TextureRebuilds",
            "MaskWidth", "MaskHeight", "RaysCast", "OccluderSamples",
            "PointLightsUsed", "ReprojectedPixels", "RejectedPixels")
PROBES = ("ProbeShadow", "ProbeRed", "ProbeGreen", "ProbeBlue", "ProbeBloom")
FRAME_SCOPES = ("Simulation.FixedUpdate", "Frame.Update", "Frame.Draw")
PREPARE_SCOPES = ("Presentation.LightingPrepare", "Presentation.LightingMaskCpu",
                  "Presentation.LightingGpuUpload")
CACHE_COUNTERS = ("AmbientCacheHits", "PortalCacheHits", "CacheLookups", "CacheBypasses",
                  "DependencySamples", "CacheCopyBytes", "CachePayloadBytes")


def distribution(values):
    """Arithmetic mean and nearest-rank percentiles; never an EMA."""
    ordered = sorted(values)
    if not ordered:
        raise ValueError("cannot summarize an empty trace")

    def percentile(fraction):
        return ordered[max(0, math.ceil(len(ordered) * fraction) - 1)]

    return {"count": len(ordered), "mean": statistics.fmean(ordered),
            "p50": percentile(0.50), "p95": percentile(0.95),
            "p99": percentile(0.99), "max": ordered[-1], "min": ordered[0]}


def integer(value, name, minimum=0):
    if type(value) is not int or value < minimum:
        raise ValueError(f"{name} must be an integer >= {minimum}")
    return value


def number(value, name, minimum=None):
    if (type(value) not in (int, float) or not math.isfinite(value)
            or (minimum is not None and value < minimum)):
        raise ValueError(f"{name} must be finite" +
                         (f" and >= {minimum}" if minimum is not None else ""))
    return value


def validate_finite(value, path="capture"):
    if isinstance(value, float) and not math.isfinite(value):
        raise ValueError(f"{path} contains a non-finite number")
    if isinstance(value, dict):
        for key, child in value.items():
            validate_finite(child, f"{path}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            validate_finite(child, f"{path}[{index}]")


def object_required(value, name):
    if not isinstance(value, dict) or not value:
        raise ValueError(f"nonempty {name} object is required")
    return value


def validate_cache_frames(frames):
    """Version-1 captures predate reuse counters; absence never implies zero hits."""
    if not any(field in frame for frame in frames for field in CACHE_COUNTERS):
        return False
    for frame in frames:
        if not all(field in frame for field in CACHE_COUNTERS):
            raise ValueError("all seven prefix-cache counters are required on every measured frame")
        for field in CACHE_COUNTERS:
            integer(frame[field], field)
        for field in CACHE_COUNTERS[:4]:
            if frame[field] > 1:
                raise ValueError(f"{field} must be zero or one per measured frame")
        ambient, portal = frame["AmbientCacheHits"], frame["PortalCacheHits"]
        lookups, bypasses = frame["CacheLookups"], frame["CacheBypasses"]
        if portal > ambient or ambient > lookups:
            raise ValueError("PortalCacheHits <= AmbientCacheHits <= CacheLookups is required")
        if lookups + bypasses != frame["PrepareExecutions"]:
            raise ValueError("CacheLookups + CacheBypasses must equal PrepareExecutions")
        if lookups:
            if frame["DependencySamples"] != frame["MaskWidth"] * frame["MaskHeight"]:
                raise ValueError("a prefix-cache lookup must freshly validate every mask dependency sample")
        elif frame["DependencySamples"] or frame["CacheCopyBytes"]:
            raise ValueError("bypassed or unscheduled preparation must have zero cache dependency/copy work")
        if ambient and not frame["CachePayloadBytes"]:
            raise ValueError("a prefix-cache hit requires a positive CachePayloadBytes estimate")
    return True


def read_capture(path):
    def reject_constant(value):
        raise ValueError(f"non-finite JSON value: {value}")

    with path.open(encoding="utf-8-sig") as stream:
        data = json.load(stream, parse_constant=reject_constant)
    object_required(data, "capture")
    validate_finite(data)
    for field in ("GoldenScene", "CaptureEnvironment", "BenchmarkContext", "FrameTiming"):
        object_required(data.get(field), field)
    if data.get("Passed") is not True:
        raise ValueError("capture Passed must be true")
    fixture = object_required(data["GoldenScene"].get("Fixture"), "GoldenScene.Fixture")
    final_frame = integer(fixture.get("Frames"), "Fixture.Frames", 1)
    warmup = integer(fixture.get("WarmupFrames"), "Fixture.WarmupFrames", 1)
    if final_frame <= warmup:
        raise ValueError("Fixture.Frames must exceed Fixture.WarmupFrames")
    measured_count = final_frame - warmup
    if integer(data.get("CapturedFrame"), "CapturedFrame", 1) != final_frame:
        raise ValueError("CapturedFrame must equal Fixture.Frames")
    if integer(data["FrameTiming"].get("SampleCount"), "FrameTiming.SampleCount", 1) != measured_count:
        raise ValueError("FrameTiming.SampleCount must equal the fixture measurement window")
    diagnostics = object_required(data.get("LightingDiagnostics"), "LightingDiagnostics")
    if type(diagnostics.get("Version")) is not int or diagnostics["Version"] != 1:
        raise ValueError("LightingDiagnostics.Version must be 1")
    if diagnostics.get("GpuTiming") != "unknown":
        raise ValueError("LightingDiagnostics.GpuTiming must explicitly be 'unknown'")
    frames = diagnostics.get("Frames")
    scopes = diagnostics.get("Scopes")
    if not isinstance(frames, list) or not frames:
        raise ValueError("LightingDiagnostics.Frames must be a nonempty trace")
    if not isinstance(scopes, list) or not scopes:
        raise ValueError("LightingDiagnostics.Scopes must be a nonempty trace")
    if len(frames) != measured_count:
        raise ValueError("measured frame count must equal Fixture.Frames - Fixture.WarmupFrames")
    for index, frame in enumerate(frames):
        object_required(frame, f"Frames[{index}]")
        frame_id = integer(frame.get("Frame"), "Frame")
        tick = integer(frame.get("SimulationTick"), "SimulationTick")
        if frame_id != warmup + index + 1:
            raise ValueError("measured frame IDs must cover warmup + 1 through Fixture.Frames")
        if tick != frame_id:
            raise ValueError("SimulationTick must equal Frame in the fixed one-tick-per-frame workload")
        for field in ("CameraX", "CameraY"):
            number(frame.get(field), field)
        if number(frame.get("Zoom"), "Zoom", 0) <= 0:
            raise ValueError("Zoom must be positive")
        for field in COUNTERS:
            integer(frame.get(field), field)
        for field in PROBES:
            number(frame.get(field), field, 0)
        if "ProbeAvailable" in frame and type(frame["ProbeAvailable"]) is not bool:
            raise ValueError("ProbeAvailable must be a boolean")
        if "EstimatedTextureBytes" in frame:
            integer(frame["EstimatedTextureBytes"], "EstimatedTextureBytes")
        if type(frame.get("HistoryReset")) is not bool:
            raise ValueError("HistoryReset must be a boolean")
        if frame["PrepareExecutions"] not in (0, 1):
            raise ValueError("PrepareExecutions must be zero or one per measured frame")
        if frame["PrepareExecutions"] and (not frame["MaskWidth"] or not frame["MaskHeight"]):
            raise ValueError("executed lighting preparation requires nonempty mask dimensions")
        if frame["UploadCalls"] and not frame["PrepareExecutions"]:
            raise ValueError("UploadCalls requires lighting preparation in the same frame")
        if frame["UploadBytes"] != frame["UploadCalls"] * frame["MaskWidth"] * frame["MaskHeight"] * 4:
            raise ValueError("UploadBytes must equal UploadCalls * mask width * mask height * 4 (RGBA8)")
    validate_cache_frames(frames)
    ordered_frame_ids = [frame["Frame"] for frame in frames]
    frame_ids = set(ordered_frame_ids)
    prepare_frame_ids = [frame["Frame"] for frame in frames if frame["PrepareExecutions"]]
    scope_frame_ids = {}
    for index, scope in enumerate(scopes):
        object_required(scope, f"Scopes[{index}]")
        name = scope.get("Name")
        if not isinstance(name, str) or not name.strip() or name in scope_frame_ids:
            raise ValueError("scope names must be nonempty and unique")
        scope_frame_ids[name] = []
        samples = scope.get("Samples")
        if not isinstance(samples, list) or not samples:
            raise ValueError(f"scope {name} must contain samples")
        previous = None
        for sample in samples:
            object_required(sample, f"{name} sample")
            frame_id = integer(sample.get("Frame"), "scope Frame")
            if frame_id not in frame_ids or (previous is not None and frame_id <= previous):
                raise ValueError(f"scope {name} frame IDs must be unique and ordered within the measured frame range")
            previous = frame_id
            scope_frame_ids[name].append(frame_id)
            number(sample.get("Milliseconds"), "Milliseconds", 0)
            integer(sample.get("AllocatedBytes"), "AllocatedBytes")
    for name in FRAME_SCOPES:
        if scope_frame_ids.get(name) != ordered_frame_ids:
            raise ValueError(f"scope {name} must contain exactly one sample per measured frame")
    for name in PREPARE_SCOPES:
        if name not in scope_frame_ids or scope_frame_ids[name] != prepare_frame_ids:
            raise ValueError(f"scope {name} samples must match the frames with PrepareExecutions = 1")
    return data


def summarize_scopes(diagnostics):
    frame_ids = [frame["Frame"] for frame in diagnostics["Frames"]]
    result = {}
    for scope in diagnostics["Scopes"]:
        per_frame = dict.fromkeys(frame_ids, 0.0)
        allocation_per_frame = dict.fromkeys(frame_ids, 0)
        for sample in scope["Samples"]:
            per_frame[sample["Frame"]] += sample["Milliseconds"]
            allocation_per_frame[sample["Frame"]] += sample["AllocatedBytes"]
        result[scope["Name"]] = {
            "clock": "CPU wall time; driver submission/stalls may be included; GPU execution unknown",
            "invocations": len(scope["Samples"]),
            "covered_frames": len({sample["Frame"] for sample in scope["Samples"]}),
            "invocations_per_measured_frame": len(scope["Samples"]) / len(frame_ids),
            "invocation_milliseconds": distribution([sample["Milliseconds"] for sample in scope["Samples"]]),
            "amortized_milliseconds_per_measured_frame": distribution(list(per_frame.values())),
            "allocated_bytes": sum(allocation_per_frame.values()),
            "allocated_bytes_per_measured_frame": distribution(list(allocation_per_frame.values()))}
    return result


def summarize_frames(frames):
    totals = {field: sum(frame[field] for frame in frames) for field in
              ("PrepareExecutions", "UploadCalls", "UploadBytes", "TextureRebuilds",
               "RaysCast", "OccluderSamples", "ReprojectedPixels", "RejectedPixels")}
    texture_bytes = [frame["EstimatedTextureBytes"] for frame in frames if "EstimatedTextureBytes" in frame]
    return {"measured_frames": len(frames), "first_frame": frames[0]["Frame"],
            "last_frame": frames[-1]["Frame"], "totals": totals,
            "prepares_per_measured_frame": totals["PrepareExecutions"] / len(frames),
            "upload_bytes_per_measured_frame": totals["UploadBytes"] / len(frames),
            "history_reset_frames": sum(frame["HistoryReset"] for frame in frames),
            "mask_sizes": [list(size) for size in sorted({(frame["MaskWidth"], frame["MaskHeight"])
                                                         for frame in frames})],
            "point_lights_used": distribution([frame["PointLightsUsed"] for frame in frames]),
            "estimated_texture_payload_bytes": distribution(texture_bytes) if texture_bytes else None,
            "texture_payload_interpretation": "Source-derived RGBA8 texture payload, excluding driver overhead; not measured VRAM.",
            "camera_change_steps": sum((first["CameraX"], first["CameraY"], first["Zoom"]) !=
                                       (second["CameraX"], second["CameraY"], second["Zoom"])
                                       for first, second in zip(frames, frames[1:]))}


def summarize_prefix_cache(frames):
    if not validate_cache_frames(frames):
        return {"available": False,
                "interpretation": "Legacy capture omits all prefix-cache counters. Cache hits, misses and payload are unavailable; absence is not evidence of zero reuse."}
    totals = {field: sum(frame[field] for frame in frames) for field in CACHE_COUNTERS[:-1]}
    prepares = sum(frame["PrepareExecutions"] for frame in frames)
    lookups = totals["CacheLookups"]

    def ratio(numerator, denominator):
        return numerator / denominator if denominator else None

    return {"available": True, "measured_prepares": prepares, "totals": totals,
            "misses_among_lookups": {"ambient": lookups - totals["AmbientCacheHits"],
                                     "portal": lookups - totals["PortalCacheHits"]},
            "ratios_per_prepare": {"lookups": ratio(lookups, prepares),
                                   "bypasses": ratio(totals["CacheBypasses"], prepares),
                                   "ambient_hits": ratio(totals["AmbientCacheHits"], prepares),
                                   "portal_hits": ratio(totals["PortalCacheHits"], prepares)},
            "ratios_per_lookup": {"ambient_hits": ratio(totals["AmbientCacheHits"], lookups),
                                  "portal_hits": ratio(totals["PortalCacheHits"], lookups),
                                  "ambient_misses": ratio(lookups - totals["AmbientCacheHits"], lookups),
                                  "portal_misses": ratio(lookups - totals["PortalCacheHits"], lookups)},
            "copy_bytes_per_measured_frame": totals["CacheCopyBytes"] / len(frames),
            "copy_bytes_per_prepare": ratio(totals["CacheCopyBytes"], prepares),
            "dependency_samples_per_lookup": ratio(totals["DependencySamples"], lookups),
            "estimated_payload_bytes": distribution([frame["CachePayloadBytes"] for frame in frames]),
            "interpretation": "Hits reuse only the raw ambient-occlusion/sky-portal prefix. Dependency samples are freshly validated and full lighting preparation continues; hits do not mean skipped frames, skipped temporal settling or GPU savings. Payload is a persistent source-derived CPU estimate, not bytes allocated each frame or measured VRAM. Undefined ratios are null."}


def summarize_probes(frames):
    result = {"interpretation": "Unclassified raw probe variation; camera, sampling, light motion and temporal history may all contribute. Not proof of flicker or a causal quality diagnosis.",
              "available_frames": sum(frame.get("ProbeAvailable", True) for frame in frames),
              "unavailable_frames": sum(not frame.get("ProbeAvailable", True) for frame in frames),
              "signals": {}}
    for field in PROBES:
        values = [frame[field] for frame in frames if frame.get("ProbeAvailable", True)]
        steps = [abs(second[field] - first[field]) for first, second in zip(frames, frames[1:])
                 if first.get("ProbeAvailable", True) and second.get("ProbeAvailable", True)]
        result["signals"][field] = {"values": distribution(values) if values else None,
                                    "range": max(values) - min(values) if values else None,
                                    "consecutive_available_steps": len(steps),
                                    "absolute_step": distribution(steps) if steps else None}
    return result


def analyze_image(path, output_dir, metadata):
    with Image.open(path) as source:
        if source.format != "PNG" or source.mode not in ("1", "L", "LA", "P", "RGB", "RGBA"):
            raise ValueError("capture screenshot must be a PNG with supported 8-bit channels")
        source.load()
        image = source.convert("RGB")
    if image.width < 1 or image.height < 2:
        raise ValueError("capture PNG must have a nonempty middle-half ROI")
    for field, actual in (("Width", image.width), ("Height", image.height)):
        if field in metadata and integer(metadata[field], field, 1) != actual:
            raise ValueError(f"capture {field} disagrees with PNG dimensions")
    top, bottom = image.height // 4, math.ceil(image.height * 3 / 4)
    roi = image.crop((0, top, image.width, bottom))
    pixels = list(getattr(roi, "get_flattened_data", roi.getdata)())
    any_clipped = [max(pixel) >= 250 for pixel in pixels]
    all_clipped = [min(pixel) >= 250 for pixel in pixels]
    luma = [0.2126 * red + 0.7152 * green + 0.0722 * blue for red, green, blue in pixels]
    heatmap = Image.new("RGB", image.size, (0, 0, 0))
    marks = Image.new("RGB", roi.size)
    marks.putdata([(255, 255, 255) if all_channels else (255, 128, 0) if any_channel else (0, 0, 0)
                   for any_channel, all_channels in zip(any_clipped, all_clipped)])
    heatmap.paste(marks, (0, top))
    heatmap.save(output_dir / "clipping.png")
    return {"width": image.width, "height": image.height,
            "roi": {"bounds_xyxy": [0, top, image.width, bottom],
                    "policy": "Full width; middle 50% height rounded outward. Outer HUD bands excluded; in-world overlays inside ROI may remain.",
                    "pixels": len(pixels)},
            "clipping_threshold_inclusive": 250,
            "any_channel_clipped_fraction": sum(any_clipped) / len(pixels),
            "all_channels_clipped_fraction": sum(all_clipped) / len(pixels),
            "display_encoded_rec709_luma_0_to_255": distribution(luma),
            "interpretation": "Display-encoded RGB approximation, not linear radiance. Alpha is excluded. Clipped-looking pixels can be authored art or emissive effects; these fractions are not an acceptance gate.",
            "clipping_heatmap": {"path": "clipping.png", "legend": {"white": "all RGB >=250",
                                  "orange": "any RGB >=250", "black": "other or outside ROI"}}}


def report(capture, output_dir):
    capture, output_dir = Path(capture), Path(output_dir)
    screenshot = capture.with_suffix(".png")
    for artifact in (output_dir / "report.json", output_dir / "clipping.png"):
        for source in (capture, screenshot):
            if artifact.resolve() == source.resolve() or (artifact.exists() and source.exists() and artifact.samefile(source)):
                raise ValueError("output artifacts must not overwrite the capture or screenshot")
    output_dir.mkdir(parents=True, exist_ok=True)
    result = {"schema_version": 1, "valid": False, "errors": [], "gpu_timing": "unknown",
              "percentile_method": "nearest rank", "scope_aggregation": "Arithmetic mean of raw invocations; per-frame sums include zero for unscheduled frames. Nested scopes overlap and must not be added together."}
    try:
        metadata = read_capture(capture)
        diagnostics = metadata["LightingDiagnostics"]
        result.update({"capture_sha256": hashlib.sha256(capture.read_bytes()).hexdigest(),
                       "screenshot_sha256": hashlib.sha256(screenshot.read_bytes()).hexdigest(),
                       "golden_scene": metadata["GoldenScene"],
                       "capture_environment": metadata["CaptureEnvironment"],
                       "benchmark_context": metadata["BenchmarkContext"],
                       "core_phase_telemetry": metadata.get("CorePhaseTelemetry"),
                       "raw_frame_interval_summary": metadata["FrameTiming"],
                       "frames": summarize_frames(diagnostics["Frames"]),
                       "prefix_cache": summarize_prefix_cache(diagnostics["Frames"]),
                       "scopes": summarize_scopes(diagnostics),
                       "probes": summarize_probes(diagnostics["Frames"]),
                       "image": analyze_image(screenshot, output_dir, metadata), "valid": True})
    except (OSError, ValueError, TypeError, OverflowError, Image.DecompressionBombError) as error:
        result["errors"].append(str(error))
    (output_dir / "report.json").write_text(json.dumps(result, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    options = parser.parse_args(argv)
    try:
        result = report(options.capture, options.output_dir)
    except (OSError, ValueError) as error:
        print(f"LIGHTING_REPORT_FAIL {error}", file=sys.stderr)
        return 1
    if not result["valid"]:
        print("LIGHTING_REPORT_FAIL " + "; ".join(result["errors"]), file=sys.stderr)
        return 1
    frames = result["frames"]
    print(f"LIGHTING_REPORT_PASS frames={frames['measured_frames']} "
          f"prepares={frames['totals']['PrepareExecutions']} "
          f"upload_bytes={frames['totals']['UploadBytes']} gpu=unknown "
          f"report={options.output_dir / 'report.json'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
