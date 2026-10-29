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

        /// <summary>
        /// <paramref name="rows"/> must not change afterwards: <see cref="TextWidth"/> and <see cref="TextHeight"/> are
        /// measured here, once, because <see cref="LabelLayout"/> reads them for every candidate run.
        /// </summary>
        public LabelContent(IReadOnlyList<LabelRow> rows, int upCount, int downCount)
        {
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            UpCount = upCount;
            DownCount = downCount;
            TextWidth = rows.Count == 0 ? 0 : rows.Max(r => r.UnitWidth * r.Scale);
            TextHeight = rows.Sum(r => r.Scale);
        }

        public int MarkerCount => UpCount + DownCount;

        /// <summary>Width of the widest row at an ID font size of 1.</summary>
        public readonly double TextWidth;

        /// <summary>Height of all rows at an ID font size of 1.</summary>
        public readonly double TextHeight;

        /// <summary>True when the rows have the same font scales in the same order, so they stack the same way.</summary>
        internal bool SameRowScales(LabelContent other)
        {
            if (other.Rows.Count != Rows.Count)
                return false;

            for (int i = 0; i < Rows.Count; i++)
            {
                if (other.Rows[i].Scale != Rows[i].Scale)
                    return false;
            }

            return true;
        }

        /// <summary>True when both have the same up and down marker counts, so a placement's marker slots fit both.</summary>
        internal bool SameMarkerCounts(LabelContent other) => other.UpCount == UpCount && other.DownCount == DownCount;
    }

    public enum LabelPlacementKind
    {
        /// <summary>
        /// Text laid out in <see cref="LabelPlacement.Circle"/> as circle annotations are, up-link markers above it and
        /// down-link markers below.
        /// </summary>
        Circle,

        /// <summary>Text along <see cref="LabelPlacement.Region"/>, with markers at its ends and in free space.</summary>
        Run
    }

    /// <summary>
    /// How a placement packs its overlapped-link markers: up to <see cref="Rows"/> hex-packed rows per direction, each
    /// marker <see cref="Scale"/> of the full size. A second row sits half a marker along, in the gaps of the first, so it
    /// adds √3/2 of a marker to the height rather than a whole one. In a run the rows lie along the axis at each end; in
    /// the circle they are arcs above and below the text, moving outward from it.
    /// </summary>
    public readonly struct MarkerPacking
    {
        /// <summary>Most rows one direction's markers may use; with fewer markers than rows, each marker has a row.</summary>
        public readonly int Rows;

        /// <summary>
        /// Marker size as a fraction of the full size: of a pitch equal to the ID font in a run, of a radius R/6 in the
        /// circle (as circle annotations draw them). Between <see cref="LabelLayout.MinimumMarkerScale"/> and 1.
        /// </summary>
        public readonly double Scale;

        public MarkerPacking(int rows, double scale)
        {
            Rows = rows;
            Scale = scale;
        }

        /// <summary>One row of full-size markers: the layout before 2026-10-29, and still the first one tried.</summary>
        public static MarkerPacking Single => new(1, 1.0);

        public override string ToString() => $"{Rows} row(s) at {Scale:F2}";
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

    /// <summary>The layout <see cref="LabelLayout.Fit(LabelContent, double)"/> chose for one <see cref="LabelContent"/>.</summary>
    public sealed class LabelPlacement
    {
        /// <summary>The layout that made this placement; a previous placement from another layout is never kept.</summary>
        internal readonly LabelLayout Owner;

        /// <summary>What the placement was laid out for: its row scales, marker counts and text width.</summary>
        internal readonly LabelContent Content;

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

        /// <summary>
        /// Every marker, one per link to another section: first the packed ones (at the run's ends, or above and below the
        /// circle's text) in a fixed order for the same placement, then the last <see cref="FreeMarkerCount"/>, placed in
        /// free space. Each lies inside the polygon and clear of its holes.
        /// </summary>
        public readonly IReadOnlyList<MarkerSlot> Markers;

        /// <summary>How the packed markers are laid out.</summary>
        public readonly MarkerPacking Packing;

        /// <summary>How many of <see cref="Markers"/>, at its end, did not fit the packing and went to free space.</summary>
        public readonly int FreeMarkerCount;

        /// <summary>
        /// Score factor for how readable the markers are, 1 when every marker is packed at full size (or there are none),
        /// lower for shrunken markers and markers in free space; see <see cref="LabelLayout.MarkerReadability"/>.
        /// </summary>
        public readonly double MarkerReadability;

        /// <summary>Text angle in radians, counter-clockwise from +X, in (−π/2, π/2]. 0 for circles.</summary>
        public double Angle => Kind == LabelPlacementKind.Run ? Region.Angle : 0;

        /// <summary>Direction the text reads along: the run's axis, or +X for the circle. Markers are ordered along it.</summary>
        public Vector2 Axis => Kind == LabelPlacementKind.Run ? Region.AxisU : new Vector2(1, 0);

        internal LabelPlacement(LabelLayout owner, LabelContent content, Circle circle, double fontSize,
            IReadOnlyList<MarkerSlot> markers, MarkerPacking packing, int freeMarkerCount, double markerReadability)
        {
            Owner = owner;
            Content = content;
            Kind = LabelPlacementKind.Circle;
            Circle = circle;
            FontSize = fontSize;
            RowCenters = [];
            Markers = markers;
            Packing = packing;
            FreeMarkerCount = freeMarkerCount;
            MarkerReadability = markerReadability;
        }

        internal LabelPlacement(LabelLayout owner, LabelContent content, Circle circle, OrientedRectangle region, double fontSize,
            OrientedRectangle textBox, double maxLineWidth, IReadOnlyList<Vector2> rowCenters, IReadOnlyList<MarkerSlot> markers,
            MarkerPacking packing, int freeMarkerCount, double markerReadability)
        {
            Owner = owner;
            Content = content;
            Kind = LabelPlacementKind.Run;
            Circle = circle;
            Region = region;
            FontSize = fontSize;
            TextBox = textBox;
            MaxLineWidth = maxLineWidth;
            RowCenters = rowCenters;
            Markers = markers;
            Packing = packing;
            FreeMarkerCount = freeMarkerCount;
            MarkerReadability = markerReadability;
        }
    }

    /// <summary>
    /// Chooses where a polygon annotation's label and overlapped-link markers go: in its maximum inscribed circle, as for
    /// circle annotations, or along the largest inscribed rectangle ("run") when a long process gives the text more room.
    /// </summary>
    /// <remarks>
    /// Each candidate is scored by the ID font size it allows. A run of width W and height H holding k end markers of
    /// diameter F allows F = min(H / (1.1·th), W / (k + 1.1·tw)), with tw and th the text's size at font 1. A run tilted
    /// by θ scores F / (1 + G·|sin θ|), so it must beat level text by a gain that grows with the angle: with the default
    /// G = 0.5, 13% at 15°, 25% at 30°, 35% at 45° and 50% at 90°. Near-level runs pay almost nothing, and a long thin
    /// process, which gains several-fold along its length, still rotates. G = ∞ keeps text level. A run's font must beat
    /// the circle's R/2 by 25%, so round cells look as circles do, or by its required gain when that is larger.
    /// <para>
    /// Markers: in a run, down-links at the left end and up-links at the right; in the circle, up-links above the text and
    /// down-links below, on the arcs circle annotations use. When one row of full-size markers does not fit (in a run, it
    /// would cost more than <see cref="MarkerFontRetention"/> of the text-only font; in the circle, it would leave its half
    /// or the polygon), the <see cref="PackingOrder"/> tries a second row staggered by half a marker, then smaller markers
    /// in more rows, down to <see cref="MinimumMarkerScale"/>, and only then sends markers to the next-deepest free spots.
    /// Every marker is checked against the real outline and holes. Shrunken and free-space markers lower the placement's
    /// score by <see cref="MarkerReadability"/>, so a run and the circle are compared with their markers' cost.
    /// </para>
    /// <para>
    /// Given the label's previous placement, <see cref="Fit(LabelContent, double, LabelPlacement)"/> keeps it while it
    /// still holds the content and the new best beats it by no more than 1 + <see cref="Hysteresis"/>, so a label near a
    /// tie does not flip back and forth while the Label rotation slider moves.
    /// </para>
    /// </remarks>
    public sealed class LabelLayout
    {
        /// <summary>How much larger a run's score must be than the circle's font size before the run is used.</summary>
        public const double CirclePreference = 1.25;

        /// <summary>
        /// Rotation gain G that <see cref="Fit(LabelContent)"/> uses: a run tilted by θ needs 1 + G·|sin θ| times the font
        /// of level text to win. It is the gain at the middle of the Label rotation preference.
        /// </summary>
        public const double DefaultRotationGain = 0.5;

        /// <summary>
        /// Rotation gain just above the "Never" end of the preference; the gain falls linearly from it to 0 at "Freely".
        /// The setting 0 itself means <see cref="NeverRotate"/>.
        /// </summary>
        public const double MaxRotationGain = 1.0;

        /// <summary>Rotation gain that keeps every label level: only level runs and the circle are considered.</summary>
        public const double NeverRotate = double.PositiveInfinity;

        /// <summary>Room around the text, as a multiple of its size.</summary>
        public const double TextPadding = 1.1;

        /// <summary>
        /// A run's end-marker packing is used while the font it leaves is at least this fraction of the font without
        /// markers; the first packing in <see cref="PackingOrder"/> that keeps it wins.
        /// </summary>
        public const double MarkerFontRetention = 0.6;

        /// <summary>End marker radius as a fraction of the marker pitch, which equals the ID font size at full size.</summary>
        public const double EndMarkerRadius = 0.45;

        /// <summary>Smallest free-space marker radius as a fraction of the ID font size, used when no room is left.</summary>
        public const double MinimumMarkerRadius = 0.1;

        /// <summary>
        /// Smallest packed marker, as a fraction of the full size: in a run a pitch of half the ID font, in the circle a
        /// radius of R/12, a quarter of the circle's ID font. Below it the section number in a marker is hard to read, so
        /// markers that still do not fit go to free space instead of shrinking further.
        /// </summary>
        public const double MinimumMarkerScale = 0.5;

        /// <summary>Most hex-packed rows of markers per direction (per run end, or per side of the circle's text).</summary>
        public const int MaxMarkerRows = 4;

        /// <summary>Distance between hex-packed rows as a fraction of the marker pitch: √3/2.</summary>
        public const double MarkerRowSpacing = 0.86602540378443865;

        /// <summary>
        /// Score cost of unreadable markers: a placement scores 1 − c·(1 − m̄), with m̄ the mean marker scale, a free-space
        /// marker counting <see cref="FreeSpaceMarkerScale"/>. At c = 0.25 markers at <see cref="MinimumMarkerScale"/>
        /// cost 12.5%, all markers in free space 19%.
        /// </summary>
        public const double MarkerShrinkCost = 0.25;

        /// <summary>What a marker in free space counts as in <see cref="MarkerReadability"/>: away from its row it is hard to find.</summary>
        public const double FreeSpaceMarkerScale = 0.25;

        /// <summary>Full-size circle marker radius as a fraction of the circle's, as <c>OverlappedLinkCircleView</c> draws circle annotations.</summary>
        public const double CircleMarkerRadius = 1.0 / 6.0;

        /// <summary>Radius of the arc full-size circle markers sit on, as a fraction of the circle's, as for circle annotations.</summary>
        public const double CircleMarkerArc = 0.75;

        /// <summary>
        /// Half the height of the circle's text, as a fraction of its radius: the inner edge of the full-size marker arc.
        /// Circle marker rows start here and move outward.
        /// </summary>
        public const double CircleTextHalfHeight = CircleMarkerArc - CircleMarkerRadius;

        /// <summary>Marker sizes tried, largest first.</summary>
        private static readonly double[] MarkerScales = [1.0, 0.75, MinimumMarkerScale];

        /// <summary>
        /// Packings tried for markers, most readable first: by marker size, largest first, then by row count, fewest
        /// first. The first is <see cref="MarkerPacking.Single"/>, then a staggered second row at full size, then smaller
        /// markers in one to <see cref="MaxMarkerRows"/> rows.
        /// </summary>
        public static readonly IReadOnlyList<MarkerPacking> PackingOrder =
            [.. MarkerScales.SelectMany(s => Enumerable.Range(1, MaxMarkerRows).Select(rows => new MarkerPacking(rows, s)))];

        /// <summary>
        /// Hysteresis h: a label keeps its previous placement unless the new best is more than 1 + h times better by
        /// <see cref="Advantage"/>. 5% hides near-ties without holding text in a placement clearly worse than the best.
        /// </summary>
        public const double Hysteresis = 0.05;

        private readonly RingSegmentSet Rings;
        private readonly RunCandidate[] Runs;

        /// <summary>|sin| of each <see cref="Runs"/> entry's angle, for its tilt factor without a sine per fit.</summary>
        private readonly double[] RunSin;
        private readonly Rectangle Bounds;
        private readonly Vector2 Centroid;

        /// <summary>The polygon's maximum inscribed circle.</summary>
        public readonly Circle InscribedCircle;

        /// <summary>Finds the inscribed circle and runs of <paramref name="polygon"/>; <see cref="Fit(LabelContent, double)"/> is then cheap.</summary>
        public LabelLayout(Polygon polygon)
        {
            if (polygon is null)
                throw new ArgumentNullException(nameof(polygon));

            Rings = new RingSegmentSet(polygon);
            Bounds = polygon.BoundingBox;
            Centroid = polygon.Centroid;
            InscribedCircle = polygon.MaximumInscribedCircle();
            Runs = [.. InscribedRuns.Find(polygon)];
            RunSin = [.. Runs.Select(r => Math.Abs(Math.Sin(r.Angle)))];
        }

        /// <summary>ID font size in a circle, as <c>StructureCircleLabels</c> draws it.</summary>
        public static double CircleFontSize(Circle circle) => circle.Radius / 2.0;

        /// <summary>
        /// ID font size a <paramref name="width"/> by <paramref name="height"/> run allows with <paramref name="markers"/>
        /// full-size end markers in one row.
        /// </summary>
        public static double RunFontSize(double width, double height, LabelContent content, int markers) =>
            RunFontSize(width, height, content, markers, markers > 0 ? 2 * EndMarkerRadius : 0);

        /// <summary>
        /// ID font size a <paramref name="width"/> by <paramref name="height"/> run allows when its end markers take
        /// <paramref name="markerWidth"/> times the font of its length and <paramref name="markerHeight"/> times the font
        /// of its thickness (see <see cref="EndBlockWidth"/> and <see cref="EndBlockHeight"/>). The height limit keeps every
        /// marker inside the run, and so inside the polygon, whatever the text's height.
        /// </summary>
        public static double RunFontSize(double width, double height, LabelContent content, double markerWidth, double markerHeight)
        {
            double th = Math.Max(content.TextHeight, 1e-9);
            double tw = content.TextWidth;
            double font = Math.Min(height / (TextPadding * th), width / Math.Max(markerWidth + (TextPadding * tw), 1e-9));
            if (markerHeight > 0)
                font = Math.Min(font, height / markerHeight);

            return Math.Max(0, font);
        }

        /// <summary>
        /// Where marker <paramref name="j"/> of a run end's <paramref name="count"/> sits in a block of up to
        /// <paramref name="rows"/> hex-packed rows, in marker pitches: <c>U</c> from the run's end inward to its centre,
        /// <c>V</c> across the axis from it. The block fills column by column, odd rows half a pitch further in, so
        /// neighbours touch and a second row adds <see cref="MarkerRowSpacing"/> to the height.
        /// </summary>
        public static (double U, double V) EndBlockSlot(int j, int count, int rows)
        {
            int used = Math.Max(1, Math.Min(rows, count));
            int column = j / used;
            int row = j % used;
            return (column + (row % 2 == 1 ? 0.5 : 0) + 0.5, (((used - 1) / 2.0) - row) * MarkerRowSpacing);
        }

        /// <summary>Length along the run, in marker pitches, of an end block of <paramref name="count"/> markers in up to <paramref name="rows"/> rows.</summary>
        public static double EndBlockWidth(int count, int rows)
        {
            double width = 0;
            for (int j = 0; j < count; j++)
                width = Math.Max(width, EndBlockSlot(j, count, rows).U + 0.5);

            return width;
        }

        /// <summary>Thickness across the run, in marker pitches, of the drawn markers of an end block.</summary>
        public static double EndBlockHeight(int count, int rows) =>
            count <= 0 ? 0 : ((Math.Min(rows, count) - 1) * MarkerRowSpacing) + (2 * EndMarkerRadius);

        /// <summary>
        /// Score factor for markers <paramref name="packed"/> of which are at <paramref name="scale"/> and
        /// <paramref name="free"/> in free space: 1 − <see cref="MarkerShrinkCost"/>·(1 − mean scale), a free marker
        /// counting <see cref="FreeSpaceMarkerScale"/>. 1 with no markers.
        /// </summary>
        public static double MarkerReadability(int packed, double scale, int free)
        {
            int count = packed + free;
            if (count <= 0)
                return 1;

            double mean = ((packed * scale) + (free * FreeSpaceMarkerScale)) / count;
            return 1 - (MarkerShrinkCost * (1 - mean));
        }

        /// <summary>
        /// Rotation gain G for the Label rotation preference <paramref name="setting"/>, 0 ("Never") to 1 ("Freely"):
        /// <see cref="NeverRotate"/> at 0 or below, otherwise (1 − setting)·<see cref="MaxRotationGain"/>, so 0.5 gives
        /// <see cref="DefaultRotationGain"/> and 1 gives 0, where any gain in font size rotates. NaN reads as the default.
        /// </summary>
        public static double RotationGainForSetting(double setting)
        {
            if (double.IsNaN(setting))
                return DefaultRotationGain;

            if (setting <= 0)
                return NeverRotate;

            return (1.0 - Math.Min(setting, 1.0)) * MaxRotationGain;
        }

        /// <summary>
        /// How many times the font of level text a run tilted by <paramref name="angle"/> radians must allow to win with
        /// rotation gain <paramref name="rotationGain"/>: 1 + G·|sin θ|. Infinite for a tilted run when G is
        /// <see cref="NeverRotate"/>; always 1 for level text.
        /// </summary>
        public static double RequiredGain(double angle, double rotationGain)
        {
            double sin = Math.Abs(Math.Sin(angle));
            if (sin == 0)
                return 1;

            return double.IsPositiveInfinity(rotationGain) ? double.PositiveInfinity : 1 + (Math.Max(0, rotationGain) * sin);
        }

        /// <summary>Score multiplier for a run at <paramref name="angle"/>: 1 / <see cref="RequiredGain"/>, so at most 1.</summary>
        public static double TiltFactor(double angle, double rotationGain) => 1.0 / RequiredGain(angle, rotationGain);

        private static double Score(OrientedRectangle region, LabelContent content, EndMarkers ends, double rotationGain) =>
            RunFontSize(region.Width, region.Height, content, ends.WidthUnits, ends.HeightUnits) * TiltFactor(region.Angle, rotationGain);

        /// <summary>
        /// The markers a run holds at its ends and how they are packed, with the length and thickness the two end blocks
        /// take in units of the ID font.
        /// </summary>
        private readonly struct EndMarkers
        {
            public readonly int Down;
            public readonly int Up;
            public readonly MarkerPacking Packing;

            /// <summary>Length of both end blocks together, times the ID font.</summary>
            public readonly double WidthUnits;

            /// <summary>Thickness of the thicker end block, times the ID font.</summary>
            public readonly double HeightUnits;

            public EndMarkers(int down, int up, MarkerPacking packing)
            {
                Down = down;
                Up = up;
                Packing = packing;
                WidthUnits = packing.Scale * (EndBlockWidth(down, packing.Rows) + EndBlockWidth(up, packing.Rows));
                HeightUnits = packing.Scale * Math.Max(EndBlockHeight(down, packing.Rows), EndBlockHeight(up, packing.Rows));
            }

            public static EndMarkers None => new(0, 0, MarkerPacking.Single);
        }

        /// <summary>The placement with the largest ID font for <paramref name="content"/>, at <see cref="DefaultRotationGain"/>.</summary>
        public LabelPlacement Fit(LabelContent content) => Fit(content, DefaultRotationGain);

        /// <summary>
        /// The placement with the largest ID font for <paramref name="content"/>, a tilted run scoring its font times
        /// <see cref="TiltFactor"/> for <paramref name="rotationGain"/>. With <see cref="NeverRotate"/> only level runs are
        /// considered, so the result is the widest level box or the inscribed circle.
        /// </summary>
        public LabelPlacement Fit(LabelContent content, double rotationGain)
        {
            if (content is null)
                throw new ArgumentNullException(nameof(content));

            if (double.IsNaN(rotationGain))
                rotationGain = DefaultRotationGain;

            // The circle is level text, so a tilted run must beat it by its required gain, as it must beat a level run.
            // The two preferences are not multiplied: a run needs the larger of the circle preference and that gain.
            // Multiplied, a thin vertical process asks 1.25 × 1.5 = 1.875 over its circle, which a two-line label
            // (run font H/2.2 against circle font H/4, 1.82) never reaches, so its text would shrink into the circle.
            // Both sides carry their markers' readability, so shrunken or scattered markers count against a placement;
            // the run's side is its text-only font F0, as before markers were scored, so full-size markers on both
            // sides decide exactly as before.
            LabelPlacement circle = PlaceInCircle(content);
            if (!TryBestRun(content, EndMarkers.None, rotationGain, out OrientedRectangle region, out double textOnlyScore))
                return circle;

            double runFont = RunFontSize(region.Width, region.Height, content, 0);
            double circleAsks = CircleThreshold(region.Angle, rotationGain) * circle.FontSize * circle.MarkerReadability;
            if (runFont < circleAsks)
                return circle;

            EndMarkers ends = ChooseEndMarkers(content, rotationGain, textOnlyScore, ref region);
            LabelPlacement run = PlaceInRun(region, content, ends);
            return runFont * run.MarkerReadability < circleAsks ? circle : run;
        }

        /// <summary>
        /// The end markers for a run and the run they fit best, trying in order: every marker in each
        /// <see cref="PackingOrder"/> packing (one row, a staggered second row, then smaller markers in more rows); then
        /// fewer markers at <see cref="MinimumMarkerScale"/>, the rest going to free space; then none. A packing is used
        /// when its best run keeps <see cref="MarkerFontRetention"/> of <paramref name="textOnlyScore"/> and every marker
        /// it places clears the outline and holes. <paramref name="region"/> is the text-only run on entry and the run
        /// chosen on return.
        /// </summary>
        private EndMarkers ChooseEndMarkers(LabelContent content, double rotationGain, double textOnlyScore, ref OrientedRectangle region)
        {
            int total = content.MarkerCount;
            if (total == 0)
                return EndMarkers.None;

            double floor = MarkerFontRetention * textOnlyScore;
            List<(double Width, double Height)> failed = [];
            if (TryPackings(content.DownCount, content.UpCount, PackingOrder, out EndMarkers ends, out OrientedRectangle fitted))
            {
                region = fitted;
                return ends;
            }

            MarkerPacking[] smallest = [.. PackingOrder.Where(p => p.Scale == MinimumMarkerScale)];
            for (int k = total - 1; k > 0; k--)
            {
                int down = Math.Min(content.DownCount, (int)Math.Round(k * (double)content.DownCount / total));
                int up = Math.Min(content.UpCount, k - down);
                down = Math.Min(content.DownCount, k - up);
                failed.Clear();
                if (TryPackings(down, up, smallest, out ends, out fitted))
                {
                    region = fitted;
                    return ends;
                }
            }

            return EndMarkers.None;

            bool TryPackings(int down, int up, IEnumerable<MarkerPacking> packings, out EndMarkers chosen, out OrientedRectangle chosenRegion)
            {
                foreach (MarkerPacking packing in packings)
                {
                    chosen = new EndMarkers(down, up, packing);
                    double width = chosen.WidthUnits, height = chosen.HeightUnits;

                    // A packing at least as long and as thick as one that failed allows no larger font, so it fails too.
                    if (failed.Exists(f => f.Width <= width && f.Height <= height))
                        continue;

                    if (!TryBestRun(content, chosen, rotationGain, out chosenRegion, out double score) || score < floor)
                    {
                        failed.Add((width, height));
                        continue;
                    }

                    if (EndMarkersClear(chosenRegion, content, chosen))
                        return true;
                }

                chosen = default;
                chosenRegion = default;
                return false;
            }
        }

        /// <summary>
        /// <see cref="Fit(LabelContent, double)"/> with hysteresis: returns <paramref name="previous"/> itself while it
        /// still holds <paramref name="content"/> (see <see cref="Holds"/>) and the new best beats it by at most
        /// 1 + <see cref="Hysteresis"/> (see <see cref="Advantage"/>); otherwise the new best. The caller keeps the
        /// returned placement and passes it back on the next fit, and drops it when the polygon changes, since a
        /// placement from another layout is never kept.
        /// </summary>
        /// <remarks>
        /// A previous circle whose link counts changed is replaced by the circle laid out for the new content before the
        /// comparison, so the circle is still kept under hysteresis when a link is added, with marker slots for every link.
        /// </remarks>
        public LabelPlacement Fit(LabelContent content, double rotationGain, LabelPlacement? previous)
        {
            LabelPlacement best = Fit(content, rotationGain);
            if (previous is not null && previous.Kind == LabelPlacementKind.Circle && ReferenceEquals(previous.Owner, this) &&
                !previous.Content.SameMarkerCounts(content))
            {
                previous = best.Kind == LabelPlacementKind.Circle ? best : PlaceInCircle(content);
            }

            if (previous is null || !Holds(previous, content))
                return best;

            if (double.IsNaN(rotationGain))
                rotationGain = DefaultRotationGain;

            return Advantage(best, previous, content, rotationGain) <= 1 + Hysteresis ? previous : best;
        }

        /// <summary>
        /// True when <paramref name="placement"/> was made by this layout and can show <paramref name="content"/> as it
        /// stands: the up and down marker counts are unchanged (so every marker has its slot), and for a run the rows also
        /// have the same scales (so its row centres stack them) and the widest row still fits between the end markers at
        /// the run's font; the circle lays any text out itself. Longer text or a new link therefore drops a run.
        /// </summary>
        internal bool Holds(LabelPlacement placement, LabelContent content)
        {
            if (!ReferenceEquals(placement.Owner, this))
                return false;

            LabelContent laidOut = placement.Content;
            if (!laidOut.SameMarkerCounts(content))
                return false;

            if (placement.Kind == LabelPlacementKind.Circle)
                return true;

            return laidOut.SameRowScales(content) &&
                   TextPadding * content.TextWidth * placement.FontSize <= placement.MaxLineWidth * (1 + 1e-9);
        }

        /// <summary>
        /// How many times better <paramref name="candidate"/> is than <paramref name="incumbent"/> for
        /// <paramref name="content"/> by the rules <see cref="Fit(LabelContent, double)"/> decides with, so above 1 means
        /// Fit would prefer the candidate. Between runs it is the ratio of their text-only scores F0 times
        /// <see cref="TiltFactor"/>. Between a run and the circle it is how far the run's text-only font F0 is above (or
        /// below) the font the circle test asks of it, <see cref="CircleThreshold"/> times R/2. Each side is multiplied by
        /// its <see cref="LabelPlacement.MarkerReadability"/>, as in Fit. Infinite when the incumbent scores nothing, as a
        /// tilted run does at <see cref="NeverRotate"/>. Both placements must hold <paramref name="content"/>'s links.
        /// </summary>
        internal double Advantage(LabelPlacement candidate, LabelPlacement incumbent, LabelContent content, double rotationGain)
        {
            static double Ratio(double a, double b) => b > 0 ? a / b : double.PositiveInfinity;
            double RunScore(LabelPlacement run) => RunFontSize(run.Region.Width, run.Region.Height, content, 0) * run.MarkerReadability;
            double CircleAsks(LabelPlacement run, LabelPlacement circle) =>
                CircleThreshold(run.Angle, rotationGain) * circle.FontSize * circle.MarkerReadability;

            return (candidate.Kind, incumbent.Kind) switch
            {
                (LabelPlacementKind.Run, LabelPlacementKind.Run) =>
                    Ratio(RunScore(candidate) * TiltFactor(candidate.Angle, rotationGain),
                          RunScore(incumbent) * TiltFactor(incumbent.Angle, rotationGain)),
                (LabelPlacementKind.Run, LabelPlacementKind.Circle) => Ratio(RunScore(candidate), CircleAsks(candidate, incumbent)),
                (LabelPlacementKind.Circle, LabelPlacementKind.Run) => Ratio(CircleAsks(incumbent, candidate), RunScore(incumbent)),
                _ => 1,
            };
        }

        /// <summary>
        /// How many times the circle's font a run at <paramref name="angle"/> must allow to replace it: the larger of
        /// <see cref="CirclePreference"/> and the tilt's <see cref="RequiredGain"/>.
        /// </summary>
        private static double CircleThreshold(double angle, double rotationGain) =>
            Math.Max(CirclePreference, RequiredGain(angle, rotationGain));

        /// <summary>
        /// The verified run with the best score for the end markers <paramref name="ends"/>. Raster sizes bound the
        /// verified ones from above, and a verified run keeps its candidate's angle, so the same tilt factor keeps the
        /// bound: runs are verified best first only until no unverified one could win. With <see cref="NeverRotate"/>
        /// tilted runs are skipped, not merely scored 0.
        /// </summary>
        /// <remarks>
        /// Picks the next best bound by a linear scan rather than sorting every candidate: the 5° angles give a round cell
        /// thousands of Pareto runs, and the scan stops after the first one or two verify, so sorting dominated the fit.
        /// Ties go to the earlier candidate, as the stable sort did.
        /// </remarks>
        private bool TryBestRun(LabelContent content, EndMarkers ends, double rotationGain, out OrientedRectangle best, out double bestScore)
        {
            best = default;
            bestScore = 0;
            bool found = false;

            bool levelOnly = double.IsPositiveInfinity(rotationGain);
            double gain = Math.Max(0, rotationGain);
            double[] bounds = new double[Runs.Length];
            for (int i = 0; i < Runs.Length; i++)
            {
                if (levelOnly && Runs[i].Angle != 0)
                {
                    bounds[i] = -1;
                    continue;
                }

                double tilt = RunSin[i] == 0 ? 1 : 1.0 / (1 + (gain * RunSin[i]));
                bounds[i] = RunFontSize(Runs[i].Width, Runs[i].Height, content, ends.WidthUnits, ends.HeightUnits) * tilt;
            }

            while (true)
            {
                int next = -1;
                double nextBound = bestScore;
                for (int i = 0; i < bounds.Length; i++)
                {
                    if (bounds[i] > nextBound)
                    {
                        next = i;
                        nextBound = bounds[i];
                    }
                }

                if (next < 0)
                    break;

                bounds[next] = -1;
                if (!Runs[next].TryVerify(out OrientedRectangle region))
                    continue;

                double score = Score(region, content, ends, rotationGain);
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
        /// Adds the end markers of <paramref name="ends"/> for a run at ID font <paramref name="font"/>: down-links in a
        /// block from the left end, up-links in a block from the right end, each block <see cref="EndBlockSlot"/>'s
        /// hex-packed rows centred on the axis. Slot j is always in the same place for the same run, font and counts.
        /// </summary>
        private static void AddEndSlots(OrientedRectangle region, double font, EndMarkers ends, List<MarkerSlot> markers)
        {
            double d = ends.Packing.Scale * font;
            double radius = EndMarkerRadius * d;
            double halfW = region.Width / 2.0;
            for (int j = 0; j < ends.Down; j++)
            {
                (double u, double v) = EndBlockSlot(j, ends.Down, ends.Packing.Rows);
                markers.Add(new MarkerSlot(region.LocalToWorld(-halfW + (u * d), v * d), radius, false));
            }

            for (int j = 0; j < ends.Up; j++)
            {
                (double u, double v) = EndBlockSlot(j, ends.Up, ends.Packing.Rows);
                markers.Add(new MarkerSlot(region.LocalToWorld(halfW - (u * d), v * d), radius, true));
            }
        }

        /// <summary>True when every end marker <paramref name="ends"/> puts in <paramref name="region"/> clears the outline and holes.</summary>
        private bool EndMarkersClear(OrientedRectangle region, LabelContent content, EndMarkers ends)
        {
            double font = RunFontSize(region.Width, region.Height, content, ends.WidthUnits, ends.HeightUnits);
            List<MarkerSlot> slots = new(ends.Down + ends.Up);
            AddEndSlots(region, font, ends, slots);
            return slots.TrueForAll(s => ClearOfOutline(s, region));
        }

        /// <summary>
        /// True when <paramref name="slot"/>'s whole circle is inside the polygon and out of its holes, by the real
        /// segments. A circle inside the inscribed circle, or inside <paramref name="verified"/> (a run checked against
        /// every ring), passes without a segment scan.
        /// </summary>
        private bool ClearOfOutline(MarkerSlot slot, OrientedRectangle? verified = null)
        {
            if (Vector2.Distance(slot.Center, InscribedCircle.Center) + slot.Radius <= InscribedCircle.Radius)
                return true;

            if (verified.HasValue && -verified.Value.SignedDistance(slot.Center) >= slot.Radius)
                return true;

            return Rings.SignedDistance(slot.Center.X, slot.Center.Y) >= slot.Radius;
        }

        /// <summary>
        /// Lays out the run's local frame: down markers from the left end, up markers from the right end (see
        /// <see cref="AddEndSlots"/>), rows stacked top to bottom in the space between, centred on the axis. Markers not
        /// in <paramref name="ends"/> go to the deepest free spots of the polygon.
        /// </summary>
        private LabelPlacement PlaceInRun(OrientedRectangle region, LabelContent content, EndMarkers ends)
        {
            double font = RunFontSize(region.Width, region.Height, content, ends.WidthUnits, ends.HeightUnits);
            List<MarkerSlot> markers = new(content.MarkerCount);
            AddEndSlots(region, font, ends, markers);

            double d = ends.Packing.Scale * font;
            double downWidth = EndBlockWidth(ends.Down, ends.Packing.Rows) * d;
            double upWidth = EndBlockWidth(ends.Up, ends.Packing.Rows) * d;
            double textCenterU = (downWidth - upWidth) / 2.0;
            double maxLineWidth = region.Width - downWidth - upWidth;
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

            int packed = markers.Count;
            PlaceInFreeSpace(markers, textBox, font / 2.0, MinimumMarkerRadius * font, content.DownCount - ends.Down, false);
            PlaceInFreeSpace(markers, textBox, font / 2.0, MinimumMarkerRadius * font, content.UpCount - ends.Up, true);

            int free = markers.Count - packed;
            return new LabelPlacement(this, content, InscribedCircle, region, font, textBox, maxLineWidth, rowCenters, markers,
                ends.Packing, free, MarkerReadability(packed, ends.Packing.Scale, free));
        }

        /// <summary>
        /// The circle placement: text as <c>StructureCircleLabels</c> draws it, up-link markers above it and down-link
        /// markers below, in the first <see cref="PackingOrder"/> packing whose rows (see <see cref="AddCircleRows"/>) hold
        /// every marker. With none, the densest packing holds what it can and the rest go to free space clear of the text.
        /// </summary>
        private LabelPlacement PlaceInCircle(LabelContent content)
        {
            double font = CircleFontSize(InscribedCircle);
            if (content.MarkerCount == 0)
                return new LabelPlacement(this, content, InscribedCircle, font, [], MarkerPacking.Single, 0, 1);

            List<MarkerSlot> markers = new(content.MarkerCount);
            foreach (MarkerPacking packing in PackingOrder)
            {
                markers.Clear();
                if (AddCircleRows(content.UpCount, true, packing, markers) == content.UpCount &&
                    AddCircleRows(content.DownCount, false, packing, markers) == content.DownCount)
                {
                    return new LabelPlacement(this, content, InscribedCircle, font, [.. markers], packing, 0,
                        MarkerReadability(markers.Count, packing.Scale, 0));
                }
            }

            MarkerPacking densest = new(MaxMarkerRows, MinimumMarkerScale);
            markers.Clear();
            int up = AddCircleRows(content.UpCount, true, densest, markers);
            int down = AddCircleRows(content.DownCount, false, densest, markers);
            int packed = markers.Count;

            double r = InscribedCircle.Radius;
            OrientedRectangle text = new(InscribedCircle.Center, 2 * r, 2 * CircleTextHalfHeight * r, 0);
            double fullRadius = CircleMarkerRadius * r;
            PlaceInFreeSpace(markers, text, fullRadius, MinimumMarkerScale * fullRadius, content.UpCount - up, true);
            PlaceInFreeSpace(markers, text, fullRadius, MinimumMarkerScale * fullRadius, content.DownCount - down, false);

            int free = markers.Count - packed;
            return new LabelPlacement(this, content, InscribedCircle, font, markers, densest, free,
                MarkerReadability(packed, densest.Scale, free));
        }

        /// <summary>
        /// Adds up to <paramref name="count"/> markers of one direction in hex-packed arcs about the inscribed circle's
        /// centre, above the text for up-links and mirrored below it for down-links, and returns how many it added. Row 0
        /// lies just outside the text (<see cref="CircleTextHalfHeight"/>); at full size it is the arc circle annotations
        /// use, radius 0.75 R with markers of R/6, centred on the vertical. Each further row is √3 marker radii farther out
        /// and half a pitch along, in the gaps of the row before. All rows share one angular pitch, the innermost row's, so
        /// the rows interleave, and no marker crosses the horizontal through the centre. A row that would leave the
        /// polygon or touch a hole is not added, nor any row past <see cref="MarkerPacking.Rows"/>.
        /// </summary>
        private int AddCircleRows(int count, bool isUp, MarkerPacking packing, List<MarkerSlot> markers)
        {
            double r = InscribedCircle.Radius;
            double markerRadius = packing.Scale * CircleMarkerRadius * r;
            if (count <= 0 || !(markerRadius > 0))
                return 0;

            double innerRadius = (CircleTextHalfHeight * r) + markerRadius;
            double pitch = 2 * Math.Asin(Math.Min(1, markerRadius / innerRadius));

            // Lattice positions t (angle t·pitch from the vertical) with |t| <= reach keep a marker in its half.
            double reach = ((Math.PI / pitch) - 1) / 2.0;
            int Capacity(int phase) => phase == 0 ? (2 * (int)Math.Floor(reach)) + 1 : 2 * (int)Math.Floor(reach + 0.5);

            Vector2 center = InscribedCircle.Center;
            double side = isUp ? 1 : -1;
            int placed = 0;
            int previousPhase = 0;
            List<MarkerSlot> row = [];
            for (int k = 0; k < packing.Rows && placed < count; k++)
            {
                // Row 0 is centred like a circle annotation's arc (an odd count on the vertical, an even one either side
                // of it); each later row takes the other phase, so its markers sit in the gaps of the row inside it.
                int remaining = count - placed;
                int matching = remaining % 2 == 1 ? 0 : 1;
                int phase = k > 0 ? 1 - previousPhase
                    : remaining <= Capacity(matching) ? matching
                    : Capacity(1) > Capacity(0) ? 1 : 0;

                previousPhase = phase;
                int take = Math.Min(remaining, Capacity(phase));
                if (take <= 0)
                    break;

                int span = (int)Math.Ceiling(reach) + 1;
                double[] lattice = [.. Enumerable.Range(-span, (2 * span) + 1)
                    .Select(i => i + (phase / 2.0))
                    .Where(t => Math.Abs(t) <= reach)
                    .OrderBy(t => Math.Abs(t)).ThenBy(t => t)
                    .Take(take)
                    .OrderBy(t => t)];

                double rowRadius = innerRadius + (k * 2 * MarkerRowSpacing * markerRadius);
                row.Clear();
                foreach (double t in lattice)
                {
                    double angle = t * pitch;
                    MarkerSlot slot = new(new Vector2(center.X + (rowRadius * Math.Sin(angle)), center.Y + (side * rowRadius * Math.Cos(angle))),
                        markerRadius, isUp);
                    if (!ClearOfOutline(slot))
                        return placed;

                    row.Add(slot);
                }

                markers.AddRange(row);
                placed += row.Count;
            }

            return placed;
        }

        /// <summary>
        /// Adds <paramref name="count"/> markers one at a time at the point farthest from the outline, the text box and
        /// every marker placed so far, each as large as that clearance allows up to <paramref name="maxRadius"/> and at
        /// least <paramref name="minRadius"/>. A marker never crosses the outline or a hole: its radius is capped by its
        /// distance to them, and where no point clears the text and the other markers it goes to the inscribed circle's
        /// centre, overlapping them rather than leaving the polygon.
        /// </summary>
        private void PlaceInFreeSpace(List<MarkerSlot> markers, OrientedRectangle textBox, double maxRadius, double minRadius, int count, bool isUp)
        {
            if (count <= 0)
                return;

            double tolerance = Math.Max(PoleOfInaccessibility.DefaultTolerance(Bounds), maxRadius / 10.0);
            for (int i = 0; i < count; i++)
            {
                Vector2 center = PoleOfInaccessibility.Maximize(Bounds, Clearance, tolerance, Centroid, out double clearance);
                double radius = Math.Max(Math.Min(maxRadius, 0.95 * clearance), minRadius);
                double room = Rings.SignedDistance(center.X, center.Y);
                if (!(room > 0))
                {
                    center = InscribedCircle.Center;
                    room = InscribedCircle.Radius;
                }

                markers.Add(new MarkerSlot(center, Math.Min(radius, 0.95 * room), isUp));
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
