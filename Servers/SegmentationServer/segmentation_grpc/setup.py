"""
Setup script for the segmentation_grpc package.
"""

from setuptools import setup, find_packages

setup(
    name="segmentation_grpc",
    version="0.1.0",
    packages=find_packages(),
    install_requires=[
        "grpcio>=1.71.0",
        "grpcio-tools>=1.71.0",
        "protobuf>=5.29.0",
    ],
    python_requires=">=3.11",
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
            "generate-grpc=segmentation_grpc.__main__:main",
        ],
    },
)