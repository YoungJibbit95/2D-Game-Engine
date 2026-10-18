"""Exact preview image contracts independent of platform PNG compression."""

from hashlib import sha256
from io import BytesIO

from PIL import Image


def previews_match(generated_bytes: bytes, existing_bytes: bytes) -> bool:
    # Pillow wheels can use different PNG/zlib encoders across operating systems.
    with Image.open(BytesIO(generated_bytes)) as generated, Image.open(BytesIO(existing_bytes)) as existing:
        return (
            generated.mode == existing.mode
            and generated.size == existing.size
            and generated.tobytes() == existing.tobytes()
        )


def preview_contract_matches(generated_bytes: bytes, generated_summary: dict,
                             existing_bytes: bytes, existing_summary: dict) -> bool:
    # The recorded hash must still authenticate the actual checked-in PNG. All
    # other metadata must exactly match the newly generated scene description.
    expected_summary = dict(generated_summary)
    expected_summary["sha256"] = sha256(existing_bytes).hexdigest()
    return previews_match(generated_bytes, existing_bytes) and expected_summary == existing_summary
