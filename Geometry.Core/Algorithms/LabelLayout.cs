using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    /// <summary>One row of label text: its width at an ID font size of 1, and its font size as a fraction of the ID's.</summary>
    public readonly struct LabelRow
    {
        /// <summary>Width of the text when its own font size is 1.</summary>
        public readonly double UnitWidth;

        /// <summary>Font size relative to the ID row; the ID row is 1.</summary>
        public readonly double Scale;

        public LabelRow(double unitWidth, double scale)
        {
            UnitWidth = unitWidth;
            Scale = scale;
        }
    }

    /// <summary>What a label must hold: rows of text from top to bottom, and the overlapped-link markers beside them.</summary>
    public sealed class LabelContent
    {
        public readonly IReadOnlyList<LabelRow> Rows;

        /// <summary>Markers for links to sections above.</summary>
        public readonly int UpCount;

        /// <summary>Markers for links to sections below.</summary>
        public readonly int DownCount;

        public LabelContent(IReadOnlyList<LabelRow> rows, int upCount, int downCount)
        {
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            UpCount = upCount;
            DownCount = downCount;
        }

        public int MarkerCount => UpCount + DownCount;

        /// <summary>Width of the widest row at an ID font size of 1.</summary>
        public double TextWidth => Rows.Count == 0 ? 0 : Rows.Max(r => r.UnitWidth * r.Scale);

        /// <summary>Height of all rows at an ID font size of 1.</summary>
        public double TextHeight => Rows.Sum(r => r.Scale);
    }

    public enum LabelPlacementKind
    {
        /// <summary>Text and markers laid out in <see cref="LabelPlacement.Circle"/> as circle annotations are.</summary>
        Circle,

        /// <summary>Text along <see cref="LabelPlacement.Region"/>, with markers at its ends and in free space.</summary>
        Run
    }

    /// <summary>Where one overlapped-link marker is drawn.</summary>
    public readonly struct MarkerSlot
    {
        public readonly Vector2 Center;
        public readonly double Radius;

        /// <summary>True for a link to a section above, false for one below.</summary>
        public readonly bool IsUp;

        public MarkerSlot(Vector2 center, double radius, bool isUp)
        {
            Center = center;
            Radius = radius;
            IsUp = isUp;
        }

        public Circle Circle => new(Center, Radius);
    }

    /// <summary>The layout <see cref="LabelLayout.Fit"/> chose for one <see cref="LabelContent"/>.</summary>
    public sealed class LabelPlacement
    {
        public readonly LabelPlacementKind Kind;

        /// <summary>The polygon's maximum inscribed circle; the layout region when <see cref="Kind"/> is Circle.</summary>
        public readonly Circle Circle;

        /// <summary>The run the text lies along. Only set when <see cref="Kind"/> is Run.</summary>
        public readonly OrientedRectangle Region;

        /// <summary>Font size of the ID row; other rows are this times their <see cref="LabelRow.Scale"/>.</summary>
        public readonly double FontSize;

        /// <summary>The padded box the text occupies. Only set when <see cref="Kind"/> is Run.</summary>
        public readonly OrientedRectangle TextBox;

        /// <summary>Width between the end markers, the most a row may take before it wraps. Only set for runs.</summary>
        public readonly double MaxLineWidth;

        /// <summary>Centre of each <see cref="LabelContent.Rows"/> entry, in order. Only set for runs.</summary>
        public readonly IReadOnlyList<Vector2> RowCenters;

        /// <summary>Every marker for a run: the end markers, then any placed in free space. Empty for circles.</summary>
        public readonly IReadOnlyList<MarkerSlot> Markers;

        /// <summary>Text angle in radians, counter-clockwise from +X, in (−π/2, π/2]. 0 for circles.</summary>
        public double Angle => Kind == LabelPlacementKind.Run ? Region.Angle : 0;

        internal LabelPlacement(Circle circle, double fontSize)
        {
            Kind = LabelPlacementKind.Circle;
            Circle = circle;
            FontSize = fontSize;
            RowCenters = [];
            Markers = [];
        }

        internal LabelPlacement(Circle circle, OrientedRectangle region, double fontSize, OrientedRectangle textBox,
            double maxLineWidth, IReadOnlyList<Vector2> rowCenters, IReadOnlyList<MarkerSlot> markers)
        {
            Kind = LabelPlacementKind.Run;
            Circle = circle;
            Region = region;
            FontSize = fontSize;
            TextBox = textBox;
            MaxLineWidth = maxLineWidth;
            RowCenters = rowCenters;
            Markers = markers;
        }
    }

    /// <summary>
    /// Chooses where a polygon annotation's label and overlapped-link markers go: in its maximum inscribed circle, as for
    /// circle annotations, or along the largest inscribed rectangle ("run") when a long process gives the text more room.
    /// </summary>
    /// <remarks>
    /// Each candidate is scored by the ID font size it allows. A run of width W and height H holding k end markers of
    /// diameter F allows F = min(H / (1.1·th), W / (k + 1.1·tw)), with tw and th the text's size at font 1. A tilted run
    /// scores 0.9 of that, so near-level text snaps to level. A run must beat the circle's R/2 by 25%, so round cells look
    /// as circles do.
    /// </remarks>
    public sealed class LabelLayout
    {
        /// <summary>How much larger a run's score must be than the circle's font size before the run is used.</summary>
        public const double CirclePreference = 1.25;

        /// <summary>Score multiplier for a run that is not level.</summary>
        public const double TiltPenalty = 0.9;

        /// <summary>Room around the text, as a multiple of its size.</summary>
        public const double TextPadding = 1.1;

        /// <summary>End markers are kept while the font is at least this fraction of the font without them.</summary>
        public const double MarkerFontRetention = 0.6;

        /// <summary>End marker radius as a fraction of the marker pitch, which equals the ID font size.</summary>
        public const double EndMarkerRadius = 0.45;

        /// <summary>Smallest free-space marker radius as a fraction of the ID font size, used when no room is left.</summary>
        public const double MinimumMarkerRadius = 0.1;

        private readonly RingSegmentSet Rings;
        private readonly IReadOnlyList<RunCandidate> Runs;
        private readonly Rectangle Bounds;
        private readonly Vector2 Centroid;

        /// <summary>The polygon's maximum inscribed circle.</summary>
        public readonly Circle InscribedCircle;

        /// <summary>Finds the inscribed circle and runs of <paramref name="polygon"/>; <see cref="Fit"/> is then cheap.</summary>
        public LabelLayout(Polygon polygon)
        {
            if (polygon is null)
                throw new ArgumentNullException(nameof(polygon));

            Rings = new RingSegmentSet(polygon);
            Bounds = polygon.BoundingBox;
            Centroid = polygon.Centroid;
            InscribedCircle = polygon.MaximumInscribedCircle();
            Runs = InscribedRuns.Find(polygon);
        }

        /// <summary>ID font size in a circle, as <c>StructureCircleLabels</c> draws it.</summary>
        public static double CircleFontSize(Circle circle) => circle.Radius / 2.0;

        /// <summary>ID font size a <paramref name="width"/> by <paramref name="height"/> run allows with <paramref name="markers"/> end markers.</summary>
        public static double RunFontSize(double width, double height, LabelContent content, int markers)
        {
            double th = Math.Max(content.TextHeight, 1e-9);
            double tw = content.TextWidth;
            return Math.Max(0, Math.Min(height / (TextPadding * th), width / Math.Max(markers + (TextPadding * tw), 1e-9)));
        }

        private static double Score(OrientedRectangle region, LabelContent content, int markers) =>
            RunFontSize(region.Width, region.Height, content, markers) * (region.Angle == 0 ? 1 : TiltPenalty);

        /// <summary>The placement with the largest ID font for <paramref name="content"/>.</summary>
        public LabelPlacement Fit(LabelContent content)
        {
            if (content is null)
                throw new ArgumentNullException(nameof(content));

            double circleFont = CircleFontSize(InscribedCircle);
            if (!TryBestRun(content, 0, out OrientedRectangle region, out double textOnlyScore) ||
                textOnlyScore < CirclePreference * circleFont)
            {
                return new LabelPlacement(InscribedCircle, circleFont);
            }

            int endMarkers = 0;
            for (int k = content.MarkerCount; k > 0; k--)
            {
                if (TryBestRun(content, k, out OrientedRectangle withMarkers, out double score) &&
                    score >= MarkerFontRetention * textOnlyScore)
                {
                    endMarkers = k;
                    region = withMarkers;
                    break;
                }
            }

            return PlaceInRun(region, content, endMarkers);
        }

        /// <summary>
        /// The verified run with the best score for <paramref name="markers"/> end markers. Raster sizes bound the
        /// verified ones from above, so runs are verified best first only until no unverified one could win.
        /// </summary>
        private bool TryBestRun(LabelContent content, int markers, out OrientedRectangle best, out double bestScore)
        {
            best = default;
            bestScore = 0;
            bool found = false;

            IEnumerable<(RunCandidate Run, double Bound)> ranked = Runs
                .Select(r => (Run: r, Bound: RunFontSize(r.Width, r.Height, content, markers) * (r.Angle == 0 ? 1 : TiltPenalty)))
                .OrderByDescending(r => r.Bound);

            foreach ((RunCandidate run, double bound) in ranked)
            {
                if (bound <= bestScore)
                    break;

                if (!run.TryVerify(out OrientedRectangle region))
                    continue;

                double score = Score(region, content, markers);
                if (score > bestScore)
                {
                    best = region;
                    bestScore = score;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Lays out the run's local frame: down markers from the left end, up markers from the right end, rows stacked
        /// top to bottom in the space between, centred on the axis. Markers beyond <paramref name="endMarkers"/> go to the
        /// deepest free spots of the polygon.
        /// </summary>
        private LabelPlacement PlaceInRun(OrientedRectangle region, LabelContent content, int endMarkers)
        {
            double font = RunFontSize(region.Width, region.Height, content, endMarkers);
            int total = content.MarkerCount;
            int downEnd = total == 0 ? 0 : Math.Min(content.DownCount, (int)Math.Round(endMarkers * (double)content.DownCount / total));
            int upEnd = Math.Min(content.UpCount, endMarkers - downEnd);
            downEnd = Math.Min(content.DownCount, endMarkers - upEnd);

            double d = font;
            double halfW = region.Width / 2.0;
            List<MarkerSlot> markers = new(total);
            for (int i = 0; i < downEnd; i++)
                markers.Add(new MarkerSlot(region.LocalToWorld(-halfW + (d / 2.0) + (i * d), 0), EndMarkerRadius * d, false));

            for (int i = 0; i < upEnd; i++)
                markers.Add(new MarkerSlot(region.LocalToWorld(halfW - (d / 2.0) - (i * d), 0), EndMarkerRadius * d, true));

            double textCenterU = (downEnd - upEnd) * d / 2.0;
            double maxLineWidth = region.Width - ((downEnd + upEnd) * d);
            double textHeight = content.TextHeight * font;
            OrientedRectangle textBox = new(region.LocalToWorld(textCenterU, 0),
                Math.Min(maxLineWidth, TextPadding * content.TextWidth * font), TextPadding * textHeight, region.Angle);

            List<Vector2> rowCenters = new(content.Rows.Count);
            double above = 0;
            foreach (LabelRow row in content.Rows)
            {
                double v = (textHeight / 2.0) - ((above + (row.Scale / 2.0)) * font);
                rowCenters.Add(region.LocalToWorld(textCenterU, v));
                above += row.Scale;
            }

            PlaceInFreeSpace(markers, textBox, font, content.DownCount - downEnd, false);
            PlaceInFreeSpace(markers, textBox, font, content.UpCount - upEnd, true);

            return new LabelPlacement(InscribedCircle, region, font, textBox, maxLineWidth, rowCenters, markers);
        }

        /// <summary>
        /// Adds <paramref name="count"/> markers one at a time at the point farthest from the outline, the text box and
        /// every marker placed so far, each as large as that clearance allows up to half the ID font size.
        /// </summary>
        private void PlaceInFreeSpace(List<MarkerSlot> markers, OrientedRectangle textBox, double font, int count, bool isUp)
        {
            if (count <= 0)
                return;

            double tolerance = Math.Max(PoleOfInaccessibility.DefaultTolerance(Bounds), font / 20.0);
            for (int i = 0; i < count; i++)
            {
                Vector2 center = PoleOfInaccessibility.Maximize(Bounds, Clearance, tolerance, Centroid, out double clearance);
                double radius = Math.Max(Math.Min(font / 2.0, 0.95 * clearance), MinimumMarkerRadius * font);
                markers.Add(new MarkerSlot(center, radius, isUp));
            }

            double Clearance(double x, double y)
            {
                Vector2 p = new(x, y);
                double f = Math.Min(Rings.SignedDistance(x, y), textBox.SignedDistance(p));
                foreach (MarkerSlot marker in markers)
                    f = Math.Min(f, Vector2.Distance(p, marker.Center) - marker.Radius);

                return f;
            }
        }
    }
}
