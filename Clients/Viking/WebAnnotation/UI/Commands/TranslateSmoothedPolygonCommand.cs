using System;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Viking.VolumeModel;
using VikingXNAGraphics;

namespace WebAnnotation.UI.Commands
{
    class TranslatePolygonCommand : RotateTranslateScaleCommand, Viking.Common.IHelpStrings
    {
        public delegate void OnCommandSuccess(GridPolygon MosaicPolygon);
        protected OnCommandSuccess success_callback;

        public override double AnnotationRadius => Math.Sqrt(OriginalMosaicPolygon.Area / Math.PI);

        public override string[] HelpStrings
        {
            get
            {
                List<string> s = new List<string>(base.HelpStrings);
                s.AddRange(TranslateOpenCurveCommand.DefaultMouseHelpStrings);
                s.Sort();
                return s.ToArray();
            }
        }

        protected GridVector2 DeltaSum = new GridVector2(0, 0);

        private GridPolygon OriginalMosaicPolygon;
        public GridPolygon TransformedMosaicPolygon;
        protected MeshModel<VertexPositionColor> _mesh;
        protected CircleView OriginalVolumePositionView;
        protected CircleView TranslatedVolumePositionView;

        public Microsoft.Xna.Framework.Color Color;

        /// <summary>
        /// True if the Polygon's boundaries should be smoothed with a curve fitting algorithm
        /// </summary>
        public bool SmoothPolygon = false;

        protected override GridVector2 VolumeRotationOrigin
        {
            get
            {
                return mapping.SectionToVolume(TransformedMosaicPolygon.Centroid);
            }
        }

        public TranslatePolygonCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon MosaicPolygon,
                                        GridVector2 VolumePosition,
                                        Microsoft.Xna.Framework.Color color,
                                        OnCommandSuccess success_callback) : base(parent, VolumePosition)
        {
            OriginalMosaicPolygon = MosaicPolygon;
            Color = color;
            TransformedMosaicPolygon = CalculateTransformedPolygon();
            CreateUpdateView();
            this.success_callback = success_callback;
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    BasicEffect basicEffect)
        {
            CircleView.Draw(graphicsDevice, scene, OverlayStyle.Luma,
                            new CircleView[] { OriginalVolumePositionView, TranslatedVolumePositionView });

            MeshView<VertexPositionColor>.Draw(graphicsDevice, scene, Parent.PolygonOverlayEffect, meshmodels: new MeshModel<VertexPositionColor>[] { _mesh });
        }

        protected override void OnAngleChanged()
        {
            TransformedMosaicPolygon = CalculateTransformedPolygon();
            CreateUpdateView();
        }

        protected override void OnSizeScaleChanged()
        {
            TransformedMosaicPolygon = CalculateTransformedPolygon();
            CreateUpdateView();
        }

        protected override void OnTranslationChanged()
        {
            TransformedMosaicPolygon = CalculateTransformedPolygon();
            CreateUpdateView();
        }

        protected GridPolygon CalculateTransformedPolygon()
        {
            GridPolygon poly = OriginalMosaicPolygon.Clone() as GridPolygon;
            if (Angle != 0)
            {
                poly = OriginalMosaicPolygon.Rotate(this.Angle);
            }

            if (SizeScale != 1.0)
            {
                poly = poly.Scale(this.SizeScale);
            }

            if (MosaicPositionDeltaSum != GridVector2.Zero)
            {
                poly = poly.Translate(this.MosaicPositionDeltaSum);
            }

            return poly;
        }

        protected void CreateUpdateView()
        {
            GridPolygon TransformedVolumePolygon = mapping.TryMapShapeSectionToVolume(this.TransformedMosaicPolygon);
            TransformedVolumePolygon = TransformedVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
            _mesh = TransformedVolumePolygon.CreateMeshForPolygon2D(Color.ConvertToHSL());

            OriginalVolumePositionView = new CircleView(new GridCircle(this.OriginalVolumePosition, 16), Microsoft.Xna.Framework.Color.Red);
            TranslatedVolumePositionView = new CircleView(new GridCircle(this.TranslatedVolumePosition, 16), Microsoft.Xna.Framework.Color.Green);
        }

        protected override void Execute()
        {
            if (this.success_callback != null)
            {
                /*
                GridPolygon VolumeShape = null;
                try
                {
                    VolumeShape = mapping.TryMapShapeSectionToVolume(this.TransformedMosaicPolygon);
                }
                catch(ArgumentOutOfRangeException)
                {
                    Trace.WriteLine("TranslateSmoothedPolygonCommand: Could not map polygon on Execute: " + TranslatedVolumePosition.ToString(), "Command");
                    return;
                }
                */
                success_callback(TransformedMosaicPolygon);
            }

            base.Execute();
        }
    }
}
