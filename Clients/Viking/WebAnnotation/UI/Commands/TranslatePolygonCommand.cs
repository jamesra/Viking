using Geometry;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using VikingXNAGraphics;

namespace WebAnnotation.UI.Commands
{
    internal class TranslatePolygonCommand : RotateTranslateScaleCommand, Viking.Common.IHelpStrings
    {
        public delegate void OnCommandSuccess(Polygon MosaicPolygon, Geometry.Vector2[] transformedExtraPoints);
        protected OnCommandSuccess success_callback;

        public override double AnnotationRadius => Math.Sqrt(OriginalMosaicPolygon.Area / Math.PI);

        public override string[] HelpStrings
        {
            get
            {
                List<string> s = [.. base.HelpStrings];
                s.AddRange(TranslateOpenCurveCommand.DefaultMouseHelpStrings);
                s.Sort();
                return [.. s];
            }
        }

        protected Geometry.Vector2 DeltaSum = new(0, 0);

        private readonly Polygon OriginalMosaicPolygon;
        public Polygon TransformedMosaicPolygon;
        protected MeshModel<VertexPositionColor> _mesh;
        protected CircleView OriginalVolumePositionView;
        protected CircleView TranslatedVolumePositionView;

        private readonly Geometry.Vector2[] _originalExtraMosaicPoints;

        /// <summary>
        /// The extra mosaic points transformed by the current command parameters
        /// </summary>
        public Geometry.Vector2[] TransformedExtraMosaicPoints { get; private set; }
        private readonly PointSetView _extraPointsView;

        public Microsoft.Xna.Framework.Color Color;

        /// <summary>
        /// True if the Polygon's boundaries should be smoothed with a curve fitting algorithm
        /// </summary>
        public bool SmoothPolygon = false;

        /// <summary>
        /// The type the moved polygon is saved as. The preview is the forward owner's shape for it
        /// (<see cref="LocationVolumeGeometry.MapShape"/>), so what is shown while moving is what is drawn after the save.
        /// </summary>
        public LocationType PreviewType { get; init; } = LocationType.CURVEPOLYGON;

        /// <summary>The moved polygon's centroid as the forward owner maps it; the drag origin when it does not map.</summary>
        protected override Geometry.Vector2 VolumeRotationOrigin =>
            LocationVolumeGeometry.TryMapPoints([TransformedMosaicPolygon.Centroid], mapping, out Geometry.Vector2[] mapped)[0] ? mapped[0] : TranslatedVolumePosition;

        public TranslatePolygonCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Polygon MosaicPolygon,
                                        Geometry.Vector2 VolumePosition,
                                        Microsoft.Xna.Framework.Color color,
                                        OnCommandSuccess success_callback) : base(parent, VolumePosition)
        {
            OriginalMosaicPolygon = MosaicPolygon;
            Color = color;
            TransformedMosaicPolygon = CalculateTransformedPolygon();
            _originalExtraMosaicPoints = null;
            CreateUpdateView();
            this.success_callback = success_callback;
        }

