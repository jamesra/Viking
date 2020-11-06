using Microsoft.Xna.Framework;
using VikingXNAGraphics;
using WebAnnotation.UI.Actions;

namespace WebAnnotation.UI.ActionViews
{
    class CutHoleActionView : IActionView, IIconTexture
    {
        public IRenderable Passive { get; set; } = null;

        public IRenderable Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.Minus;

        public readonly CutHoleAction model;

        public CutHoleActionView(CutHoleAction action)
        {
            model = action;
            CreateDefaultVisuals();
        }

        public void CreateDefaultVisuals()
        {
            SolidPolygonView view = new SolidPolygonView(model.NewSmoothVolumeInteriorPolygon, Color.Black.SetAlpha(0.5f));
            Passive = view;
            Active = new SolidPolygonView(model.NewSmoothVolumeInteriorPolygon, Color.Black.SetAlpha(1f));
        }
    }
}
