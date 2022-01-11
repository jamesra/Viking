using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Viking.VolumeModel;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotation.View;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.ViewModel
{
    /// <summary>
    /// A StructureLink and the two locations that should be connected visually in a view
    /// </summary>
    public class SectionStructureLinkViewKey : IEquatable<SectionStructureLinkViewKey>
    {
        public readonly StructureLinkKey LinkID;
        public readonly long SourceLocID;
        public readonly long TargetLocID;

        public SectionStructureLinkViewKey(StructureLinkKey link, long Source, long Target)
        {
            this.LinkID = link;
            this.SourceLocID = Source;
            this.TargetLocID = Target;
        }

        public static SectionStructureLinkViewKey CreateForNearestLocations(StructureLinkKey linkKey, ICollection<LocationCanvasView> SourceLocations, ICollection<LocationCanvasView> TargetLocations)
        {
            //Brute force a search for the shortest distance between the two structures.
            double MinDistance = double.MaxValue;
            LocationCanvasView BestSourceLoc = null;
            LocationCanvasView BestTargetLoc = null;

            if (SourceLocations.Count == 1 && TargetLocations.Count == 1)
            {
                return new SectionStructureLinkViewKey(linkKey, SourceLocations.First().ID, TargetLocations.First().ID);
            }

            foreach (LocationCanvasView SourceLoc in SourceLocations)
            {
                foreach (LocationCanvasView TargetLoc in TargetLocations)
                {
                    double dist = SourceLoc.Distance(TargetLoc.VolumeShapeAsRendered);
                    if (dist < MinDistance)
                    {
                        BestSourceLoc = SourceLoc;
                        BestTargetLoc = TargetLoc;
                        MinDistance = dist;
                    }
                }
            }

            if (BestSourceLoc != null)
            {
                return new SectionStructureLinkViewKey(linkKey, BestSourceLoc.ID, BestTargetLoc.ID);
            }

            return null;
        }

        public bool Equals(SectionStructureLinkViewKey other)
        {
            if ((other) == null)
                return false;

            if (!this.LinkID.Equals(other.LinkID))
                return false;

            return this.SourceLocID == other.SourceLocID && this.TargetLocID == other.TargetLocID;
        }
    }

    public delegate ContextMenu StructureLinkContextMenuGeneratorDelegate(IViewStructureLink key);

    abstract class StructureLinkViewModelBase : Viking.Objects.UIObjBase, ICanvasGeometryView, IViewStructureLink
    {
        StructureLinkObj modelObj;

        /// <summary>
        /// LocationOnSection is the location on the section being viewed
        /// </summary>
        public LocationObj SourceLocation;

        /// <summary>
        /// LocationOnSection is the location on the reference section
        /// </summary>
        public LocationObj TargetLocation;

        StructureLinkContextMenuGeneratorDelegate ContextMenuGenerator = null;

        public override string ToString()
        {
            return modelObj.ToString();
        }

        public override int GetHashCode()
        {
            return modelObj.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            StructureLinkViewModelBase Obj = obj as StructureLinkViewModelBase;
            if (Obj != null)
            {
                return modelObj.Equals(Obj.modelObj);
            }

            StructureLinkObj Obj2 = obj as StructureLinkObj;
            if (Obj2 != null)
            {
                return modelObj.Equals(Obj2);
            }

            return false;
        }

        public long SourceID
        {
            get
            {
                return modelObj.SourceID;
            }
        }

        public long TargetID
        {
            get
            {
                return modelObj.TargetID;
            }
        }

        public bool Bidirectional
        {
            get { return modelObj.Bidirectional; }
        }

        /// <summary>
        /// Use this version only for searches
        /// </summary>
        /// <param name="linkObj"></param>
        public StructureLinkViewModelBase(SectionStructureLinkViewKey linkKey, Viking.VolumeModel.IVolumeToSectionTransform mapper)
            : this(linkKey)
        {
            CreateView(linkKey, mapper);
        }

        private StructureLinkViewModelBase(SectionStructureLinkViewKey linkKey) : base()
        {
            this.modelObj = Store.StructureLinks[linkKey.LinkID];
            this.SourceLocation = Store.Locations[linkKey.SourceLocID];
            this.TargetLocation = Store.Locations[linkKey.TargetLocID];

            this.ContextMenuGenerator = StructureLink_CanvasContextMenuView.ContextMenuGenerator;
        }

        public override System.Windows.Forms.ContextMenu ContextMenu
        {
            get
            {
                if (ContextMenuGenerator != null)
                    return ContextMenuGenerator(this);

                return null;

            }
        }

        public override void Delete()
        {
            Store.StructureLinks.Remove(this.modelObj);
            try
            {
                Store.StructureLinks.Save();
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
            }

        }

        public override void Save()
        {
            try
            {
                Store.StructureLinks.Save();
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
            }
        }


        /// <summary>
        /// Return true if two annotations can be joined with a structure link
        /// </summary>
        /// <param name="TargetObj"></param>
        /// <param name="OriginObj"></param>
        /// <returns></returns>
        public static bool IsValidStructureLinkTarget(LocationObj TargetObj, LocationObj OriginObj)
        {
            if (TargetObj == null || OriginObj == null)
                return false;

            //Cannot link a location object to itself
            if (TargetObj.ID == OriginObj.ID)
                return false;

            return IsValidStructureLinkTarget(TargetObj.Parent, OriginObj.Parent);
        }

        private static bool IsExistingLink(StructureObj TargetObj, StructureObj OriginObj)
        {
            //Do not recreate existing link
            if (TargetObj.CopyLinksAsync.Any(link => (link.SourceID == TargetObj.ID && link.TargetID == OriginObj.ID) ||
                                                (link.SourceID == OriginObj.ID && link.TargetID == TargetObj.ID)))
                return true;

            //Do not recreate existing link
            if (OriginObj.CopyLinksAsync.Any(link => (link.SourceID == TargetObj.ID && link.TargetID == OriginObj.ID) ||
                                                (link.SourceID == OriginObj.ID && link.TargetID == TargetObj.ID)))
                return true;

            return false;
        }

        /// <summary>
        /// Return true if two annotations can be joined with a structure link
        /// </summary>
        /// <param name="TargetObj"></param>
        /// <param name="OriginObj"></param>
        /// <returns></returns>
        public static bool IsValidStructureLinkTarget(StructureObj TargetObj, StructureObj OriginObj)
        {
            if (TargetObj == null || OriginObj == null)
                return false;

            //Cannot link a structure to itself
            if (TargetObj.ID == OriginObj.ID)
                return false;

            if (IsExistingLink(TargetObj, OriginObj))
                return false;

            //Can link synapses with the same parent
            if (TargetObj.ParentID == OriginObj.ParentID)
                return true;

            //Cannot link to higher levels in our parent heirarchy
            if (OriginObj.ParentID.HasValue && !IsValidStructureLinkTarget(TargetObj, OriginObj.Parent))
            {
                return false;
            }

            if (TargetObj.ParentID.HasValue && !IsValidStructureLinkTarget(TargetObj.Parent, OriginObj))
            {
                return false;
            }

            return true;
        }

        public abstract bool IsVisible(Scene scene);
        public abstract bool Contains(GridVector2 Position);
        public abstract bool Intersects(GridLineSegment line);
        public abstract double Distance(GridVector2 Position);
        public abstract double DistanceFromCenterNormalized(GridVector2 Position);

        public abstract Geometry.GridRectangle BoundingBox
        {
            get;
        }

        public StructureLinkKey Key
        {
            get
            {
                return this.modelObj.ID;
            }
        }

        int ICanvasView.VisualHeight
        {
            get
            {
                return 0;
            }
        }

        protected abstract void CreateView(SectionStructureLinkViewKey key, Viking.VolumeModel.IVolumeToSectionTransform mapper);

        public abstract double Distance(SqlGeometry Position);
    }

    class StructureLinkCirclesView : StructureLinkViewModelBase
    {
        public LineView lineView;
        public Geometry.GridLineSegment lineSegment;

        public double LineWidth
        {
            get
            {
                return ((SourceLocation.Radius + TargetLocation.Radius));
            }
        }

        public double Radius
        {
            get
            {
                return this.LineWidth / 2.0;
            }
        }

        public float alpha
        {
            get { return (float)color.A / 255.0f; }
            set
            {
                lineView.Color = new Microsoft.Xna.Framework.Color((int)lineView.Color.R,
                                                                   (int)lineView.Color.G,
                                                                   (int)lineView.Color.B,
                                                                   (int)(value * 255.0f));
            }
        }

        public Microsoft.Xna.Framework.Color color
        {
            get { return lineView.Color; }
            set { lineView.Color = value; }
        }


        public static Microsoft.Xna.Framework.Color DefaultColor = new Microsoft.Xna.Framework.Color((byte)(255),
                (byte)(255),
                (byte)(255),
                (byte)(128));

        public StructureLinkCirclesView(SectionStructureLinkViewKey key, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(key, mapper)
        {

        }


        public override double Distance(GridVector2 Position)
        {
            return lineSegment.DistanceToPoint(Position) - this.Radius;
        }

        public override double Distance(SqlGeometry shape)
        {
            return lineSegment.ToSqlGeometry().STDistance(shape).Value;
        }

        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return lineSegment.DistanceToPoint(Position) / (this.LineWidth / 2.0);
        }

        public override bool Contains(GridVector2 Position)
        {
            return lineSegment.DistanceToPoint(Position) < this.LineWidth;
        }

        public override bool Intersects(GridLineSegment line)
        {
            return lineSegment.Intersects(line);
        }

        public override bool IsVisible(Scene scene)
        {
            //Do not draw unless the line is at least four pixels wide
            return this.LineWidth >= Math.Max(scene.DevicePixelWidth, scene.DevicePixelHeight) * 4;
        }

        public override Geometry.GridRectangle BoundingBox
        {
            get
            {
                return GridRectangle.Pad(lineSegment.BoundingBox, this.LineWidth);
            }
        }

        protected override void CreateView(SectionStructureLinkViewKey key, Viking.VolumeModel.IVolumeToSectionTransform mapper)
        {
            StructureLinkObj link = Store.StructureLinks[key.LinkID];
            LocationObj source = Store.Locations[key.SourceLocID];
            LocationObj target = Store.Locations[key.TargetLocID];

            GridVector2 sourceVolumePosition = mapper.SectionToVolume(source.Position);
            GridVector2 targetVolumePosition = mapper.SectionToVolume(target.Position);

            lineView = new LineView(sourceVolumePosition, targetVolumePosition, Math.Min(source.Radius, target.Radius), DefaultColor,
                                    link.Bidirectional ? LineStyle.AnimatedBidirectional : LineStyle.AnimatedLinear);
            lineSegment = new GridLineSegment(sourceVolumePosition, targetVolumePosition);
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          StructureLinkCirclesView[] listToDraw)
        {
            LineView[] linesToDraw = listToDraw.Select(l => l.lineView).ToArray();

            LineView.Draw(device, scene, lineManager, linesToDraw);
        }

    }

    /// <summary>
    /// Link structures represented by curves
    /// </summary>
    class StructureLinkCurvesView : StructureLinkViewModelBase
    {
        public LinkedPolyLineSimpleView lineView;
        public Geometry.GridLineSegment[] lineSegments;
        public static float DefaultLineWidth = 16.0f;

        public double LineWidth
        {
            get
            {
                return ((SourceLocation.Width.Value + TargetLocation.Width.Value) / 2.0);
            }
        }

        public double Radius
        {
            get
            {
                return this.LineWidth / 2.0;
            }
        }

        public float alpha
        {
            get { return (float)color.A / 255.0f; }
            set
            {
                lineView.Color = new Microsoft.Xna.Framework.Color((int)lineView.Color.R,
                                                                   (int)lineView.Color.G,
                                                                   (int)lineView.Color.B,
                                                                   (int)(value * 255.0f));
            }
        }

        public Microsoft.Xna.Framework.Color color
        {
            get { return lineView.Color; }
            set { lineView.Color = value; }
        }


        public static Microsoft.Xna.Framework.Color DefaultColor = new Microsoft.Xna.Framework.Color((byte)(255),
                (byte)(255),
                (byte)(255),
                (byte)(192));

        public StructureLinkCurvesView(SectionStructureLinkViewKey key, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(key, mapper)
        {
            CreateLineSegments();
        }

        private void CreateLineSegments()
        {
            this.lineSegments = lineView.Lines.Select(l => new GridLineSegment(l.Source, l.Destination)).ToArray();
        }


        public override double Distance(GridVector2 Position)
        {
            return lineSegments.Select(l => l.DistanceToPoint(Position) - this.Radius).Min();
        }

        public override double Distance(SqlGeometry shape)
        {
            return lineSegments.Select(l => l.ToSqlGeometry().STDistance(shape).Value).Min();
        }

        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return lineSegments.Select(l => l.DistanceToPoint(Position) / (this.LineWidth / 2.0)).Min();
        }

        public override bool Contains(GridVector2 Position)
        {
            return lineSegments.Any(l => l.DistanceToPoint(Position) < this.LineWidth);
        }

        public override bool Intersects(GridLineSegment line)
        {
            return lineSegments.Any(l => l.Intersects(line));
        }

        public override bool IsVisible(Scene scene)
        {
            //Do not draw unless the line is at least four pixels wide
            return this.LineWidth >= Math.Max(scene.DevicePixelWidth, scene.DevicePixelHeight) * 4;
        }

        public override Geometry.GridRectangle BoundingBox
        {
            get
            {
                GridRectangle bbox = lineSegments[0].BoundingBox;
                foreach (GridLineSegment l in lineSegments)
                {
                    bbox = GridRectangle.Union(bbox, l.BoundingBox);
                }

                bbox = GridRectangle.Union(bbox, bbox.LowerLeft - new GridVector2(this.Radius, this.Radius));
                bbox = GridRectangle.Union(bbox, bbox.UpperRight + new GridVector2(this.Radius, this.Radius));

                return bbox;
            }
        }

        protected override void CreateView(SectionStructureLinkViewKey key, Viking.VolumeModel.IVolumeToSectionTransform mapper)
        {
            StructureLinkObj link = Store.StructureLinks[key.LinkID];
            LocationObj source = Store.Locations[key.SourceLocID];
            LocationObj target = Store.Locations[key.TargetLocID];

            SqlGeometry sourceShape = mapper.TryMapShapeSectionToVolume(source.MosaicShape);
            SqlGeometry targetShape = mapper.TryMapShapeSectionToVolume(target.MosaicShape);

            lineView = new LinkedPolyLineSimpleView(sourceShape.ToPoints(), targetShape.ToPoints(), (float)this.LineWidth, DefaultColor, link.Bidirectional ? LineStyle.AnimatedBidirectional : LineStyle.AnimatedLinear);
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          StructureLinkCurvesView[] listToDraw)
        {
            LinkedPolyLineSimpleView[] linesToDraw = listToDraw.Select(l => l.lineView).ToArray();

            LinkedPolyLineSimpleView.Draw(device, scene, lineManager, linesToDraw);
        }
    }
}
