"""python -m segmentation_grpc regenerates stubs from the shared proto."""

from __future__ import annotations

import logging
import sys

from segmentation_grpc.generate_grpc import generate_grpc_code


def main() -> int:
    """Generate gRPC code and return a process exit status."""
    logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")
    logging.getLogger(__name__).info("Generating gRPC code...")
    if generate_grpc_code(force=True):
        logging.getLogger(__name__).info("gRPC code generation successful.")
        return 0
    logging.getLogger(__name__).error("gRPC code generation failed.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
