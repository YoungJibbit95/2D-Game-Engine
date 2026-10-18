import contextlib
import copy
import io
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from lighting_diagnostic_report import COUNTERS, FRAME_SCOPES, PREPARE_SCOPES, PROBES, main, report


class LightingDiagnosticReportTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.capture = self.root / "capture.json"
        self.output = self.root / "analysis"
        frames = []
        for index in range(4):
            frame = {field: 0 for field in COUNTERS}
            frame.update({field: index / 10 for field in PROBES})
            frame.update({"Frame": index + 181, "SimulationTick": index + 181,
                          "CameraX": -1046522 + index, "CameraY": 926, "Zoom": 2,
                          "HistoryReset": False, "MaskWidth": 10, "MaskHeight": 5,
                          "EstimatedTextureBytes": 1800})
            if index in (0, 2):
                frame.update({"PrepareExecutions": 1, "UploadCalls": 2, "UploadBytes": 400})
            frames.append(frame)
        prepare_samples = [{"Frame": 181, "Milliseconds": 1, "AllocatedBytes": 0},
                           {"Frame": 183, "Milliseconds": 9, "AllocatedBytes": 8}]
        scopes = [{"Name": name, "Samples": copy.deepcopy(prepare_samples)} for name in PREPARE_SCOPES]
        scopes.extend({"Name": name, "Samples": [
            {"Frame": index + 181, "Milliseconds": 0.5, "AllocatedBytes": 0} for index in range(4)]}
            for name in FRAME_SCOPES)
        self.metadata = {"Width": 8, "Height": 8, "Passed": True, "CapturedFrame": 184,
                         "GoldenScene": {"Fixture": {"SceneId": "synthetic_diagnostic",
                                                      "Frames": 184, "WarmupFrames": 180}},
                         "CaptureEnvironment": {"OS": "test"},
                         "BenchmarkContext": {"CPU": "synthetic", "GPUTime": "unknown"},
                         "CorePhaseTelemetry": {"IsEnabled": True, "TimestampFrequency": 1000,
                                                "Measurements": [{"Phase": 14, "Samples": 4, "TotalElapsedTicks": 2}]},
                         "FrameTiming": {"SampleCount": 4, "AverageMilliseconds": 9.5},
                         "LightingDiagnostics": {"Version": 1, "GpuTiming": "unknown", "Frames": frames,
                                                 "Scopes": scopes}}
        Image.new("RGB", (8, 8), (40, 60, 80)).save(self.capture.with_suffix(".png"))
        self.write()

    def write(self, metadata=None):
        self.capture.write_text(json.dumps(metadata if metadata is not None else self.metadata), encoding="utf-8")

    def analyze(self):
        return report(self.capture, self.output)

    def test_raw_arithmetic_and_amortized_distributions_are_distinct(self):
        result = self.analyze()
        self.assertTrue(result["valid"], result["errors"])
        scope = result["scopes"]["Presentation.LightingPrepare"]
        self.assertEqual(5, scope["invocation_milliseconds"]["mean"])
        self.assertEqual(1, scope["invocation_milliseconds"]["p50"])
        self.assertEqual(9, scope["invocation_milliseconds"]["p95"])
        self.assertEqual(9, scope["invocation_milliseconds"]["p99"])
        self.assertEqual(2.5, scope["amortized_milliseconds_per_measured_frame"]["mean"])
        self.assertEqual(0, scope["amortized_milliseconds_per_measured_frame"]["p50"])
        self.assertEqual(2, scope["allocated_bytes_per_measured_frame"]["mean"])
        self.assertEqual(0.5, result["frames"]["prepares_per_measured_frame"])
        self.assertEqual(800, result["frames"]["totals"]["UploadBytes"])
        self.assertEqual(self.metadata["FrameTiming"], result["raw_frame_interval_summary"])
        self.assertEqual(self.metadata["BenchmarkContext"], result["benchmark_context"])
        self.assertEqual(self.metadata["CorePhaseTelemetry"], result["core_phase_telemetry"])
        self.assertEqual(1800, result["frames"]["estimated_texture_payload_bytes"]["mean"])
        self.assertIn("not measured VRAM", result["frames"]["texture_payload_interpretation"])

    def test_duplicate_scope_invocations_in_one_frame_fail(self):
        self.metadata["LightingDiagnostics"]["Scopes"][0]["Samples"].insert(1,
            {"Frame": 181, "Milliseconds": 3, "AllocatedBytes": 4})
        self.write()
        result = self.analyze()
        self.assertFalse(result["valid"])
        self.assertIn("unique and ordered", result["errors"][0])

    def test_failed_missing_or_inconsistent_capture_attestation_fails(self):
        for field, value in (("Passed", False), ("Passed", 1), ("Passed", None),
                             ("CapturedFrame", 183), ("CapturedFrame", True), ("CapturedFrame", None)):
            with self.subTest(field=field, value=value):
                changed = copy.deepcopy(self.metadata)
                changed[field] = value
                self.write(changed)
                self.assertFalse(self.analyze()["valid"])

    def test_fixture_window_and_frame_interval_count_must_match(self):
        for field, value in (("Frames", 185), ("Frames", 180), ("Frames", True),
                             ("WarmupFrames", 179), ("WarmupFrames", 0), ("WarmupFrames", None)):
            with self.subTest(field=field, value=value):
                changed = copy.deepcopy(self.metadata)
                changed["GoldenScene"]["Fixture"][field] = value
                self.write(changed)
                self.assertFalse(self.analyze()["valid"])
        for count in (3, 5, True, None):
            changed = copy.deepcopy(self.metadata)
            changed["FrameTiming"]["SampleCount"] = count
            self.write(changed)
            self.assertFalse(self.analyze()["valid"])

    def test_shortened_or_shifted_contiguous_trace_fails(self):
        for trace in (self.metadata["LightingDiagnostics"]["Frames"][1:],
                      self.metadata["LightingDiagnostics"]["Frames"][:-1]):
            changed = copy.deepcopy(self.metadata)
            changed["LightingDiagnostics"]["Frames"] = trace
            self.write(changed)
            result = self.analyze()
            self.assertFalse(result["valid"])
            self.assertIn("measured frame count", result["errors"][0])
        changed = copy.deepcopy(self.metadata)
        for frame in changed["LightingDiagnostics"]["Frames"]:
            frame["Frame"] += 1
            frame["SimulationTick"] += 1
        self.write(changed)
        self.assertFalse(self.analyze()["valid"])

    def test_simulation_tick_must_advance_once_per_frame(self):
        for tick in (181, 183):
            changed = copy.deepcopy(self.metadata)
            changed["LightingDiagnostics"]["Frames"][1]["SimulationTick"] = tick
            self.write(changed)
            result = self.analyze()
            self.assertFalse(result["valid"])
            self.assertIn("SimulationTick must equal Frame", result["errors"][0])

    def test_every_required_scope_and_its_per_frame_samples_are_required(self):
        for name in FRAME_SCOPES + PREPARE_SCOPES:
            with self.subTest(name=name, mutation="omitted"):
                changed = copy.deepcopy(self.metadata)
                changed["LightingDiagnostics"]["Scopes"] = [scope for scope in changed["LightingDiagnostics"]["Scopes"]
                                                           if scope["Name"] != name]
                self.write(changed)
                result = self.analyze()
                self.assertFalse(result["valid"])
                self.assertIn(name, result["errors"][0])
            with self.subTest(name=name, mutation="sample omitted"):
                changed = copy.deepcopy(self.metadata)
                scope = next(scope for scope in changed["LightingDiagnostics"]["Scopes"] if scope["Name"] == name)
                scope["Samples"].pop()
                self.write(changed)
                self.assertFalse(self.analyze()["valid"])

    def test_scope_counts_and_frame_counter_schedule_must_agree(self):
        changed = copy.deepcopy(self.metadata)
        changed["LightingDiagnostics"]["Frames"][2].update({"PrepareExecutions": 0, "UploadCalls": 0, "UploadBytes": 0})
        self.write(changed)
        result = self.analyze()
        self.assertFalse(result["valid"])
        self.assertIn("PrepareExecutions", result["errors"][0])
        # Equal invocation counts on different frames must not hide a shifted schedule.
        changed = copy.deepcopy(self.metadata)
        changed["LightingDiagnostics"]["Scopes"][0]["Samples"][1]["Frame"] = 184
        self.write(changed)
        self.assertFalse(self.analyze()["valid"])

    def test_upload_payload_and_single_prepare_counter_are_validated(self):
        for field, value in (("PrepareExecutions", 2), ("UploadBytes", 399),
                             ("UploadBytes", 0), ("UploadCalls", 0), ("MaskWidth", 11)):
            with self.subTest(field=field, value=value):
                changed = copy.deepcopy(self.metadata)
                changed["LightingDiagnostics"]["Frames"][0][field] = value
                self.write(changed)
                self.assertFalse(self.analyze()["valid"])
        changed = copy.deepcopy(self.metadata)
        changed["LightingDiagnostics"]["Frames"][0]["PrepareExecutions"] = 0
        self.write(changed)
        self.assertFalse(self.analyze()["valid"])

    def test_probe_availability_excludes_missing_values_and_gaps(self):
        frames = self.metadata["LightingDiagnostics"]["Frames"]
        frames[1].update({"ProbeAvailable": False, "ProbeRed": 100})
        self.write()
        probes = self.analyze()["probes"]
        self.assertEqual(3, probes["available_frames"])
        self.assertEqual(1, probes["unavailable_frames"])
        red = probes["signals"]["ProbeRed"]
        self.assertAlmostEqual(0.3, red["range"])
        self.assertAlmostEqual(1 / 6, red["values"]["mean"])
        self.assertEqual(1, red["consecutive_available_steps"])
        self.assertAlmostEqual(0.1, red["absolute_step"]["mean"])

    def test_entirely_unavailable_probe_has_no_signal_distribution(self):
        for frame in self.metadata["LightingDiagnostics"]["Frames"]:
            frame["ProbeAvailable"] = False
        self.write()
        result = self.analyze()
        self.assertTrue(result["valid"], result["errors"])
        self.assertEqual(0, result["probes"]["available_frames"])
        self.assertEqual(4, result["probes"]["unavailable_frames"])
        for signal in result["probes"]["signals"].values():
            self.assertIsNone(signal["values"])
            self.assertIsNone(signal["range"])
            self.assertIsNone(signal["absolute_step"])

    def test_optional_probe_and_texture_fields_are_typed_and_backward_compatible(self):
        for field, value in (("ProbeAvailable", 0), ("ProbeAvailable", None),
                             ("EstimatedTextureBytes", -1), ("EstimatedTextureBytes", True)):
            changed = copy.deepcopy(self.metadata)
            changed["LightingDiagnostics"]["Frames"][0][field] = value
            self.write(changed)
            self.assertFalse(self.analyze()["valid"])
        for frame in self.metadata["LightingDiagnostics"]["Frames"]:
            frame.pop("EstimatedTextureBytes")
        self.write()
        result = self.analyze()
        self.assertTrue(result["valid"], result["errors"])
        self.assertEqual(4, result["probes"]["available_frames"])
        self.assertIsNone(result["frames"]["estimated_texture_payload_bytes"])

    def test_clipping_uses_only_middle_half_and_rgb_channels(self):
        image = Image.new("RGBA", (8, 8), (255, 255, 255, 0))
        for y in range(2, 6):
            for x in range(8):
                image.putpixel((x, y), (0, 0, 0, 255))
        image.putpixel((0, 2), (250, 250, 250, 255))
        image.putpixel((1, 2), (250, 10, 10, 255))
        image.save(self.capture.with_suffix(".png"))
        image_result = self.analyze()["image"]
        self.assertEqual([0, 2, 8, 6], image_result["roi"]["bounds_xyxy"])
        self.assertEqual(2 / 32, image_result["any_channel_clipped_fraction"])
        self.assertEqual(1 / 32, image_result["all_channels_clipped_fraction"])
        self.assertEqual(0, image_result["display_encoded_rec709_luma_0_to_255"]["p50"])
        with Image.open(self.output / "clipping.png") as heatmap:
            self.assertEqual((0, 0, 0), heatmap.getpixel((0, 0)))
            self.assertEqual((255, 255, 255), heatmap.getpixel((0, 2)))
            self.assertEqual((255, 128, 0), heatmap.getpixel((1, 2)))

    def test_missing_metadata_and_empty_traces_fail(self):
        for field in ("GoldenScene", "CaptureEnvironment", "BenchmarkContext", "FrameTiming", "LightingDiagnostics"):
            with self.subTest(field=field):
                changed = copy.deepcopy(self.metadata)
                del changed[field]
                self.write(changed)
                self.assertFalse(self.analyze()["valid"])
        for field in ("Frames", "Scopes"):
            changed = copy.deepcopy(self.metadata)
            changed["LightingDiagnostics"][field] = []
            self.write(changed)
            self.assertFalse(self.analyze()["valid"])

    def test_nonfinite_negative_and_boolean_numbers_fail(self):
        mutations = [("CameraX", float("nan")), ("Zoom", float("inf")), ("Zoom", 0),
                     ("UploadBytes", -1), ("Frame", True), ("ProbeBloom", -0.1)]
        for field, value in mutations:
            with self.subTest(field=field, value=value):
                changed = copy.deepcopy(self.metadata)
                changed["LightingDiagnostics"]["Frames"][0][field] = value
                self.write(changed)
                self.assertFalse(self.analyze()["valid"])
        changed = copy.deepcopy(self.metadata)
        changed["LightingDiagnostics"]["Scopes"][0]["Samples"][0]["Milliseconds"] = -0.1
        self.write(changed)
        self.assertFalse(self.analyze()["valid"])

    def test_frame_gaps_duplicates_tick_reversal_and_scope_range_fail(self):
        for field, index, value in (("Frame", 1, 181), ("Frame", 1, 185), ("SimulationTick", 1, 180)):
            changed = copy.deepcopy(self.metadata)
            changed["LightingDiagnostics"]["Frames"][index][field] = value
            self.write(changed)
            self.assertFalse(self.analyze()["valid"])
        changed = copy.deepcopy(self.metadata)
        changed["LightingDiagnostics"]["Scopes"][0]["Samples"][1]["Frame"] = 185
        self.write(changed)
        self.assertFalse(self.analyze()["valid"])

    def test_gpu_measurement_claim_is_rejected(self):
        self.metadata["LightingDiagnostics"]["GpuTiming"] = "measured"
        self.write()
        self.assertFalse(self.analyze()["valid"])

    def test_invalid_json_png_and_mismatched_dimensions_fail_with_artifact(self):
        self.capture.write_text("{bad", encoding="utf-8")
        self.assertFalse(self.analyze()["valid"])
        self.write()
        self.capture.with_suffix(".png").write_bytes(b"not a PNG")
        self.assertFalse(self.analyze()["valid"])
        Image.new("RGB", (9, 8)).save(self.capture.with_suffix(".png"))
        self.assertFalse(self.analyze()["valid"])
        self.assertFalse(json.loads((self.output / "report.json").read_text())["valid"])

    def test_probe_variation_is_unclassified_and_inputs_are_immutable(self):
        before = self.capture.read_bytes(), self.capture.with_suffix(".png").read_bytes()
        result = self.analyze()
        probe = result["probes"]["signals"]["ProbeRed"]
        self.assertAlmostEqual(0.3, probe["range"])
        self.assertAlmostEqual(0.1, probe["absolute_step"]["mean"])
        self.assertIn("Not proof of flicker", result["probes"]["interpretation"])
        self.assertEqual(before, (self.capture.read_bytes(), self.capture.with_suffix(".png").read_bytes()))

    def test_output_alias_is_rejected_without_overwriting_capture(self):
        alias = self.root / "report.json"
        self.capture.rename(alias)
        alias.with_suffix(".png").write_bytes(self.root.joinpath("capture.png").read_bytes())
        before = alias.read_bytes()
        with self.assertRaisesRegex(ValueError, "overwrite"):
            report(alias, self.root)
        self.assertEqual(before, alias.read_bytes())

    def test_cli_has_concise_success_and_nonzero_failure(self):
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            self.assertEqual(0, main(["--capture", str(self.capture), "--output-dir", str(self.output)]))
        self.assertTrue(output.getvalue().startswith("LIGHTING_REPORT_PASS frames=4"))
        self.assertEqual(1, len(output.getvalue().splitlines()))
        self.capture.write_text("null", encoding="utf-8")
        with contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1, main(["--capture", str(self.capture), "--output-dir", str(self.output)]))


if __name__ == "__main__":
    unittest.main()
