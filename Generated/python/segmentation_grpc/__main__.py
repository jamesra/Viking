"""
Main entry point for the segmentation_grpc package.

This module allows running the package as a Python module:
python -m segmentation_grpc
"""

import sys
from segmentation_grpc.generate_grpc import generate_grpc_code

def main():
    """Generate the gRPC code when the package is run as a module."""
    print("Generating gRPC code...")
    if generate_grpc_code():
        print("gRPC code generation successful.")
        return 0
    else:
        print("gRPC code generation failed.")
        return 1

if __name__ == "__main__":
    sys.exit(main())