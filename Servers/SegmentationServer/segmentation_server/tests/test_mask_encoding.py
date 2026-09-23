import io
import os
import sys
import unittest

import numpy as np
from PIL import Image

CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
PACKAGE_ROOT = os.path.join(os.path.dirname(CURRENT_DIR), "segmentation_server")
if PACKAGE_ROOT not in sys.path:
    sys.path.insert(0, PACKAGE_ROOT)

from mask_encoding import (  # noqa: E402
    decode_binary_mask_png,
    encode_binary_mask_png,
    encode_labeled_image_png,
    encode_probability_mask_png,
    padded_logit_crop,
)


class MaskEncodingTests(unittest.TestCase):
    def test_one_bit_png_round_trips_boolean_mask(self) -> None:
        mask = np.zeros((16, 20), dtype=np.bool_)
        mask[2:14, 3:17] = True
        mask[7, 19] = True

        png_bytes = encode_binary_mask_png(mask)
        image = Image.open(io.BytesIO(png_bytes))
        decoded = decode_binary_mask_png(png_bytes)

        self.assertEqual("1", image.mode)
        self.assertEqual((20, 16), image.size)
        np.testing.assert_array_equal(mask, decoded)

    def test_probability_png_places_logit_zero_at_mid_gray(self) -> None:
        logits = np.array([[-20.0, 0.0, 20.0]], dtype=np.float32)

        png_bytes = encode_probability_mask_png(logits)
        image = Image.open(io.BytesIO(png_bytes))

        self.assertEqual("L", image.mode)
        self.assertEqual((3, 1), image.size)
        self.assertEqual([0, 128, 255], list(image.getdata()))

    def test_padded_crop_keeps_samples_outside_the_boolean_mask(self) -> None:
        logits = np.zeros((10, 12), dtype=np.float32)
        logits[4:7, 5:8] = 2.0
        logits[4:7, 4] = -0.4
        mask = logits > 0.0

        crop, x0, y0 = padded_logit_crop(logits, mask, pad=2)

        self.assertEqual(3, x0)
        self.assertEqual(2, y0)
        self.assertAlmostEqual(-0.4, float(crop[4 - y0, 4 - x0]))
        self.assertGreater(crop.shape[0], 3)
        self.assertGreater(crop.shape[1], 3)

    def test_labeled_image_omission_is_opt_in(self) -> None:
        labeled = np.zeros((8, 8), dtype=np.uint16)
        labeled[2:6, 2:6] = 1

        omitted = encode_labeled_image_png(labeled, omit=True)
        included = encode_labeled_image_png(labeled, omit=False)

        self.assertEqual(b"", omitted)
        self.assertGreater(len(included), 0)
        self.assertEqual(b"\x89PNG", included[:4])


if __name__ == "__main__":
    unittest.main()
