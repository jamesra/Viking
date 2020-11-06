using Microsoft.Xna.Framework;
using System;
using VikingXNAGraphics;
using WebAnnotation.UI.Actions;

namespace WebAnnotation.UI.ActionViews
{
    class LinkStructureActionView : IActionView, IIconTexture
    {
        public IRenderable Passive { get; set; } = null;

        public IRenderable Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.Connect;

        public readonly LinkStructureAction model;

        public LinkStructureActionView(LinkStructureAction action)
        {
            model = action;
            CreateDefaultVisuals();
        }

        public void CreateDefaultVisuals()
        {
            LineView view = new LineView(model.Source.VolumePosition, model.Target.VolumePosition,
                Math.Min(model.Source.Radius, model.Target.Radius),
                Color.White.SetAlpha(0.5f), LineStyle.AnimatedLinear);
            Passive = view;
            Active = new LineView(model.Source.VolumePosition, model.Target.VolumePosition,
                Math.Min(model.Source.Radius, model.Target.Radius),
                Color.White.SetAlpha(1f), LineStyle.AnimatedLinear);
        }
    }
}
