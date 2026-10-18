"""Export matched lighting-prefix reuse observations and exact image diffs.

Each root is one completed nine-scene, three-process diagnostic run. CPU ranges
describe separate processes; they are not pooled percentiles, confidence bounds
or evidence of a statistically significant speedup. Approved references are never
updated. Calibration requires Release, a reported software cap of 0 and an Immediate
presentation interval. These attest requested pacing; external pacing and genuine
GPU time remain unknown.
"""

import argparse
import csv
import hashlib
import json
from pathlib import Path
import sys

from golden_scene_compare import compare
from lighting_diagnostic_report import distribution, number, read_capture, summarize_frames, summarize_prefix_cache, summarize_scopes


SCENES = ("forest_day", "forest_night", "cave_lit", "forest_pan", "forest_zoom",
          "cave_panzoom", "dense_lights", "night_no_bloom", "core_dirty")
ROLES = ("before", "off", "on")
BINARY_FIELDS = ("ClientAssemblyVersion", "ClientAssemblySHA256",
                 "CoreAssemblyVersion", "CoreAssemblySHA256")
SCOPE_NAMES = (("Presentation.LightingPrepare", "prepare"),
               ("Presentation.LightingMaskCpu", "mask"),
               ("Presentation.LightingGpuUpload", "upload_cpu"))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_json(path):
    def reject_constant(value):
        raise ValueError(f"non-finite JSON value in {path}: {value}")

    with path.open(encoding="utf-8-sig") as stream:
        data = json.load(stream, parse_constant=reject_constant)
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected a JSON object")
    return data


def required_string(data, key):
    value = data.get(key)
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"nonempty {key} is required")
    return value


def cache_columns(cache):
    result = {"cache_available": cache["available"]}
    for field, source in (("cache_lookups", "CacheLookups"), ("cache_bypasses", "CacheBypasses"),
                          ("ambient_cache_hits", "AmbientCacheHits"), ("portal_cache_hits", "PortalCacheHits"),
                          ("dependency_samples", "DependencySamples"), ("cache_copy_bytes", "CacheCopyBytes")):
        result[field] = cache["totals"][source] if cache["available"] else None
    for kind in ("ambient", "portal"):
        result[kind + "_misses_among_lookups"] = cache["misses_among_lookups"][kind] if cache["available"] else None
        for denominator in ("prepare", "lookup"):
            result[f"{kind}_hit_ratio_per_{denominator}"] = (
                cache[f"ratios_per_{denominator}"][kind + "_hits"] if cache["available"] else None)
    for statistic in ("min", "mean", "max"):
        result[f"cache_payload_{statistic}_bytes"] = cache["estimated_payload_bytes"][statistic] if cache["available"] else None
    result["cache_copy_bytes_per_measured_frame"] = cache["copy_bytes_per_measured_frame"] if cache["available"] else None
    return result


def process_row(role, entry, capture, report_path, capture_path):
    diagnostics = capture["LightingDiagnostics"]
    frames = summarize_frames(diagnostics["Frames"])
    scopes = summarize_scopes(diagnostics)
    cache = summarize_prefix_cache(diagnostics["Frames"])
    if role == "off":
        if (not cache["available"] or cache["totals"]["CacheLookups"] != 0
                or cache["totals"]["CacheBypasses"] != frames["totals"]["PrepareExecutions"]):
            raise ValueError("OFF captures must attest cache bypasses for every preparation")
    if role == "on" and not cache["available"]:
        raise ValueError("ON captures must include actual prefix-cache counters")
    row = {"role": role, "scene": entry["SceneId"], "repeat": entry["Repeat"],
           "resolution": f"{capture['Width']}x{capture['Height']}",
           "measured_frames": frames["measured_frames"],
           "prepare_executions": frames["totals"]["PrepareExecutions"]}
    for name, label in SCOPE_NAMES:
        scope = scopes[name]
        row[label + "_scope_invocations"] = scope["invocations"]
        for statistic in ("mean", "p95", "p99", "max"):
            row[f"{label}_{statistic}_ms"] = scope["invocation_milliseconds"][statistic]
            row[f"{label}_amortized_{statistic}_ms_per_frame"] = scope["amortized_milliseconds_per_measured_frame"][statistic]
        raw = next(scope for scope in diagnostics["Scopes"] if scope["Name"] == name)
        allocations = distribution([sample["AllocatedBytes"] for sample in raw["Samples"]])
        for statistic in ("mean", "p95", "p99", "max"):
            row[f"{label}_allocated_{statistic}_bytes_per_invocation"] = allocations[statistic]
            row[f"{label}_allocated_{statistic}_bytes_per_frame"] = scope["allocated_bytes_per_measured_frame"][statistic]
        row[label + "_allocated_total_bytes"] = scope["allocated_bytes"]
    timing = capture["FrameTiming"]
    for source, label in (("AverageMilliseconds", "mean"), ("P95Milliseconds", "p95"), ("P99Milliseconds", "p99")):
        row[f"frame_interval_{label}_ms"] = number(timing[source], "FrameTiming." + source, 0)
    for source, label in (("UploadCalls", "actual_texture_upload_calls"), ("UploadBytes", "logical_texture_upload_bytes"),
                          ("TextureRebuilds", "texture_rebuilds")):
        row[label] = frames["totals"][source]
    row["logical_texture_upload_bytes_per_frame"] = frames["upload_bytes_per_measured_frame"]
    row["point_lights_used_max"] = frames["point_lights_used"]["max"]
    row.update(cache_columns(cache))
    row.update({"capture_sha256": digest(capture_path), "screenshot_sha256": digest(capture_path.with_suffix(".png")),
                "report_sha256": digest(report_path)})
    return row


