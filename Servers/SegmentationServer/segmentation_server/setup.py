"""
Setup script for the segmentation_server package.
"""

from pathlib import Path

from setuptools import setup, find_packages

# Read version from pyproject.toml to keep a single source of truth
def _get_version() -> str:
    pyproject = Path(__file__).resolve().parent / "pyproject.toml"
    for line in pyproject.read_text(encoding="utf-8").splitlines():
        if line.strip().startswith("version ="):
            return line.split("=", 1)[1].strip().strip('"').strip("'")
    return "0.1.0"

setup(
    name="segmentation_server",
    version=_get_version(),
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
            "segmentation-server=segmentation_server.__main__:main",
        ],
    },
)