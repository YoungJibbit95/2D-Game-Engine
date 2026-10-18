import contextlib
import copy
import csv
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from lighting_diagnostic_report import CACHE_COUNTERS, COUNTERS, FRAME_SCOPES, PREPARE_SCOPES, PROBES
from lighting_reuse_compare import ROLES, SCENES, export, main


class LightingReuseCompareTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.output = self.root / "dossier"
        self.roots = {role: self.root / role for role in ROLES}
        self.paths = {}
        for role in ROLES:
            entries = []
            for scene in SCENES:
                for repeat in range(3):
                    sample = self.roots[role] / scene / str(repeat)
                    capture_path = sample / "capture" / "client-smoke-playing.json"
                    capture_path.parent.mkdir(parents=True)
                    image = capture_path.with_suffix(".png")
                    Image.new("RGB", (8, 8), (40, 60, 80)).save(image)
                    report_path = sample / "report" / "report.json"
                    report_path.parent.mkdir()
                    self.paths[(role, scene, repeat)] = capture_path, report_path
                    self.write_capture(role, scene, repeat, self.capture(role, scene, repeat))
                    entries.append({"SceneId": scene, "Repeat": repeat, "Report": str(report_path),
                                    "SourceFixtureSHA256": hashlib.sha256(scene.encode()).hexdigest()})
            (self.roots[role] / "summary.json").write_text(json.dumps({"Version": 1, "Failures": [], "Results": entries}), encoding="utf-8")

    def capture(self, role, scene, repeat):
        frames = []
        for index in range(4):
            frame = {field: 0 for field in COUNTERS}
            frame.update({field: 0.1 for field in PROBES})
            frame.update({"Frame": 181 + index, "SimulationTick": 181 + index,
                          "CameraX": -1000, "CameraY": 500, "Zoom": 2, "HistoryReset": False,
                          "MaskWidth": 10, "MaskHeight": 5, "PointLightsUsed": 2})
            if index in (0, 2):
                frame.update({"PrepareExecutions": 1, "UploadCalls": 2, "UploadBytes": 400})
            if role != "before":
                frame.update({field: 0 for field in CACHE_COUNTERS})
                if role == "off":
                    frame["CacheBypasses"] = frame["PrepareExecutions"]
                else:
                    frame["CachePayloadBytes"] = 600
                    if index in (0, 2):
                        frame.update({"CacheLookups": 1, "DependencySamples": 50, "CacheCopyBytes": 400})
                    if index == 2:
                        frame.update({"AmbientCacheHits": 1, "PortalCacheHits": 1})
            frames.append(frame)
        durations = [2, 10] if role != "on" else [1, 5 + repeat * 4]
        scopes = [{"Name": name, "Samples": [{"Frame": frame, "Milliseconds": duration, "AllocatedBytes": 0}
                    for frame, duration in zip((181, 183), durations)]} for name in PREPARE_SCOPES]
        scopes.extend({"Name": name, "Samples": [{"Frame": frame, "Milliseconds": 1, "AllocatedBytes": 0}
                       for frame in range(181, 185)]} for name in FRAME_SCOPES)
        binary = "old" if role == "before" else "candidate"
        benchmark = {"CPU": "controlled-cpu", "Configuration": "Release",
                     "OSDescription": "controlled-os-build", "LogicalProcessors": "12",
                     "SoftwareFrameRateLimit": "0", "PresentationInterval": "Immediate",
                     "ClientAssemblyVersion": binary, "CoreAssemblyVersion": binary,
                     "ClientAssemblySHA256": hashlib.sha256((binary + "client").encode()).hexdigest(),
                     "CoreAssemblySHA256": hashlib.sha256((binary + "core").encode()).hexdigest()}
        if role != "before":
            benchmark["LightingPreparationReuse"] = role
        return {"Passed": True, "CapturedFrame": 184, "Width": 8, "Height": 8,
                "GoldenScene": {"Fixture": {"SceneId": "diag_" + scene, "Frames": 184, "WarmupFrames": 180},
                                "StateHash": scene, "SettingsFingerprint": "unchanged"},
                "CaptureEnvironment": {"Renderer": "controlled", "DriverIdentity": "driver-1"},
                "BenchmarkContext": benchmark,
                "FrameTiming": {"SampleCount": 4, "AverageMilliseconds": 8, "P95Milliseconds": 10, "P99Milliseconds": 12},
                "LightingDiagnostics": {"Version": 1, "GpuTiming": "unknown", "Frames": frames, "Scopes": scopes}}

    def write_capture(self, role, scene, repeat, capture):
        capture_path, report_path = self.paths[(role, scene, repeat)]
        capture_path.write_text(json.dumps(capture), encoding="utf-8")
        report = {"valid": True, "capture_sha256": hashlib.sha256(capture_path.read_bytes()).hexdigest(),
                  "screenshot_sha256": hashlib.sha256(capture_path.with_suffix(".png").read_bytes()).hexdigest(),
                  "golden_scene": capture["GoldenScene"], "capture_environment": capture["CaptureEnvironment"],
                  "benchmark_context": capture["BenchmarkContext"]}
        report_path.write_text(json.dumps(report), encoding="utf-8")

    def run_export(self):
        with contextlib.redirect_stdout(io.StringIO()):
            return export(self.roots["before"], self.roots["off"], self.roots["on"], self.output)

    def test_process_ranges_and_legacy_cache_do_not_pool_or_fabricate_data(self):
        dossier = self.run_export()
        self.assertEqual(81, dossier["process_observations"])
        self.assertEqual(9, len(dossier["groups"]))
        group = dossier["groups"][0]
        self.assertEqual({"min": 5, "max": 13}, group["roles"]["on"]["per_process_ranges"]["prepare_p95_ms"])
        self.assertEqual({"min": 3, "max": 7}, group["roles"]["on"]["per_process_ranges"]["prepare_mean_ms"])
        self.assertFalse(group["roles"]["before"]["cache_available"])
        self.assertNotIn("ambient_cache_hits", group["roles"]["before"]["per_process_ranges"])
        self.assertEqual({"min": 1, "max": 1}, group["roles"]["on"]["per_process_ranges"]["ambient_cache_hits"])
        self.assertEqual(18, len(dossier["exact_comparisons"]))
        self.assertTrue(all(item["accepted"] for item in dossier["exact_comparisons"]))
        with (self.output / "per_process.csv").open(newline="", encoding="utf-8") as stream:
            rows = list(csv.DictReader(stream))
        self.assertEqual(81, len(rows))
        self.assertEqual("", rows[0]["ambient_cache_hits"])
        self.assertNotEqual(dossier["roles"]["before"]["binary"], dossier["roles"]["on"]["binary"])
        self.assertEqual(dossier["roles"]["off"]["binary"], dossier["roles"]["on"]["binary"])
        self.assertIn("do not prove live advancing-time benefit", dossier["interpretation"])

    def test_missing_or_duplicate_repeat_and_failed_run_are_rejected(self):
        path = self.roots["off"] / "summary.json"
        original = json.loads(path.read_text())
        mutations = []
        missing = copy.deepcopy(original)
        missing["Results"].pop()
        mutations.append(missing)
        duplicate = copy.deepcopy(original)
        duplicate["Results"][-1] = duplicate["Results"][0]
        mutations.append(duplicate)
        failed = copy.deepcopy(original)
        failed["Failures"] = ["capture failed"]
        mutations.append(failed)
        for mutation in mutations:
            with self.subTest(mutation=mutation["Failures"]):
                path.write_text(json.dumps(mutation), encoding="utf-8")
                with self.assertRaises(ValueError):
                    self.run_export()
                self.assertFalse((self.output / "per_process.csv").exists())

    def test_fixture_identity_is_required_across_roles_and_repeats(self):
        capture = self.capture("on", SCENES[0], 1)
        capture["GoldenScene"]["SettingsFingerprint"] = "changed-quality"
        self.write_capture("on", SCENES[0], 1, capture)
        with self.assertRaisesRegex(ValueError, "fixture/settings/state/source identity"):
            self.run_export()
        self.assertFalse((self.output / "comparison.json").exists())
        self.write_capture("on", SCENES[0], 1, self.capture("on", SCENES[0], 1))
        path = self.roots["on"] / "summary.json"
        summary = json.loads(path.read_text())
        summary["Results"][0]["SourceFixtureSHA256"] = "different-source-fixture"
        path.write_text(json.dumps(summary), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "fixture/settings/state/source identity"):
            self.run_export()

    def test_binary_and_render_environment_mixing_fail(self):
        for field, value in (("ClientAssemblySHA256", "other-client"),
                             ("CoreAssemblyVersion", "other-core"), ("CPU", "other-cpu"),
                             ("OSDescription", "different-os-build"), ("LogicalProcessors", "24"),
                             ("SoftwareFrameRateLimit", "60"), ("PresentationInterval", "One"),
                             ("Configuration", "Debug"), ("LightingPreparationReuse", "off")):
            with self.subTest(field=field):
                capture = self.capture("on", SCENES[0], 1)
                capture["BenchmarkContext"][field] = value
                self.write_capture("on", SCENES[0], 1, capture)
                with self.assertRaises(ValueError):
                    self.run_export()
                self.write_capture("on", SCENES[0], 1, self.capture("on", SCENES[0], 1))
        capture = self.capture("on", SCENES[0], 1)
        capture["CaptureEnvironment"]["DriverIdentity"] = "different-driver"
        self.write_capture("on", SCENES[0], 1, capture)
        with self.assertRaisesRegex(ValueError, "render environment"):
            self.run_export()
        self.write_capture("on", SCENES[0], 1, self.capture("on", SCENES[0], 1))
        for repeat in range(3):
            for scene in SCENES:
                capture = self.capture("on", scene, repeat)
                capture["BenchmarkContext"]["ClientAssemblySHA256"] = "different-on-binary"
                self.write_capture("on", scene, repeat, capture)
        with self.assertRaisesRegex(ValueError, "OFF and ON"):
            self.run_export()

    def test_raw_capture_and_png_sha_binding_are_required(self):
        capture_path, report_path = self.paths[("off", SCENES[0], 0)]
        capture_path.write_text(capture_path.read_text() + "\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "raw capture SHA"):
            self.run_export()
        self.write_capture("off", SCENES[0], 0, self.capture("off", SCENES[0], 0))
        Image.new("RGB", (8, 8), (41, 60, 80)).save(capture_path.with_suffix(".png"))
        with self.assertRaisesRegex(ValueError, "PNG SHA"):
            self.run_export()

    def test_off_must_bypass_and_on_cannot_omit_cache_counters(self):
        capture = self.capture("on", SCENES[0], 0)
        capture["BenchmarkContext"]["LightingPreparationReuse"] = "off"
        self.write_capture("off", SCENES[0], 0, capture)
        with self.assertRaisesRegex(ValueError, "OFF captures must attest"):
            self.run_export()
        self.write_capture("off", SCENES[0], 0, self.capture("off", SCENES[0], 0))
        capture = self.capture("on", SCENES[0], 0)
        for frame in capture["LightingDiagnostics"]["Frames"]:
            for field in CACHE_COUNTERS:
                del frame[field]
        self.write_capture("on", SCENES[0], 0, capture)
        with self.assertRaisesRegex(ValueError, "ON captures must include"):
            self.run_export()

    def test_pixel_regression_retains_diffs_and_rejects_dossier(self):
        capture_path, report_path = self.paths[("on", SCENES[0], 0)]
        Image.new("RGB", (8, 8), (41, 60, 80)).save(capture_path.with_suffix(".png"))
        self.write_capture("on", SCENES[0], 0, self.capture("on", SCENES[0], 0))
        with self.assertRaisesRegex(ValueError, "exact visual comparison failed"):
            self.run_export()
        comparison = self.output / "diffs" / SCENES[0] / "off-versus-on" / "comparison.json"
        self.assertFalse(json.loads(comparison.read_text())["accepted"])
        self.assertTrue(comparison.with_name("diff.png").exists())
        self.assertFalse((self.output / "comparison.json").exists())
        self.assertFalse((self.output / "per_process.csv").exists())

    def test_cli_returns_nonzero_on_invalid_cohort(self):
        capture = self.capture("off", SCENES[0], 0)
        capture["FrameTiming"]["P99Milliseconds"] = "not-a-duration"
        self.write_capture("off", SCENES[0], 0, capture)
        with self.assertRaisesRegex(ValueError, "FrameTiming.P99Milliseconds"):
            self.run_export()
        path = self.roots["before"] / "summary.json"
        path.write_text("null", encoding="utf-8")
        with contextlib.redirect_stderr(io.StringIO()):
            result = main(["--before-root", str(self.roots["before"]), "--off-root", str(self.roots["off"]),
                           "--on-root", str(self.roots["on"]), "--output-dir", str(self.output)])
        self.assertEqual(1, result)


if __name__ == "__main__":
    unittest.main()
