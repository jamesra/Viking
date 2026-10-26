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

            // SetAlpha returns a new colour, so assign it back; the mouse-over effect halves the link's alpha here.
            set => Color = _Color.SetAlpha(value);
        }

        public int MinSection { get; private set; }
        public int MaxSection { get; private set; }

        protected int LinkDirection => MinSection == MaxSection ? 0 : MinSection < Z ? -1 : 1;

        public Geometry.Rectangle BoundingBox => Geometry.Rectangle.Pad(LineSegment.BoundingBox, MaxRadius);

        /// <summary>The larger endpoint radius: the widest the pinched link gets.</summary>
        public double MaxRadius => Math.Max(A.Radius, B.Radius);

        /// <summary>
        /// Opacity at the link's waist as a fraction of its opacity at full width. The shader fades alpha linearly
        /// with how far the local width has narrowed from full width toward the waist, so a link is strongest next to the
        /// locations it joins and faint in the middle.
        /// </summary>
        internal const float WaistOpacity = 0.3f;

        /// <summary>Width in screen pixels of the contrasting border drawn just inside a link's outline.</summary>
        internal const float BorderPixels = 1.5f;

        /// <summary>
        /// Width in screen pixels of the contrasting shadow drawn outside a link's outline. It is as opaque as the link at
        /// the outline and fades to nothing this far out.
        /// </summary>
        internal const float ShadowPixels = 5f;

        /// <summary>Height in screen pixels of the text labelling each end of a link.</summary>
        internal const float LabelPixels = 14f;

        /// <summary>Screen pixels between a marker's edge and the label beside it, and between the two labels.</summary>
        internal const float LabelGapPixels = 4f;

        /// <summary>
        /// The opacity a link of stored alpha <paramref name="linkAlpha"/> is drawn with at <paramref name="visibility"/>
        /// (0.1 to 1, from the Link visibility preference). Links are stored fully opaque; the mouse-over effect halves the
        /// stored alpha, and the drawn opacity follows it.
        /// </summary>
        internal static float DrawnAlpha(float linkAlpha, float visibility) => MathHelper.Clamp(linkAlpha * visibility, 0f, 1f);

        /// <summary>
        /// Opacity of the end labels for a link of stored alpha <paramref name="linkAlpha"/>. Labels stay readable at low
        /// visibility: fully opaque from 50% up, and never below half, but they fade with the link on mouse-over.
        /// </summary>
        internal static float LabelAlpha(float linkAlpha, float visibility) => MathHelper.Clamp(linkAlpha * Math.Min(1f, 0.5f + visibility), 0f, 1f);

        /// <summary>
        /// The label beside one end of a link: the ID of the location at the other end, with an arrow pointing to it.
        /// <paramref name="upright"/> is true when the link runs left to right on screen from this end, so the text reads
        /// toward the other end and the arrow follows the ID ("34532 ->"). Otherwise the text is turned over to stay
        /// upright and reads back toward this end, so the arrow comes first ("&lt;- 34532"). Either way the arrow points
        /// away from this end.
        /// </summary>
        internal static string EndLabelText(long otherID, bool upright) => upright ? $"{otherID} ->" : $"<- {otherID}";

        /// <summary>
        /// True when labels <paramref name="widthA"/> and <paramref name="widthB"/> pixels wide fit, with their gaps, in the
        /// <paramref name="roomPixels"/> of link between the two markers' edges.
        /// </summary>
        internal static bool EndLabelsFit(double roomPixels, double widthA, double widthB) =>
            roomPixels >= widthA + widthB + (3 * LabelGapPixels);

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
                    out int minSection, out int maxSection, out Color color))
            {
                return false;
            }

            view = new LocationLinkView(key, Z, mapProvider, circleA, circleB, minSection, maxSection, color);
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
            Circle circleA, Circle circleB, int minSection, int maxSection, Color color)
        {
            Key = key;
            this.Z = Z;
            this.mapProvider = mapProvider;
            A = circleA;
            B = circleB;
            MinSection = minSection;
            MaxSection = maxSection;
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
                    out int minSection, out int maxSection, out Color color))
            {
                return false;
            }

            A = circleA;
            B = circleB;
            MinSection = minSection;
            MaxSection = maxSection;
            Color = color;
            return true;
        }

        private static bool TryMapEndpointPositions(
            LocationObj locA, LocationObj locB, int displayZ, IVolumeTransformProvider mapProvider,
            out Circle circleA, out Circle circleB,
            out int minSection, out int maxSection, out Color color)
        {
            circleA = default;
            circleB = default;
            minSection = 0;
            maxSection = 0;
            color = default;

            IVolumeToSectionTransform sourceMapper = mapProvider.GetSectionToVolumeTransform((int)Math.Round(locA.Z));
            IVolumeToSectionTransform targetMapper = mapProvider.GetSectionToVolumeTransform((int)Math.Round(locB.Z));

            if (!sourceMapper.TrySectionToVolume(locA.Position, out Geometry.Vector2 aVolumePosition))
                return false;
            if (!targetMapper.TrySectionToVolume(locB.Position, out Geometry.Vector2 bVolumePosition))
                return false;

            circleA = new Circle(aVolumePosition, locA.Radius * (displayZ == locA.Z ? 1.0 : Global.AdjacentLocationRadiusScalar));
            circleB = new Circle(bVolumePosition, locB.Radius * (displayZ == locB.Z ? 1.0 : Global.AdjacentLocationRadiusScalar));

            minSection = (int)Math.Round(locA.Z < locB.Z ? locA.Z : locB.Z);
            maxSection = (int)Math.Round(locA.Z < locB.Z ? locB.Z : locA.Z);

            color = GetLocationLinkColor(LocationCanvasView.ColorForStructure(locA.Parent, 1f), maxSection - minSection, minSection < displayZ ? -1 : 1, false);
            return true;
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
            //IVolumeToSectionMapper sourceMapper = mapProvider.GetMapping((int)Math.Round(A.Z));
            //IVolumeToSectionMapper targetMapper = mapProvider.GetMapping((int)Math.Round(B.Z));
            //Geometry.Vector2 sourceVolumePosition = sourceMapper.SectionToVolume(A.Position);
            //Geometry.Vector2 targetVolumePosition = targetMapper.SectionToVolume(B.Position); 

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

            SqlGeometry? shapeA = OverlapShape(locationA);
            SqlGeometry? shapeB = OverlapShape(locationB);
            overlaps = shapeA is not null && shapeB is not null && shapeA.STIntersects(shapeB).IsTrue;
            return true;
        }

        /// <summary>
        /// The shape one endpoint is tested with in <see cref="LinksOverlap"/>, in volume space, or null when the
        /// location cannot be mapped into the volume.
        /// </summary>
        private SqlGeometry? OverlapShape(LocationObj location)
        {
            IVolumeToSectionTransform mapper = mapProvider.GetSectionToVolumeTransform((int)Math.Round(location.Z));
            bool onViewedSection = location.Z == Z;
            // On the viewed section a polygon view reports its mapped polygon as VolumeShapeAsRendered, so map the
            // shape directly and skip a view that smooths and triangulates the ring in the background. A closed
            // curve view builds its rendered shape from its own control points, so it still goes through the view.
            bool mapDirectly = onViewedSection
                ? location.TypeCode is LocationType.POLYGON or LocationType.CURVEPOLYGON
                : IsPolygonType(location.TypeCode);
            if (mapDirectly)
            {
                try
                {
                    SqlGeometry? mapped = mapper.TryMapShapeSectionToVolume(location.MosaicShape);
                    if (mapped is not null)
                        return mapped;
                }
                catch (ArgumentException)
                {
                    // A polygon SQL cannot represent is tested by its circle, the shape it is drawn with.
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

                return null;
            }
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
            Color DrawnColor(LocationLinkView l) => l.Color.SetAlpha(DrawnAlpha(l.Alpha, visibility));

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

            foreach (IGrouping<Color, LocationLinkView> group in links.GroupBy(l => lineManager.UseHSLColor ? DrawnColor(l).ConvertToHCL() : DrawnColor(l)))
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

                lineManager.DrawPinched(lines, shapes, group.Key, scene.ViewProj, time, drawCaps, WaistOpacity, contrast, BorderPixels,
                    contrast, ShadowPixels, (float)scene.Camera.Downsample);
            }
        }

        /// <summary>
        /// Labels both ends of each link, just beyond its markers where the link is strongest, with the ID of the location
        /// at the other end and an arrow pointing to it. The text runs along the link and is turned to stay upright. A link
        /// too short on screen for both labels gets none. The caller has begun <paramref name="spriteBatch"/> with
        /// premultiplied alpha blending.
        /// </summary>
        public static void DrawLabels(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                                      Microsoft.Xna.Framework.Graphics.SpriteFont font,
                                      VikingXNA.Scene scene,
                                      IEnumerable<LocationLinkView> listToDraw)
        {
            float fontScale = LabelPixels / font.LineSpacing;
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

                // Near A the label names B and points toward it; near B it names A and points back.
                bool uprightAtA = towardB.X >= 0;
                bool uprightAtB = towardB.X <= 0;
                string textA = EndLabelText(link.Key.B, uprightAtA);
                string textB = EndLabelText(link.Key.A, uprightAtB);
                Vector2 sizeA = font.MeasureString(textA);
                Vector2 sizeB = font.MeasureString(textB);

                if (!EndLabelsFit(screenLength - radiusA - radiusB, sizeA.X * fontScale, sizeB.X * fontScale))
                    continue;

                float alpha = LabelAlpha(link.Alpha, visibility);
                Color text = textColor * alpha;
                Color outline = outlineColor * alpha;

                DrawEndLabel(spriteBatch, font, screenA, towardB, radiusA, textA, sizeA, uprightAtA, fontScale, text, outline);
                DrawEndLabel(spriteBatch, font, screenB, -towardB, radiusB, textB, sizeB, uprightAtB, fontScale, text, outline);
            }
        }

        /// <summary>
        /// Draws <paramref name="text"/> along the link, starting <see cref="LabelGapPixels"/> beyond the edge of the marker
        /// centred at <paramref name="markerCentre"/> and running <paramref name="towardOther"/>.
        /// </summary>
        private static void DrawEndLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                                         Microsoft.Xna.Framework.Graphics.SpriteFont font,
                                         Geometry.Vector2 markerCentre, Vector2 towardOther, double markerRadius,
                                         string text, Vector2 textSize, bool upright, float fontScale,
                                         Color textColor, Color outlineColor)
        {
            float along = (float)markerRadius + LabelGapPixels + (textSize.X * fontScale / 2f);
            Vector2 position = new Vector2((float)markerCentre.X, (float)markerCentre.Y) + (towardOther * along);
            float rotation = (float)Math.Atan2(towardOther.Y, towardOther.X) + (upright ? 0f : MathHelper.Pi);
            LabelView.DrawOutlinedString(spriteBatch, font, text, position, textColor, outlineColor, rotation, textSize / 2f, fontScale);
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
