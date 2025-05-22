using Geometry;
using Microsoft.Xna.Framework;
using SqlGeometryUtils;
using System;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotationModel;

namespace WebAnnotation.UI.Actions
{
    /// <summary>
    /// Replace the exterior contour of an annotation with the passed contour
    /// </summary>
    internal class ChangeToPolygonAction : IAction, IActionView, IEquatable<ChangeToPolygonAction>
    {
        public readonly LocationObj Location;
        private readonly IVolumeToSectionTransform Transform;

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

        public static implicit operator Action(ChangeToPolygonAction a)
        {
            return a.Execute;
        }

        public IRenderable Passive { get; set; } = null;

        public IRenderable Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.None;

        public ChangeToPolygonAction(LocationObj location, GridPolygon newVolumePolygon, IVolumeToSectionTransform transform = null)
        {
            Location = location;
            Transform = transform == null ?
                WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform
                : transform;
            NewVolumePolygon = newVolumePolygon;
            NewSmoothVolumePolygon = NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);

            CreateDefaultVisuals();
        }

        public void OnExecute()
        {
            Microsoft.SqlServer.Types.SqlGeometry original_mosaic_polygon = Location.MosaicShape;
            GridPolygon mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolygon);
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
            {
                return true;
            }

            if (Type != other.Type)
            {
                return false;
            }

            ChangeToPolygonAction other_action = other as ChangeToPolygonAction;
            if (other_action == null)
            {
                return false;
            }

            return Equals(other_action);
        }

        public bool Equals(ChangeToPolygonAction other)
        {
            if (other.Location.ID != Location.ID)
            {
                return false;
            }

            return NewVolumePolygon.Equals(other.NewVolumePolygon);
        }
    }

    /// <summary>
    /// Replace the exterior contour of an annotation with the passed contour
    /// </summary>
    internal class ChangeToPolylineAction : IAction, IActionView, IEquatable<ChangeToPolylineAction>
    {
        public readonly LocationObj Location;
        private readonly IVolumeToSectionTransform Transform;

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

        public static implicit operator Action(ChangeToPolylineAction a)
        {
            return a.Execute;
        }

        public IRenderable Passive { get; set; } = null;

        public IRenderable Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.None;

        public ChangeToPolylineAction(LocationObj location, GridPolyline newVolumePolyline, IVolumeToSectionTransform transform = null)
        {
            Location = location;
            Transform = transform == null ?
                WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform
                : transform;
            NewVolumePolyline = newVolumePolyline;
            NewSmoothVolumePolyline = NewVolumePolyline.Smooth(Global.NumClosedCurveInterpolationPoints);

            CreateDefaultVisuals();
        }

        public void OnExecute()
        {
            GridPolyline mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolyline);
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
            {
                return true;
            }

            if (Type != other.Type)
            {
                return false;
            }

            ChangeToPolylineAction other_action = other as ChangeToPolylineAction;
            if (other_action == null)
            {
                return false;
            }

            return Equals(other_action);
        }

        public bool Equals(ChangeToPolylineAction other)
        {
            if (other.Location.ID != Location.ID)
            {
                return false;
            }

            return NewVolumePolyline.Equals(other.NewVolumePolyline);
        }
    }
}
