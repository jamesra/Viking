
using Microsoft.Xna.Framework;
using VikingXNAGraphics;
using WebAnnotation.UI.Actions;

namespace WebAnnotation.UI.ActionViews
{
    class ChangeToPolygonActionView : IActionView, IIconTexture
    {
        public IRenderable Passive { get; set; }
        public IRenderable Active { get; set; }
        public BuiltinTexture Icon => BuiltinTexture.Circle;

        ChangeToPolygonAction model;

        public ChangeToPolygonActionView(ChangeToPolygonAction action)
        {
            model = action;
            CreateDefaultVisuals();
        }
        public void CreateDefaultVisuals()
        {
            SolidPolygonView view = new SolidPolygonView(model.NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Color.Green.SetAlpha(0.5f));
            Passive = view;
            Active = new SolidPolygonView(model.NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Color.Green.SetAlpha(1f));
        }
    }

    class ChangeToPolylineActionView : IActionView, IIconTexture
    {
        public IRenderable Passive { get; set; }
        public IRenderable Active { get; set; }
        public BuiltinTexture Icon => BuiltinTexture.Circle;

        ChangeToPolylineAction model;

        public ChangeToPolylineActionView(ChangeToPolylineAction action)
        {
            model = action;
            CreateDefaultVisuals();
        }

        public void CreateDefaultVisuals()
        {
            PolyLineView view = new PolyLineView(model.NewVolumePolyline.Smooth(Global.NumClosedCurveInterpolationPoints),
                Color.Green.SetAlpha(0.5f));
            Passive = view;
            Active = new PolyLineView(model.NewVolumePolyline.Smooth(Global.NumClosedCurveInterpolationPoints),
                Color.Green.SetAlpha(1f));
        }
    }
}
