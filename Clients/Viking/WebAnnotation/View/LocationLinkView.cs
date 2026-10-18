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

            set
            {
                _Color.SetAlpha(value);
                lineView?.Color = lineView.Color.SetAlpha(value);
            }
        }

        public int MinSection { get; private set; }
        public int MaxSection { get; private set; }

        protected int LinkDirection => MinSection == MaxSection ? 0 : MinSection < Z ? -1 : 1;

        public Geometry.Rectangle BoundingBox => Geometry.Rectangle.Pad(LineSegment.BoundingBox, MaxRadius);

        /// <summary>The larger endpoint radius: the widest the pinched link gets.</summary>
        public double MaxRadius => Math.Max(A.Radius, B.Radius);

        /// <summary>How much more opaque the link is at its waist than at full width: alpha * (1 + boost).</summary>
        internal const float WaistOpacityBoost = 1.0f;

        /// <summary>
        /// Waist half width in world units at the zoom links were last drawn at. Hit tests have no scene, and every
        /// link is drawn with the same camera, so they use this.
        /// </summary>
        internal static double LastDrawnWaistRadius { get; private set; } = Global.AnnotationSettings.DEFAULT_LOCATION_LINK_WAIST_PIXELS / 2.0;

        /// <summary>
        /// Half width of a pinched link <paramref name="x"/> world units from A's centre along a link of
        /// <paramref name="length"/>. Each side narrows at a constant rate from its radius and reaches
        /// <paramref name="waist"/> one diameter from its centre. The outline is the union of the two tapers, so when
        /// the markers are close the two sides meet first and the link barely narrows. The waist is never wider than the smaller radius.
        /// Matches PinchedHalfWidth in PinchedLineShaders.fx.
        /// </summary>
        internal static double HalfWidthAt(double x, double length, double radiusA, double radiusB, double waist)
        {
            double w = Math.Min(waist, Math.Min(radiusA, radiusB));
            double kA = (radiusA - w) / (2 * radiusA);
            double kB = (radiusB - w) / (2 * radiusB);
            return Math.Max(w, Math.Max(radiusA - (kA * x), radiusB - (kB * (length - x))));
        }

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

            int alpha = 64;

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
        public bool LinksOverlap()
        {
            if (LinkDirection == 0) //Links on the same section never overlap, this is to ensure they are displayed and because the convention of Viking is annotations of the same structure do not overlap
            {
                return false;
            }

            SqlGeometry? shapeA = OverlapShape(Store.Locations[Key.A]);
            SqlGeometry? shapeB = OverlapShape(Store.Locations[Key.B]);
            return shapeA is not null && shapeB is not null && shapeA.STIntersects(shapeB).IsTrue;
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

            if (!lineManager.SupportsPinched)
            {
                LineView.Draw(device, scene, lineManager, [.. links.Select(l => l.lineView)]);
                return;
            }

            // The waist is a fixed number of screen pixels, so convert it to world units at this zoom.
            double waistRadius = Global.AnnotationSettings.LocationLinkWaistPixels / 2.0 * scene.Camera.Downsample;
            LastDrawnWaistRadius = waistRadius;
            bool drawCaps = Global.AnnotationSettings.LocationLinkDrawUnderMarkers;
            float time = (float)(DateTime.UtcNow.Millisecond / 1000.0);

            foreach (IGrouping<Color, LocationLinkView> group in links.GroupBy(l => lineManager.UseHSLColor ? l.Color.ConvertToHCL() : l.Color))
            {
                RoundLineCode.RoundLine[] lines = [.. group.Select(l => new RoundLineCode.RoundLine(
                    new Vector2((float)l.A.Center.X, (float)l.A.Center.Y),
                    new Vector2((float)l.B.Center.X, (float)l.B.Center.Y)))];
                Vector4[] shapes = [.. group.Select(l => new Vector4(
                    (float)l.A.Radius,
                    (float)l.B.Radius,
                    (float)Math.Min(waistRadius, Math.Min(l.A.Radius, l.B.Radius)),
                    0f))];

                lineManager.DrawPinched(lines, shapes, group.Key, scene.ViewProj, time, drawCaps, WaistOpacityBoost);
            }
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
