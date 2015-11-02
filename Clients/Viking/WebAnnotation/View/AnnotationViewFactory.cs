using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel;

namespace WebAnnotation.View
{
    static class AnnotationViewFactory
    {
        public static LocationCanvasView Create(LocationObj obj)
        {
            switch(obj.TypeCode)
            {
                case LocationType.CIRCLE:
                    return new LocationCircleView(obj);
                case LocationType.OPENCURVE:
                    return new LocationOpenCurveView(obj);
                case LocationType.CLOSEDCURVE:
                    return new LocationClosedCurveView(obj);
                case LocationType.POLYLINE:
                    return new LocationLineView(obj);
                default:
                    throw new NotImplementedException("View for type " + obj.TypeCode.ToString() + " is not implemented");
            }
        }
    }
}
