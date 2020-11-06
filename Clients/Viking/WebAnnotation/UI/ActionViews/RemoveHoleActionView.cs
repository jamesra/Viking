
using Microsoft.Xna.Framework;
using VikingXNAGraphics;
using WebAnnotation.UI.Actions;

namespace WebAnnotation.UI.ActionViews
{
    class RemoveHoleActionView : IActionView, IIconTexture
    {
        public IRenderable Passive { get; set; } = null;

        public IRenderable Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.Plus;

        public readonly RemoveHoleAction model;

        public RemoveHoleActionView(RemoveHoleAction action)
        {
            model = action;
            CreateDefaultVisuals();
        }

        public void CreateDefaultVisuals()
        {
            SolidPolygonView view = new SolidPolygonView(model.VolumePolygonToRemove.Smooth(Global.NumClosedCurveInterpolationPoints),
                                                         Color.Magenta.SetAlpha(0.5f));
            Passive = view;
        }
    }
}
