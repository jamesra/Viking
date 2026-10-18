using Geometry;
using Microsoft.Xna.Framework;
using SqlGeometryUtils;
using System;
using System.Diagnostics;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotationModel;
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
            NewSmoothVolumePolygon = NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints, Global.PenSimplifyThreshold);

            CreateDefaultVisuals();
        }

        /// <summary>
        /// Maps the new volume polygon to the section, makes the location a curve polygon with that shape, and saves it.
        /// Changes nothing, and writes a trace line, when any vertex of the polygon cannot be mapped to the section.
        /// On a save fault the location's original type and shape are restored.
        /// </summary>
        public void OnExecute()
        {
            Microsoft.SqlServer.Types.SqlGeometry original_mosaic_polygon = Location.MosaicShape;
            LocationType originalType = Location.TypeCode;
            Polygon mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolygon);
            if (mosaic_polygon is null)
            {
                Trace.WriteLine("ChangeToPolygonAction: new polygon crosses an unmapped region of the section; location " + Location.ID + " unchanged.", "WebAnnotation");
                return;
            }

            Location.TypeCode = LocationType.CURVEPOLYGON;
            Location.SetShapeFromGeometryInSection(Transform, mosaic_polygon.ToSqlGeometry());

            try
            {
                Store.Locations.Save();
                UndoManager.PushLocationShape("Change Type", Location, Transform, original_mosaic_polygon, originalType);
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
                Location.TypeCode = originalType;
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

        /// <summary>
        /// Maps the new volume polyline to the section, makes the location a polyline with that shape, and saves it.
        /// Changes nothing, and writes a trace line, when any vertex of the polyline cannot be mapped to the section.
        /// On a save fault the location's original type and shape are restored.
        /// </summary>
        public void OnExecute()
        {
            Microsoft.SqlServer.Types.SqlGeometry originalShape = Location.MosaicShape;
            LocationType originalType = Location.TypeCode;
            Polyline mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolyline);
            if (mosaic_polygon is null)
            {
                Trace.WriteLine("ChangeToPolylineAction: new polyline crosses an unmapped region of the section; location " + Location.ID + " unchanged.", "WebAnnotation");
                return;
            }

            Location.TypeCode = LocationType.POLYLINE;
            Location.SetShapeFromGeometryInSection(Transform, mosaic_polygon.ToSqlGeometry());

            try
            {
                Store.Locations.Save();
                UndoManager.PushLocationShape("Change Type", Location, Transform, originalShape, originalType);
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
                Location.TypeCode = originalType;
                Location.SetShapeFromGeometryInSection(Transform, originalShape);
            }
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
