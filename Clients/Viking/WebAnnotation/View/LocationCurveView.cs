using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using WebAnnotationModel;

namespace WebAnnotation.View
{
    abstract class LocationCurveView : LocationLineViewBase
    {
        public LocationCurveView(LocationObj obj) : base(obj) { }
        public abstract GridVector2[] MosaicCurveControlPoints { get; }
        public abstract GridVector2[] VolumeCurveControlPoints { get; }
    }
}
