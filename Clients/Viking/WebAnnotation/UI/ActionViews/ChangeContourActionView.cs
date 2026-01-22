using Geometry;
using Microsoft.Xna.Framework;
using System;
using VikingXNAGraphics;
using WebAnnotation.UI.Actions;

namespace WebAnnotation.UI.ActionViews
{
    internal class Change2DContourActionView : IActionView, IIconTexture
    {
        public IRenderable Passive { get; set; }
        public IRenderable Active { get; set; }
        public BuiltinTexture Icon { get; private set; } = BuiltinTexture.None;

        private readonly Change2DContourAction model;

        public Change2DContourActionView(Change2DContourAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            model = action;
            Icon = GetDefaultIcon(model.RetraceType);
            CreateDefaultVisuals();
        }

        public static BuiltinTexture GetDefaultIcon(RetraceCommandAction action)
        {
            return action switch
            {
                RetraceCommandAction.NONE => BuiltinTexture.None,
                RetraceCommandAction.GROW_EXTERIOR_RING => BuiltinTexture.Plus,
                RetraceCommandAction.SHRINK_EXTERIOR_RING => BuiltinTexture.Minus,
                RetraceCommandAction.GROW_INTERNAL_RING => BuiltinTexture.Plus,
                RetraceCommandAction.SHRINK_INTERNAL_RING => BuiltinTexture.Minus,
                RetraceCommandAction.CREATE_INTERNAL_RING => BuiltinTexture.Circle,
                RetraceCommandAction.REPLACE_EXTERIOR_RING => BuiltinTexture.Circle,
                RetraceCommandAction.REPLACE_INTERIOR_RING => BuiltinTexture.Circle,
                _ => BuiltinTexture.None,
            };
        }

        public void CreateDefaultVisuals()
        {
            GridPolygon smoothedPoly = model.NewSmoothedVolumePolygon; //NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
            SolidPolygonView view = new(model.NewVolumePolygon, GetShapeColor(model.RetraceType).SetAlpha(0.5f));
            Passive = view;
            Active = new SolidPolygonView(model.NewVolumePolygon, GetShapeColor(model.RetraceType).SetAlpha(0.75f));
        }

        public Color GetShapeColor(RetraceCommandAction action)
        {
            Color DefaultStructureColor = Color.Green;
            try
            {
                DefaultStructureColor = model.Location.Parent.Type.Color.ToXNAColor();
            }
            catch (NullReferenceException)
            {
            }

            return action switch
            {
                RetraceCommandAction.NONE => Color.Gray,
                RetraceCommandAction.GROW_EXTERIOR_RING => DefaultStructureColor,
                RetraceCommandAction.SHRINK_EXTERIOR_RING => model.ClockwiseContour ? DefaultStructureColor.Invert() : DefaultStructureColor,
                RetraceCommandAction.GROW_INTERNAL_RING => DefaultStructureColor,
                RetraceCommandAction.SHRINK_INTERNAL_RING => model.ClockwiseContour ? DefaultStructureColor.Invert() : DefaultStructureColor,
                RetraceCommandAction.CREATE_INTERNAL_RING => Color.White,
                RetraceCommandAction.REPLACE_EXTERIOR_RING => model.ClockwiseContour ? DefaultStructureColor.Invert() : DefaultStructureColor,
                RetraceCommandAction.REPLACE_INTERIOR_RING => DefaultStructureColor,
                _ => throw new NotImplementedException(),
            };
        }

    }

    internal class Change1DContourActionView : IActionView, IIconTexture
    {
        public IRenderable Passive { get; set; }
        public IRenderable Active { get; set; }
        public BuiltinTexture Icon => BuiltinTexture.None;

        private readonly Change1DContourAction model;

        public Change1DContourActionView(Change1DContourAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            model = action;
            CreateDefaultVisuals();
        }

        public void CreateDefaultVisuals()
        {
            PolyLineView view = new(model.NewVolumePolyline.Smooth(Global.NumClosedCurveInterpolationPoints), Color.Green.SetAlpha(0.5f));
            Passive = view;
            Active = new PolyLineView(model.NewVolumePolyline.Smooth(Global.NumClosedCurveInterpolationPoints), Color.Green.SetAlpha(1f));
        }
    }
}
