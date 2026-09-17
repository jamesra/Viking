"""
Setup script for the segmentation_grpc package.
"""

from setuptools import setup, find_packages

setup(
    name="segmentation_grpc",
    version="0.1.0",
    packages=find_packages(),
    install_requires=[
        "grpcio",
        "grpcio-tools",
        "protobuf",
    ],
    python_requires=">=3.7",
    description="gRPC interface for the segmentation service",
    author="James Anderson",
    include_package_data=True,
    package_data={
        "segmentation_grpc": ["*.py"],
    },
    # Note: Proto file is shared from gRPC_Protos/Segmentation/SAM2/segmentation.proto
    # Run generate-grpc to regenerate Python gRPC code from the shared proto
    entry_points={
        "console_scripts": [
            "generate-grpc=segmentation_grpc.generate_grpc:generate_grpc_code",
        ],
    },
)