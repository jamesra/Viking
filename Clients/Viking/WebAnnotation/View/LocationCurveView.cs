using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using WebAnnotationModel;
using SqlGeometryUtils;
using Microsoft.Xna.Framework;

namespace WebAnnotation.View
{
    abstract class LocationCurveView : LocationLineViewBase, VikingXNAGraphics.IColorView
    {
        public abstract GridVector2[] MosaicCurveControlPoints { get; }
        public abstract GridVector2[] VolumeCurveControlPoints { get; }
        public abstract Color Color { get; set; }
        public abstract float Alpha { get; set; }

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
