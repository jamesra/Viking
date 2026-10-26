"""Generate Python gRPC stubs from the shared segmentation.proto and the geometry proto it imports.

The protos live under ``gRPC_Protos`` and import each other relative to that root
(``segmentation.proto`` imports ``Common/geometry.proto``). protoc is run with that root as the import path, so the
generated modules first land in ``Segmentation/SAM2/`` and ``Common/`` of a scratch directory; they are then copied
flat into this package (``segmentation_pb2.py``, ``segmentation_pb2_grpc.py``, ``geometry_pb2.py``) and their imports
rewritten to ``from segmentation_grpc import ...``. The descriptor pool still names the files by their paths under
``gRPC_Protos``, so the import of ``Common/geometry.proto`` resolves the same way it does for every other language.
"""

from __future__ import annotations

import logging
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import List, Optional

logger = logging.getLogger(__name__)

# Protos relative to the gRPC_Protos root, and the module each generates.
SEGMENTATION_PROTO = Path('Segmentation') / 'SAM2' / 'segmentation.proto'
GEOMETRY_PROTO = Path('Common') / 'geometry.proto'

_DOCKER_ROOT = Path('/home/user') / 'gRPC_Protos'

# (module protoc writes, relative to the output root) -> (module name in this package)
_GENERATED = {
    Path('Segmentation') / 'SAM2' / 'segmentation_pb2.py': 'segmentation_pb2.py',
    Path('Segmentation') / 'SAM2' / 'segmentation_pb2_grpc.py': 'segmentation_pb2_grpc.py',
    Path('Common') / 'geometry_pb2.py': 'geometry_pb2.py',
}

# protoc's import lines for the nested layout, and what they become in the flat package.
_IMPORT_REWRITES = (
    (
        'from Common import geometry_pb2 as Common_dot_geometry__pb2',
        'from segmentation_grpc import geometry_pb2 as Common_dot_geometry__pb2',
    ),
    (
        'from Segmentation.SAM2 import segmentation_pb2 as Segmentation_dot_SAM2_dot_segmentation__pb2',
        'from segmentation_grpc import segmentation_pb2 as Segmentation_dot_SAM2_dot_segmentation__pb2',
    ),
)


def _find_proto_root() -> Optional[Path]:
    """The ``gRPC_Protos`` directory: the Docker copy first, then the repository's, found by walking up."""
    if (_DOCKER_ROOT / SEGMENTATION_PROTO).exists():
        return _DOCKER_ROOT
    for parent in Path(__file__).resolve().parents:
        candidate = parent / 'gRPC_Protos'
        if (candidate / SEGMENTATION_PROTO).exists():
            return candidate
    return None


def _find_proto_file() -> Optional[Path]:
    """The shared segmentation.proto, or None when no gRPC_Protos checkout is found."""
    root = _find_proto_root()
    return None if root is None else root / SEGMENTATION_PROTO


def generate_grpc_code(force: bool = False) -> bool:
    """Generate segmentation_pb2*.py and geometry_pb2.py next to this module.

    Args:
        force: Regenerate even when stubs are newer than the protos.

    Returns:
        True if generation succeeded or was skipped as up to date.
    """
    root = _find_proto_root()
    if root is None:
        logger.error("Proto file not found (looked for gRPC_Protos/%s)", SEGMENTATION_PROTO.as_posix())
        return False
    protos = [root / SEGMENTATION_PROTO, root / GEOMETRY_PROTO]
    missing = [proto for proto in protos if not proto.exists()]
    if missing:
        logger.error("Proto file(s) not found: %s", ", ".join(str(proto) for proto in missing))
        return False

    current_dir = Path(__file__).resolve().parent
    if not force:
        outputs = [current_dir / name for name in _GENERATED.values()]
        newest_proto = max(proto.stat().st_mtime for proto in protos)
        if all(output.exists() for output in outputs) and newest_proto <= min(o.stat().st_mtime for o in outputs):
            logger.info("No changes detected in the proto files. Skipping code generation.")
            return True

    with tempfile.TemporaryDirectory(prefix='segmentation_grpc_') as scratch:
        out = Path(scratch)
        cmd: List[str] = [
            sys.executable,
            '-m',
            'grpc_tools.protoc',
            f'--proto_path={root}',
            f'--python_out={out}',
            f'--grpc_python_out={out}',
            *(str(proto) for proto in protos),
        ]
        try:
            subprocess.check_call(cmd)
        except subprocess.CalledProcessError as error:
            logger.error("Error generating gRPC code: %s", error)
            return False
        except OSError as error:
            logger.error("Unexpected error generating gRPC code: %s", error)
            return False

        for generated, name in _GENERATED.items():
            source = out / generated
            if not source.exists():
                # protoc writes no *_pb2_grpc.py content worth keeping for a file without services; it still writes
                # the file, so a missing one is a protoc problem.
                logger.error("protoc did not write %s", generated.as_posix())
                return False
            shutil.copyfile(source, current_dir / name)
    logger.info("Successfully generated gRPC code from %s", root)
    _fix_imports(current_dir)
    return True


def _fix_imports(current_dir: Path) -> None:
    """Rewrite protoc's nested-package imports so the stubs load as modules of the segmentation_grpc package."""
    for name in _GENERATED.values():
        path = current_dir / name
        if not path.exists():
            continue
        content = path.read_text(encoding='utf-8')
        updated = content
        for old, new in _IMPORT_REWRITES:
            updated = updated.replace(old, new)
        if updated != content:
            path.write_text(updated, encoding='utf-8')
            logger.info("Fixed imports in %s", path)


if __name__ == '__main__':
    logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")
    sys.exit(0 if generate_grpc_code(force=True) else 1)
