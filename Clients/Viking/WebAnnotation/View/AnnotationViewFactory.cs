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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="OnAdjacentSection">Indicates the location is not on the section being displayed.</param>
        /// <returns></returns>
        public static LocationCanvasView Create(LocationObj obj, bool OnAdjacentSection)
        {
            if (!OnAdjacentSection)
            {
                return Create(obj);
            }
            else
            {
                return CreateAdjacent(obj);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="OnAdjacentSection">Indicates the location is not on the section being displayed.</param>
        /// <returns></returns>
        public static LocationCanvasView Create(LocationObj obj)
        {
            switch (obj.TypeCode)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="OnAdjacentSection">Indicates the location is not on the section being displayed.</param>
        /// <returns></returns>
        public static LocationCanvasView CreateAdjacent(LocationObj obj)
        {
            switch (obj.TypeCode)
            {
                case LocationType.CIRCLE:
                    return new AdjacentLocationCircleView(obj);
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
