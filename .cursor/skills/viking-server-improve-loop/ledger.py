"""Read and edit an improvement-loop ledger without hand-written read-modify-write code.

Every write copies the ledger to ``ledger.json.bak``, writes a temp file and renames it into place, and keeps the
file's UTF-8 BOM and line endings. Values are JSON. ``{"value": [...]}`` wrappers left by PowerShell writes are
unwrapped on read.

Usage (run with ``python -I``)::

    ledger.py LEDGER get KEY
    ledger.py LEDGER set KEY JSON
    ledger.py LEDGER append KEY JSON
    ledger.py LEDGER update KEY FIELD=VALUE JSON_OBJECT   # merge fields into list entries whose FIELD equals VALUE
    ledger.py LEDGER remove KEY FIELD=VALUE               # drop list entries whose FIELD equals VALUE
    ledger.py LEDGER incr KEY [N]

KEY may be dotted (``parity.done``). ``update`` and ``remove`` fail when no entry matches, so a typo never passes
silently.
"""

import json
import os
import shutil
import sys
from typing import Any

BOM: bytes = b"\xef\xbb\xbf"


def load(path: str) -> tuple[dict[str, Any], bool, str]:
    """Return (data, has_bom, newline) for the ledger at ``path``."""
    with open(path, "rb") as f:
        raw = f.read()
    has_bom = raw.startswith(BOM)
    newline = "\r\n" if b"\r\n" in raw else "\n"
    data = json.loads(raw.decode("utf-8-sig"))
    for key, value in list(data.items()):
        if isinstance(value, dict) and set(value) == {"value"}:
            data[key] = value["value"]
    return data, has_bom, newline


def save(path: str, data: dict[str, Any], has_bom: bool, newline: str) -> None:
    """Back up ``path``, then replace it atomically with ``data`` in the file's original encoding and line endings."""
    shutil.copyfile(path, path + ".bak")
    text = json.dumps(data, indent=2, ensure_ascii=False).replace("\n", newline)
    tmp = path + ".tmp"
    with open(tmp, "wb") as f:
        if has_bom:
            f.write(BOM)
        f.write(text.encode("utf-8"))
    os.replace(tmp, path)


def resolve(data: dict[str, Any], key: str) -> tuple[dict[str, Any], str]:
    """Return the container holding the last part of dotted ``key``, and that part."""
    parts = key.split(".")
    node = data
    for part in parts[:-1]:
        node = node.setdefault(part, {})
    return node, parts[-1]


def matches(entry: Any, selector: str) -> bool:
    """True when list entry ``entry`` has the field named in ``FIELD=VALUE`` equal to VALUE (compared as text)."""
    field, _, value = selector.partition("=")
    return isinstance(entry, dict) and str(entry.get(field)) == value


def main(argv: list[str]) -> None:
    """Run one ledger command; see the module docstring."""
    if len(argv) < 4:
        sys.exit(__doc__)
    path, command, key = argv[1], argv[2], argv[3]
    args = argv[4:]
    data, has_bom, newline = load(path)
    node, last = resolve(data, key)

    if command == "get":
        sys.stdout.reconfigure(encoding="utf-8")
        print(json.dumps(node.get(last), indent=2, ensure_ascii=False))
        return
    if command == "set":
        node[last] = json.loads(args[0])
    elif command == "append":
        node.setdefault(last, []).append(json.loads(args[0]))
    elif command == "incr":
        node[last] = node.get(last, 0) + (int(args[0]) if args else 1)
    elif command in ("update", "remove"):
        entries = node.get(last, [])
        hits = [e for e in entries if matches(e, args[0])]
        if not hits:
            sys.exit(f"no entry in {key} has {args[0]}")
        if command == "update":
            for entry in hits:
                entry.update(json.loads(args[1]))
        else:
            node[last] = [e for e in entries if not matches(e, args[0])]
    else:
        sys.exit(f"unknown command {command}\n{__doc__}")
    save(path, data, has_bom, newline)


if __name__ == "__main__":
    main(sys.argv)
