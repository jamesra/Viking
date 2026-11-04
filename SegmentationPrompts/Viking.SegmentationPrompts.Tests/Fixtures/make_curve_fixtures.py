"""Write curve_fixtures.json: rings fitted by the training exporter's own curve fit (nornir ``curves.py``).

The library must fit a CURVEPOLYGON exactly as the exporter that made the training masks did, so its tests compare
against the exporter's output rather than a second copy of the rules. Rerun after either side changes:

    python make_curve_fixtures.py D:\\src\\git\\nornir\\nornir-buildmanager
"""

from __future__ import annotations

import enum
import importlib.util
import json
import math
import sys
import types
from pathlib import Path
from typing import Any, Dict, List, Sequence, Tuple

Point = Tuple[float, float]

# Control rings as the WKT stores them: closed (the first point repeated last), mosaic pixels.
RINGS: Dict[str, List[Point]] = {
    "square": [(0, 0), (100, 0), (100, 100), (0, 100), (0, 0)],
    "irregular": [(10, 12), (240.5, -3.25), (310, 140), (205.75, 260), (40, 230), (-20, 120), (10, 12)],
    "concave": [(0, 0), (300, 0), (300, 300), (150, 120), (0, 300), (0, 0)],
    "duplicates": [(0, 0), (0, 0), (50, 0), (50.0004, 0), (100, 50), (50, 100), (0, 50), (0, 0)],
    "sharp_tip": [(0, 0), (400, 10), (0, 20), (-30, 10), (0, 0)],
    "real_cell": [
        (24611.2, 31544.9), (24702.6, 31512.3), (24790.1, 31555.8), (24821.4, 31661.0), (24768.9, 31748.2),
        (24672.5, 31770.4), (24590.7, 31716.6), (24566.3, 31620.1), (24611.2, 31544.9),
    ],
    "triangle": [(5, 5), (60, 5), (30, 50), (5, 5)],
}


def _load_curves(buildmanager: Path) -> Any:
    """``curves.py`` by path, its two type-only imports stubbed, so the rest of nornir need not be installed."""

    class LocationType(enum.IntEnum):
        CURVEPOLYGON = 6

    package = "nornir_buildmanager.operations.segmentationtraining"
    records = types.ModuleType(f"{package}.records")
    records.LocationType = LocationType
    wkt = types.ModuleType(f"{package}.wkt")
    wkt.PolygonRings = tuple
    wkt.Ring = tuple
    sys.modules[f"{package}.records"] = records
    sys.modules[f"{package}.wkt"] = wkt
    path = buildmanager / "nornir_buildmanager" / "operations" / "segmentationtraining" / "curves.py"
    spec = importlib.util.spec_from_file_location(f"{package}.curves", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _fixture(curves: Any, ring: Sequence[Point]) -> Dict[str, Any]:
    fitted = curves.fit_closed_curve_ring(tuple((float(x), float(y)) for x, y in ring))
    assert all(math.isfinite(v) for point in fitted for v in point)
    return {"control": [list(p) for p in ring], "fitted": [list(p) for p in fitted]}


def main(argv: Sequence[str]) -> None:
    curves = _load_curves(Path(argv[1]))
    out = {name: _fixture(curves, ring) for name, ring in RINGS.items()}
    target = Path(__file__).with_name("curve_fixtures.json")
    target.write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8")
    print(f"{target}: {', '.join(f'{k} {len(v["fitted"])}' for k, v in out.items())}")


if __name__ == "__main__":
    main(sys.argv)
