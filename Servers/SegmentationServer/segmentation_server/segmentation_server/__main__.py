"""
Segmentation Service Main Entry Point

This module provides the main entry point for the segmentation service.
It handles command-line arguments and starts the gRPC server.
"""

import asyncio
import argparse

# Import the generate_grpc_code function from the segmentation_grpc package
from segmentation_grpc.generate_grpc import generate_grpc_code

# Import the serve function from the server module
from segmentation_server.server import serve


async def main():
    """Main entry point for the segmentation service."""
    # Parse command-line arguments
    parser = argparse.ArgumentParser(description='Start the segmentation service.')
    parser.add_argument('--port', type=int, default=50051,
                        help='The port to listen on (default: 50051)')
    parser.add_argument('--workers', type=int, default=10,
                        help='The number of worker threads (default: 10)')
    parser.add_argument('--generate-grpc', action='store_true',
                        help='Generate gRPC code before starting the server')
    args = parser.parse_args()
    
    # Generate gRPC code if requested
    print("Generating gRPC code...")
    if not generate_grpc_code(args.generate_grpc):
        print("Failed to generate gRPC code. Exiting.")
        return
    
    # Start the server
    print(f"Starting segmentation service on port {args.port} with {args.workers} workers...")
    await serve(port=args.port, max_workers=args.workers)


if __name__ == '__main__':
    # Run the main function
    asyncio.run(main())