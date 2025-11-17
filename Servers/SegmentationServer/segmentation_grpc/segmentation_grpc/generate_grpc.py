"""
Generate gRPC Code

This script generates the Python code for the gRPC service from the proto file.
It uses the protoc compiler to generate the code.
"""

import subprocess
import sys
from pathlib import Path
from typing import List


def generate_grpc_code(force: bool = False) -> bool:
    """Generate the gRPC code from the proto file.

    Args:
        force: Force regeneration even if regenerated files are newer than the proto.

    Returns:
        True if generation succeeded or was skipped because files are up to date, False otherwise.
    """
    current_dir: Path = Path(__file__).resolve().parent

    # Path to the shared proto file
    # Try Docker container path first, then fall back to local development path
    docker_proto_file: Path = Path('/home/user') / 'gRPC_Protos' / 'Segmentation' / 'SAM2' / 'segmentation.proto'
    local_proto_file: Path = current_dir.parents[3] / 'gRPC_Protos' / 'Segmentation' / 'SAM2' / 'segmentation.proto'

    proto_file: Path = docker_proto_file if docker_proto_file.exists() else local_proto_file

    # Check if the proto file exists
    if not proto_file.exists():
        print(f"Error: Proto file not found at {proto_file}")
        return False

    # Figure out if the proto file was modified after the segmentation_pb2_grpc.py file
    if not force:
        pb2_grpc_file: Path = current_dir / 'segmentation_pb2_grpc.py'
        if pb2_grpc_file.exists():
            proto_mtime = proto_file.stat().st_mtime
            pb2_grpc_mtime = pb2_grpc_file.stat().st_mtime
            if proto_mtime <= pb2_grpc_mtime:
                print("No changes detected in the proto file. Skipping code generation.")
                return True

    # Command to generate the gRPC code
    proto_dir: Path = proto_file.parent
    cmd: List[str] = [
        sys.executable,
        '-m',
        'grpc_tools.protoc',
        f'--proto_path={proto_dir}',
        f'--python_out={current_dir}',
        f'--grpc_python_out={current_dir}',
        str(proto_file),
    ]

    try:
        subprocess.check_call(cmd)
        print(f"Successfully generated gRPC code from {proto_file}")

        _fix_imports(current_dir)

        return True
    except subprocess.CalledProcessError as error:
        print(f"Error generating gRPC code: {error}")
        return False
    except Exception as error:  # pylint: disable=broad-except
        print(f"Unexpected error: {error}")
        return False


def _fix_imports(current_dir: Path) -> None:
    """Fix imports in the generated files to use the segmentation_grpc package."""
    pb2_grpc_file: Path = current_dir / 'segmentation_pb2_grpc.py'
    if pb2_grpc_file.exists():
        content = pb2_grpc_file.read_text(encoding='utf-8')

        updated_content = content.replace(
            'import segmentation_pb2 as segmentation__pb2',
            'from segmentation_grpc import segmentation_pb2 as segmentation__pb2'
        )

        if updated_content != content:
            pb2_grpc_file.write_text(updated_content, encoding='utf-8')
            print(f"Fixed imports in {pb2_grpc_file}")


if __name__ == '__main__':
    generate_grpc_code()