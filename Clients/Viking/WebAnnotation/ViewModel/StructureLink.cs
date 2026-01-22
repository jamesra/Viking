using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Viking.Common;
using Viking.VolumeModel;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotation.View;
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{
    /// <summary>
    /// A StructureLink and the two locations that should be connected visually in a view
    /// </summary>
    public class SectionStructureLinkViewKey(StructureLinkKey link, long Source, long Target) : IEquatable<SectionStructureLinkViewKey>
    {
        public readonly StructureLinkKey LinkID = link;
        public readonly long SourceLocID = Source;
        public readonly long TargetLocID = Target;

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
            if ((other) is null)
            {
                return false;
            }

            if (!LinkID.Equals(other.LinkID))
            {
                return false;
            }

            return SourceLocID == other.SourceLocID && TargetLocID == other.TargetLocID;
        }
    }

    public delegate ContextMenuStrip StructureLinkContextMenuGeneratorDelegate(IViewStructureLink key);

    internal abstract class StructureLinkViewModelBase : Viking.Objects.UIObjBase, ICanvasGeometryView, IViewStructureLink, IContextMenu
    {
        private readonly StructureLinkObj modelObj;

        /// <summary>
        /// LocationOnSection is the location on the section being viewed
        /// </summary>
        public LocationObj SourceLocation;

        /// <summary>
        /// LocationOnSection is the location on the reference section
        /// </summary>
        public LocationObj TargetLocation;
        private readonly StructureLinkContextMenuGeneratorDelegate? ContextMenuGenerator = null;

        public override string ToString() => modelObj.ToString();

        public override int GetHashCode() => modelObj.GetHashCode();

        public override bool Equals(object obj)
        {
            if (obj is StructureLinkViewModelBase Obj)
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

        public long SourceID => modelObj.SourceID;

        public long TargetID => modelObj.TargetID;

        public bool Bidirectional => modelObj.Bidirectional;

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
            modelObj = Store.StructureLinks[linkKey.LinkID];
            SourceLocation = Store.Locations[linkKey.SourceLocID];
            TargetLocation = Store.Locations[linkKey.TargetLocID];

            ContextMenuGenerator = StructureLink_CanvasContextMenuView.ContextMenuGenerator;
        }

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

        public override void Delete()
        {
            Store.StructureLinks.Remove(modelObj);
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
            if (TargetObj is null || OriginObj is null)
            {
                return false;
            }

            //Cannot link a location object to itself
            if (TargetObj.ID == OriginObj.ID)
            {
                return false;
            }

            return IsValidStructureLinkTarget(TargetObj.Parent, OriginObj.Parent);
        }

        private static bool IsExistingLink(StructureObj TargetObj, StructureObj OriginObj)
        {
            //Do not recreate existing link
            if (TargetObj.LinksCopy.Any(link => (link.SourceID == TargetObj.ID && link.TargetID == OriginObj.ID) ||
                                                (link.SourceID == OriginObj.ID && link.TargetID == TargetObj.ID)))
            {
                return true;
            }

            //Do not recreate existing link
            if (OriginObj.LinksCopy.Any(link => (link.SourceID == TargetObj.ID && link.TargetID == OriginObj.ID) ||
                                                (link.SourceID == OriginObj.ID && link.TargetID == TargetObj.ID)))
            {
                return true;
            }

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
            if (TargetObj is null || OriginObj is null)
            {
                return false;
            }

            //Cannot link a structure to itself
            if (TargetObj.ID == OriginObj.ID)
            {
                return false;
            }

            if (IsExistingLink(TargetObj, OriginObj))
            {
                return false;
            }

            //Can link synapses with the same parent
            if (TargetObj.ParentID == OriginObj.ParentID)
            {
                return true;
            }

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

        public StructureLinkKey Key => modelObj.ID;

        int ICanvasView.VisualHeight => 0;

        protected abstract void CreateView(SectionStructureLinkViewKey key, Viking.VolumeModel.IVolumeToSectionTransform mapper);

        public abstract double Distance(SqlGeometry Position);
    }

    internal class StructureLinkCirclesView(SectionStructureLinkViewKey key, Viking.VolumeModel.IVolumeToSectionTransform mapper) : StructureLinkViewModelBase(key, mapper)
    {
        public LineView lineView;
        public Geometry.GridLineSegment lineSegment;

        public double LineWidth => ((SourceLocation.Radius + TargetLocation.Radius));

        public double Radius => LineWidth / 2.0;

        public float alpha
        {
            get => color.A / 255.0f;
            set => lineView.Color = new Microsoft.Xna.Framework.Color(lineView.Color.R,
                                                                   lineView.Color.G,
                                                                   lineView.Color.B,
                                                                   (int)(value * 255.0f));
        }

        public Microsoft.Xna.Framework.Color color
        {
            get => lineView.Color;
            set => lineView.Color = value;
        }


        public static Microsoft.Xna.Framework.Color DefaultColor = new(255,
                255,
                255,
                128);

        public override double Distance(GridVector2 Position) => lineSegment.DistanceToPoint(Position) - Radius;

        public override double Distance(SqlGeometry shape) => lineSegment.ToSqlGeometry().STDistance(shape).Value;

        public override double DistanceFromCenterNormalized(GridVector2 Position) => lineSegment.DistanceToPoint(Position) / (LineWidth / 2.0);

        public override bool Contains(GridVector2 Position) => lineSegment.DistanceToPoint(Position) < LineWidth;

        public override bool Intersects(GridLineSegment line) => lineSegment.Intersects(line);

        public override bool IsVisible(Scene scene) =>
            //Do not draw unless the line is at least four pixels wide
            LineWidth >= Math.Max(scene.DevicePixelWidth, scene.DevicePixelHeight) * 4;

        public override Geometry.GridRectangle BoundingBox => GridRectangle.Pad(lineSegment.BoundingBox, LineWidth);

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
            LineView[] linesToDraw = [.. listToDraw.Select(l => l.lineView)];

            LineView.Draw(device, scene, lineManager, linesToDraw);
        }

    }

    /// <summary>
    /// Link structures represented by curves
    /// </summary>
    internal class StructureLinkCurvesView : StructureLinkViewModelBase
    {
        public LinkedPolyLineSimpleView lineView;
        public Geometry.GridLineSegment[] lineSegments;
        public static float DefaultLineWidth = 16.0f;

        public double LineWidth => ((SourceLocation.Width.Value + TargetLocation.Width.Value) / 2.0);

        public double Radius => LineWidth / 2.0;

        public float alpha
        {
            get => color.A / 255.0f;
            set => lineView.Color = new Microsoft.Xna.Framework.Color(lineView.Color.R,
                                                                   lineView.Color.G,
                                                                   lineView.Color.B,
                                                                   (int)(value * 255.0f));
        }

        public Microsoft.Xna.Framework.Color color
        {
            get => lineView.Color;
            set => lineView.Color = value;
        }


        public static Microsoft.Xna.Framework.Color DefaultColor = new(255,
                255,
                255,
                192);

        public StructureLinkCurvesView(SectionStructureLinkViewKey key, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(key, mapper)
        {
            CreateLineSegments();
        }

        private void CreateLineSegments() => lineSegments = [.. lineView.Lines.Select(l => new GridLineSegment(l.Source, l.Destination))];


        public override double Distance(GridVector2 Position) => lineSegments.Select(l => l.DistanceToPoint(Position) - Radius).Min();

        public override double Distance(SqlGeometry shape) => lineSegments.Select(l => l.ToSqlGeometry().STDistance(shape).Value).Min();

        public override double DistanceFromCenterNormalized(GridVector2 Position) => lineSegments.Select(l => l.DistanceToPoint(Position) / (LineWidth / 2.0)).Min();

        public override bool Contains(GridVector2 Position) => lineSegments.Any(l => l.DistanceToPoint(Position) < LineWidth);

        public override bool Intersects(GridLineSegment line) => lineSegments.Any(l => l.Intersects(line));

        public override bool IsVisible(Scene scene) =>
            //Do not draw unless the line is at least four pixels wide
            LineWidth >= Math.Max(scene.DevicePixelWidth, scene.DevicePixelHeight) * 4;

        public override Geometry.GridRectangle BoundingBox
        {
            get
            {
                GridRectangle bbox = lineSegments[0].BoundingBox;
                foreach (GridLineSegment l in lineSegments)
                {
                    bbox = GridRectangle.Union(bbox, l.BoundingBox);
                }

                bbox = GridRectangle.Union(bbox, bbox.LowerLeft - new GridVector2(Radius, Radius));
                bbox = GridRectangle.Union(bbox, bbox.UpperRight + new GridVector2(Radius, Radius));

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

            lineView = new LinkedPolyLineSimpleView(sourceShape.ToPoints(), targetShape.ToPoints(), (float)LineWidth, DefaultColor, link.Bidirectional ? LineStyle.AnimatedBidirectional : LineStyle.AnimatedLinear);
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          StructureLinkCurvesView[] listToDraw)
        {
            LinkedPolyLineSimpleView[] linesToDraw = [.. listToDraw.Select(l => l.lineView)];

            LinkedPolyLineSimpleView.Draw(device, scene, lineManager, linesToDraw);
        }
    }
}
