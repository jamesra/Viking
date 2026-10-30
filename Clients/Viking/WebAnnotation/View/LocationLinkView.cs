using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.Common;
using Viking.VolumeModel;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotation.View;
using WebAnnotationModel;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.ViewModel
{

    public delegate ContextMenuStrip LocationLinkContextMenuGeneratorDelegate(IViewLocationLink key);

    /// <summary>
    /// This class represents a link between locations. This object is a little unique because it is
    /// not tied to the database object like the other *obj classes
    /// </summary>
    public class LocationLinkView : Viking.Objects.UIObjBase, ICanvasGeometryView, IEquatable<LocationLinkView>, IColorView, IViewLocationLink, IContextMenu
    {
        public readonly LocationLinkKey Key;

        public override int GetHashCode() => Key.GetHashCode();

        public override bool Equals(object obj)
        {
            if (System.Object.ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is LocationLinkView obj_link)
            {
                return Key.Equals(obj_link.Key);
            }

            if (typeof(LocationLinkKey).IsInstanceOfType(obj))
            {
                LocationLinkKey obj_key = (LocationLinkKey)obj;
                return Key.Equals(obj_key);
            }

            return false;
        }

        public static bool operator ==(LocationLinkView A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if (A is not null)
            {
                return A.Equals(B);
            }

            return false;
        }

        public static bool operator !=(LocationLinkView A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if (A is not null)
            {
                return !A.Equals(B);
            }

            return true;
        }

        public override string ToString() => Key.ToString() + " Sections: " + MinSection.ToString() + "-" + MaxSection.ToString();

        /// <summary>
        /// LocationOnSection is the location on the section being viewed
        /// </summary>
        public Circle A;

        /// <summary>
        /// LocationOnSection is the location on the section being viewed
        /// </summary>
        public Circle B;

        /// <summary>
        /// Section number we are displaying the location link on
        /// </summary>
        public int Z;

        public LineView? lineView = null;

        private Color _Color;

        public Color Color
        {
            get => _Color;

            set
            {
                _Color = value;
                lineView?.Color = value;
            }
        }

        public float Alpha
        {
            get => _Color.GetAlpha();

            // SetAlpha returns a new colour, so assign it back; the Del fade-in scales the link's alpha here.
            set => Color = _Color.SetAlpha(value);
        }

        /// <summary>How long a link takes to fade between its hovered and resting looks.</summary>
        internal static readonly TimeSpan HoverFadeDuration = TimeSpan.FromMilliseconds(350);

        private bool _IsMouseOver;
        private float _HoverFrom;
        private DateTime _HoverChangedUtc = DateTime.MinValue;

        /// <summary>
        /// True while the mouse is over the link; set by <see cref="WebAnnotation.Actions.MouseOverLocationCanvasViewEffect"/>.
        /// A hovered link is drawn fully opaque along its whole length with its end labels, and all of it can be picked, so
        /// the mouse can follow it through the waist that is invisible and unpickable otherwise. Picking switches at once;
        /// the look fades over <see cref="HoverFadeDuration"/> (<see cref="HoverAmount"/>).
        /// </summary>
        public bool IsMouseOver
        {
            get => _IsMouseOver;
            set
            {
                if (_IsMouseOver == value)
                    return;

                _HoverFrom = HoverAmount;
                _IsMouseOver = value;
                _HoverChangedUtc = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// How far the link's look is toward hovered, 0 (resting) to 1 (hovered), moving at a steady rate since the mouse
        /// last came on or went off it. Read when drawing; the overlay keeps redrawing while <see cref="IsHoverFading"/>.
        /// </summary>
        public float HoverAmount => HoverAmountAt(_HoverFrom, _IsMouseOver, DateTime.UtcNow - _HoverChangedUtc);

        /// <summary>True while <see cref="HoverAmount"/> is still moving toward the hovered or resting look.</summary>
        public bool IsHoverFading => _IsMouseOver ? HoverAmount < 1f : HoverAmount > 0f;

        /// <summary>
        /// <see cref="HoverAmount"/> <paramref name="elapsed"/> after the mouse came on (<paramref name="hovered"/>) or went
        /// off the link while the look stood at <paramref name="from"/>: it moves the whole way in
        /// <see cref="HoverFadeDuration"/>, so a fade reversed halfway takes half as long to undo.
        /// </summary>
        internal static float HoverAmountAt(float from, bool hovered, TimeSpan elapsed)
        {
            float step = (float)(elapsed.TotalMilliseconds / HoverFadeDuration.TotalMilliseconds);
            return hovered ? Math.Min(1f, from + step) : Math.Max(0f, from - step);
        }

        public int MinSection { get; private set; }
        public int MaxSection { get; private set; }

        /// <summary>Section of the location at end A (<see cref="LocationLinkKey.A"/>).</summary>
        public int SectionA { get; private set; }

        /// <summary>Section of the location at end B (<see cref="LocationLinkKey.B"/>).</summary>
        public int SectionB { get; private set; }

        protected int LinkDirection => MinSection == MaxSection ? 0 : MinSection < Z ? -1 : 1;

        public Geometry.Rectangle BoundingBox => Geometry.Rectangle.Pad(LineSegment.BoundingBox, MaxRadius);

        /// <summary>The larger endpoint radius: the widest the pinched link gets.</summary>
        public double MaxRadius => Math.Max(A.Radius, B.Radius);

        /// <summary>
        /// Opacity at the waist of a link the mouse is not over, as a fraction of its opacity next to the markers. The
        /// shader fades alpha linearly in <see cref="FadeDepthAt"/>, so a link is strongest next to the locations it joins
        /// and its middle is invisible. A hovered link does not fade.
        /// </summary>
        internal const float WaistOpacity = 0f;

        /// <summary>
        /// Deepest <see cref="FadeDepthAt"/> at which a link the mouse is not over can be picked: where it is drawn at
        /// least a quarter as opaque as next to its markers. What cannot be seen cannot be picked.
        /// </summary>
        internal const double MaxPickableFadeDepth = 0.75;

        /// <summary>Width in screen pixels of the contrasting border drawn just inside a link's outline.</summary>
        internal const float BorderPixels = 1.5f;

        /// <summary>
        /// Width in screen pixels of the contrasting shadow drawn outside a link's outline. It is as opaque as the link at
        /// the outline and fades to nothing this far out.
        /// </summary>
        internal const float ShadowPixels = 5f;

        /// <summary>
        /// Height of an end label's text as a fraction of the radius of the marker it sits beside. Labels are sized in
        /// volume space, as location labels are, so they zoom with the image, and at this size a label of up to seven
        /// digits fits in the strong part of the link before it fades.
        /// </summary>
        internal const double LabelHeightPerRadius = 0.16;

        /// <summary>Gap between a marker's edge and its label, and between the two labels, as a fraction of that marker's radius.</summary>
        internal const double LabelGapPerRadius = 0.05;

        /// <summary>An end label shorter than this many screen pixels is not drawn; it could not be read.</summary>
        internal const double MinLabelPixels = 6;

        /// <summary>
        /// The opacity a link of stored alpha <paramref name="linkAlpha"/> is drawn with. <paramref name="visibility"/> is the
        /// Link visibility preference (0.1 to 1), or 1 for a hovered link. Links are stored fully opaque; Del's fade-in
        /// scales the stored alpha, and the drawn opacity follows it.
        /// </summary>
        internal static float DrawnAlpha(float linkAlpha, float visibility) => MathHelper.Clamp(linkAlpha * visibility, 0f, 1f);

        /// <summary>
        /// Opacity of the end labels for a link of stored alpha <paramref name="linkAlpha"/>. Labels stay readable at low
        /// visibility: fully opaque from 50% up, and never below half, but they fade in with the link after Del hid it.
        /// </summary>
        internal static float LabelAlpha(float linkAlpha, float visibility) => MathHelper.Clamp(linkAlpha * Math.Min(1f, 0.5f + visibility), 0f, 1f);

        /// <summary>
        /// How far <paramref name="x"/> world units from A's centre is into the fade from the nearer marker: 0 at the
        /// marker's edge and inside it, 1 one marker radius further on, where a long link reaches its waist. Each end is
        /// measured by its own radius, so the end at a small marker starts at full opacity as the large one's does.
        /// Matches PinchedFadeDepth in PinchedLineShaders.fx.
        /// </summary>
        internal static double FadeDepthAt(double x, double length, double radiusA, double radiusB)
        {
            double fromA = (x - radiusA) / Math.Max(radiusA, 1e-6);
            double fromB = (length - x - radiusB) / Math.Max(radiusB, 1e-6);
            return Math.Max(0, Math.Min(1, Math.Min(fromA, fromB)));
        }

        /// <summary>
        /// True when the end at <paramref name="aCentre"/> is at least as far from <paramref name="position"/> as the end at
        /// <paramref name="bCentre"/>, so Go to partner from there goes to A.
        /// </summary>
        internal static bool AIsPartnerOf(Geometry.Vector2 position, Geometry.Vector2 aCentre, Geometry.Vector2 bCentre) =>
            Geometry.Vector2.Distance(position, aCentre) >= Geometry.Vector2.Distance(position, bCentre);

        /// <summary>
        /// Which section arrow goes with the label at an end on <paramref name="endSection"/> whose partner is on
        /// <paramref name="otherSection"/>, seen from <paramref name="viewedSection"/>: 1 for an up arrow (the partner is on
        /// a higher section, as Viking draws adjacent locations above with its up arrow), -1 for a down arrow, and 0 for
        /// none. Only the end on the viewed section gets an arrow; the adjacent section's end shows the number alone.
        /// </summary>
        internal static int SectionArrowAt(int endSection, int otherSection, int viewedSection) =>
            endSection != viewedSection ? 0 : Math.Sign(otherSection - endSection);

        /// <summary>
        /// True when the end labels fit in the <paramref name="room"/> of link between the two markers' edges: each label
        /// its own gap from its marker, and the larger gap between them. All lengths in the same units.
        /// </summary>
        internal static bool EndLabelsFit(double room, double widthA, double gapA, double widthB, double gapB) =>
            room >= gapA + widthA + Math.Max(gapA, gapB) + widthB + gapB;

        /// <summary>Opacity of a link whose markers touch, as a fraction of its usual opacity (see <see cref="ClosenessOpacity"/>).</summary>
        internal const float NearestLinkOpacity = 0.5f;

        /// <summary>
        /// Opacity factor for a link of <paramref name="length"/> (centre to centre) between markers of radii
        /// <paramref name="radiusA"/> and <paramref name="radiusB"/>. The deepest the link fades (<see cref="FadeDepthAt"/>,
        /// at the point both ends measure the same) is gap / (rA + rB), with gap the distance between the markers' edges.
        /// A link that reaches its fully faded waist (gap at least rA + rB) is unchanged, factor 1. A shorter one never
        /// fades fully and is drawn as a wide, solid block, so its factor falls with that peak fade depth to
        /// <see cref="NearestLinkOpacity"/> as the markers touch, letting the image show through.
        /// </summary>
        internal static float ClosenessOpacity(double length, double radiusA, double radiusB)
        {
            double reach = radiusA + radiusB;
            double peakFadeDepth = reach > 0 ? Math.Max(0, Math.Min(1, (length - reach) / reach)) : 1;
            return MathHelper.Lerp(NearestLinkOpacity, 1f, (float)peakFadeDepth);
        }

        /// <summary>
        /// How much of a link's colour chroma its fill keeps. The direction colours (red, orange or yellow toward a lower
        /// section, blue toward a higher one) are fully saturated and the red looked harsh at the opacity links are now drawn
        /// with. The chroma is reduced in OKLCH (<see cref="Soften"/>), which keeps hue and lightness as the eye sees them:
        /// fading the RGB channels toward grey instead turned a burnt orange into a yellowish tan.
        /// </summary>
        internal const float FillChroma = 0.75f;

        /// <summary>
        /// The colour a link of direction colour <paramref name="linkColor"/> is filled with, and the colour the section
        /// number on an overlapped-link marker going the same way is drawn in, so the two always match.
        /// </summary>
        internal static Color FillColorFor(Color linkColor) => Soften(linkColor, FillChroma);

        /// <summary>
        /// <paramref name="color"/> with its OKLCH chroma scaled by <paramref name="chroma"/>, keeping its OKLCH hue and
        /// lightness (Ottosson 2020, "A perceptual color space for image processing"): the colour stays the same hue to the
        /// eye, only less vivid. Alpha is kept.
        /// </summary>
        internal static Color Soften(Color color, float chroma)
        {
            static double ToLinear(byte channel)
            {
                double c = channel / 255.0;
                return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            static byte FromLinear(double c)
            {
                c = Math.Max(0, Math.Min(1, c));
                double s = c <= 0.0031308 ? 12.92 * c : (1.055 * Math.Pow(c, 1 / 2.4)) - 0.055;
                return (byte)Math.Round(Math.Max(0, Math.Min(1, s)) * 255);
            }

            double r = ToLinear(color.R), g = ToLinear(color.G), b = ToLinear(color.B);
            double l = Math.Pow((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b), 1 / 3.0);
            double m = Math.Pow((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b), 1 / 3.0);
            double s = Math.Pow((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b), 1 / 3.0);

            double lightness = (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s);
            double labA = ((1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s)) * chroma;
            double labB = ((0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s)) * chroma;

            double l3 = Math.Pow(lightness + (0.3963377774 * labA) + (0.2158037573 * labB), 3);
            double m3 = Math.Pow(lightness - (0.1055613458 * labA) - (0.0638541728 * labB), 3);
            double s3 = Math.Pow(lightness - (0.0894841775 * labA) - (1.2914855480 * labB), 3);

            return new Color(
                FromLinear((4.0767416621 * l3) - (3.3077115913 * m3) + (0.2309699292 * s3)),
                FromLinear((-1.2684380046 * l3) + (2.6097574011 * m3) - (0.3413193965 * s3)),
                FromLinear((-0.0041960863 * l3) - (0.7034186147 * m3) + (1.7076147010 * s3)),
                color.A);
        }

        /// <summary>
        /// The border and shadow color for a link of color <paramref name="linkColor"/>: black for a light link and white for
        /// a dark one, judged by perceived brightness, so the link stands out from the image under it.
        /// </summary>
        internal static Color BorderColorFor(Color linkColor)
        {
            float brightness = ((0.3f * linkColor.R) + (0.59f * linkColor.G) + (0.11f * linkColor.B)) / 255f;
            return brightness > 0.5f ? Color.Black : Color.White;
        }

        /// <summary>
        /// Waist half width in world units at the zoom links were last drawn at. Hit tests have no scene, and every
        /// link is drawn with the same camera, so they use this.
        /// </summary>
        internal static double LastDrawnWaistRadius { get; private set; } = Global.AnnotationSettings.DEFAULT_LOCATION_LINK_WAIST_PIXELS / 2.0;

        /// <summary>
        /// Half width of a pinched link <paramref name="x"/> world units from A's centre along a link of
        /// <paramref name="length"/>. Each side eases on a raised cosine from its radius down to the neck, flat at
        /// both ends of the curve. Far apart, the neck is <paramref name="waist"/>, reached one diameter from each
        /// centre. Close together, the neck is where straight tapers at that rate would meet, so the link barely
        /// narrows and the two curves meet smoothly. The waist is never wider than the smaller radius.
        /// Matches PinchedHalfWidth in PinchedLineShaders.fx.
        /// </summary>
        internal static double HalfWidthAt(double x, double length, double radiusA, double radiusB, double waist)
        {
            double w = Math.Min(waist, Math.Min(radiusA, radiusB));
            double kA = (radiusA - w) / (2 * radiusA);
            double kB = (radiusB - w) / (2 * radiusB);

            double neck = w;
            double taperA = 2 * radiusA;
            double taperB = 2 * radiusB;
            if (kA + kB > 0)
            {
                double m = Math.Max(0, Math.Min(length, (radiusA - radiusB + (kB * length)) / (kA + kB)));
                double meet = radiusA - (kA * m);
                if (meet > w)
                {
                    neck = meet;
                    taperA = m;
                    taperB = length - m;
                }
            }

            if (x < taperA)
                return neck + ((radiusA - neck) * Ease(x / taperA));
            if (x > length - taperB)
                return neck + ((radiusB - neck) * Ease((length - x) / taperB));
            return neck;
        }

        /// <summary>1 at <paramref name="t"/> = 0 and 0 at 1, with zero slope at both ends.</summary>
        private static double Ease(double t) => 0.5 * (1 + Math.Cos(Math.PI * Math.Max(0, Math.Min(1, t))));

        /// <summary>
        /// <paramref name="position"/> in the link's own frame: <paramref name="along"/> from A's centre toward B,
        /// <paramref name="across"/> the unsigned distance from the centre line.
        /// </summary>
        private void ToLinkFrame(Geometry.Vector2 position, out double along, out double across, out double length)
        {
            Geometry.Vector2 delta = B.Center - A.Center;
            length = Math.Sqrt((delta.X * delta.X) + (delta.Y * delta.Y));
            Geometry.Vector2 offset = position - A.Center;
            if (length <= 0)
            {
                along = 0;
                across = Math.Sqrt((offset.X * offset.X) + (offset.Y * offset.Y));
                return;
            }

            along = ((offset.X * delta.X) + (offset.Y * delta.Y)) / length;
            across = Math.Abs((offset.X * delta.Y) - (offset.Y * delta.X)) / length;
        }

        /// <summary>
        /// Distance from <paramref name="position"/> to the drawn outline (0 inside), and the half width the
        /// position is measured against for <see cref="DistanceFromCenterNormalized"/>.
        /// </summary>
        private double DistanceToOutline(Geometry.Vector2 position, double waist, bool drawCaps, out double normalized)
        {
            ToLinkFrame(position, out double along, out double across, out double length);
            if (!drawCaps)
            {
                // Nothing is drawn inside the markers, so a point there is not on the link.
                foreach (Circle end in new[] { A, B })
                {
                    double fromCentre = Geometry.Vector2.Distance(position, end.Center);
                    if (fromCentre < end.Radius)
                    {
                        normalized = double.MaxValue;
                        return end.Radius - fromCentre;
                    }
                }
            }

            if (drawCaps && (along < 0 || along > length))
            {
                Circle end = along < 0 ? A : B;
                double fromCentre = Geometry.Vector2.Distance(position, end.Center);
                normalized = fromCentre / end.Radius;
                return Math.Max(0, fromCentre - end.Radius);
            }

            double clamped = Math.Max(0, Math.Min(length, along));
            if (!IsMouseOver && FadeDepthAt(clamped, length, A.Radius, B.Radius) > MaxPickableFadeDepth)
            {
                normalized = double.MaxValue;
                return double.MaxValue;
            }

            double halfWidth = HalfWidthAt(clamped, length, A.Radius, B.Radius, waist);
            double beyondEnd = along - clamped;
            double beyondSide = Math.Max(0, across - halfWidth);
            normalized = Math.Sqrt((beyondEnd * beyondEnd) + (across * across)) / halfWidth;
            return Math.Sqrt((beyondEnd * beyondEnd) + (beyondSide * beyondSide));
        }

        private double DistanceToOutline(Geometry.Vector2 position, out double normalized) =>
            DistanceToOutline(position, LastDrawnWaistRadius, Global.AnnotationSettings.LocationLinkDrawUnderMarkers, out normalized);

        public Geometry.LineSegment LineSegment => new(A.Center, B.Center);

        public LocationLinkContextMenuGeneratorDelegate? ContextMenuGenerator = null;

        protected IVolumeTransformProvider mapProvider;

        /// <summary>
        /// Create a link view when both endpoints map into volume space. Returns false without throwing if either point is unmappable.
        /// </summary>
        public static bool TryCreate(LocationLinkKey key, int Z, IVolumeTransformProvider mapProvider, out LocationLinkView view)
        {
            view = null;
            if (mapProvider is null)
                return false;

            if (!Store.Locations.TryGetValue(key.A, out LocationObj locA) ||
                !Store.Locations.TryGetValue(key.B, out LocationObj locB))
            {
                return false;
            }

            if (!TryMapEndpointPositions(locA, locB, Z, mapProvider,
                    out Circle circleA, out Circle circleB,
                    out int sectionA, out int sectionB, out Color color))
            {
                return false;
            }

            view = new LocationLinkView(key, Z, mapProvider, circleA, circleB, sectionA, sectionB, color);
            return true;
        }

        public LocationLinkView(LocationLinkKey key, int Z, IVolumeTransformProvider mapProvider)
        {
            Key = key;
            this.Z = Z;
            this.mapProvider = mapProvider;
            if (!UpdatePropertiesFromLocations(mapProvider))
            {
                throw new ArgumentOutOfRangeException(nameof(key),
                    $"Could not map location link {key} to volume");
            }

            lineView = CreateView();
        }

        public LocationLinkView(LocationObj LocOne, LocationObj LocTwo, int Z, IVolumeTransformProvider mapProvider)
            : this(new LocationLinkKey(
                (LocOne ?? throw new ArgumentNullException(nameof(LocOne))).ID,
                (LocTwo ?? throw new ArgumentNullException(nameof(LocTwo))).ID), Z, mapProvider)
        {
        }

        private LocationLinkView(LocationLinkKey key, int Z, IVolumeTransformProvider mapProvider,
            Circle circleA, Circle circleB, int sectionA, int sectionB, Color color)
        {
            Key = key;
            this.Z = Z;
            this.mapProvider = mapProvider;
            A = circleA;
            B = circleB;
            SetSections(sectionA, sectionB);
            Color = color;
            lineView = CreateView();
        }

        /// <summary>
        /// Maps both link endpoints into volume space. Returns false if either point cannot be mapped.
        /// </summary>
        private bool UpdatePropertiesFromLocations(IVolumeTransformProvider mapProvider)
        {
            if (!Store.Locations.TryGetValue(Key.A, out LocationObj locA) ||
                !Store.Locations.TryGetValue(Key.B, out LocationObj locB))
            {
                return false;
            }

            if (!TryMapEndpointPositions(locA, locB, Z, mapProvider,
                    out Circle circleA, out Circle circleB,
                    out int sectionA, out int sectionB, out Color color))
            {
                return false;
            }

            A = circleA;
            B = circleB;
            SetSections(sectionA, sectionB);
            Color = color;
            return true;
        }

        private void SetSections(int sectionA, int sectionB)
        {
            SectionA = sectionA;
            SectionB = sectionB;
            MinSection = Math.Min(sectionA, sectionB);
            MaxSection = Math.Max(sectionA, sectionB);
        }

        private static bool TryMapEndpointPositions(
            LocationObj locA, LocationObj locB, int displayZ, IVolumeTransformProvider mapProvider,
            out Circle circleA, out Circle circleB,
            out int sectionA, out int sectionB, out Color color)
        {
            circleA = default;
            circleB = default;
            sectionA = 0;
            sectionB = 0;
            color = default;

            // An adjacent end hidden under a location it links to on the viewed section is drawn at that location, so the
            // link does not end in empty space. The sections, colour and labels still describe the real ends.
            LocationObj drawnA = CoveringLocation(locA, locB, displayZ, mapProvider) ?? locA;
            LocationObj drawnB = CoveringLocation(locB, locA, displayZ, mapProvider) ?? locB;

            // Both ends take their geometry from the owner every view draws with, so a link meets what is drawn.
            LocationVolumeGeometry geometryA, geometryB;
            try
            {
                geometryA = LocationVolumeGeometry.For(drawnA, mapProvider);
                geometryB = LocationVolumeGeometry.For(drawnB, mapProvider);
            }
            catch (ArgumentException)
            {
                return false;
            }

            circleA = DrawnCircle(drawnA, geometryA, displayZ);
            circleB = DrawnCircle(drawnB, geometryB, displayZ);

            // A polygon is not the circle made from its centroid and area, so each polygon end on the viewed section is
            // moved to its outline, toward the other end.
            Circle anchoredA = OutlineEnd(drawnA, geometryA, displayZ, circleB.Center) ?? circleA;
            Circle anchoredB = OutlineEnd(drawnB, geometryB, displayZ, circleA.Center) ?? circleB;
            circleA = anchoredA;
            circleB = anchoredB;

            // A drawn line is only its width thick, so its end takes its width from the partner's end (or from the
            // partner's line width when both are lines), and is placed last, toward the partner's settled end.
            bool lineA = IsDrawnAsLine(drawnA, displayZ);
            bool lineB = IsDrawnAsLine(drawnB, displayZ);
            Circle onLineA = (lineA ? LineEnd(drawnA, geometryA, circleB.Center, lineB ? geometryB.Width / 2.0 : circleB.Radius) : null) ?? circleA;
            Circle onLineB = (lineB ? LineEnd(drawnB, geometryB, circleA.Center, lineA ? geometryA.Width / 2.0 : circleA.Radius) : null) ?? circleB;
            circleA = onLineA;
            circleB = onLineB;

            sectionA = (int)Math.Round(locA.Z);
            sectionB = (int)Math.Round(locB.Z);
            int minSection = Math.Min(sectionA, sectionB);
            int maxSection = Math.Max(sectionA, sectionB);

            color = GetLocationLinkColor(LocationCanvasView.ColorForStructure(locA.Parent, 1f), maxSection - minSection, minSection < displayZ ? -1 : 1, false);
            return true;
        }

        /// <summary>
        /// The location on the viewed section that hides <paramref name="end"/>, or null. An end on an adjacent section that
        /// overlaps a location it links to on <paramref name="displayZ"/> is not drawn: Viking shows it only as the up/down
        /// symbol inside that location (the overlapped links in SectionLocationLinksAnnotationsViewModel), so a link to it
        /// would end in empty space. The test is the one that hides it (<see cref="OverlapShape"/> intersection). The
        /// link's own <paramref name="partner"/> is skipped: a link whose ends overlap is drawn as that symbol, not a line.
        /// The first covering location found wins.
        /// </summary>
        private static LocationObj? CoveringLocation(LocationObj end, LocationObj partner, int displayZ, IVolumeTransformProvider mapProvider)
        {
            if (end.Z == displayZ)
                return null;

            SqlGeometry? endShape = null;
            foreach (long linkedID in end.LinksCopy)
            {
                if (linkedID == partner.ID || !Store.Locations.TryGetValue(linkedID, out LocationObj linked) || linked.Z != displayZ)
                    continue;

                endShape ??= OverlapShape(end, displayZ, mapProvider);
                if (endShape is null)
                    return null;

                SqlGeometry? linkedShape = OverlapShape(linked, displayZ, mapProvider);
                if (linkedShape is not null && endShape.STIntersects(linkedShape).IsTrue)
                    return linked;
            }

            return null;
        }

        private static bool IsPolygonLike(LocationType type) =>
            type is LocationType.POLYGON or LocationType.CURVEPOLYGON or LocationType.CLOSEDCURVE;

        /// <summary>
        /// True when <paramref name="loc"/> is drawn as a line seen from <paramref name="displayZ"/>: an open curve or polyline
        /// on the viewed section, or a polyline on an adjacent one (an adjacent open curve is drawn as a circle).
        /// </summary>
        private static bool IsDrawnAsLine(LocationObj loc, int displayZ) =>
            loc.TypeCode == LocationType.POLYLINE || (loc.TypeCode == LocationType.OPENCURVE && displayZ == loc.Z);

        /// <summary>
        /// The circle <paramref name="loc"/> is drawn as, or stands for, seen from <paramref name="displayZ"/>: its circle
        /// (or a circle of its volume radius at its centre) on the viewed section, its marker on an adjacent one.
        /// </summary>
        private static Circle DrawnCircle(LocationObj loc, LocationVolumeGeometry geometry, int displayZ) =>
            displayZ == loc.Z
                ? geometry.Circle ?? new Circle(geometry.Center, geometry.Radius)
                : geometry.AdjacentMarker(Global.AdjacentLocationRadiusScalar);

        /// <summary>
        /// For a location drawn as a line (<see cref="IsDrawnAsLine"/>), the link end anchored on that line toward
        /// <paramref name="partnerCentre"/> with radius <paramref name="partnerRadius"/> (see <see cref="AnchorOnLine"/>).
        /// The line is the one its view draws: the polyline's control points, or the open curve's smoothed
        /// <see cref="LocationVolumeGeometry.Shape"/>, half <see cref="LocationVolumeGeometry.Width"/> either side. Null when
        /// the line has no length.
        /// </summary>
        private static Circle? LineEnd(LocationObj loc, LocationVolumeGeometry geometry, Geometry.Vector2 partnerCentre, double partnerRadius)
        {
            try
            {
                IReadOnlyList<Geometry.Vector2> points = loc.TypeCode == LocationType.OPENCURVE ? geometry.Shape.ToPoints() : geometry.ControlPoints;
                return AnchorOnLine(points, geometry.Width / 2.0, partnerCentre, partnerRadius);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// The circle a link end is drawn from so that the link leaves a drawn line (<paramref name="points"/>, drawn
        /// <paramref name="halfWidth"/> either side) at the point Q nearest <paramref name="partnerCentre"/>, from the line's
        /// edge, as wide as the partner's end: radius r = max(<paramref name="halfWidth"/>, <paramref name="partnerRadius"/>),
        /// centre Q + dir·(halfWidth - r) with dir the unit direction from Q to the partner, so the circle's front is the
        /// line's edge. A bridge only as thick as the line would fade out within a line width and be too small to label.
        /// Null with fewer than two distinct points, or when the partner is on the line.
        /// </summary>
        internal static Circle? AnchorOnLine(IReadOnlyList<Geometry.Vector2> points, double halfWidth, Geometry.Vector2 partnerCentre, double partnerRadius)
        {
            Geometry.Vector2? nearest = null;
            double nearestDistance = double.MaxValue;
            for (int i = 1; i < points.Count; i++)
            {
                if (points[i - 1] == points[i])
                    continue;

                double distance = new LineSegment(points[i - 1], points[i]).DistanceToPoint(partnerCentre, out Geometry.Vector2 onLine);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = onLine;
                }
            }

            if (nearest is null || nearestDistance <= halfWidth)
                return null;

            Geometry.Vector2 q = nearest.Value;
            Geometry.Vector2 dir = (partnerCentre - q) / nearestDistance;
            double radius = Math.Max(halfWidth, partnerRadius);
            return new Circle(q + (dir * (halfWidth - radius)), radius);
        }

        /// <summary>
        /// For a polygon-like location on the viewed section, the link end anchored on its drawn
        /// <see cref="LocationVolumeGeometry.Outline"/> toward <paramref name="partnerCentre"/> (see <see cref="AnchorOnOutline"/>).
        /// Null for any other end, or when the outline cannot be anchored.
        /// </summary>
        private static Circle? OutlineEnd(LocationObj loc, LocationVolumeGeometry geometry, int displayZ, Geometry.Vector2 partnerCentre)
        {
            if (displayZ != loc.Z || !IsPolygonLike(loc.TypeCode))
                return null;

            try
            {
                return geometry.Outline is SqlGeometry outline ? AnchorOnOutline(outline.ToPolygon(), partnerCentre) : null;
            }
            catch (ArgumentException)
            {
                // Not a polygon SQL can give us (a bad ring): keep the circle.
                return null;
            }
        }

        /// <summary>
        /// The circle a link end is drawn from so that the link leaves <paramref name="outline"/> where the line from its
        /// centroid to <paramref name="partnerCentre"/> last crosses the outline (exterior or a hole), heading for the
        /// partner. The circle sits just inside that exit point E: centre E - dir·r, radius r, with dir the unit direction
        /// from E to the partner. r is how thick the cell is there: the distance to the nearest edge from the middle of the
        /// chord the link's line cuts through the cell ending at E, capped at the equal-area radius. So the bridge is never
        /// wider than the cell. The hourglass then starts at the outline, its fade starts there, and its label sits just
        /// outside the cell. Null when the line never crosses the outline or the cell has no thickness there, so the caller
        /// keeps its circle.
        /// </summary>
        internal static Circle? AnchorOnOutline(Polygon outline, Geometry.Vector2 partnerCentre)
        {
            LineSegment[] edges = [.. outline.ExteriorSegments.Concat(outline.InteriorPolygons.SelectMany(p => p.ExteriorSegments))];
            Geometry.Vector2 origin = outline.Centroid;
            if (Geometry.Vector2.Distance(origin, partnerCentre) <= 0)
                return null;

            Geometry.Vector2? exit = FarthestCrossing(edges, new LineSegment(origin, partnerCentre), origin);
            if (exit is null)
                return null;

            Geometry.Vector2 exitPoint = exit.Value;
            double towardPartner = Geometry.Vector2.Distance(exitPoint, partnerCentre);
            if (towardPartner <= 0)
                return null;

            Geometry.Vector2 dir = (partnerCentre - exitPoint) / towardPartner;

            // Walk back from E across the cell to where the line entered it, to measure the cell where the link leaves.
            double span = outline.BoundingBox.Width + outline.BoundingBox.Height;
            Geometry.Vector2 entry = NearestCrossing(edges, new LineSegment(exitPoint, exitPoint - (dir * span)), exitPoint, span * 1e-9) ?? origin;
            Geometry.Vector2 chordMiddle = (entry + exitPoint) / 2.0;
            double thickness = edges.Min(e => e.DistanceToPoint(chordMiddle));

            double radius = Math.Min(thickness, Math.Sqrt(outline.Area / Math.PI));
            if (!(radius > 0))
                return null;

            return new Circle(exitPoint - (dir * radius), radius);
        }

        /// <summary>The crossing of <paramref name="line"/> with any of <paramref name="edges"/> farthest from <paramref name="from"/>, or null.</summary>
        private static Geometry.Vector2? FarthestCrossing(IEnumerable<LineSegment> edges, LineSegment line, Geometry.Vector2 from)
        {
            Geometry.Vector2? best = null;
            double bestDistance = -1;
            foreach (LineSegment edge in edges)
            {
                if (edge.Intersects(in line, out Geometry.Vector2 crossing) && Geometry.Vector2.Distance(from, crossing) > bestDistance)
                {
                    bestDistance = Geometry.Vector2.Distance(from, crossing);
                    best = crossing;
                }
            }

            return best;
        }

        /// <summary>
        /// The crossing of <paramref name="line"/> with any of <paramref name="edges"/> nearest <paramref name="from"/> but more
        /// than <paramref name="minDistance"/> from it (so the crossing <paramref name="from"/> lies on is skipped), or null.
        /// </summary>
        private static Geometry.Vector2? NearestCrossing(IEnumerable<LineSegment> edges, LineSegment line, Geometry.Vector2 from, double minDistance)
        {
            Geometry.Vector2? best = null;
            double bestDistance = double.MaxValue;
            foreach (LineSegment edge in edges)
            {
                if (!edge.Intersects(in line, out Geometry.Vector2 crossing))
                    continue;

                double distance = Geometry.Vector2.Distance(from, crossing);
                if (distance > minDistance && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = crossing;
                }
            }

            return best;
        }

        public void GetCanvasViews(LocationLinkKey key, IVolumeTransformProvider mapProvider, out LocationCanvasView AView, out LocationCanvasView BView)
        {
            LocationObj A = Store.Locations[Key.A];
            LocationObj B = Store.Locations[Key.B];
            IVolumeToSectionTransform MapperA = mapProvider.GetSectionToVolumeTransform((int)Math.Round(A.Z));
            IVolumeToSectionTransform MapperB = mapProvider.GetSectionToVolumeTransform((int)Math.Round(B.Z));

            AView = A.Z == Z ? AnnotationViewFactory.Create(A, MapperA) : AnnotationViewFactory.CreateAdjacent(A, MapperA);
            BView = B.Z == Z ? AnnotationViewFactory.Create(B, MapperB) : AnnotationViewFactory.CreateAdjacent(B, MapperB);
        }

        int ICanvasView.VisualHeight => 0;

        public double LineWidth => LineRadius * 2.0;

        public double LineRadius => Math.Min(A.Radius, B.Radius);

        private LineView CreateView()
        {
            LineView line = new(A.Center, B.Center, LineWidth, Color, LineStyle.Standard);
            return line;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="StructureTypeColor"></param>
        /// <param name="section_span_distance">Number of sections the location link crosses</param>
        /// <param name="direction">Direction the link is in from the current section</param>
        /// <returns></returns>
        internal static Microsoft.Xna.Framework.Color GetLocationLinkColor(Color structure_type_color, int section_span_distance, double direction, bool IsMouseOver)
        {
            if (section_span_distance == 0)
            {
                //This is an error state, we shouldn't have a link between annotations on the same section
                return Color.Red;
            }

            section_span_distance = section_span_distance < 0 ? -1 : 1;

            int red = (int)((float)(structure_type_color.R * .5f) + (128 * direction));
            red = 255 - (red / section_span_distance);
            red = red > 255 ? 255 : red;
            red = red < 0 ? 0 : red;
            int blue = (int)((float)(structure_type_color.B * .5f) + (128 * -direction));
            blue = 255 - (blue / section_span_distance);
            blue = blue > 255 ? 255 : blue;
            blue = blue < 0 ? 0 : blue;
            int green = (int)((float)structure_type_color.G);
            green = 255 - (green / section_span_distance);
            green = green < 0 ? 0 : green;

            // Fully opaque: the Link visibility preference sets the opacity a link is drawn with.
            int alpha = 255;

            //If you don't cast to byte the wrong constructor is used and the alpha value is wrong
            return new Microsoft.Xna.Framework.Color((byte)(red),
                (byte)(green),
                (byte)(blue),
                (byte)(alpha));
        }

        /// <summary>
        /// True when the two linked locations overlap as seen from <see cref="Z"/>. An overlapped link is not drawn
        /// as a line; the section shows the small up/down circle symbol instead.
        /// </summary>
        /// <remarks>
        /// Any overlap counts, including touching. Each endpoint is tested with the shape it is drawn with, except that
        /// an endpoint on an adjacent section which is a polygon (polygon, curve polygon or closed curve) is tested
        /// with its real outline, mapped into volume space, and not with the inscribed circle that stands in for it on
        /// screen. A polygon whose circle is clear of the other location can still overlap it. Links whose
        /// endpoints are on the same section never overlap.
        /// Called while the link is added to a section, under the tracker's lock, so it must stay cheap and must not
        /// throw for a bad polygon: that case falls back to the circle. An endpoint that cannot be mapped into the
        /// volume at all (the factory throws <see cref="ArgumentException"/>) leaves the answer unknown, and the
        /// link is reported as not overlapped so its line is drawn.
        /// </remarks>
        /// <returns>
        /// False when an endpoint is no longer in the location store (deleted, or evicted when another section was
        /// opened) after the caller found it; <paramref name="overlaps"/> is then meaningless and the link should be
        /// skipped. Reads the store with the same non-throwing lookup the caller's checks use.
        /// </returns>
        public bool TryLinksOverlap(out bool overlaps)
        {
            overlaps = false;
            if (LinkDirection == 0) //Links on the same section never overlap, this is to ensure they are displayed and because the convention of Viking is annotations of the same structure do not overlap
            {
                return true;
            }

            if (!Store.Locations.TryGetValue(Key.A, out LocationObj locationA) ||
                !Store.Locations.TryGetValue(Key.B, out LocationObj locationB))
            {
                return false;
            }

            SqlGeometry? shapeA = OverlapShape(locationA, Z, mapProvider);
            SqlGeometry? shapeB = OverlapShape(locationB, Z, mapProvider);
            overlaps = shapeA is not null && shapeB is not null && shapeA.STIntersects(shapeB).IsTrue;
            return true;
        }

        /// <summary>
        /// The shape one endpoint is tested with in <see cref="TryLinksOverlap"/> (and <see cref="CoveringLocation"/>), in
        /// volume space as seen from <paramref name="displayZ"/>, or null when the location cannot be mapped into the volume.
        /// </summary>
        private static SqlGeometry? OverlapShape(LocationObj location, int displayZ, IVolumeTransformProvider mapProvider)
        {
            IVolumeToSectionTransform mapper = mapProvider.GetSectionToVolumeTransform((int)Math.Round(location.Z));
            bool onViewedSection = location.Z == displayZ;
            // On the viewed section a polygon view hit-tests the owner's Outline, so use it directly and skip a view that
            // smooths and triangulates the ring in the background. Off the viewed section a polygon-like end is tested with
            // that same outline, not the marker circle. Only an exact mapping counts; otherwise the drawn shape is tested.
            bool useOutline = onViewedSection
                ? location.TypeCode is LocationType.POLYGON or LocationType.CURVEPOLYGON
                : IsPolygonType(location.TypeCode);
            if (useOutline)
            {
                try
                {
                    LocationVolumeGeometry geometry = LocationVolumeGeometry.For(location, mapper);
                    if (geometry.IsExact && geometry.Outline is SqlGeometry outline)
                        return outline;
                }
                catch (ArgumentException)
                {
                    // Falls through to the shape it is drawn with.
                }
            }

            try
            {
                LocationCanvasView view = onViewedSection
                    ? AnnotationViewFactory.Create(location, mapper)
                    : AnnotationViewFactory.CreateAdjacent(location, mapper);
                return view.VolumeShapeAsRendered;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static bool IsPolygonType(LocationType type) =>
            type is LocationType.POLYGON or LocationType.CURVEPOLYGON or LocationType.CLOSEDCURVE;

        #region IUIObjectBasic Members

        public override System.Windows.Forms.ContextMenuStrip ContextMenu
        {
            get
            {
                if (ContextMenuGenerator != null)
                {
                    return ContextMenuGenerator(this);
                }

                return LinkContextMenu();
            }
        }

        /// <summary>
        /// The menu a right-click on the link opens. Go to partner pans the view to whichever end is farther from where the
        /// mouse was when the menu opened, the other side of the link from the end the user is looking at. It stays on the
        /// viewed section and keeps the zoom: the partner's end is drawn here, so there is no need to change section.
        /// </summary>
        private System.Windows.Forms.ContextMenuStrip LinkContextMenu()
        {
            Geometry.Vector2 mouse = AnnotationOverlay.CurrentOverlay?.MouseWorldPosition ?? LineSegment.PointAlongLine(0.5);
            bool aIsPartner = AIsPartnerOf(mouse, A.Center, B.Center);
            long partnerID = aIsPartner ? Key.A : Key.B;
            Geometry.Vector2 partnerEnd = aIsPartner ? A.Center : B.Center;
            int viewedSection = Z;

            System.Windows.Forms.ContextMenuStrip menu = new();
            System.Windows.Forms.ToolStripMenuItem goToPartner = new($"Go to partner {partnerID}");
            goToPartner.Click += (sender, e) =>
                Viking.UI.State.ViewerControl?.GoToLocation(new Vector2((float)partnerEnd.X, (float)partnerEnd.Y), viewedSection);
            menu.Items.Add(goToPartner);
            return menu;
        }

        public override string ToolTip => Key.A.ToString() + " -> " + Key.B.ToString();

        LocationLinkKey IViewLocationLink.Key => Key;

        public override void Save() => throw new NotImplementedException();

        #endregion

        public override void Delete()
        {
            CallBeforeDelete();

            Store.LocationLinks.DeleteLink(Key.A, Key.B);

            CallAfterDelete();
        }

        public static bool IsValidLocationLinkTarget(LocationObj target, LocationObj OriginObj)
        {
            if (target is null || OriginObj is null)
            {
                return false;
            }

            //Check to make sure it isn't the same structure on the same section
            if (target.ParentID != OriginObj.ParentID)
            {
                return false;
            }

            if (target.Z == OriginObj.Z)
            {
                return false;
            }

            if (OriginObj.LinksCopy.Contains(target.ID))
            {
                return false;
            }

            return true;
        }

        public bool IsVisible(Scene scene) => Math.Min(LineSegment.Length, LineWidth) / scene.Camera.Downsample > 2.0;

        public bool Contains(Geometry.Vector2 Position) => DistanceToOutline(Position, out _) <= 0;

        public bool Intersects(LineSegment line) => LineSegment.Intersects(line);

        public double Distance(Geometry.Vector2 Position) => DistanceToOutline(Position, out _);

        public double Distance(Microsoft.SqlServer.Types.SqlGeometry shape) => LineSegment.ToSqlGeometry().STDistance(shape).Value;

        public double DistanceFromCenterNormalized(Geometry.Vector2 Position)
        {
            DistanceToOutline(Position, out double normalized);
            return normalized;
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.LumaOverlayRoundLineManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          IEnumerable<LocationLinkView> listToDraw)
        {
            LocationLinkView[] links = [.. listToDraw.Where(l => l is not null)];
            if (links.Length == 0)
                return;

            float visibility = Global.AnnotationSettings.LocationLinkVisibility;
            Color DrawnColor(LocationLinkView l) => FillColorFor(l.Color).SetAlpha(
                DrawnAlpha(l.Alpha, MathHelper.Lerp(visibility, 1f, l.HoverAmount)) * ClosenessOpacity(l.LineSegment.Length, l.A.Radius, l.B.Radius));
            float DrawnWaistOpacity(LocationLinkView l) => MathHelper.Lerp(WaistOpacity, 1f, l.HoverAmount);

            if (!lineManager.SupportsPinched)
            {
                LineView[] lineViews = [.. links.Select(l => new LineView(l.A.Center, l.B.Center, l.LineWidth, DrawnColor(l), LineStyle.Standard))];
                LineView.Draw(device, scene, lineManager, lineViews);
                return;
            }

            // The waist is a fixed number of screen pixels, so convert it to world units at this zoom.
            double waistRadius = Global.AnnotationSettings.LocationLinkWaistPixels / 2.0 * scene.Camera.Downsample;
            LastDrawnWaistRadius = waistRadius;
            bool drawCaps = Global.AnnotationSettings.LocationLinkDrawUnderMarkers;
            float time = (float)(DateTime.UtcNow.Millisecond / 1000.0);

            foreach (IGrouping<(Color Color, float WaistOpacity), LocationLinkView> group in links.GroupBy(l =>
                (lineManager.UseHSLColor ? DrawnColor(l).ConvertToHCL() : DrawnColor(l), DrawnWaistOpacity(l))))
            {
                RoundLineCode.RoundLine[] lines = [.. group.Select(l => new RoundLineCode.RoundLine(
                    new Vector2((float)l.A.Center.X, (float)l.A.Center.Y),
                    new Vector2((float)l.B.Center.X, (float)l.B.Center.Y)))];
                Vector4[] shapes = [.. group.Select(l => new Vector4(
                    (float)l.A.Radius,
                    (float)l.B.Radius,
                    (float)Math.Min(waistRadius, Math.Min(l.A.Radius, l.B.Radius)),
                    0f))];

                // The border and shadow are plain RGB even for the HSL manager; see DrawPinched.
                Color contrast = BorderColorFor(group.First().Color);

                lineManager.DrawPinched(lines, shapes, group.Key.Color, scene.ViewProj, time, drawCaps,
                    group.Key.WaistOpacity, contrast, BorderPixels,
                    contrast, ShadowPixels, (float)scene.Camera.Downsample);
            }
        }

        /// <summary>
        /// Labels both ends of each link the mouse is over (fading in and out with <see cref="HoverAmount"/>), just beyond its
        /// markers where the link is strongest, with the ID of the location at the other end. The text runs along the link
        /// and is turned to stay upright. Each
        /// label is sized in volume space from its marker's radius (<see cref="LabelHeightPerRadius"/>), so it zooms with
        /// the image; one too small to read is skipped, and a link too short for both labels gets none. The caller has
        /// begun <paramref name="spriteBatch"/> with premultiplied alpha blending.
        /// </summary>
        public static void DrawLabels(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                                      Microsoft.Xna.Framework.Graphics.SpriteFont font,
                                      VikingXNA.Scene scene,
                                      IEnumerable<LocationLinkView> listToDraw)
        {
            float visibility = Global.AnnotationSettings.LocationLinkVisibility;

            // Labels sit on the image, so they are always outlined, in the colors the label outline preference picks.
            LabelOutlineStyle style = LabelView.OutlineStyleAccessor();
            (Color textColor, Color outlineColor) = LabelView.OutlineColors(Color.Black,
                style == LabelOutlineStyle.Off ? LabelOutlineStyle.BlackTextWhiteOutline : style);

            foreach (LocationLinkView link in listToDraw.Where(l => l is not null))
            {
                Geometry.Vector2 screenA = scene.WorldToScreen(link.A.Center);
                Geometry.Vector2 screenB = scene.WorldToScreen(link.B.Center);
                Vector2 delta = new((float)(screenB.X - screenA.X), (float)(screenB.Y - screenA.Y));
                double screenLength = delta.Length();
                double worldLength = link.LineSegment.Length;
                if (screenLength <= 0 || worldLength <= 0)
                    continue;

                double pixelsPerUnit = screenLength / worldLength;
                double radiusA = link.A.Radius * pixelsPerUnit;
                double radiusB = link.B.Radius * pixelsPerUnit;
                Vector2 towardB = delta / (float)screenLength;

                // Near A the label names B; near B it names A. Text runs along the link and is turned over to stay upright.
                bool uprightAtA = towardB.X >= 0;
                bool uprightAtB = towardB.X <= 0;
                string textA = link.Key.B.ToString();
                string textB = link.Key.A.ToString();
                Vector2 sizeA = font.MeasureString(textA);
                Vector2 sizeB = font.MeasureString(textB);
                float scaleA = (float)(LabelHeightPerRadius * radiusA / font.LineSpacing);
                float scaleB = (float)(LabelHeightPerRadius * radiusB / font.LineSpacing);
                double gapA = LabelGapPerRadius * radiusA;
                double gapB = LabelGapPerRadius * radiusB;

                if (!EndLabelsFit(screenLength - radiusA - radiusB, sizeA.X * scaleA, gapA, sizeB.X * scaleB, gapB))
                    continue;

                // The number is shown only while the mouse is over the link, fading with its hovered look.
                float alpha = LabelAlpha(link.Alpha, visibility) * link.HoverAmount;
                if (alpha <= 0f)
                    continue;

                Color text = textColor * alpha;
                Color outline = outlineColor * alpha;

                if (LabelHeightPerRadius * radiusA >= MinLabelPixels)
                {
                    DrawEndLabel(spriteBatch, font, screenA, towardB, radiusA, gapA, textA, sizeA, uprightAtA, scaleA, text, outline,
                        SectionArrowAt(link.SectionA, link.SectionB, link.Z));
                }

                if (LabelHeightPerRadius * radiusB >= MinLabelPixels)
                {
                    DrawEndLabel(spriteBatch, font, screenB, -towardB, radiusB, gapB, textB, sizeB, uprightAtB, scaleB, text, outline,
                        SectionArrowAt(link.SectionB, link.SectionA, link.Z));
                }
            }
        }

        /// <summary>
        /// Draws <paramref name="text"/> along the link, starting <paramref name="gap"/> screen pixels beyond the edge of the
        /// marker centred at <paramref name="markerCentre"/> and running <paramref name="towardOther"/>. A
        /// <paramref name="sectionArrow"/> of 1 adds an up arrow (<see cref="ArrowGlyph"/>) on the line above the text, -1 a
        /// down arrow on the line below, each at the end of the line nearest the marker and outlined like the text; 0 adds
        /// none.
        /// </summary>
        private static void DrawEndLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                                         Microsoft.Xna.Framework.Graphics.SpriteFont font,
                                         Geometry.Vector2 markerCentre, Vector2 towardOther, double markerRadius, double gap,
                                         string text, Vector2 textSize, bool upright, float fontScale,
                                         Color textColor, Color outlineColor, int sectionArrow)
        {
            float along = (float)(markerRadius + gap) + (textSize.X * fontScale / 2f);
            Vector2 position = new Vector2((float)markerCentre.X, (float)markerCentre.Y) + (towardOther * along);
            float rotation = (float)Math.Atan2(towardOther.Y, towardOther.X) + (upright ? 0f : MathHelper.Pi);
            LabelView.DrawOutlinedString(spriteBatch, font, text, position, textColor, outlineColor, rotation, textSize / 2f, fontScale);

            if (sectionArrow == 0)
                return;

            Microsoft.Xna.Framework.Graphics.Texture2D arrow = ArrowGlyph.Get(spriteBatch.GraphicsDevice);

            // The text's own axes: along its line, and down to the next line.
            Vector2 alongText = new((float)Math.Cos(rotation), (float)Math.Sin(rotation));
            Vector2 downText = new(-alongText.Y, alongText.X);
            float lineHeight = font.LineSpacing * fontScale;
            float iconSize = lineHeight * 0.9f;

            // Upright text starts at the marker; turned-over text ends there.
            float halfWidth = textSize.X * fontScale / 2f;
            float acrossLine = upright ? -halfWidth + (iconSize / 2f) : halfWidth - (iconSize / 2f);
            Vector2 iconCentre = position + (alongText * acrossLine) + (downText * (sectionArrow > 0 ? -lineHeight : lineHeight));

            // The arrow names a section direction, not a screen direction, so it is not turned with the text.
            LabelView.DrawOutlinedTexture(spriteBatch, arrow, iconCentre, textColor, outlineColor,
                new Vector2(arrow.Width / 2f, arrow.Height / 2f), iconSize / arrow.Width,
                sectionArrow > 0 ? Microsoft.Xna.Framework.Graphics.SpriteEffects.None : Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipVertically);
        }

        public bool Equals(LocationLinkView other)
        {
            if (other is null)
            {
                return false;
            }

            return Key.Equals(other.Key) && Z == other.Z;
        }
    }
}
