using Geometry;
using Microsoft.Xna.Framework;
using SqlGeometryUtils;
using System;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

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
        public readonly Polygon NewVolumePolygon;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly Polygon NewSmoothVolumePolygon;

        public LocationAction Type => LocationAction.CHANGETYPE;

        public Action Execute => OnExecute;

        public static implicit operator Action(ChangeToPolygonAction a)
        {
            return a.Execute;
        }

        public IRenderable? Passive { get; set; } = null;

        public IRenderable? Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.None;

        public ChangeToPolygonAction(LocationObj location, Polygon newVolumePolygon, IVolumeToSectionTransform? transform = null)
        {
            Location = location;
            Transform = transform ?? AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;
            NewVolumePolygon = newVolumePolygon;
            NewSmoothVolumePolygon = NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);

            CreateDefaultVisuals();
        }

        public async void OnExecute()
        {
            Microsoft.SqlServer.Types.SqlGeometry original_mosaic_polygon = Location.MosaicShape.ToSqlGeometry();
            Polygon mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolygon);
            Location.TypeCode = LocationType.CURVEPOLYGON;
            Location.SetShapeFromGeometryInSection(Transform, mosaic_polygon.ToSqlGeometry());

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

        public void CreateDefaultVisuals()
        {
            SolidPolygonView view = new(NewSmoothVolumePolygon, Color.Green.SetAlpha(0.5f));
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

            if (other is not ChangeToPolygonAction other_action)
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
        public readonly Polyline NewVolumePolyline;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly Polyline NewSmoothVolumePolyline;

        public LocationAction Type => LocationAction.CHANGETYPE;

        public Action Execute => OnExecute;

        public static implicit operator Action(ChangeToPolylineAction a)
        {
            return a.Execute;
        }

        public IRenderable? Passive { get; set; } = null;

        public IRenderable? Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.None;

        public ChangeToPolylineAction(LocationObj location, Polyline newVolumePolyline, IVolumeToSectionTransform? transform = null)
        {
            Location = location;
            Transform = transform ?? AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;
            NewVolumePolyline = newVolumePolyline;
            NewSmoothVolumePolyline = NewVolumePolyline.Smooth(Global.NumClosedCurveInterpolationPoints);

            CreateDefaultVisuals();
        }

        public async void OnExecute()
        {
            Polyline mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolyline);
            Location.TypeCode = LocationType.POLYLINE;
            Location.SetShapeFromGeometryInSection(Transform, mosaic_polygon.ToSqlGeometry());

            await Store.Locations.Save();
        }

        public void CreateDefaultVisuals()
        {
            PolyLineView view = new(NewSmoothVolumePolyline, Color.Green.SetAlpha(0.5f));
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

            if (other is not ChangeToPolylineAction other_action)
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
