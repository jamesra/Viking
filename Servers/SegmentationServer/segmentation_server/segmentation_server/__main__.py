"""Command-line entry for the segmentation gRPC server."""

from __future__ import annotations

import argparse
import asyncio
import logging
from dataclasses import dataclass

from segmentation_grpc.generate_grpc import generate_grpc_code
from segmentation_server.compile_config import env_compile_image_encoder_enabled
from segmentation_server.demo_site import DEFAULT_DEMO_PORT, demo_enabled, demo_plaintext, demo_port_for
from segmentation_server.healthcheck import tls_port
from segmentation_server.image_cache import (
    DEFAULT_GPU_MAX_BYTES,
    DEFAULT_MAX_ENTRIES,
    DEFAULT_MAX_MEMORY_BYTES,
    DEFAULT_TILE_IDLE_SECONDS,
    DEFAULT_TTL_SECONDS,
)
from segmentation_server.server import DEFAULT_TLS_PORT, PortBindError, TlsConfigurationError, serve
from segmentation_server.tile_work import install_queued_console_logging

logger = logging.getLogger(__name__)


@dataclass
class CLIArgs:
    port: int
    workers: int
    inference_workers: int
    generate_grpc: bool
    cache_ttl_seconds: int
    cache_max_memory_bytes: int
    cache_max_images: int
    compile_image_encoder: bool
    demo_site: bool
    demo_port: int
    demo_bind: str | None = None
    demo_plaintext: bool = False
    cache_gpu_max_bytes: int = DEFAULT_GPU_MAX_BYTES
    cache_tile_idle_seconds: float = DEFAULT_TILE_IDLE_SECONDS


