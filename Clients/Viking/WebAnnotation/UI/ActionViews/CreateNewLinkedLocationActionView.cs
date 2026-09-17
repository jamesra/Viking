using System;
using Geometry;
using Microsoft.Xna.Framework;
using VikingXNAGraphics;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.UI.ActionViews
{
    internal class CreateNewLinkedLocationActionView : IActionView, IIconTexture, IColorView
    {
        private readonly CreateNewLinkedLocationAction model;

        public IShape2D Shape { get; private set; }

        public IRenderable Passive { get; set; }
        public IRenderable Active { get; set; }
        public BuiltinTexture Icon { get; private set; } = BuiltinTexture.Chain;
        public Color Color { get; set; }
        public float Alpha { get => Color.GetAlpha(); set => Color = Color.SetAlpha(value); }

        public CreateNewLinkedLocationActionView(CreateNewLinkedLocationAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            model = action;
            CreateDefaultVisuals();
        }

        public void CreateDefaultVisuals()
        {
            Active = null;

            Store.Locations.TryGetObjectByID(model.ExistingLocID, out LocationObj existing_loc);
            if (existing_loc != null)
            {
                Store.StructureTypes.TryGetObjectByID(existing_loc.Parent.TypeID, out StructureTypeObj structure_type);

                Color = model != null ? structure_type.Color.ToXNAColor() : Color.White;
            }


            if (model.NewVolumeShape.ShapeType.IsClosed())
            {
                Polygon smoothedPoly = (Polygon)model.NewVolumeShape; //NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
                Shape = smoothedPoly;
                SolidPolygonView view = new(smoothedPoly, Color);
                Active = view;
            }
            else if (model.NewVolumeShape.ShapeType.IsOpen())
            {
                Polyline smoothedPoly = (Polyline)model.NewVolumeShape; //NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
                Shape = smoothedPoly;
                PolyLineView view = new(smoothedPoly, Color);
                Active = view;
            }

            //OK, generate buttons for default structure types
        }
    }
}
