"""Generate Python gRPC stubs from the shared segmentation.proto."""

from __future__ import annotations

import logging
import subprocess
import sys
from pathlib import Path
from typing import List, Optional

logger = logging.getLogger(__name__)

_DOCKER_PROTO = Path('/home/user') / 'gRPC_Protos' / 'Segmentation' / 'SAM2' / 'segmentation.proto'


def _find_proto_file() -> Optional[Path]:
    """Prefer the Docker mount, then walk parents for the repo's gRPC_Protos copy."""
    if _DOCKER_PROTO.exists():
        return _DOCKER_PROTO
    for parent in Path(__file__).resolve().parents:
        candidate = parent / 'gRPC_Protos' / 'Segmentation' / 'SAM2' / 'segmentation.proto'
        if candidate.exists():
            return candidate
    return None


def generate_grpc_code(force: bool = False) -> bool:
    """Generate segmentation_pb2*.py next to this module.

    Args:
        force: Regenerate even when stubs are newer than the proto.

    Returns:
        True if generation succeeded or was skipped as up to date.
    """
    proto_file = _find_proto_file()
    if proto_file is None:
        logger.error("Proto file not found (looked for gRPC_Protos/Segmentation/SAM2/segmentation.proto)")
        return False

    current_dir = Path(__file__).resolve().parent
    if not force:
        pb2_grpc_file = current_dir / 'segmentation_pb2_grpc.py'
        if pb2_grpc_file.exists() and proto_file.stat().st_mtime <= pb2_grpc_file.stat().st_mtime:
            logger.info("No changes detected in the proto file. Skipping code generation.")
            return True

    cmd: List[str] = [
        sys.executable,
        '-m',
        'grpc_tools.protoc',
        f'--proto_path={proto_file.parent}',
        f'--python_out={current_dir}',
        f'--grpc_python_out={current_dir}',
        str(proto_file),
    ]

    try:
        subprocess.check_call(cmd)
        logger.info("Successfully generated gRPC code from %s", proto_file)
        _fix_imports(current_dir)
        return True
    except subprocess.CalledProcessError as error:
        logger.error("Error generating gRPC code: %s", error)
        return False
    except OSError as error:
        logger.error("Unexpected error generating gRPC code: %s", error)
        return False


def _fix_imports(current_dir: Path) -> None:
    """Rewrite the protoc import so stubs load as part of the segmentation_grpc package."""
    pb2_grpc_file = current_dir / 'segmentation_pb2_grpc.py'
    if not pb2_grpc_file.exists():
        return
    content = pb2_grpc_file.read_text(encoding='utf-8')
    updated_content = content.replace(
        'import segmentation_pb2 as segmentation__pb2',
        'from segmentation_grpc import segmentation_pb2 as segmentation__pb2',
    )
    if updated_content != content:
        pb2_grpc_file.write_text(updated_content, encoding='utf-8')
        logger.info("Fixed imports in %s", pb2_grpc_file)


if __name__ == '__main__':
    logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")
    sys.exit(0 if generate_grpc_code(force=True) else 1)
