using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using WebAnnotationModel;
using SqlGeometryUtils;

namespace WebAnnotation.View
{
    abstract class LocationCurveView : LocationLineViewBase
    {
        public abstract GridVector2[] MosaicCurveControlPoints { get; }
        public abstract GridVector2[] VolumeCurveControlPoints { get; }

        public LocationCurveView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj, mapper)
        { 
        }

        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            //TODO: Find a more accurate measurement.  Returning 0 means the line is always on top in selection.
            GridLineSegment[] segs = GridLineSegment.SegmentsFromPoints(this.VolumeCurveControlPoints);
            double MinDistance = segs.Min(l => l.DistanceToPoint(Position));
            return (MinDistance - (this.Width / 2.0));
        }
    }
}
