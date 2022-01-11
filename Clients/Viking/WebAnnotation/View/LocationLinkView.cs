using Geometry;
using Microsoft.Xna.Framework;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes;
using Viking.VolumeModel;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotation.View;
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{

    public delegate ContextMenu LocationLinkContextMenuGeneratorDelegate(IViewLocationLink key);

    /// <summary>
    /// This class represents a link between locations. This object is a little unique because it is
    /// not tied to the database object like the other *obj classes
    /// </summary>
    public class LocationLinkView : Viking.Objects.UIObjBase, ICanvasGeometryView, IEquatable<LocationLinkView>, IColorView, IViewLocationLink
    {
        public readonly LocationLinkKey Key;

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (System.Object.ReferenceEquals(this, obj))
                return true;

            LocationLinkView obj_link = obj as LocationLinkView;
            if ((object)obj_link != null)
                return this.Key.Equals(obj_link.Key);

            if (typeof(LocationLinkKey).IsInstanceOfType(obj))
            {
                LocationLinkKey obj_key = (LocationLinkKey)obj;
                return this.Key.Equals(obj_key);
            }

            return false;
        }

        public static bool operator ==(LocationLinkView A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if ((object)A != null)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(LocationLinkView A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if ((object)A != null)
                return !A.Equals(B);

            return true;
        }

        public override string ToString()
        {
            return Key.ToString() + " Sections: " + MinSection.ToString() + "-" + MaxSection.ToString();
        }

        /// <summary>
        /// LocationOnSection is the location on the section being viewed
        /// </summary>
        public GridCircle A;

        /// <summary>
        /// LocationOnSection is the location on the section being viewed
        /// </summary>
        public GridCircle B;

        /// <summary>
        /// Section number we are displaying the location link on
        /// </summary>
        public int Z;

        public LineView lineView = null;

        private Color _Color;

        public Color Color
        {
            get
            {
                return _Color;
            }

            set
            {
                _Color = value;
                if (lineView != null)
                    lineView.Color = value;
            }
        }

        public float Alpha
        {
            get
            {
                return _Color.GetAlpha();
            }

            set
            {
                _Color.SetAlpha(value);
                if (lineView != null)
                {
                    lineView.Color = lineView.Color.SetAlpha(value);
                }
            }
        }

        public int MinSection { get; private set; }
        public int MaxSection { get; private set; }

        public GridRectangle BoundingBox
        {
            get
            {
                return GridRectangle.Pad(LineSegment.BoundingBox, this.LineRadius);
            }
        }

        public Geometry.GridLineSegment LineSegment
        {
            get
            {
                return new Geometry.GridLineSegment(A.Center, B.Center);
            }
        }

        public LocationLinkContextMenuGeneratorDelegate ContextMenuGenerator = null;

        protected IVolumeTransformProvider mapProvider;

        public LocationLinkView(LocationLinkKey key, int Z, IVolumeTransformProvider mapProvider)
        {
            this.Key = key;
            this.Z = Z;
            this.mapProvider = mapProvider;
            UpdatePropertiesFromLocations(mapProvider);

            ContextMenuGenerator = LocationLink_CanvasContextMenuView.ContextMenuGenerator;

            this.lineView = CreateView();
        }

        public LocationLinkView(LocationObj LocOne, LocationObj LocTwo, int Z, IVolumeTransformProvider mapProvider) : this(new LocationLinkKey(LocOne.ID, LocTwo.ID), Z, mapProvider)
        {
            if (LocOne == null)
                throw new ArgumentNullException("LocOne");

            if (LocTwo == null)
                throw new ArgumentNullException("LocTwo");

            UpdatePropertiesFromLocations(mapProvider);

            this.lineView = CreateView();
        }

        private bool _LocationsOverlapped;

        private void UpdatePropertiesFromLocations(IVolumeTransformProvider mapProvider)
        {
            LocationObj A = Store.Locations[this.Key.A];
            LocationObj B = Store.Locations[this.Key.B];
            IVolumeToSectionTransform sourceMapper = mapProvider.GetSectionToVolumeTransform((int)Math.Round(A.Z));
            IVolumeToSectionTransform targetMapper = mapProvider.GetSectionToVolumeTransform((int)Math.Round(B.Z));
            GridVector2 AVolumePosition = sourceMapper.SectionToVolume(A.Position);
            GridVector2 BVolumePosition = targetMapper.SectionToVolume(B.Position);

            this.A = new GridCircle(AVolumePosition, A.Radius * (Z == A.Z ? 1.0 : Global.AdjacentLocationRadiusScalar));
            this.B = new GridCircle(BVolumePosition, B.Radius * (Z == B.Z ? 1.0 : Global.AdjacentLocationRadiusScalar));

            this.MinSection = (int)Math.Round(A.Z < B.Z ? A.Z : B.Z);
            this.MaxSection = (int)Math.Round(A.Z < B.Z ? B.Z : A.Z);

            this.Color = GetLocationLinkColor(A.Parent.Type.Color.ToXNAColor(), this.MaxSection - this.MinSection, this.MinSection < Z ? -1 : 1, false);
        }

        public void GetCanvasViews(LocationLinkKey key, IVolumeTransformProvider mapProvider, out LocationCanvasView AView, out LocationCanvasView BView)
        {
            LocationObj A = Store.Locations[this.Key.A];
            LocationObj B = Store.Locations[this.Key.B];
            IVolumeToSectionTransform MapperA = mapProvider.GetSectionToVolumeTransform((int)Math.Round(A.Z));
            IVolumeToSectionTransform MapperB = mapProvider.GetSectionToVolumeTransform((int)Math.Round(B.Z));

            AView = A.Z == this.Z ? AnnotationViewFactory.Create(A, MapperA) : AnnotationViewFactory.CreateAdjacent(A, MapperA);
            BView = B.Z == this.Z ? AnnotationViewFactory.Create(B, MapperB) : AnnotationViewFactory.CreateAdjacent(B, MapperB);
        }

        int ICanvasView.VisualHeight
        {
            get
            {
                return 0;
            }
        }

        public double LineWidth
        {
            get
            {
                return LineRadius * 2.0;
            }
        }

        public double LineRadius
        {
            get { return Math.Min(A.Radius, B.Radius); }
        }

        private LineView CreateView()
        {
            //IVolumeToSectionMapper sourceMapper = mapProvider.GetMapping((int)Math.Round(A.Z));
            //IVolumeToSectionMapper targetMapper = mapProvider.GetMapping((int)Math.Round(B.Z));
            //GridVector2 sourceVolumePosition = sourceMapper.SectionToVolume(A.Position);
            //GridVector2 targetVolumePosition = targetMapper.SectionToVolume(B.Position); 

            LineView line = new LineView(A.Center, B.Center, this.LineWidth, this.Color, LineStyle.Standard);
            return line;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="StructureTypeColor"></param>
        /// <param name="section_span_distance">Number of sections the location link crosses</param>
        /// <param name="direction">Direction the link is in from the current section</param>
        /// <returns></returns>
        private Microsoft.Xna.Framework.Color GetLocationLinkColor(Color structure_type_color, int section_span_distance, double direction, bool IsMouseOver)
        {
            if (section_span_distance == 0)
            {
                //This is an error state, we shouldn't have a link between annotations on the same section
                return Color.White;
            }

            int red = (int)((float)((float)structure_type_color.R * .5f) + (128 * direction));
            red = 255 - (red / section_span_distance);
            red = red > 255 ? 255 : red;
            red = red < 0 ? 0 : red;
            int blue = (int)((float)((float)structure_type_color.B * .5f) + (128 * -direction));
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
            LocationCanvasView AView;
            LocationCanvasView BView;
            GetCanvasViews(this.Key, this.mapProvider, out AView, out BView);

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
                //return GridVector2.Distance(A.VolumePosition, B.VolumePosition) <= A.Radius + LocationCanvasView.CalcOffSectionRadius((float)B.Radius);
            }

            if (B.Section == sectionNumber)
            {
                return B.VolumeShape.STIntersects(A.VolumeShape).IsTrue;
                //return GridVector2.Distance(A.VolumePosition, B.VolumePosition) <= B.Radius + LocationCanvasView.CalcOffSectionRadius((float)A.Radius);
            } 
            
            return false; 
            */
        }

        #region IUIObjectBasic Members

        public override System.Windows.Forms.ContextMenu ContextMenu
        {
            get
            {
                if (this.ContextMenuGenerator != null)
                {
                    return ContextMenuGenerator(this);
                }

                return null;
            }
        }

        public override string ToolTip
        {
            get { return Key.A.ToString() + " -> " + Key.B.ToString(); }
        }

        LocationLinkKey IViewLocationLink.Key
        {
            get
            {
                return this.Key;
            }
        }

        public override void Save()
        {
            throw new NotImplementedException();
        }

        #endregion

        public override void Delete()
        {
            CallBeforeDelete();

            Store.LocationLinks.DeleteLink(Key.A, Key.B);

            CallAfterDelete();
        }

        public static bool IsValidLocationLinkTarget(LocationObj target, LocationObj OriginObj)
        {
            if (target == null || OriginObj == null)
                return false;

            //Check to make sure it isn't the same structure on the same section
            if (target.ParentID != OriginObj.ParentID)
                return false;

            if (target.Z == OriginObj.Z)
                return false;

            if (OriginObj.LinksCopy.Contains(target.ID))
                return false;

            return true;
        }

        public bool IsVisible(Scene scene)
        {
            return Math.Min(LineSegment.Length, this.LineWidth) / scene.Camera.Downsample > 2.0;
        }

        public bool Contains(GridVector2 Position)
        {
            double d = LineSegment.DistanceToPoint(Position);
            return (d - this.LineRadius) <= 0;
        }

        public bool Intersects(GridLineSegment line)
        {
            return this.LineSegment.Intersects(line);
        }

        public double Distance(GridVector2 Position)
        {
            double d = LineSegment.DistanceToPoint(Position) - this.LineRadius;
            if (d < 0)
                d = 0;
            return d;
        }

        public double Distance(Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            return this.LineSegment.ToSqlGeometry().STDistance(shape).Value;
        }

        public double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return LineSegment.DistanceToPoint(Position) / this.LineRadius;
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.LumaOverlayRoundLineManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          IEnumerable<LocationLinkView> listToDraw)
        {
            LineView[] linesToDraw = listToDraw.Select(l => l.lineView).ToArray();

            LineView.Draw(device, scene, lineManager, linesToDraw);
        }

        public bool Equals(LocationLinkView other)
        {
            if ((object)other == null)
                return false;

            return this.Key.Equals(other.Key) && this.Z == other.Z;
        }
    }
}
