using Geometry;
using Microsoft.Xna.Framework;
using System.Linq;
using WebAnnotationModel;

namespace WebAnnotation.View
{
    internal abstract class LocationCurveView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : LocationLineViewBase(obj, mapper), VikingXNAGraphics.IColorView
    {
        public abstract GridVector2[] MosaicCurveControlPoints { get; }
        public abstract GridVector2[] VolumeCurveControlPoints { get; }
        public abstract Color Color { get; set; }
        public abstract float Alpha { get; set; }

        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            if (PointIntersectsAnyControlPoint(Position))
            {
                return VolumeControlPoints.Select(p => GridVector2.Distance(p, Position) / ControlPointRadius).Min();
            }
            else
            {
                //TODO: Find a more accurate measurement.  Returning 0 means the line is always on top in selection.
                GridLineSegment[] segs = GridLineSegment.SegmentsFromPoints(VolumeCurveControlPoints);
                double MinDistance = segs.Min(l => l.DistanceToPoint(Position));
                return MinDistance / (LineWidth / 2.0);
            }
        }

        protected override bool PointIntersectsAnyLineSegment(GridVector2 WorldPosition)
        {
            //TODO: This could be optimized considerably
            GridLineSegment[] lineSegs = GridLineSegment.SegmentsFromPoints(VolumeCurveControlPoints);
            //Find the line segment the NewControlPoint intersects
            int iNearest = lineSegs.NearestSegment(WorldPosition, out double MinDistance);
            return MinDistance < LineWidth / 2.0f;
        }
    }
}