async def main() -> None:
    """Parse CLI flags and start the gRPC server."""
    parser = argparse.ArgumentParser(description='Start the segmentation service.')
    parser.add_argument('--tls-port', type=int, default=tls_port(),
                        help=f'The TLS gRPC port to listen on (default: SEGMENTATION_TLS_PORT, else {DEFAULT_TLS_PORT}). '
                             'The container health check dials this port.')
    parser.add_argument('--port', type=int, default=None, help=argparse.SUPPRESS)
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
    parser.add_argument(
        '--cache-ttl-seconds',
        type=int,
        default=DEFAULT_TTL_SECONDS,
        help=f'Unused cached-image lifetime in seconds (default: {DEFAULT_TTL_SECONDS})',
    )
    parser.add_argument(
        '--cache-max-memory-bytes',
        type=int,
        default=DEFAULT_MAX_MEMORY_BYTES,
        help=(
            'Cap on the cache\'s host memory: encoded tiles plus embeddings moved off the GPU '
            f'(default: {DEFAULT_MAX_MEMORY_BYTES}, 8 GiB)'
        ),
    )
    parser.add_argument(
        '--gpu-cache-max-bytes',
        type=int,
        default=DEFAULT_GPU_MAX_BYTES,
        help=(
            'Cap on cached embeddings kept on the GPU; past it the least recently used move to host memory '
            f'(default: {DEFAULT_GPU_MAX_BYTES}, 512 MiB). Each pull back is logged, so thrashing shows.'
        ),
    )
    parser.add_argument(
        '--cache-idle-seconds',
        type=float,
        default=DEFAULT_TILE_IDLE_SECONDS,
        help=f'Shared tiles unused this long leave the cache (default: {DEFAULT_TILE_IDLE_SECONDS}; 0 keeps them)',
    )
    parser.add_argument(
        '--cache-max-images',
        type=int,
        default=DEFAULT_MAX_ENTRIES,
        help=(
            f'Max arbitrary uploaded images (default: {DEFAULT_MAX_ENTRIES}). '
            'Shared tiles ignore this and stay until the byte cap or GPU memory requires a drop.'
        ),
    )
    parser.add_argument(
        '--compile-image-encoder',
        action=argparse.BooleanOptionalAction,
        default=None,
        help=(
            'torch.compile SAM2 Hiera encoder on CUDA (default on). '
            'Use --no-compile-image-encoder to disable. First warmup can take minutes.'
        ),
    )
    parser.add_argument(
        '--demo-site',
        action=argparse.BooleanOptionalAction,
        default=None,
        help=(
            'Serve the optional HTTPS point-prompt page '
            f'(default: SEGMENTATION_DEMO_SITE, off). Port {DEFAULT_DEMO_PORT}.'
        ),
    )
    parser.add_argument(
        '--demo-port',
        type=int,
        default=None,
        help=f'Port for the demo page (default: 80 with --demo-plaintext, else {DEFAULT_DEMO_PORT})',
    )
    parser.add_argument(
        '--demo-plaintext',
        action=argparse.BooleanOptionalAction,
        default=None,
        help=(
            'Serve the demo page as plain HTTP, for a reverse proxy that terminates TLS '
            '(default: SEGMENTATION_DEMO_PLAINTEXT, off). Needs no certificate.'
        ),
    )
    parser.add_argument(
        '--demo-bind',
        type=str,
        default=None,
        help=(
            'Address the demo page listens on (default: SEGMENTATION_DEMO_BIND, else 127.0.0.1). '
            'A container that publishes the port needs 0.0.0.0; set SEGMENTATION_DEMO_TOKEN when it does.'
        ),
    )
    args = parser.parse_args()
    if args.port is not None:
        parser.error(
            "--port named the cleartext gRPC listener, which no longer exists. "
            "Use --tls-port for the TLS listener (default 443) and drop the cleartext port mapping."
        )

    compile_image_encoder = (
        args.compile_image_encoder
        if args.compile_image_encoder is not None
        else env_compile_image_encoder_enabled()
    )
    cli_args = CLIArgs(
        port=args.tls_port,
        workers=args.workers,
        inference_workers=args.inference_workers,
        generate_grpc=args.generate_grpc,
        cache_ttl_seconds=args.cache_ttl_seconds,
        cache_max_memory_bytes=args.cache_max_memory_bytes,
        cache_max_images=args.cache_max_images,
        compile_image_encoder=compile_image_encoder,
        demo_site=demo_enabled(args.demo_site),
        demo_port=demo_port_for(args.demo_port, demo_plaintext(args.demo_plaintext)),
        demo_bind=args.demo_bind,
        demo_plaintext=demo_plaintext(args.demo_plaintext),
        cache_gpu_max_bytes=args.gpu_cache_max_bytes,
        cache_tile_idle_seconds=args.cache_idle_seconds,
    )

    if cli_args.generate_grpc:
        logger.info("Generating gRPC code...")
        if not generate_grpc_code(True):
            logger.error("Failed to generate gRPC code. Exiting.")
            raise SystemExit(1)
    else:
        logger.info("Using committed gRPC stubs (pass --generate-grpc to regenerate)")

    logger.info(
        "Starting segmentation service (TLS gRPC) on port %s with %s gRPC workers and %s inference workers "
        "(cache ttl=%ss, host memory=%s bytes, GPU embeddings=%s bytes, tile idle=%ss, max_images=%s, "
        "compile_image_encoder=%s, demo_site=%s, demo_port=%s)",
        cli_args.port,
        cli_args.workers,
        cli_args.inference_workers,
        cli_args.cache_ttl_seconds,
        cli_args.cache_max_memory_bytes,
        cli_args.cache_gpu_max_bytes,
        cli_args.cache_tile_idle_seconds,
        cli_args.cache_max_images,
        cli_args.compile_image_encoder,
        cli_args.demo_site,
        cli_args.demo_port,
    )
    await serve(
        port=cli_args.port,
        max_workers=cli_args.workers,
        inference_workers=cli_args.inference_workers,
        cache_ttl_seconds=cli_args.cache_ttl_seconds,
        cache_max_memory_bytes=cli_args.cache_max_memory_bytes,
        cache_max_images=cli_args.cache_max_images,
        compile_image_encoder=cli_args.compile_image_encoder,
        demo_site=cli_args.demo_site,
        demo_port=cli_args.demo_port,
        demo_bind=cli_args.demo_bind,
        demo_plaintext=cli_args.demo_plaintext,
        cache_gpu_max_bytes=cli_args.cache_gpu_max_bytes,
        cache_tile_idle_seconds=cli_args.cache_tile_idle_seconds,
    )


def run() -> None:
    """Sync wrapper for `python -m segmentation_server` and the console script."""
    listener = install_queued_console_logging()
    try:
        asyncio.run(main())
    except (TlsConfigurationError, PortBindError) as error:
        logger.critical("Cannot start: %s", error)
        raise SystemExit(2) from error
    finally:
        listener.stop()


if __name__ == '__main__':
    run()