def load_role(role, root):
    summary = read_json(root / "summary.json")
    if summary.get("Failures") != []:
        raise ValueError(f"{role}: diagnostic run has failures or missing failure attestation")
    entries = summary.get("Results")
    if not isinstance(entries, list) or len(entries) != len(SCENES) * 3:
        raise ValueError(f"{role}: expected exactly nine scenes with three repeats each")
    expected = {(scene, repeat) for scene in SCENES for repeat in range(3)}
    loaded = {}
    binary = None
    common = None
    for entry in entries:
        if not isinstance(entry, dict) or type(entry.get("Repeat")) is not int:
            raise ValueError(f"{role}: invalid scene/repeat entry")
        key = (entry.get("SceneId"), entry["Repeat"])
        if key not in expected or key in loaded:
            raise ValueError(f"{role}: duplicate or unexpected scene/repeat {key}")
        report_path = Path(required_string(entry, "Report"))
        if not report_path.is_absolute():
            raise ValueError("Report paths must be absolute")
        report = read_json(report_path)
        if report.get("valid") is not True:
            raise ValueError(f"{role}: invalid diagnostic report {report_path}")
        capture_paths = list(report_path.parent.parent.glob("*/client-smoke-playing.json"))
        if len(capture_paths) != 1:
            raise ValueError("expected exactly one raw capture beside each report directory")
        capture_path = capture_paths[0]
        capture = read_capture(capture_path)
        if digest(capture_path) != report.get("capture_sha256"):
            raise ValueError("raw capture SHA does not match its diagnostic report")
        if digest(capture_path.with_suffix(".png")) != report.get("screenshot_sha256"):
            raise ValueError("PNG SHA does not match its diagnostic report")
        for raw_name, report_name in (("GoldenScene", "golden_scene"), ("CaptureEnvironment", "capture_environment"),
                                      ("BenchmarkContext", "benchmark_context")):
            if capture[raw_name] != report.get(report_name):
                raise ValueError(f"diagnostic report metadata does not match raw {raw_name}")
        benchmark = capture["BenchmarkContext"]
        if role != "before" and benchmark.get("LightingPreparationReuse") != role:
            raise ValueError(f"{role}: BenchmarkContext.LightingPreparationReuse must be '{role}'")
        if benchmark.get("Configuration") != "Release":
            raise ValueError(f"{role}: calibration requires Configuration 'Release'")
        if benchmark.get("SoftwareFrameRateLimit") != "0" or benchmark.get("PresentationInterval") != "Immediate":
            raise ValueError(f"{role}: calibration requires reported requested pacing SoftwareFrameRateLimit '0' and PresentationInterval 'Immediate'")
        current_binary = {field: required_string(benchmark, field) for field in BINARY_FIELDS}
        current_common = {"CPU": required_string(benchmark, "CPU"),
                          "Configuration": required_string(benchmark, "Configuration"),
                          "OSDescription": required_string(benchmark, "OSDescription"),
                          "LogicalProcessors": required_string(benchmark, "LogicalProcessors"),
                          "SoftwareFrameRateLimit": benchmark["SoftwareFrameRateLimit"],
                          "PresentationInterval": benchmark["PresentationInterval"],
                          "CaptureEnvironment": capture["CaptureEnvironment"]}
        if binary is not None and current_binary != binary:
            raise ValueError(f"{role}: loaded binary versions/hashes changed within the role")
        if common is not None and current_common != common:
            raise ValueError(f"{role}: CPU/OS/logical processors/configuration/render environment changed within the role")
        binary, common = current_binary, current_common
        loaded[key] = {"capture": capture, "capture_path": capture_path,
                       "signature": {"GoldenScene": capture["GoldenScene"],
                                     "SourceFixtureSHA256": required_string(entry, "SourceFixtureSHA256")},
                       "row": process_row(role, entry, capture, report_path, capture_path)}
    if set(loaded) != expected:
        raise ValueError(f"{role}: missing scene/repeat cohorts")
    return {"entries": loaded, "binary": binary, "common": common, "root": str(root)}


