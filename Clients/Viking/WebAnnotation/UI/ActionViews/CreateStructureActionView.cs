using System;
using Geometry;
using Microsoft.Xna.Framework;
using VikingXNAGraphics;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;

namespace WebAnnotation.UI.ActionViews
{
    internal class CreateStructureActionView : IActionView, IIconTexture, IColorView
    {
        private readonly CreateStructureActionBase model;

        public IShape2D Shape { get; private set; }

        public IRenderable Passive { get; set; }
        public IRenderable Active { get; set; }
        public BuiltinTexture Icon { get; private set; } = BuiltinTexture.Plus;
        public Color Color { get; set; }
        public float Alpha { get => Color.GetAlpha(); set => Color = Color.SetAlpha(value); }

        public CreateStructureActionView(CreateStructureActionBase action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            model = action;
            CreateDefaultVisuals();
        }

        public void CreateDefaultVisuals()
        {
            Active = null;

            Color = Color.White;
            if (model != null)
            {
                StructureTypeObj structure_type = Store.StructureTypes.GetObjectByID(model.TypeID, false);
                if (structure_type != null)
                {
                    Color = structure_type.Color.ToXNAColor();
                }
            }

            if (model is Create2DStructureAction)
            {
                Create2DStructureAction action = model as Create2DStructureAction;
                GridPolygon smoothedPoly = action.NewVolumePolygon; //NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
                Shape = smoothedPoly;
                SolidPolygonView view = new(action.NewVolumePolygon, Color);
                Active = view;
            }
            else if (model is Create1DStructureAction)
            {
                Create1DStructureAction action = model as Create1DStructureAction;
                GridPolyline smoothedPoly = action.NewVolumeShape; //NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
                Shape = smoothedPoly;
                PolyLineView view = new(smoothedPoly, Color);
                Active = view;
            }

            //OK, generate buttons for default structure types
        }
    }
}
