"""
Setup script for the SegmentationServer package.
"""

from setuptools import setup, find_packages

setup(
    name="SegmentationServer",
    version="0.1.0",
    packages=find_packages(),
    install_requires=[
        "grpcio",
        "grpcio-tools",
        "protobuf",
        "numpy",
        "pillow",
        "opencv-python",
        "torch",
        "sam2",
        "segmentation_grpc",
    ],
    python_requires=">=3.7",
    description="Server for the segmentation service",
    author="James Anderson",
    entry_points={
        "console_scripts": [
            "segmentation-server=SegmentationServer.__main__:main",
        ],
    },
)