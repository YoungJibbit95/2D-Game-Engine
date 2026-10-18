"""Cross-platform preview regression: exact pixels, independent PNG compression."""

from io import BytesIO
from hashlib import sha256
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image

from build_ui_sample_preview import build_preview, load_assets, main, previews_match
from preview_integrity import preview_contract_matches


def encode(image: Image.Image, compression: int) -> bytes:
    output = BytesIO()
    image.save(output, format="PNG", compress_level=compression)
    return output.getvalue()


class PreviewIntegrityTests(unittest.TestCase):
    def test_identical_pixels_with_different_compression_match(self):
        image = Image.new("RGB", (16, 16), (20, 30, 40))
        uncompressed = encode(image, 0)
        compressed = encode(image, 9)
        self.assertNotEqual(uncompressed, compressed)
        self.assertTrue(previews_match(uncompressed, compressed))

    def test_one_changed_pixel_is_rejected(self):
        image = Image.new("RGB", (16, 16), (20, 30, 40))
        original = encode(image, 9)
        image.putpixel((8, 8), (20, 30, 41))
        self.assertFalse(previews_match(original, encode(image, 9)))

    def test_different_dimensions_are_rejected(self):
        self.assertFalse(previews_match(
            encode(Image.new("RGB", (16, 16)), 9),
            encode(Image.new("RGB", (8, 32)), 9)))

    def test_mode_and_alpha_changes_are_rejected(self):
        rgb = Image.new("RGB", (16, 16))
        self.assertFalse(previews_match(encode(rgb, 9), encode(rgb.convert("RGBA"), 9)))
        alpha = rgb.convert("RGBA")
        changed = alpha.copy()
        changed.putpixel((8, 8), (0, 0, 0, 254))
        self.assertFalse(previews_match(encode(alpha, 9), encode(changed, 9)))

    def test_invalid_png_is_rejected(self):
        with self.assertRaises(OSError):
            previews_match(encode(Image.new("RGB", (16, 16)), 9), b"invalid PNG")

    def test_check_does_not_reencode_its_reference_file(self):
        data_root = Path(__file__).resolve().parents[2]
        manifest = data_root / "assets" / "sprites.json"
        image = build_preview(load_assets(data_root, manifest)).convert("RGB")
        reference_bytes = encode(image, 0)
        with tempfile.TemporaryDirectory(prefix="yjse-preview-") as directory:
            reference = Path(directory) / "reference.png"
            reference.write_bytes(reference_bytes)
            arguments = ["preview", "--data-root", str(data_root), "--manifest", str(manifest),
                         "--output", str(reference), "--check-existing", str(reference)]
            with patch("sys.argv", arguments):
                main()
            self.assertEqual(reference_bytes, reference.read_bytes())

    def test_summary_contract_keeps_reference_hash_but_ignores_compressor(self):
        image = Image.new("RGBA", (16, 16), (20, 30, 40, 255))
        generated = encode(image, 9)
        existing = encode(image, 0)
        summary = {"dimensions": [16, 16], "assetCount": 24, "sha256": sha256(generated).hexdigest()}
        reference = dict(summary, sha256=sha256(existing).hexdigest())
        self.assertTrue(preview_contract_matches(generated, summary, existing, reference))
        self.assertFalse(preview_contract_matches(generated, summary, existing, dict(reference, sha256="wrong")))
        self.assertFalse(preview_contract_matches(generated, summary, existing, dict(reference, assetCount=23)))
        self.assertFalse(preview_contract_matches(generated, summary, existing, dict(reference, unexpected=True)))


if __name__ == "__main__":
    unittest.main()
