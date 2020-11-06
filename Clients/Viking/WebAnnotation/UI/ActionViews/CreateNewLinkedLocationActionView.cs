using Geometry;
using Microsoft.Xna.Framework;
using VikingXNAGraphics;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;

namespace WebAnnotation.UI.ActionViews
{
    internal class CreateNewLinkedLocationActionView : IActionView, IIconTexture, IColorView
    {
        CreateNewLinkedLocationAction model;

        public IShape2D Shape { get; private set; }

        public IRenderable Passive { get; set; }
        public IRenderable Active { get; set; }
        public BuiltinTexture Icon { get; private set; } = BuiltinTexture.Chain;
        public Color Color { get; set; }
        public float Alpha { get { return this.Color.GetAlpha(); } set { this.Color = this.Color.SetAlpha(value); } }

        public CreateNewLinkedLocationActionView(CreateNewLinkedLocationAction action)
        {
            model = action;
            CreateDefaultVisuals();
        }

        public void CreateDefaultVisuals()
        {
            Active = null;

            LocationObj existing_loc = Store.Locations.GetObjectByID(model.ExistingLocID, true);
            if (existing_loc != null)
            {
                StructureTypeObj structure_type = Store.StructureTypes.GetObjectByID(existing_loc.Parent.TypeID, false);

                if (model != null)
                {
                    Color = structure_type.Color.ToXNAColor();
                }
                else
                {
                    Color = Color.White;
                }
            }


            if (model.NewVolumeShape.ShapeType.IsClosed())
            {
                GridPolygon smoothedPoly = (GridPolygon)model.NewVolumeShape; //NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
                Shape = smoothedPoly;
                SolidPolygonView view = new SolidPolygonView(smoothedPoly, Color);
                Active = view;
            }
            else if (model.NewVolumeShape.ShapeType.IsOpen())
            {
                GridPolyline smoothedPoly = (GridPolyline)model.NewVolumeShape; //NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
                Shape = smoothedPoly;
                PolyLineView view = new PolyLineView(smoothedPoly, Color);
                Active = view;
            }

            //OK, generate buttons for default structure types
        }
    }
}
