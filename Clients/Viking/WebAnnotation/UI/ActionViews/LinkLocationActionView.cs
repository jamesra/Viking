using Microsoft.Xna.Framework;
using System;
using VikingXNAGraphics;
using WebAnnotation.UI.Actions;

namespace WebAnnotation.UI.ActionViews
{
    class LinkLocationActionView : IActionView, IIconTexture
    {
        public IRenderable Passive { get; set; } = null;

        public IRenderable Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.Chain;

        public readonly LinkLocationAction model;
        public LinkLocationActionView(LinkLocationAction action)
        {
            model = action;
            CreateDefaultVisuals();
        }

        public void CreateDefaultVisuals()
        {
            LineView view = new LineView(model.A.VolumePosition, model.B.VolumePosition, Math.Min(model.A.Radius, model.B.Radius), Color.White.SetAlpha(0.5f), LineStyle.Standard);
            Passive = view;
            Active = new LineView(model.A.VolumePosition, model.B.VolumePosition, Math.Min(model.A.Radius, model.B.Radius), Color.White.SetAlpha(1f), LineStyle.Standard);
        }
    }
}
