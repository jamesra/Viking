using SqlGeometryUtils;
using System;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.View
{
    internal static class AnnotationViewFactory
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
        /// <summary>On-section view matching TypeCode. POINT uses LocationCircleView.</summary>
        public static LocationCanvasView Create(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapping)
        {
            LocationVolumeGeometry geometry = LocationVolumeGeometry.For(obj, mapping);
            switch (obj.TypeCode)
            {
                case LocationType.CIRCLE:
                case LocationType.POINT:
                    return new LocationCircleView(obj, geometry);
                case LocationType.OPENCURVE:
                    return new LocationOpenCurveView(obj, geometry);
                case LocationType.CURVEPOLYGON:
                case LocationType.POLYGON:
                    {
                        LocationPolygonView polyview = new(obj, geometry);
                        Task.Run(() => polyview.Initialize()).ContinueWith(
                            t => polyview.ReportInitializeFailure(t.Exception.GetBaseException()),
                            TaskContinuationOptions.OnlyOnFaulted);
                        return polyview;
                    }
                case LocationType.CLOSEDCURVE:
                    return new LocationClosedCurveView(obj, geometry);
                case LocationType.POLYLINE:
                    return new LocationLineView(obj, geometry);
                default:
                    throw new NotImplementedException("View for type " + obj.TypeCode.ToString() + " is not implemented");
            }
        }

        /// <summary>
        /// The view of a location on an adjacent section. Circles, points, curves and polygons are drawn as the volume
        /// marker circle <see cref="LocationVolumeGeometry.AdjacentMarker"/> gives; location links end on the same circle.
        /// Polylines are drawn as a line.
        /// </summary>
        public static LocationCanvasView CreateAdjacent(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapping)
        {

            switch (obj.TypeCode)
            {
                case LocationType.CIRCLE:
                case LocationType.POINT:
                case LocationType.OPENCURVE:
                case LocationType.POLYGON:
                case LocationType.CURVEPOLYGON:
                case LocationType.CLOSEDCURVE:
                    return new AdjacentLocationCircleView(obj, LocationVolumeGeometry.For(obj, mapping).AdjacentMarker(Global.AdjacentLocationRadiusScalar));
                case LocationType.POLYLINE:
                    {
                        AdjacentLocationLineView view = new(obj, LocationVolumeGeometry.For(obj, mapping))
                        {
                            Color = new Microsoft.Xna.Framework.Color(1, 1, 1, 0.2f)
                        };
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
            LocationObj targetLocation = Store.Locations[key.TargetLocID];
            LocationVolumeGeometry source = LocationVolumeGeometry.For(sourceLocation, mapper);
            LocationVolumeGeometry target = LocationVolumeGeometry.For(targetLocation, mapper);

            //If the location types don't match then use the default circle view, which all annotations are compatible with
            if (sourceLocation.TypeCode != targetLocation.TypeCode)
            {
                return new StructureLinkCirclesView(key, source, target);
            }

            switch (sourceLocation.TypeCode)
            {
                case LocationType.OPENCURVE:
                case LocationType.POLYLINE:
                    return new StructureLinkCurvesView(key, source, target);
                default:
                    return new StructureLinkCirclesView(key, source, target);
                    throw new NotImplementedException("StructureLink View for type " + sourceLocation.TypeCode.ToString() + " is not implemented");
            }
        }
    }
}