"""Cross-platform preview regression: exact pixels, independent PNG compression."""

from io import BytesIO
import unittest

from PIL import Image

from build_ui_sample_preview import previews_match


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


if __name__ == "__main__":
    unittest.main()
