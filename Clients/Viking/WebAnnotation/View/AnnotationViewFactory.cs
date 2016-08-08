using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel;
using WebAnnotation.ViewModel;
using SqlGeometryUtils;

namespace WebAnnotation.View
{
    static class AnnotationViewFactory
    {
        /*
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="OnAdjacentSection">Indicates the location is not on the section being displayed.</param>
        /// <returns></returns>
        public static LocationCanvasView Create(LocationObj obj, Viking.VolumeModel.IVolumeToSectionMapper mapper, bool OnAdjacentSection)
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
        */
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="OnAdjacentSection">Indicates the location is not on the section being displayed.</param>
        /// <returns></returns>
        public static LocationCanvasView Create(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapping)
        {
            switch (obj.TypeCode)
            {
                case LocationType.CIRCLE:
                    return new LocationCircleView(obj, mapping);
                case LocationType.OPENCURVE:
                    return new LocationOpenCurveView(obj, mapping);
                case LocationType.CLOSEDCURVE:
                    return new LocationClosedCurveView(obj, mapping);
                case LocationType.POLYLINE:
                    return new LocationLineView(obj, mapping);
                case LocationType.POINT:
                    return new LocationCircleView(obj, mapping);
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
        public static LocationCanvasView CreateAdjacent(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapping)
        {

            switch (obj.TypeCode)
            {
                case LocationType.CIRCLE:
                    return new AdjacentLocationCircleView(obj, mapping);
                case LocationType.POINT:
                    return new AdjacentLocationCircleView(obj, mapping);
                case LocationType.OPENCURVE:
                    {
                        return new AdjacentLocationCircleView(obj, mapping, obj.Radius);
                        /*
                        LocationOpenCurveView view = new LocationOpenCurveView(obj, mapping, obj.Radius);
                        view.Color = new Microsoft.Xna.Framework.Color(1, 1, 1, 0.2f);
                        view.LabelTextColor = new Microsoft.Xna.Framework.Color(1, 1, 1, 0.5f);
                        view.ParentLabelTextAlpha = 0.5f;
                        return view;
                        */
                    }
                case LocationType.CLOSEDCURVE:
                    {
                        AdjacentLocationCircleView view = new AdjacentLocationCircleView(obj, obj.MosaicShape.CalculateInscribedCircle(), mapping);
                        return view;
                    }
                case LocationType.POLYLINE:
                    {
                        AdjacentLocationLineView view = new AdjacentLocationLineView(obj, mapping);
                        view.Color = new Microsoft.Xna.Framework.Color(1, 1, 1, 0.2f);
                        return view;
                    }
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
        public static StructureLinkViewModelBase Create(SectionStructureLinkViewKey key, Viking.VolumeModel.IVolumeToSectionTransform mapper)
        {
            LocationObj sourceLocation = Store.Locations[key.SourceLocID];
            switch (sourceLocation.TypeCode)
            {
                case LocationType.CIRCLE:
                    return new StructureLinkCirclesView(key, mapper);
                case LocationType.OPENCURVE:
                    StructureLinkCurvesView view = new StructureLinkCurvesView(key, mapper);
                    return view;
                case LocationType.POLYLINE:
                    return new StructureLinkCurvesView(key, mapper);
                default:
                    throw new NotImplementedException("StructureLink View for type " + sourceLocation.TypeCode.ToString() + " is not implemented");
            }
        }
    }
}