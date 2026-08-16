using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
#if NETFRAMEWORK
using System.Windows.Forms;
#endif
using Viking.AnnotationServiceTypes;
using Viking.Common;
using Viking.VolumeModel;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotation.View;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.ViewModel
{

#if NETFRAMEWORK
    public delegate ContextMenuStrip LocationLinkContextMenuGeneratorDelegate(IViewLocationLink key);
#endif

    /// <summary>
    /// This class represents a link between locations. This object is a little unique because it is
    /// not tied to the database object like the other *obj classes
    /// </summary>
    public class LocationLinkView : Viking.Objects.UIObjBase, ICanvasGeometryView, IEquatable<LocationLinkView>, IColorView, IViewLocationLink
#if NETFRAMEWORK
        , IContextMenu
#endif
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
                if (lineView != null)
                    lineView.Color = value;
            }
        }

        public float Alpha
        {
            get => _Color.GetAlpha();

            set
            {
                _Color.SetAlpha(value);
                if (lineView != null)
                    lineView.Color = lineView.Color.SetAlpha(value);
            }
        }

        public int MinSection { get; private set; }
        public int MaxSection { get; private set; }

        protected int LinkDirection => MinSection == MaxSection ? 0 : MinSection < Z ? -1 : 1;

        public Rectangle BoundingBox => Rectangle.Pad(LineSegment.BoundingBox, LineRadius);

        public Geometry.LineSegment LineSegment => new(A.Center, B.Center);

#if NETFRAMEWORK
        public LocationLinkContextMenuGeneratorDelegate? ContextMenuGenerator = null;
#endif

        protected IVolumeTransformProvider mapProvider;

        /// <summary>
        /// Create a link view when both endpoints map into volume space. Returns false without throwing if either point is unmappable.
        /// </summary>
        public static bool TryCreate(LocationLinkKey key, int Z, IVolumeTransformProvider mapProvider, out LocationLinkView? view)
        {
            view = null;
            if (mapProvider is null)
                return false;

            if (!Store.Locations.TryGetObjectByID(key.A, out LocationObj locA) ||
                !Store.Locations.TryGetObjectByID(key.B, out LocationObj locB))
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

        private readonly bool _LocationsOverlapped;

        /// <summary>
        /// Maps both link endpoints into volume space. Returns false if either point cannot be mapped.
        /// </summary>
        private bool UpdatePropertiesFromLocations(IVolumeTransformProvider mapProvider)
        {
            if (!Store.Locations.TryGetObjectByID(Key.A, out LocationObj locA) ||
                !Store.Locations.TryGetObjectByID(Key.B, out LocationObj locB))
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

            uint typeColor = locA.Parent?.Type?.Color ?? locB.Parent?.Type?.Color ?? 0x808080u;
            color = GetLocationLinkColor(typeColor.ToXNAColor(), maxSection - minSection, minSection < displayZ ? -1 : 1, false);
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
        private static Microsoft.Xna.Framework.Color GetLocationLinkColor(Color structure_type_color, int section_span_distance, double direction, bool IsMouseOver)
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
        /// Return true if the locations overlap when viewed from the passed section
        /// </summary>
        /// <param name="sectionNumber"></param>
        /// <returns></returns>
        public bool LinksOverlap()
        {
            if (LinkDirection == 0) //Links on the same section never overlap, this is to ensure they are displayed and because the convention of Viking is annotations of the same structure do not overlap
            {
                return false;
            }

            GetCanvasViews(Key, mapProvider, out LocationCanvasView AView, out LocationCanvasView BView);

            return AView.Intersects(BView.VolumeShapeAsRendered);
            /*
            this.Key.A 
            int sectionNumber = Z; 
            return A.Intersects(B);
            */
            /*
            //Don't draw if the link falls within the radius of the location we are drawing
            if (A.Section == sectionNumber)
            {
                return A.VolumeShape.STIntersects(B.VolumeShape).IsTrue;
                //return Geometry.Vector2.Distance(A.VolumePosition, B.VolumePosition) <= A.Radius + LocationCanvasView.CalcOffSectionRadius((float)B.Radius);
            }

            if (B.Section == sectionNumber)
            {
                return B.VolumeShape.STIntersects(A.VolumeShape).IsTrue;
                //return Geometry.Vector2.Distance(A.VolumePosition, B.VolumePosition) <= B.Radius + LocationCanvasView.CalcOffSectionRadius((float)A.Radius);
            } 
            
            return false; 
            */
        }

        #region IUIObjectBasic Members

#if NETFRAMEWORK
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
#endif

        public override string ToolTip => Key.A.ToString() + " -> " + Key.B.ToString();

        LocationLinkKey IViewLocationLink.Key => Key;

        public override void Save() => throw new NotImplementedException();

        #endregion

        public override void Delete()
        {
            CallBeforeDelete();
            _ = DeleteAsync();
        }

        async System.Threading.Tasks.Task DeleteAsync()
        {
            await Store.LocationLinks.DeleteLink(Key.A, Key.B);
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

        public bool Contains(Geometry.Vector2 Position)
        {
            double d = LineSegment.DistanceToPoint(Position);
            return (d - LineRadius) <= 0;
        }

        public bool Intersects(LineSegment line) => LineSegment.Intersects(line);

        public double Distance(Geometry.Vector2 Position)
        {
            double d = LineSegment.DistanceToPoint(Position) - LineRadius;
            return d < 0 ? 0 : d;
        }

        public double Distance(Microsoft.SqlServer.Types.SqlGeometry shape) => LineSegment.ToSqlGeometry().STDistance(shape).Value;

        public double DistanceFromCenterNormalized(Geometry.Vector2 Position) => LineSegment.DistanceToPoint(Position) / LineRadius;

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.LumaOverlayRoundLineManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          IEnumerable<LocationLinkView> listToDraw)
        {
            LineView[] linesToDraw = [.. listToDraw.Select(l => l.lineView)];

            LineView.Draw(device, scene, lineManager, linesToDraw);
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
