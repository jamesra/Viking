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

        Change2DContourAction model;

        public Change2DContourActionView(Change2DContourAction action)
        {
            model = action;
            Icon = GetDefaultIcon(model.RetraceType);
            CreateDefaultVisuals();
        }

        public static BuiltinTexture GetDefaultIcon(RetraceCommandAction action)
        {
            switch (action)
            {
                case RetraceCommandAction.NONE:
                    return BuiltinTexture.None;
                case RetraceCommandAction.GROW_EXTERIOR_RING:
                    return BuiltinTexture.Plus;
                case RetraceCommandAction.SHRINK_EXTERIOR_RING:
                    return BuiltinTexture.Minus;
                case RetraceCommandAction.GROW_INTERNAL_RING:
                    return BuiltinTexture.Plus;
                case RetraceCommandAction.SHRINK_INTERNAL_RING:
                    return BuiltinTexture.Minus;
                case RetraceCommandAction.CREATE_INTERNAL_RING:
                    return BuiltinTexture.Circle;
                case RetraceCommandAction.REPLACE_EXTERIOR_RING:
                    return BuiltinTexture.Circle;
                case RetraceCommandAction.REPLACE_INTERIOR_RING:
                    return BuiltinTexture.Circle;
            }

            return BuiltinTexture.None;
        }

        public void CreateDefaultVisuals()
        {
            GridPolygon smoothedPoly = model.NewSmoothedVolumePolygon; //NewVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
            SolidPolygonView view = new SolidPolygonView(model.NewVolumePolygon, GetShapeColor(model.RetraceType).SetAlpha(0.5f));
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
            catch (NullReferenceException e)
            {
            }

            switch (action)
            {
                case RetraceCommandAction.NONE:
                    return Color.Gray;
                case RetraceCommandAction.GROW_EXTERIOR_RING:
                    return DefaultStructureColor;
                case RetraceCommandAction.SHRINK_EXTERIOR_RING:
                    return model.ClockwiseContour ? DefaultStructureColor.Invert() : DefaultStructureColor;
                case RetraceCommandAction.GROW_INTERNAL_RING:
                    return DefaultStructureColor;
                case RetraceCommandAction.SHRINK_INTERNAL_RING:
                    return model.ClockwiseContour ? DefaultStructureColor.Invert() : DefaultStructureColor;
                case RetraceCommandAction.CREATE_INTERNAL_RING:
                    return Color.White;
                case RetraceCommandAction.REPLACE_EXTERIOR_RING:
                    return model.ClockwiseContour ? DefaultStructureColor.Invert() : DefaultStructureColor;
                case RetraceCommandAction.REPLACE_INTERIOR_RING:
                    return DefaultStructureColor;
            }

            throw new NotImplementedException();
        }

    }

    internal class Change1DContourActionView : IActionView, IIconTexture
    {
        public IRenderable Passive { get; set; }
        public IRenderable Active { get; set; }
        public BuiltinTexture Icon => BuiltinTexture.None;

        Change1DContourAction model;

        public Change1DContourActionView(Change1DContourAction action)
        {
            model = action;
            CreateDefaultVisuals();
        }

        public void CreateDefaultVisuals()
        {
            PolyLineView view = new PolyLineView(model.NewVolumePolyline.Smooth(Global.NumClosedCurveInterpolationPoints), Color.Green.SetAlpha(0.5f));
            Passive = view;
            Active = new PolyLineView(model.NewVolumePolyline.Smooth(Global.NumClosedCurveInterpolationPoints), Color.Green.SetAlpha(1f));
        }
    }
}
