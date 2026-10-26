# Publication notes: VikingLegacy

The WCF Viking client (released Viking, VikingAU, MonogameTestbed) on the `Legacy` branch of the Viking repo. Material
for future papers: citations, formulas, algorithmic decisions and the reasoning behind them. Entries made before
2026-10-26 are in Viking's notes (`D:\src\git\Viking\docs\publication_notes.md`, `dev`). Index of all projects:
`~/.cursor/publication-notes.md`. Format and rules: the `publication-notes` skill.

## Methods and contributions

## Formulas

- **Location link fade depth** (`Clients/Viking/WebAnnotation/View/LocationLinkView.cs` `FadeDepthAt`, shader
  `PinchedFadeDepth` in `Clients/MonogameXNAGraphicsShared/Content/PinchedLineShaders.fx`): for a point $x$ along a link of
  length $L$ between end circles of radii $r_A, r_B$,
  $t(x) = \operatorname{clamp}\left(\min\left(\frac{x - r_A}{r_A}, \frac{L - x - r_B}{r_B}\right), 0, 1\right)$; drawn
  opacity $\alpha(x) = \alpha_0 \cdot \operatorname{lerp}(1, w, t(x))$ with waist opacity $w = 0$ at rest and $1$ when hovered.
- **Polygon link anchor** (`LocationLinkView.AnchorOnOutline`): with centroid $c$, partner centre $p$, exit point $E$ =
  the crossing of segment $cp$ with the outline farthest from $c$, $d = (p - E)/\lVert p - E \rVert$, entry $N$ = the
  nearest crossing behind $E$ along $-d$, the end circle has radius
  $r = \min(\operatorname{dist}(\tfrac{N + E}{2}, \partial P), \sqrt{A/\pi})$ and centre $E - r\,d$.

## Algorithmic decisions

- **2026-10-26 Links to drawn lines anchor on the line, as wide as the partner's end.** Why: a line or open curve end
  was a circle at its centroid half the line's length across, unrelated to what is drawn. The end now leaves the drawn
  line (polyline vertices, or the open curve smoothed as its view smooths it) at the point $Q$ nearest the partner, from
  the line's edge: radius $r = \max(w/2, r_\text{partner})$, centre $Q + d\,(w/2 - r)$. Rejected (user's choice): a
  bridge only the line's width $w$ thick, which fades out within a line width (the fade runs one end radius) and is too
  small to label. Open curves on an adjacent section are drawn as that half-length circle, so the link uses it there.
  Where: `LocationLinkView.AnchorOnLine`, `AnnotationViewFactory.AdjacentOpenCurveMarkerCircle`.

- **2026-10-26 Links to polygons anchor on the outline.** Why: a link end was the circle at the polygon's centroid with
  the equal-area radius $\sqrt{A/\pi}$, so on a long cell the link's wide end lay under the fill and on a thin cell the
  link began in empty space; adjacent-section polygons are drawn as `CalculateInscribedCircle` (centroid to nearest
  vertex), which the link did not use either. Now the viewed-section end is a circle just inside the point where the
  link's line last leaves the outline, sized by the cell's thickness there (formula above), and the adjacent end is the
  drawn marker circle. Rejected: the centroid-to-nearest-vertex "inscribed" radius for the viewed end (about half the
  diagonal of a rectangle, wider than the cell); the first crossing in list order (`IntersectionPoint`), which is not the
  exit on concave shapes. Where: `LocationLinkView.AnchorOnOutline`, `AnnotationViewFactory.AdjacentPolygonMarkerCircle`.
- **2026-10-26 Link middles are invisible and unpickable until hovered; hover fades over 350 ms.** Why: user feedback on
  RC1 that links cluttered the view between cells. Opacity falls linearly from each marker's edge to 0 one radius on; a
  link at rest cannot be picked where it is below a quarter of its end opacity ($t > 0.75$). Hovering draws it fully and
  makes all of it pickable so the mouse can follow it; the look and the end labels fade at a constant rate over 350 ms,
  so a reversed fade takes the time already spent. Where: `LocationLinkView` (`IsMouseOver`, `HoverAmountAt`).
- **2026-10-26 Fade measured from each marker's edge by its own radius.** Why: the earlier fade by how far the width had
  narrowed from the larger radius started the small end of a link between unequal markers already faded. Where:
  `FadeDepthAt` / `PinchedFadeDepth`.
- **2026-10-26 Link end labels sized in volume space, 0.16 of the marker radius.** Why: fixed screen-pixel labels did not
  zoom with the image as location labels do (user, RC1 location 986804); 0.16 keeps a seven-digit ID inside the strong
  part of the link. Labels under 6 screen pixels are skipped.
- **2026-10-22/26 Link border and shadow colours bypass the HCL blend.** Why: `BlendHCLColorOverBackground` returns the
  background for a colour of zero luma and keeps the background's luma when its luma alpha is 0, so the "contrasting"
  black or white border came out as the image itself or a grey at the image's brightness and barely showed. Border and
  the new outside shadow (5 px, black around light links, white around dark) are composited as plain RGB; the fill keeps
  the luma blend it had at alpha 64 ($\alpha_\text{luma} = 0.75$) so raising opacity does not change its colour. Where:
  `PinchedLineShaders.fx` `MyPSPinchedHSV`.

## Discussion points

## References

## Open questions for a paper
