using Geometry;
using Microsoft.Xna.Framework;
using SqlGeometryUtils;
using System;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.UI.Actions
{
    /// <summary>
    /// Removes an interior polygon from the annotation
    /// </summary>
    internal class RemoveHoleAction : IAction, IActionView, IEquatable<RemoveHoleAction>
    {
        public readonly LocationObj Location;
        private readonly IVolumeToSectionTransform Transform;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly Polygon UpdatedMosaicPolygon;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly Polygon VolumePolygonToRemove;

        public LocationAction Type => LocationAction.CUTHOLE;

        public Action Execute => OnExecute;

        public static implicit operator Action(RemoveHoleAction a)
        {
            return a.Execute;
        }

        public IRenderable? Passive { get; set; } = null;

        public IRenderable? Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.Minus;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="location"></param>
        /// <param name="transform"></param>
        /// <param name="innerPoint">A point inside the interior hole in volume space</param>
        public RemoveHoleAction(LocationObj location, int innerPoly, IVolumeToSectionTransform? transform = null)
        {
            Location = location;
            Transform = transform ?? AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;

            Polygon volumePoly = location.VolumeShape.ToPolygon();
            VolumePolygonToRemove = volumePoly.InteriorPolygons[innerPoly];

            UpdatedMosaicPolygon = location.MosaicShape.ToPolygon();
            UpdatedMosaicPolygon.TryRemoveInteriorRing(innerPoly);

            CreateDefaultVisuals();
        }

        private void OnExecute()
        {
            Microsoft.SqlServer.Types.SqlGeometry original_mosaic_shape = Location.MosaicShape.ToSqlGeometry();

            Location.SetShapeFromGeometryInSection(Transform, UpdatedMosaicPolygon.ToSqlGeometry());

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
            SolidPolygonView view = new(VolumePolygonToRemove.Smooth(Global.NumClosedCurveInterpolationPoints),
                                                         Color.Magenta.SetAlpha(0.5f));
            Passive = view;
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

            if (other is not RemoveHoleAction other_action)
            {
                return false;
            }

            return Equals(other_action);
        }

        public bool Equals(RemoveHoleAction other)
        {
            if (other.Location.ID != Location.ID)
            {
                return false;
            }

            return VolumePolygonToRemove.Equals(other.VolumePolygonToRemove);
        }
    }
}
