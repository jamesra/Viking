"""Log formatting for SegmentTiles prompts. No heavy imports so tests can load it alone."""

from typing import List, Tuple


def describe_prompts(
    foreground: List[Tuple[int, int]],
    background: List[Tuple[int, int]],
    max_background: int = 12,
) -> str:
    """Volume-space prompt geometry for one SegmentTiles log line.

    Lets a client report ("this spot under-expanded") be matched to a server session
    by coordinates, since the session hash cannot be computed by the client. The first
    foreground point is the circle center for auto-segment and the first centroid for
    grouped proposals. Background points are listed up to max_background so the line
    stays one line.
    """
    if not foreground:
        fg_text = "fg0=none fgbox=none"
    else:
        xs = [int(x) for x, _ in foreground]
        ys = [int(y) for _, y in foreground]
        fg_text = (
            f"fg0=({xs[0]},{ys[0]}) fgbox=({min(xs)},{min(ys)})-({max(xs)},{max(ys)})"
        )
    shown = ",".join(f"({int(x)},{int(y)})" for x, y in background[:max_background])
    more = f"+{len(background) - max_background}" if len(background) > max_background else ""
    return f"{fg_text} bg=[{shown}{more}]"
