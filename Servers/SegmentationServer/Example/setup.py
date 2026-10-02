"""Setup script for the segmentation example client."""

from setuptools import setup

setup(
    name="segmentation-example",
    version="0.1.0",
    py_modules=["client_example"],
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
    description="Example client for the segmentation service",
    author="James Anderson",
)