        public TranslatePolygonCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Polygon MosaicPolygon,
                                        Geometry.Vector2 VolumePosition,
                                        Geometry.Vector2[] extra_mosaic_points, //Additional points to display for user feedback
                                        Microsoft.Xna.Framework.Color color,
                                        OnCommandSuccess success_callback) : base(parent, VolumePosition)
        {
            OriginalMosaicPolygon = MosaicPolygon;
            Color = color;
            _originalExtraMosaicPoints = extra_mosaic_points;

            if (_originalExtraMosaicPoints != null && _originalExtraMosaicPoints.Length > 0)
            {
                _extraPointsView = new PointSetView(GetComplementaryColor(color), 14);
            }

            TransformedMosaicPolygon = CalculateTransformedPolygon();
            TransformedExtraMosaicPoints = CalculateTransformedPoints();
            CreateUpdateView();
            this.success_callback = success_callback;
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    BasicEffect basicEffect)
        {
            CircleView.Draw(graphicsDevice, scene, OverlayStyle.Luma,
                            new CircleView[] { OriginalVolumePositionView, TranslatedVolumePositionView });

            var oldValue = Parent.PolygonOverlayEffect.InputLumaAlphaValue;
            Parent.PolygonOverlayEffect.InputLumaAlphaValue = 1f;
            MeshView<VertexPositionColor>.Draw(graphicsDevice, scene, Parent.PolygonOverlayEffect, cullmode: CullMode.CullClockwiseFace, meshmodels: new MeshModel<VertexPositionColor>[] { _mesh });
            Parent.PolygonOverlayEffect.InputLumaAlphaValue = oldValue;

            // Draw extra points if they exist
            _extraPointsView?.Draw(graphicsDevice, scene, OverlayStyle.Luma);
        }

        protected override void OnAngleChanged()
        {
            TransformedMosaicPolygon = CalculateTransformedPolygon();
            TransformedExtraMosaicPoints = CalculateTransformedPoints();
            CreateUpdateView();
        }

        protected override void OnSizeScaleChanged()
        {
            TransformedMosaicPolygon = CalculateTransformedPolygon();
            TransformedExtraMosaicPoints = CalculateTransformedPoints();
            CreateUpdateView();
        }

        protected override void OnTranslationChanged()
        {
            TransformedMosaicPolygon = CalculateTransformedPolygon();
            TransformedExtraMosaicPoints = CalculateTransformedPoints();
            CreateUpdateView();
        }

        protected Polygon CalculateTransformedPolygon()
        {
            Polygon? poly = OriginalMosaicPolygon.Clone() as Polygon;
            if (Angle != 0)
            {
                poly = OriginalMosaicPolygon.Rotate(Angle);
            }

            if (Math.Abs(SizeScale - 1.0) > Geometry.Global.Epsilon)
            {
                poly = poly.Scale(SizeScale);
            }

            if (MosaicPositionDeltaSum != Geometry.Vector2.Zero)
            {
                poly = poly.Translate(MosaicPositionDeltaSum);
            }

            return poly;
        }

        protected Geometry.Vector2[] CalculateTransformedPoints()
        {
            if (this._originalExtraMosaicPoints is null || this._originalExtraMosaicPoints.Length == 0)
            {
                return null;
            }

            List<Geometry.Vector2> mosaic_points = [];
            var mosaic_centroid = TransformedMosaicPolygon.Centroid;
            if (_originalExtraMosaicPoints != null && _originalExtraMosaicPoints.Length > 0)
            {
                mosaic_points.AddRange(_originalExtraMosaicPoints);
            }
            Geometry.Vector2[] transformedPoints = [.. mosaic_points];
            // Apply rotation around polygon centroid
            if (Angle != 0)
            {
                transformedPoints = transformedPoints.Rotate(Angle, mosaic_centroid);
            }
            // Apply scale around polygon centroid
            if (Math.Abs(SizeScale - 1.0) > Geometry.Global.Epsilon)
            {
                transformedPoints = transformedPoints.Scale(SizeScale, mosaic_centroid);
            }
            // Apply translation
            if (MosaicPositionDeltaSum != Geometry.Vector2.Zero)
            {
                transformedPoints = transformedPoints.Translate(MosaicPositionDeltaSum);
            }
            return [.. transformedPoints];
        }

        /// <summary>
        /// Rebuilds the preview mesh and markers for the current rotation, scale and translation.
        /// When a vertex of the transformed polygon falls outside the section's mapping, the previous
        /// mesh is kept (none on the first call) so dragging across a mapping gap does not throw.
        /// </summary>
        protected void CreateUpdateView()
        {
            SqlGeometry? TransformedVolumeShape = LocationVolumeGeometry.MapShape(PreviewType, TransformedMosaicPolygon.ToSqlGeometry(), mapping);
            if (TransformedVolumeShape is not null)
            {
                _mesh = TransformedVolumeShape.ToPolygon().CreateMeshForPolygon2D(Color.ConvertToHCL());
            }

            OriginalVolumePositionView = new CircleView(new Circle(OriginalVolumePosition, 16), Microsoft.Xna.Framework.Color.Red);
            TranslatedVolumePositionView = new CircleView(new Circle(TranslatedVolumePosition, 16), Microsoft.Xna.Framework.Color.Green);

            if (TransformedExtraMosaicPoints != null)
            {
                bool[] mapped = LocationVolumeGeometry.TryMapPoints(this.TransformedExtraMosaicPoints, mapping, out Geometry.Vector2[] transformedVolumePoints);

                _extraPointsView.Points = [.. transformedVolumePoints];
                _extraPointsView.UpdateViews();
            }
        }

        /// <summary>
        /// Passes the transformed polygon to the success callback, unless part of it lies outside the
        /// section's mapping; then the callback is skipped (the location keeps its shape) and a trace line is written.
        /// </summary>
        protected override void Execute()
        {
            if (success_callback != null)
            {
                if (LocationVolumeGeometry.TryMapPolygon(TransformedMosaicPolygon, mapping) is null)
                {
                    Trace.WriteLine("TranslatePolygonCommand: polygon released over an unmapped region at " + TranslatedVolumePosition.ToString() + "; not applied.", "Command");
                }
                else
                {
                    success_callback(TransformedMosaicPolygon, TransformedExtraMosaicPoints);
                }
            }

            base.Execute();
        }

        private Microsoft.Xna.Framework.Color GetComplementaryColor(Microsoft.Xna.Framework.Color color)
        {
            // Calculate complementary color by inverting RGB values
            return new Microsoft.Xna.Framework.Color(
                255 - color.R,
                255 - color.G,
                255 - color.B,
                color.A
            );
        }
    }
}
