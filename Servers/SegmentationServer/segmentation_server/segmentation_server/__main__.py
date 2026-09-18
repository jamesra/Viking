"""Command-line entry for the segmentation gRPC server."""

from __future__ import annotations

import argparse
import asyncio
import logging
from dataclasses import dataclass
from typing import Optional

from segmentation_grpc.generate_grpc import generate_grpc_code
from segmentation_server.server import serve

logger = logging.getLogger(__name__)


@dataclass
class CLIArgs:
    port: int
    workers: int
    inference_workers: int
    generate_grpc: bool


async def main() -> None:
    """Parse CLI flags and start the gRPC server."""
    parser = argparse.ArgumentParser(description='Start the segmentation service.')
    parser.add_argument('--port', type=int, default=50051,
                        help='The port to listen on (default: 50051)')
    parser.add_argument('--workers', type=int, default=10,
                        help='gRPC callback thread pool size (default: 10)')
    parser.add_argument(
        '--inference-workers',
        type=int,
        default=1,
        help='SAM2 inference thread pool size (default: 1; extra workers usually contend for GPU memory)',
    )
    parser.add_argument('--generate-grpc', action='store_true',
                        help='Regenerate Python gRPC stubs from the proto before starting')
    args = parser.parse_args()

    cli_args = CLIArgs(
        port=args.port,
        workers=args.workers,
        inference_workers=args.inference_workers,
        generate_grpc=args.generate_grpc,
    )

    if cli_args.generate_grpc:
        logger.info("Generating gRPC code...")
        if not generate_grpc_code(True):
            logger.error("Failed to generate gRPC code. Exiting.")
            return
    else:
        logger.info("Using committed gRPC stubs (pass --generate-grpc to regenerate)")

    logger.info(
        "Starting segmentation service on port %s with %s gRPC workers and %s inference workers",
        cli_args.port,
        cli_args.workers,
        cli_args.inference_workers,
    )
    await serve(
        port=cli_args.port,
        max_workers=cli_args.workers,
        inference_workers=cli_args.inference_workers,
    )


def run() -> None:
    """Sync wrapper for `python -m segmentation_server` and the console script."""
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    asyncio.run(main())


if __name__ == '__main__':
    run()
