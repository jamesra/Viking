"""One-off migration of the disk embedding cache from format 1 (torch.save ``.pt``) to format 2 (``.emb``).

Run with the server stopped, against the cache root it uses::

    python3 -m segmentation_server.migrate_embedding_cache /var/cache/segmentation-embeddings [--workers N]

Each ``.pt`` file is read, re-encoded with :mod:`segmentation_server.embedding_codec`, checked to decode to the
same tensors bit for bit, written beside it as ``.emb`` with the old file's mtime (the store's LRU order), and
only then deleted. A file that cannot be read or does not round-trip is deleted, the same as the server does with
an unreadable blob, so nothing in the old format is left behind. Running it again is harmless: it only touches
``.pt`` files.
"""

from __future__ import annotations

import argparse
import os
import sys
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
from typing import List, Tuple

import torch

from segmentation_server import embedding_codec
from segmentation_server.embedding_store import EMBEDDING_SUFFIX

_OLD_SUFFIX = ".pt"
_OLD_FORMAT = 1


def migrate_file(path_text: str) -> Tuple[str, int, int]:
    """Convert one ``.pt`` blob; returns (outcome, old bytes, new bytes) with outcome ``migrated`` or ``dropped``."""
    path = Path(path_text)
    stat = path.stat()
    target = path.with_suffix(EMBEDDING_SUFFIX)
    try:
        payload = torch.load(path, map_location="cpu", weights_only=True)
        found = payload.get("format_version")
        if found is None or int(found.item()) != _OLD_FORMAT:
            raise ValueError(f"format {found} is not {_OLD_FORMAT}")
        blob = embedding_codec.encode(payload)
        restored = embedding_codec.decode(blob)
        for name in embedding_codec.tensor_names(payload):
            if restored[name].dtype != payload[name].dtype or not torch.equal(restored[name], payload[name]):
                raise ValueError(f"{name} does not round-trip")
        if restored["orig_hw"].tolist() != payload["orig_hw"].tolist():
            raise ValueError("orig_hw does not round-trip")
    except Exception as error:  # an unreadable blob is dropped, as the server would drop it
        print(f"dropped {path}: {error}", file=sys.stderr)
        path.unlink(missing_ok=True)
        return "dropped", stat.st_size, 0
    temporary = target.with_suffix(target.suffix + ".tmp")
    temporary.write_bytes(blob)
    os.utime(temporary, ns=(stat.st_atime_ns, stat.st_mtime_ns))
    os.replace(temporary, target)
    path.unlink()
    return "migrated", stat.st_size, len(blob)


def find_old_files(root: Path) -> List[str]:
    """Every format-1 blob under ``root`` (tile PNGs and other stores are left alone)."""
    return [str(path) for path in root.rglob(f"*{_OLD_SUFFIX}") if path.is_file()]


def main(argv: List[str]) -> int:
    """Migrate every ``.pt`` blob under the given cache root and print a summary."""
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("root", type=Path)
    parser.add_argument("--workers", type=int, default=max(1, (os.cpu_count() or 2) // 2))
    args = parser.parse_args(argv)
    files = find_old_files(args.root)
    counts = {"migrated": 0, "dropped": 0}
    old_bytes = new_bytes = 0
    with ProcessPoolExecutor(max_workers=args.workers) as pool:
        for index, (outcome, before, after) in enumerate(pool.map(migrate_file, files, chunksize=16), start=1):
            counts[outcome] += 1
            old_bytes += before
            new_bytes += after
            if index % 500 == 0:
                print(f"{index}/{len(files)}", flush=True)
    print(
        f"migrated={counts['migrated']} dropped={counts['dropped']} "
        f"old={old_bytes / 2**30:.2f} GiB new={new_bytes / 2**30:.2f} GiB"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
