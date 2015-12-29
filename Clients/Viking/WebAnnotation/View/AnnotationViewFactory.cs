using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel;
using WebAnnotation.ViewModel;

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
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="OnAdjacentSection">Indicates the location is not on the section being displayed.</param>
        /// <returns></returns>
        public static StructureLinkViewModelBase Create(StructureLinkObj linkObj,
                                                LocationObj sourceLoc,
                                                LocationObj targetLoc)
        {
            switch (sourceLoc.TypeCode)
            {
                case LocationType.CIRCLE:
                    return new StructureLinkCirclesView(linkObj, sourceLoc, targetLoc);
                case LocationType.OPENCURVE:
                    return new StructureLinkCurvesView(linkObj, sourceLoc, targetLoc);
                default:
                    throw new NotImplementedException("StructureLink View for type " + sourceLoc.TypeCode.ToString() + " is not implemented");
            }
        }
    }
}