def numeric_ranges(rows):
    result = {}
    for key, value in rows[0].items():
        if type(value) in (int, float) and key != "repeat":
            result[key] = {"min": min(row[key] for row in rows), "max": max(row[key] for row in rows)}
    return result


def export(before_root, off_root, on_root, output_dir):
    output_dir = Path(output_dir)
    roles = {role: load_role(role, Path(root)) for role, root in
             (("before", before_root), ("off", off_root), ("on", on_root))}
    if any(roles[role]["common"] != roles["before"]["common"] for role in ("off", "on")):
        raise ValueError("all roles must share CPU, OS, logical processor count, configuration and controlled render environment")
    if roles["off"]["binary"] != roles["on"]["binary"]:
        raise ValueError("OFF and ON must use identical loaded binary versions/hashes")
    rows, groups = [], []
    for scene in SCENES:
        signature = roles["before"]["entries"][(scene, 0)]["signature"]
        grouped = {}
        for role in ROLES:
            observations = []
            for repeat in range(3):
                entry = roles[role]["entries"][(scene, repeat)]
                if entry["signature"] != signature:
                    raise ValueError(f"fixture/settings/state/source identity differs for {scene}/{role}/{repeat}")
                observations.append(entry["row"])
            rows.extend(observations)
            grouped[role] = {"processes": 3, "cache_available": observations[0]["cache_available"],
                             "per_process_ranges": numeric_ranges(observations)}
            if any(row["cache_available"] != observations[0]["cache_available"] for row in observations):
                raise ValueError(f"cache schema availability changed within {role}/{scene}")
        overlap = {}
        for first in ("before", "off"):
            overlap[first + "_versus_on"] = {}
            for field in ("prepare_mean_ms", "prepare_p95_ms", "mask_mean_ms", "upload_cpu_mean_ms", "frame_interval_p95_ms"):
                before_range = grouped[first]["per_process_ranges"][field]
                after_range = grouped["on"]["per_process_ranges"][field]
                overlap[first + "_versus_on"][field] = (
                    max(before_range["min"], after_range["min"]) <= min(before_range["max"], after_range["max"]))
        groups.append({"scene": scene, "fixture_contract": signature, "roles": grouped,
                       "observed_process_range_overlap": overlap})
    comparisons = []
    failed = []
    for scene in SCENES:
        on_capture = roles["on"]["entries"][(scene, 0)]["capture_path"]
        for role in ("before", "off"):
            reference = roles[role]["entries"][(scene, 0)]["capture_path"]
            destination = output_dir / "diffs" / scene / (role + "-versus-on")
            result = compare(reference.with_suffix(".png"), on_capture.with_suffix(".png"), destination, mode="exact")
            comparisons.append({"scene": scene, "reference_role": role, "candidate_role": "on",
                                "accepted": result["accepted"],
                                "artifact_directory": str(destination.relative_to(output_dir))})
            if not result["accepted"]:
                failed.append(scene + "/" + role + "-versus-on")
    if failed:
        raise ValueError("exact visual comparison failed; inspect OUT/diffs: " + ", ".join(failed))
    dossier = {"schema_version": 1, "gpu_time": "unknown", "common_environment": roles["before"]["common"],
               "roles": {role: {key: roles[role][key] for key in ("binary", "root")} for role in ROLES},
               "process_observations": len(rows), "groups": groups, "exact_comparisons": comparisons,
               "interpretation": "Three independent process observations per role/scene. Ranges of per-process means/p95/p99 are not pooled percentiles, confidence intervals or statistical significance. Inclusive CPU scopes overlap. CPU upload time includes unknown driver/GPU waits; genuine GPU time is unknown. Software cap 0 and Immediate presentation are reported requested pacing, not proof that external driver/OS pacing or waits are absent. Range overlap is descriptive only and does not establish speedup or equivalence. These frozen-clock workloads do not prove live advancing-time benefit. Cache hits reuse only AO/sky-portal preparation; remaining lighting, temporal processing and uploads still execute. Legacy cache counters remain unavailable. Upload bytes and cache payload are source-derived logical quantities, not measured bus traffic or VRAM. The diagnostic runner independently checks exact repeats; this dossier compares the first process of each role per scene and preserves approved references."}
    output_dir.mkdir(parents=True, exist_ok=True)
    with (output_dir / "per_process.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    (output_dir / "comparison.json").write_text(json.dumps(dossier, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(f"LIGHTING_REUSE_COMPARE_PASS processes={len(rows)} scenes={len(groups)} exact={len(comparisons)} gpu=unknown output={output_dir}")
    return dossier


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before-root", type=Path, required=True)
    parser.add_argument("--off-root", type=Path, required=True)
    parser.add_argument("--on-root", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    options = parser.parse_args(argv)
    try:
        export(**vars(options))
    except (OSError, ValueError, TypeError, KeyError, StopIteration, OverflowError) as error:
        print(f"LIGHTING_REUSE_COMPARE_FAIL {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
