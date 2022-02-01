using Geometry;
using SqlGeometryUtils;
using System;
using System.Diagnostics;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotationModel;

namespace WebAnnotation.UI.Actions
{

    /// <summary>
    /// Replace the exterior contour of an annotation with the passed contour
    /// </summary>
    internal class Change2DContourAction : IAction, IEquatable<Change2DContourAction>
    {
        public readonly LocationObj Location;

        IVolumeToSectionTransform Transform;

        /// <summary>
        /// The mosaic space polygon we want to commit to the database
        /// </summary>
        public readonly GridPolygon NewMosaicPolygon;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly GridPolygon NewVolumePolygon;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly GridPolygon NewSmoothedVolumePolygon;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        //public readonly GridPolygon NewSmoothVolumePolygon;

        public LocationAction Type => LocationAction.CHANGEBOUNDARY;

        public RetraceCommandAction RetraceType { get; } = RetraceCommandAction.NONE;

        public Action Execute => OnExecute;

        public static implicit operator Action(Change2DContourAction a) => a.Execute;

        /// <summary>
        /// Indicates if this action represents the ClockWiseContour when we are cutting a shape in half
        /// </summary>
        internal bool ClockwiseContour = false;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.None;
        public Change2DContourAction(long locationID, RetraceCommandAction retraceType, GridPolygon newMosaicPolygon, GridPolygon newVolumePolygon = null, bool ClockwiseContour = false, IVolumeToSectionTransform transform = null)
        {
            Debug.Assert(newMosaicPolygon.TotalUniqueVerticies < 1000, "This is a huge polygon, why?");

            this.ClockwiseContour = ClockwiseContour;
            RetraceType = retraceType;
            this.Location = Store.Locations[locationID];

            this.Transform = transform ?? WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;

            this.NewMosaicPolygon = newMosaicPolygon;
            this.NewVolumePolygon = newVolumePolygon ?? Transform.TryMapShapeSectionToVolume(newMosaicPolygon);
            this.NewSmoothedVolumePolygon = NewVolumePolygon;//newVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints); 
        }

        public Change2DContourAction(LocationObj location, RetraceCommandAction retraceType, GridPolygon newMosaicPolygon, GridPolygon newVolumePolygon = null, bool ClockwiseContour = false, IVolumeToSectionTransform transform = null)
        {
            Debug.Assert(newMosaicPolygon.TotalUniqueVerticies < 1000, "This is a huge polygon, why?");

            this.ClockwiseContour = ClockwiseContour;
            RetraceType = retraceType;
            this.Location = location;
            this.Transform = transform == null ?
                WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform
                : transform;

            this.NewMosaicPolygon = newMosaicPolygon;

            this.NewVolumePolygon = newVolumePolygon == null ? Transform.TryMapShapeSectionToVolume(newMosaicPolygon) : newVolumePolygon;
            this.NewSmoothedVolumePolygon = NewVolumePolygon;//newVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
        }

        public void OnExecute()
        {
            var original_mosaic_polygon = Location.MosaicShape;
            //var mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolygon);
            Location.SetShapeFromGeometryInSection(Transform, NewMosaicPolygon.ToSqlGeometry());

            try
            {
                Store.Locations.Save();
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
                return true;

            if (this.Type != other.Type)
                return false;

            Change2DContourAction other_action = other as Change2DContourAction;
            if (other_action == null)
                return false;

            return this.Equals(other_action);
        }

        public bool Equals(Change2DContourAction other)
        {
            if (other.Location.ID != this.Location.ID)
                return false;

            return this.NewVolumePolygon.Equals(other.NewVolumePolygon);
        }

        public override string ToString()
        {
            return string.Format("{0} {1} {2}", base.ToString(), this.Type, this.RetraceType);
        }
    }

    /// <summary>
    /// Replace the exterior contour of an annotation with the passed contour
    /// </summary>
    class Change1DContourAction : IAction, IEquatable<Change1DContourAction>
    {
        public readonly LocationObj Location;

        IVolumeToSectionTransform Transform;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly GridPolyline NewVolumePolyline;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly GridPolyline NewSmoothVolumePolyline;

        public LocationAction Type => LocationAction.CHANGEBOUNDARY;

        public Action Execute => OnExecute;

        public static implicit operator Action(Change1DContourAction a) => a.Execute;


        public Change1DContourAction(LocationObj location, GridPolyline newVolumePolyline, IVolumeToSectionTransform transform = null)
        {
            //TODO: This can be merged back into ChangeContour by passing an IShape2D parameter
            this.Location = location;
            this.Transform = transform == null ?
                WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform
                : transform;
            this.NewVolumePolyline = newVolumePolyline;
            NewSmoothVolumePolyline = newVolumePolyline.Smooth(Global.NumOpenCurveInterpolationPoints);
        }

        public void OnExecute()
        {
            var mosaic_shape = Transform.TryMapShapeVolumeToSection(NewVolumePolyline);
            Location.SetShapeFromGeometryInSection(Transform, mosaic_shape.ToSqlGeometry());

            Store.Locations.Save();
        }

        public bool Equals(IAction other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (this.Type != other.Type)
                return false;

            Change1DContourAction other_action = other as Change1DContourAction;
            if (other_action == null)
                return false;

            return this.Equals(other_action);
        }

        public bool Equals(Change1DContourAction other)
        {
            if (other.Location.ID != this.Location.ID)
                return false;

            return this.NewVolumePolyline.Equals(other.NewVolumePolyline);
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", base.ToString(), this.Type);
        }

    }
}
