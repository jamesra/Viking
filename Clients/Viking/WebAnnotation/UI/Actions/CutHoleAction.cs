using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using SqlGeometryUtils;
using System;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Actions
{
    /// <summary>
    /// Adds a new interior polygon to the location
    /// </summary>
    internal class CutHoleAction : IAction, IActionView, IEquatable<CutHoleAction>
    {
        public readonly LocationObj Location;
        private readonly IVolumeToSectionTransform Transform;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly GridPolygon NewVolumeInteriorPolygon;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly GridPolygon NewSmoothVolumeInteriorPolygon;

        public LocationAction Type => LocationAction.CUTHOLE;

        public Action Execute => OnExecute;

        public static implicit operator Action(CutHoleAction a)
        {
            return a.Execute;
        }

        public IRenderable? Passive { get; set; } = null;

        public IRenderable? Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.Minus;

        public CutHoleAction(LocationObj location, GridPolygon newVolumeInteriorPolygon, IVolumeToSectionTransform? transform = null)
        {
            Location = location;
            Transform = transform ?? AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;
            NewVolumeInteriorPolygon = newVolumeInteriorPolygon;
            NewSmoothVolumeInteriorPolygon = NewVolumeInteriorPolygon.Smooth(Global.NumClosedCurveInterpolationPoints);

            CreateDefaultVisuals();
        }

        public void OnExecute()
        {
            SqlGeometry original_mosaic_shape = Location.MosaicShape;
            GridVector2[] mosaic_points = Transform.VolumeToSection(NewVolumeInteriorPolygon.ExteriorRing);
            SqlGeometry updatedMosaicShape = Location.MosaicShape.AddInteriorPolygon(mosaic_points);

            Location.SetShapeFromGeometryInSection(Transform, updatedMosaicShape);

            try
            {
                Store.Locations.Save();
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
                Location.SetShapeFromGeometryInSection(Transform, original_mosaic_shape);
            }
        }

        public void CreateDefaultVisuals()
        {
            SolidPolygonView view = new(NewSmoothVolumeInteriorPolygon, Color.Black.SetAlpha(0.5f));
            Passive = view;
            Active = new SolidPolygonView(NewSmoothVolumeInteriorPolygon, Color.Black.SetAlpha(1f));
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

            if (other is not CutHoleAction other_action)
            {
                return false;
            }

            return Equals(other_action);
        }

        public bool Equals(CutHoleAction other)
        {
            if (other.Location.ID != Location.ID)
            {
                return false;
            }

            return NewVolumeInteriorPolygon.Equals(other.NewVolumeInteriorPolygon);
        }
    }
}
