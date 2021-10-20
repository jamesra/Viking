using Viking.AnnotationServiceTypes.Interfaces;
using Geometry;
using Microsoft.Xna.Framework;
using SqlGeometryUtils;
using System;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotationModel;

namespace WebAnnotation.UI.Actions
{
    /// <summary>
    /// Replace the exterior contour of an annotation with the passed contour
    /// </summary>
    class ChangeToPolygonAction : IAction, IActionView, IEquatable<ChangeToPolygonAction>
    {
        public readonly LocationObj Location;

        IVolumeToSectionTransform Transform;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly GridPolygon NewVolumePolygon;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly GridPolygon NewSmoothVolumePolygon;

        public LocationAction Type => LocationAction.CHANGETYPE;

        public Action Execute => OnExecute;

        public static implicit operator Action(ChangeToPolygonAction a) => a.Execute;

        public IRenderable Passive { get; set; } = null;

        public IRenderable Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.None;

        public ChangeToPolygonAction(LocationObj location, GridPolygon newVolumePolygon, IVolumeToSectionTransform transform = null)
        {
            this.Location = location;
            this.Transform = transform == null ?
                WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform
                : transform;
            this.NewVolumePolygon = newVolumePolygon;
            NewSmoothVolumePolygon = NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);

            CreateDefaultVisuals();
        }

        public void OnExecute()
        {
            var original_mosaic_polygon = Location.MosaicShape;
            var mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolygon);
            Location.TypeCode = LocationType.CURVEPOLYGON;
            Location.SetShapeFromGeometryInSection(Transform, mosaic_polygon.ToSqlGeometry());

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

        public void CreateDefaultVisuals()
        {
            SolidPolygonView view = new SolidPolygonView(NewSmoothVolumePolygon, Color.Green.SetAlpha(0.5f));
            Passive = view;
            Active = new SolidPolygonView(NewSmoothVolumePolygon, Color.Green.SetAlpha(1f));
        }

        public bool Equals(IAction other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (this.Type != other.Type)
                return false;

            ChangeToPolygonAction other_action = other as ChangeToPolygonAction;
            if (other_action == null)
                return false;

            return this.Equals(other_action);
        }

        public bool Equals(ChangeToPolygonAction other)
        {
            if (other.Location.ID != this.Location.ID)
                return false;

            return this.NewVolumePolygon.Equals(other.NewVolumePolygon);
        }
    }

    /// <summary>
    /// Replace the exterior contour of an annotation with the passed contour
    /// </summary>
    class ChangeToPolylineAction : IAction, IActionView, IEquatable<ChangeToPolylineAction>
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

        public LocationAction Type => LocationAction.CHANGETYPE;

        public Action Execute => OnExecute;

        public static implicit operator Action(ChangeToPolylineAction a) => a.Execute;

        public IRenderable Passive { get; set; } = null;

        public IRenderable Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.None;

        public ChangeToPolylineAction(LocationObj location, GridPolyline newVolumePolyline, IVolumeToSectionTransform transform = null)
        {
            this.Location = location;
            this.Transform = transform == null ?
                WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform
                : transform;
            this.NewVolumePolyline = newVolumePolyline;
            this.NewSmoothVolumePolyline = NewVolumePolyline.Smooth(Global.NumClosedCurveInterpolationPoints);

            CreateDefaultVisuals();
        }

        public void OnExecute()
        {
            var mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolyline);
            Location.TypeCode = LocationType.POLYLINE;
            Location.SetShapeFromGeometryInSection(Transform, mosaic_polygon.ToSqlGeometry());

            Store.Locations.Save();
        }

        public void CreateDefaultVisuals()
        {
            PolyLineView view = new PolyLineView(NewSmoothVolumePolyline, Color.Green.SetAlpha(0.5f));
            Passive = view;
            Active = new PolyLineView(NewSmoothVolumePolyline, Color.Green.SetAlpha(1f));
        }

        public bool Equals(IAction other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (this.Type != other.Type)
                return false;

            ChangeToPolylineAction other_action = other as ChangeToPolylineAction;
            if (other_action == null)
                return false;

            return this.Equals(other_action);
        }

        public bool Equals(ChangeToPolylineAction other)
        {
            if (other.Location.ID != this.Location.ID)
                return false;

            return this.NewVolumePolyline.Equals(other.NewVolumePolyline);
        }
    }
}
