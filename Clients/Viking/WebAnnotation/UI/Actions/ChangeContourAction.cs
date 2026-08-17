using Geometry;
using SqlGeometryUtils;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Actions
{

    /// <summary>
    /// Replace the exterior contour of an annotation with the passed contour
    /// </summary>
    internal class Change2DContourAction : IAction, IEquatable<Change2DContourAction>
    {
        public readonly LocationObj Location;
        private readonly IVolumeToSectionTransform Transform;

        /// <summary>
        /// The mosaic space polygon we want to commit to the database
        /// </summary>
        public readonly Polygon NewMosaicPolygon;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly Polygon NewVolumePolygon;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly Polygon NewSmoothedVolumePolygon;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        //public readonly Polygon NewSmoothVolumePolygon;

        public LocationAction Type => LocationAction.CHANGEBOUNDARY;

        public RetraceCommandAction RetraceType { get; } = RetraceCommandAction.NONE;

        public Action Execute => OnExecute;

        public static implicit operator Action(Change2DContourAction a)
        {
            return a.Execute;
        }

        /// <summary>
        /// Indicates if this action represents the ClockWiseContour when we are cutting a shape in half
        /// </summary>
        internal bool ClockwiseContour = false;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.None;
        public Change2DContourAction(long locationID, RetraceCommandAction retraceType, Polygon newMosaicPolygon, Polygon? newVolumePolygon = null, bool ClockwiseContour = false, IVolumeToSectionTransform? transform = null)
        {
            Debug.Assert(newMosaicPolygon.TotalUniqueVertices < 1000, "This is a huge polygon, why?");

            this.ClockwiseContour = ClockwiseContour;
            RetraceType = retraceType;
            Location = Store.Locations[locationID];

            Transform = transform ?? WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;

            NewMosaicPolygon = newMosaicPolygon;
            NewVolumePolygon = newVolumePolygon ?? Transform.TryMapShapeSectionToVolume(newMosaicPolygon);
            NewSmoothedVolumePolygon = NewVolumePolygon;//newVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints); 
        }

        public Change2DContourAction(LocationObj location, RetraceCommandAction retraceType, Polygon newMosaicPolygon, Polygon? newVolumePolygon = null, bool ClockwiseContour = false, IVolumeToSectionTransform? transform = null)
        {
            Debug.Assert(newMosaicPolygon.TotalUniqueVertices < 1000, "This is a huge polygon, why?");

            this.ClockwiseContour = ClockwiseContour;
            RetraceType = retraceType;
            Location = location;
            Transform = transform ?? AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;

            NewMosaicPolygon = newMosaicPolygon;

            NewVolumePolygon = newVolumePolygon is null ? Transform.TryMapShapeSectionToVolume(newMosaicPolygon) : newVolumePolygon;
            NewSmoothedVolumePolygon = NewVolumePolygon;//newVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
        }

        public async void OnExecute()
        {
            Microsoft.SqlServer.Types.SqlGeometry original_mosaic_polygon = Location.MosaicShape.ToSqlGeometry();
            //var mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolygon);
            Location.SetShapeFromGeometryInSection(Transform, NewMosaicPolygon.ToSqlGeometry());

            try
            {
                await Store.Locations.Save();
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
                Location.SetShapeFromGeometryInSection(Transform, original_mosaic_polygon);
            }
        }

        public bool Equals(IAction other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Type != other.Type)
            {
                return false;
            }

            if (other is not Change2DContourAction other_action)
            {
                return false;
            }

            return Equals(other_action);
        }

        public bool Equals(Change2DContourAction other)
        {
            if (other.Location.ID != Location.ID)
            {
                return false;
            }

            return NewVolumePolygon.Equals(other.NewVolumePolygon);
        }

        public override string ToString() => $"{base.ToString()} {Type} {RetraceType}";
    }

    /// <summary>
    /// Replace the exterior contour of an annotation with the passed contour
    /// </summary>
    internal class Change1DContourAction(LocationObj location, Polyline newVolumePolyline, IVolumeToSectionTransform? transform = null) : IAction, IEquatable<Change1DContourAction>
    {
        public readonly LocationObj Location = location;
        private readonly IVolumeToSectionTransform Transform = transform ?? AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly Polyline NewVolumePolyline = newVolumePolyline;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly Polyline NewSmoothVolumePolyline = newVolumePolyline.Smooth(Global.NumOpenCurveInterpolationPoints);

        public LocationAction Type => LocationAction.CHANGEBOUNDARY;

        public Action Execute => OnExecute;

        public static implicit operator Action(Change1DContourAction a)
        {
            return a.Execute;
        }

        public async void OnExecute()
        {
            Polyline mosaic_shape = Transform.TryMapShapeVolumeToSection(NewVolumePolyline);
            Location.SetShapeFromGeometryInSection(Transform, mosaic_shape.ToSqlGeometry());

            await Store.Locations.Save();
        }

        public bool Equals(IAction other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Type != other.Type)
            {
                return false;
            }

            if (other is not Change1DContourAction other_action)
            {
                return false;
            }

            return Equals(other_action);
        }

        public bool Equals(Change1DContourAction other)
        {
            if (other.Location.ID != Location.ID)
            {
                return false;
            }

            return NewVolumePolyline.Equals(other.NewVolumePolyline);
        }

        public override string ToString() => $"{base.ToString()} {Type}";

    }
}
