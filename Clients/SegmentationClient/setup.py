"""
Setup script for the SegmentationClient package.
"""

from setuptools import setup, find_packages

setup(
    name="SegmentationClient",
    version="0.1.0",
    packages=find_packages(),
    install_requires=[
        "grpcio",
        "grpcio-tools",
        "protobuf",
        "numpy",
        "pillow",
        "matplotlib",
        "opencv-python",
        "segmentation_grpc",
    ],
    python_requires=">=3.7",
    description="Client for the segmentation service",
    author="James Anderson",
    entry_points={
        "console_scripts": [
            "segmentation-client=SegmentationClient.client_example:main",
            "test-segmentation-service=SegmentationClient.test_service:test_service",
        ],
    },
)