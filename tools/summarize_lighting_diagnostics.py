"""Export reviewable references from completed runs of run_lighting_diagnostics.ps1.

Raw captures stay in the existing smoke artifacts. CSV values are per-process
observations; JSON ranges describe repeated runs, never pooled GPU timings.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw

from lighting_diagnostic_report import distribution, read_capture


def export(roots, output):
    rows, captures, signatures, previews, context = [], {}, {}, {}, None
    for root in roots:
        summary = json.loads((root / "summary.json").read_text(encoding="utf-8-sig"))
        if summary["Failures"]:
            raise ValueError(f"incomplete run: {root}")
        for entry in summary["Results"]:
            report_path = Path(entry["Report"])
            report = json.loads(report_path.read_text(encoding="utf-8-sig"))
            if not report["valid"]:
                raise ValueError(f"invalid report: {report_path}")
            capture_paths = list(report_path.parent.parent.glob("*/client-smoke-playing.json"))
            if len(capture_paths) != 1:
                raise ValueError("expected exactly one fresh capture per report")
            capture_path = capture_paths[0]
            capture = read_capture(capture_path)
            if hashlib.sha256(capture_path.read_bytes()).hexdigest() != report["capture_sha256"]:
                raise ValueError("capture changed after report generation")
            if hashlib.sha256(capture_path.with_suffix('.png').read_bytes()).hexdigest() != report["screenshot_sha256"]:
                raise ValueError("screenshot changed after report generation")
            benchmark = report["benchmark_context"]
            identity = {key: benchmark[key] for key in
                        ("CPU", "Configuration", "ClientAssemblyVersion", "ClientAssemblySHA256", "CoreAssemblySHA256")}
            identity["CaptureEnvironment"] = report["capture_environment"]
            if context is not None and identity != context:
                raise ValueError("cannot combine different binaries, configurations or render environments")
            context = identity
            size = f"{report['image']['width']}x{report['image']['height']}"
            signature = {"GoldenScene": capture["GoldenScene"], "SourceFixtureSHA256": entry["SourceFixtureSHA256"]}
            key = (size, entry["SceneId"])
            if key in signatures and signatures[key] != signature:
                raise ValueError("scene repeats have different fixture/settings/state contracts")
            signatures[key] = signature
            row = {"resolution": size, "scene": entry["SceneId"], "repeat": entry["Repeat"],
                   "frames": report["frames"]["measured_frames"]}
            for name, short in (("Presentation.LightingPrepare", "prepare"),
                                ("Presentation.LightingMaskCpu", "mask"),
                                ("Presentation.LightingGpuUpload", "upload_cpu")):
                scope = report["scopes"][name]
                row[short + "_scope_invocations"] = scope["invocations"]
                for statistic in ("mean", "p95", "p99"):
                    row[short + "_" + statistic + "_ms"] = scope["invocation_milliseconds"][statistic]
                row[short + "_amortized_ms"] = scope["amortized_milliseconds_per_measured_frame"]["mean"]
            timing = report["raw_frame_interval_summary"]
            for field in ("AverageMilliseconds", "P95Milliseconds", "P99Milliseconds"):
                row["frame_" + field] = timing[field]
            row["logical_upload_bytes_per_frame"] = report["frames"]["upload_bytes_per_measured_frame"]
            row["texture_rebuilds"] = report["frames"]["totals"]["TextureRebuilds"]
            row["lights_used_max"] = report["frames"]["point_lights_used"]["max"]
            if len(report["frames"]["mask_sizes"]) != 1:
                raise ValueError("reference export expects one mask size per process")
            row["mask_width"], row["mask_height"] = report["frames"]["mask_sizes"][0]
            row["estimated_texture_payload_bytes"] = report["frames"]["estimated_texture_payload_bytes"]["max"]
            row["logical_upload_calls"] = report["frames"]["totals"]["UploadCalls"]
            phase = next(p for p in report["core_phase_telemetry"]["Measurements"] if p["Phase"] == 14)
            row["core_lighting_mean_ms"] = phase["AverageMilliseconds"]
            row["core_lighting_samples"] = phase["Samples"]
            row["any_channel_clipped_fraction"] = report["image"]["any_channel_clipped_fraction"]
            row["all_channels_clipped_fraction"] = report["image"]["all_channels_clipped_fraction"]
            row["capture_sha256"] = report["capture_sha256"]
            row["screenshot_sha256"] = report["screenshot_sha256"]
            row["report_sha256"] = hashlib.sha256(report_path.read_bytes()).hexdigest()
            rows.append(row)
            if entry["Repeat"] == 0:
                captures[(size, entry["SceneId"])] = capture
                previews[(size, entry["SceneId"])] = (capture_path.with_suffix('.png'), report_path.parent / 'clipping.png')
    if not rows:
        raise ValueError("no completed measurements")
    groups = []
    for size, scene in sorted({(row["resolution"], row["scene"]) for row in rows}):
        samples = [row for row in rows if (row["resolution"], row["scene"]) == (size, scene)]
        if {row["repeat"] for row in samples} != {0, 1, 2} or len(samples) != 3:
            raise ValueError("reference export requires exactly three fresh processes per scene/resolution")
        groups.append({"resolution": size, "scene": scene, "repeats": 3,
                       "fixture_contract": signatures[(size, scene)],
                       "per_process_ranges": {field: {"min": min(row[field] for row in samples),
                                                       "max": max(row[field] for row in samples)}
                                              for field in samples[0] if field not in
                                              ("resolution", "scene", "repeat", "capture_sha256", "screenshot_sha256", "report_sha256")}})
    probes = []
    for size in sorted({key[0] for key in captures}):
        for reference, candidate in (("forest_night", "forest_pan"), ("forest_night", "forest_zoom"), ("cave_lit", "cave_panzoom")):
            first, second = captures[(size, reference)], captures[(size, candidate)]
            comparable = all(first["GoldenScene"][key] == second["GoldenScene"][key]
                             for key in ("ContentFingerprint", "SettingsFingerprint"))
            def camera_independent_fixture(data):
                fixture = dict(data["GoldenScene"]["Fixture"])
                fixture.pop("SceneId")
                fixture["Diagnostic"] = {key: value for key, value in fixture["Diagnostic"].items() if key != "CameraPath"}
                return fixture
            comparable &= camera_independent_fixture(first) == camera_independent_fixture(second)
            pairs = list(zip(first["LightingDiagnostics"]["Frames"], second["LightingDiagnostics"]["Frames"], strict=True))
            comparable &= all(a["Frame"] == b["Frame"] and a["SimulationTick"] == b["SimulationTick"] for a, b in pairs)
            available = [(a, b) for a, b in pairs if a["ProbeAvailable"] and b["ProbeAvailable"]]
            probes.append({"resolution": size, "reference": reference, "candidate": candidate,
                           "matched_configured_inputs_and_ticks": comparable,
                           "authoritative_state_hash_equal": first["GoldenScene"]["StateHash"] == second["GoldenScene"]["StateHash"],
                           "state_hash_limitation": "Different scene IDs name different worlds; world names participate in the state hash. Full authoritative-state equivalence is not asserted by this configured-input comparison.",
                           "available_pairs": len(available),
                           "absolute_probe_difference": {field: distribution([abs(a[field] - b[field]) for a, b in available])
                                                         for field in ("ProbeShadow", "ProbeRed", "ProbeGreen", "ProbeBlue", "ProbeBloom")}
                           if comparable and available else None})
    data = {"version": 1, "context": context, "gpu_time": "unknown", "groups": groups,
            "matched_world_probe_observations": probes,
            "interpretation": "Per-process CPU ranges; inclusive scopes overlap. Upload bytes are logical RGBA8 payload, not measured bus traffic. Probe differences compare nearest mask samples at one fixed world position and include grid quantization, visibility and temporal history; they are not proof of flicker or a spatial image-quality gate."}
    output.mkdir(parents=True, exist_ok=True)
    with (output / "native_runs.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    (output / "native_summary.json").write_text(json.dumps(data, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    # Review aid only: quantification uses original pixels, never these thumbnails.
    scenes = ("forest_night", "night_no_bloom", "dense_lights", "cave_lit")
    if all(("1920x1080", scene) in previews for scene in scenes):
        canvas = Image.new("RGB", (1280, 4 * 386), "#161b22")
        draw = ImageDraw.Draw(canvas)
        for index, scene in enumerate(scenes):
            row = next(row for row in rows if row["resolution"] == "1920x1080" and row["scene"] == scene and row["repeat"] == 0)
            label = f"{scene}: any RGB >=250 {100 * row['any_channel_clipped_fraction']:.4f}% / all RGB {100 * row['all_channels_clipped_fraction']:.4f}% of ROI"
            draw.text((8, index * 386 + 6), label, fill="white")
            for column, path in enumerate(previews[("1920x1080", scene)]):
                with Image.open(path) as image:
                    canvas.paste(image.convert("RGB").resize((640, 360), Image.Resampling.NEAREST), (column * 640, index * 386 + 26))
        canvas.save(output / "quality_review.png")
    print(f"LIGHTING_REFERENCE_EXPORT rows={len(rows)} groups={len(groups)} output={output}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-root", type=Path, action="append", required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    options = parser.parse_args()
    export(options.run_root, options.output_dir)
