using Geometry;
using Microsoft.Xna.Framework;
using System.Linq;
using WebAnnotationModel;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.View
{
    internal abstract class LocationCurveView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : LocationLineViewBase(obj, mapper), VikingXNAGraphics.IColorView
    {
        public abstract Geometry.Vector2[] MosaicCurveControlPoints { get; }
        public abstract Geometry.Vector2[] VolumeCurveControlPoints { get; }
        public abstract Color Color { get; set; }
        public abstract float Alpha { get; set; }

        public override double DistanceFromCenterNormalized(Geometry.Vector2 Position)
        {
            if (PointIntersectsAnyControlPoint(Position))
            {
                return VolumeControlPoints.Select(p => Geometry.Vector2.Distance(p, Position) / ControlPointRadius).Min();
            }
            else
            {
                //TODO: Find a more accurate measurement.  Returning 0 means the line is always on top in selection.
                LineSegment[] segs = LineSegment.SegmentsFromPoints(VolumeCurveControlPoints);
                double MinDistance = segs.Min(l => l.DistanceToPoint(Position));
                return MinDistance / (LineWidth / 2.0);
            }
        }

        protected override Geometry.Vector2[] HitTestPoints => VolumeCurveControlPoints;
    }
}
