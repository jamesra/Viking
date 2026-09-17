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
