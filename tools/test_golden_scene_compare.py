import contextlib
import copy
import io
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from golden_scene_compare import compare, main


class GoldenSceneCompareTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.reference = self.root / "reference.png"
        self.candidate = self.root / "candidate.png"
        self.output = self.root / "comparison"
        self.metadata = {"GoldenScene": {"SceneId": "forest-day", "Seed": 2107,
            "Tick": 120, "Camera": [320, 180], "Weather": "clear",
            "Lights": [], "ContentFingerprint": "fixture-content-sha"},
            "CaptureEnvironment": {"OS": "controlled", "Renderer": {"Driver": "mesa-pinned"}},
            "FrameTiming": {"SampleCount": 120, "CpuFrameMeanMs": 8.2}}
        for path in (self.reference, self.candidate):
            Image.new("RGBA", (20, 20), (80, 120, 160, 255)).save(path)
            self.write_metadata(path, self.metadata)

    def write_metadata(self, path, metadata):
        path.with_suffix(".json").write_text(json.dumps(metadata), encoding="utf-8")

    def run_compare(self, **kwargs):
        return compare(self.reference, self.candidate, self.output, **kwargs)

    def test_compression_only_change_is_exactly_accepted(self):
        with Image.open(self.reference) as image:
            image.save(self.candidate, compress_level=0)
        result = self.run_compare()
        self.assertTrue(result["accepted"])
        self.assertNotEqual(result["reference_sha256"], result["candidate_sha256"])
        self.assertEqual(result["reference_pixels_sha256"], result["candidate_pixels_sha256"])
        self.assertEqual(0, result["maximum_absolute_channel_error"])

    def test_one_pixel_fails_exact_with_useful_artifacts(self):
        with Image.open(self.candidate) as image:
            image.putpixel((4, 5), (81, 120, 160, 255))
            image.save(self.candidate)
        result = self.run_compare()
        self.assertFalse(result["accepted"])
        self.assertEqual(1, result["maximum_absolute_channel_error"])
        self.assertAlmostEqual(1 / 400, result["differing_pixel_fraction"])
        self.assertEqual(1, result["differing_pixel_count"])
        self.assertEqual((4, 5, 5, 6), result["difference_bounds"])
        with Image.open(self.output / "diff.png") as diff:
            self.assertEqual((8, 0, 0), diff.getpixel((4, 5)))
        with Image.open(self.output / "preview.png") as preview:
            self.assertEqual((60, 20), preview.size)
        self.assertFalse(json.loads((self.output / "comparison.json").read_text())["accepted"])

    def test_environment_change_requires_explicit_portable_mode(self):
        other = copy.deepcopy(self.metadata)
        other["CaptureEnvironment"]["Renderer"]["Driver"] = "other-gpu"
        self.write_metadata(self.candidate, other)
        exact = self.run_compare()
        self.assertFalse(exact["accepted"])
        portable = self.run_compare(mode="portable")
        self.assertTrue(portable["accepted"])
        self.assertEqual("Renderer.Driver", portable["environment_differences"][0]["path"])

    def test_same_pixels_with_wrong_fixture_fail_portable(self):
        for field, value in (("Seed", 2108), ("Tick", 121), ("ContentFingerprint", "changed")):
            with self.subTest(field=field):
                other = copy.deepcopy(self.metadata)
                other["GoldenScene"][field] = value
                self.write_metadata(self.candidate, other)
                self.assertFalse(self.run_compare(mode="portable")["accepted"])

    def test_black_and_substantial_corruption_fail_portable(self):
        for color in ((0, 0, 0, 255), (80, 120, 190, 255)):
            with self.subTest(color=color):
                Image.new("RGBA", (20, 20), color).save(self.candidate)
                result = self.run_compare(mode="portable")
                self.assertFalse(result["accepted"])
                self.assertGreater(result["over_threshold_pixel_fraction"], 0.01)

    def test_tolerable_delta_and_custom_threshold(self):
        Image.new("RGBA", (20, 20), (81, 121, 161, 255)).save(self.candidate)
        self.assertTrue(self.run_compare(mode="portable")["accepted"])
        self.assertFalse(self.run_compare(mode="portable", mean_threshold=0.5)["accepted"])

    def test_portable_pixel_fraction_is_an_independent_gate(self):
        with Image.open(self.candidate) as image:
            for x in range(5):
                image.putpixel((x, 0), (93, 120, 160, 255))
            image.save(self.candidate)
        result = self.run_compare(mode="portable")
        self.assertLess(result["mean_absolute_channel_error"], 2)
        self.assertFalse(result["accepted"])
        self.assertTrue(self.run_compare(mode="portable", pixel_threshold=0.02)["accepted"])
        self.assertTrue(self.run_compare(mode="portable", channel_threshold=13)["accepted"])

    def test_palette_color_changes_cannot_hide_in_identical_indices(self):
        for path, red in ((self.reference, 80), (self.candidate, 90)):
            image = Image.new("P", (20, 20), 0)
            image.putpalette([red, 120, 160] + [0] * 765)
            image.save(path)
        result = self.run_compare()
        self.assertFalse(result["accepted"])
        self.assertEqual(10, result["maximum_absolute_channel_error"])

    def test_commit_and_measured_diagnostics_do_not_change_fixture_identity(self):
        other = copy.deepcopy(self.metadata)
        other["Commit"] = "candidate-revision"
        other["FrameTiming"]["CpuFrameMeanMs"] = 9.1
        self.write_metadata(self.candidate, other)
        self.assertTrue(self.run_compare()["accepted"])

    def test_alpha_only_change_is_detected_and_visible(self):
        Image.new("RGBA", (20, 20), (80, 120, 160, 250)).save(self.candidate)
        self.assertFalse(self.run_compare()["accepted"])
        with Image.open(self.output / "diff.png") as diff:
            self.assertEqual((40, 40, 40), diff.getpixel((0, 0)))

    def test_metadata_required_and_explicit_sidecar_supported(self):
        sidecar = self.candidate.with_suffix(".json")
        override = self.root / "smoke-result.json"
        sidecar.rename(override)
        self.assertFalse(self.run_compare()["accepted"])
        self.assertTrue((self.output / "diff.png").exists())
        result = self.run_compare(candidate_metadata=override)
        self.assertTrue(result["accepted"])
        self.assertEqual(self.metadata["FrameTiming"], result["candidate_diagnostics"]["FrameTiming"])

    def test_invalid_images_metadata_dimensions_and_thresholds_fail(self):
        self.candidate.write_bytes(b"not a PNG")
        self.assertFalse(self.run_compare()["accepted"])
        for image in (Image.new("RGB", (20, 20)), Image.new("RGBA", (21, 20))):
            image.save(self.candidate)
            self.assertFalse(self.run_compare()["accepted"])
        Image.new("RGBA", (20, 20)).save(self.candidate)
        for value in ("{}", "[]", "{bad json", '{"GoldenScene": {"Tick": NaN}}'):
            self.candidate.with_suffix(".json").write_text(value)
            self.assertFalse(self.run_compare()["accepted"])
        self.assertFalse(self.run_compare(mean_threshold=float("nan"))["accepted"])

    def test_cli_returns_nonzero_for_regression(self):
        arguments = ["--reference", str(self.reference), "--candidate", str(self.candidate),
                     "--output-dir", str(self.output), "--mode", "exact"]
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, main(arguments))
            self.candidate.with_suffix(".json").unlink()
            self.assertNotEqual(0, main(arguments))


if __name__ == "__main__":
    unittest.main()
