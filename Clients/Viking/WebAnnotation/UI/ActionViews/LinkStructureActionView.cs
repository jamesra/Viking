using Microsoft.Xna.Framework;
using System;
using VikingXNAGraphics;
using WebAnnotation.UI.Actions;

namespace WebAnnotation.UI.ActionViews
{
    internal class LinkStructureActionView : IActionView, IIconTexture
    {
        public IRenderable? Passive { get; set; } = null;

        public IRenderable? Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.Connect;

        public readonly LinkStructureAction model;

        public LinkStructureActionView(LinkStructureAction action)
        {
            model = action;
            CreateDefaultVisuals();
        }

        public void CreateDefaultVisuals()
        {
            double width = Math.Min(model.Source.VolumeRadius(), model.Target.VolumeRadius());
            LineView view = new(model.Source.VolumeCenter(), model.Target.VolumeCenter(), width,
                Color.White.SetAlpha(0.5f), LineStyle.AnimatedLinear);
            Passive = view;
            Active = new LineView(model.Source.VolumeCenter(), model.Target.VolumeCenter(), width,
                Color.White.SetAlpha(1f), LineStyle.AnimatedLinear);
        }
    }
}
