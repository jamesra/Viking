"""
Segmentation Service Main Entry Point

This module provides the main entry point for the segmentation service.
It handles command-line arguments and starts the gRPC server.
"""

import asyncio
import argparse
from dataclasses import dataclass
from typing import Optional

# Import the generate_grpc_code function from the segmentation_grpc package
from segmentation_grpc.generate_grpc import generate_grpc_code

# Import the serve function from the server module
from segmentation_server.server import serve


@dataclass
class CLIArgs:
    port: int
    workers: int
    inference_workers: Optional[int]
    generate_grpc: bool


async def main() -> None:
    """Main entry point for the segmentation service."""
    # Parse command-line arguments
    parser = argparse.ArgumentParser(description='Start the segmentation service.')
    parser.add_argument('--port', type=int, default=50051,
                        help='The port to listen on (default: 50051)')
    parser.add_argument('--workers', type=int, default=10,
                        help='The number of worker threads for gRPC server (default: 10)')
    parser.add_argument('--inference-workers', type=int, default=None,
                        help='The number of worker threads for model inference (default: same as --workers)')
    parser.add_argument('--generate-grpc', action='store_true',
                        help='Generate gRPC code before starting the server')
    args = parser.parse_args()

    cli_args = CLIArgs(
        port=args.port,
        workers=args.workers,
        inference_workers=args.inference_workers,
        generate_grpc=args.generate_grpc,
    )
    
    # Generate gRPC code if requested
    if cli_args.generate_grpc:
        print("Generating gRPC code...")
        if not generate_grpc_code(cli_args.generate_grpc):
            print("Failed to generate gRPC code. Exiting.")
            return
    else:
        print("Skipping gRPC code generation (using pre-generated code)...")
    
    # Start the server
    inference_workers_str = f"{cli_args.inference_workers if cli_args.inference_workers is not None else cli_args.workers} inference"
    print(f"Starting segmentation service on port {cli_args.port} with {cli_args.workers} gRPC workers and {inference_workers_str} workers...")
    await serve(port=cli_args.port, max_workers=cli_args.workers, inference_workers=cli_args.inference_workers)


if __name__ == '__main__':
    # Run the main function
    asyncio.run(main())