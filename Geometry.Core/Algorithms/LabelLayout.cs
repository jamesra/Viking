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

        /// <summary>Every marker for a run: the end markers, then any placed in free space. Empty for circles.</summary>
        public readonly IReadOnlyList<MarkerSlot> Markers;

        /// <summary>Text angle in radians, counter-clockwise from +X, in (−π/2, π/2]. 0 for circles.</summary>
        public double Angle => Kind == LabelPlacementKind.Run ? Region.Angle : 0;

        internal LabelPlacement(LabelLayout owner, LabelContent content, Circle circle, double fontSize)
        {
            Owner = owner;
            Content = content;
            Kind = LabelPlacementKind.Circle;
            Circle = circle;
            FontSize = fontSize;
            RowCenters = [];
            Markers = [];
        }

        internal LabelPlacement(LabelLayout owner, LabelContent content, Circle circle, OrientedRectangle region, double fontSize,
            OrientedRectangle textBox, double maxLineWidth, IReadOnlyList<Vector2> rowCenters, IReadOnlyList<MarkerSlot> markers)
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

        /// <summary>End markers are kept while the font is at least this fraction of the font without them.</summary>
        public const double MarkerFontRetention = 0.6;

        /// <summary>End marker radius as a fraction of the marker pitch, which equals the ID font size.</summary>
        public const double EndMarkerRadius = 0.45;

        /// <summary>Smallest free-space marker radius as a fraction of the ID font size, used when no room is left.</summary>
        public const double MinimumMarkerRadius = 0.1;

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

        /// <summary>ID font size a <paramref name="width"/> by <paramref name="height"/> run allows with <paramref name="markers"/> end markers.</summary>
        public static double RunFontSize(double width, double height, LabelContent content, int markers)
        {
            double th = Math.Max(content.TextHeight, 1e-9);
            double tw = content.TextWidth;
            return Math.Max(0, Math.Min(height / (TextPadding * th), width / Math.Max(markers + (TextPadding * tw), 1e-9)));
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

        private static double Score(OrientedRectangle region, LabelContent content, int markers, double rotationGain) =>
            RunFontSize(region.Width, region.Height, content, markers) * TiltFactor(region.Angle, rotationGain);

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
            double circleFont = CircleFontSize(InscribedCircle);
            if (!TryBestRun(content, 0, rotationGain, out OrientedRectangle region, out double textOnlyScore))
                return new LabelPlacement(this, content, InscribedCircle, circleFont);

            double runFont = RunFontSize(region.Width, region.Height, content, 0);
            if (runFont < CircleThreshold(region.Angle, rotationGain) * circleFont)
                return new LabelPlacement(this, content, InscribedCircle, circleFont);

            int endMarkers = 0;
            for (int k = content.MarkerCount; k > 0; k--)
            {
                if (TryBestRun(content, k, rotationGain, out OrientedRectangle withMarkers, out double score) &&
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
        /// <see cref="Fit(LabelContent, double)"/> with hysteresis: returns <paramref name="previous"/> itself while it
        /// still holds <paramref name="content"/> (see <see cref="Holds"/>) and the new best beats it by at most
        /// 1 + <see cref="Hysteresis"/> (see <see cref="Advantage"/>); otherwise the new best. The caller keeps the
        /// returned placement and passes it back on the next fit, and drops it when the polygon changes, since a
        /// placement from another layout is never kept.
        /// </summary>
        public LabelPlacement Fit(LabelContent content, double rotationGain, LabelPlacement? previous)
        {
            LabelPlacement best = Fit(content, rotationGain);
            if (previous is null || !Holds(previous, content))
                return best;

            if (double.IsNaN(rotationGain))
                rotationGain = DefaultRotationGain;

            return Advantage(best, previous, content, rotationGain) <= 1 + Hysteresis ? previous : best;
        }

        /// <summary>
        /// True when <paramref name="placement"/> was made by this layout and can show <paramref name="content"/> as it
        /// stands: always for the circle, which lays any content out itself; for a run only when the rows have the same
        /// scales (so its row centres stack them), the up and down marker counts are unchanged (so every marker has its
        /// slot) and the widest row still fits between the end markers at the run's font. Longer text or a new link
        /// therefore drops the placement.
        /// </summary>
        internal bool Holds(LabelPlacement placement, LabelContent content)
        {
            if (!ReferenceEquals(placement.Owner, this))
                return false;

            if (placement.Kind == LabelPlacementKind.Circle)
                return true;

            LabelContent laidOut = placement.Content;
            return laidOut.SameRowScales(content) &&
                   laidOut.UpCount == content.UpCount &&
                   laidOut.DownCount == content.DownCount &&
                   TextPadding * content.TextWidth * placement.FontSize <= placement.MaxLineWidth * (1 + 1e-9);
        }

        /// <summary>
        /// How many times better <paramref name="candidate"/> is than <paramref name="incumbent"/> for
        /// <paramref name="content"/> by the rules <see cref="Fit(LabelContent, double)"/> decides with, so above 1 means
        /// Fit would prefer the candidate. Between runs it is the ratio of their text-only scores F0 times
        /// <see cref="TiltFactor"/>. Between a run and the circle it is how far the run's text-only font F0 is above (or
        /// below) the font the circle test asks of it, <see cref="CircleThreshold"/> times R/2. Infinite when the
        /// incumbent scores nothing, as a tilted run does at <see cref="NeverRotate"/>.
        /// </summary>
        internal double Advantage(LabelPlacement candidate, LabelPlacement incumbent, LabelContent content, double rotationGain)
        {
            double circleFont = CircleFontSize(InscribedCircle);
            static double Ratio(double a, double b) => b > 0 ? a / b : double.PositiveInfinity;
            double TextOnlyFont(LabelPlacement run) => RunFontSize(run.Region.Width, run.Region.Height, content, 0);
            double CircleAsks(LabelPlacement run) => CircleThreshold(run.Angle, rotationGain) * circleFont;

            return (candidate.Kind, incumbent.Kind) switch
            {
                (LabelPlacementKind.Run, LabelPlacementKind.Run) =>
                    Ratio(TextOnlyFont(candidate) * TiltFactor(candidate.Angle, rotationGain),
                          TextOnlyFont(incumbent) * TiltFactor(incumbent.Angle, rotationGain)),
                (LabelPlacementKind.Run, LabelPlacementKind.Circle) => Ratio(TextOnlyFont(candidate), CircleAsks(candidate)),
                (LabelPlacementKind.Circle, LabelPlacementKind.Run) => Ratio(CircleAsks(incumbent), TextOnlyFont(incumbent)),
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
        /// The verified run with the best score for <paramref name="markers"/> end markers. Raster sizes bound the
        /// verified ones from above, and a verified run keeps its candidate's angle, so the same tilt factor keeps the
        /// bound: runs are verified best first only until no unverified one could win. With <see cref="NeverRotate"/>
        /// tilted runs are skipped, not merely scored 0.
        /// </summary>
        /// <remarks>
        /// Picks the next best bound by a linear scan rather than sorting every candidate: the 5° angles give a round cell
        /// thousands of Pareto runs, and the scan stops after the first one or two verify, so sorting dominated the fit.
        /// Ties go to the earlier candidate, as the stable sort did.
        /// </remarks>
        private bool TryBestRun(LabelContent content, int markers, double rotationGain, out OrientedRectangle best, out double bestScore)
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
                bounds[i] = RunFontSize(Runs[i].Width, Runs[i].Height, content, markers) * tilt;
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

                double score = Score(region, content, markers, rotationGain);
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

            return new LabelPlacement(this, content, InscribedCircle, region, font, textBox, maxLineWidth, rowCenters, markers);
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
