using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.VolumeModel;
using VikingXNAGraphics;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    internal class AdjustPolygonVertexCommand : AnnotationCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        private readonly GridPolygon OriginalMosaicPolygon;
        private readonly GridPolygon OriginalVolumePolygon;

        private GridPolygon OutputVolumePolygon;
        private PositionColorMeshModel polygonView;
        private GridPolygon? AdjustedPolygon = null; //The polygon we are adjusting.  This can be an interior polygon.
        private bool ControlPointSelected = false;
        private PolygonIndex iOriginalVolumePolyControlPoint;
        private PolygonIndex iAdjustedControlPoint; //The index of the vertex in the exterior ring to adjust. 

        private Color _color;

        /// <summary>
        /// Returns unsmoothed mosaic and volume polygons with the new point
        /// </summary>
        /// <param name="MosaicPolygon"></param>
        /// <param name="VolumePolygon"></param>
        public delegate void OnCommandSuccess(GridPolygon MosaicPolygon, GridPolygon VolumePolygon);

        private readonly OnCommandSuccess success_callback;
        private readonly Viking.VolumeModel.IVolumeToSectionTransform mapping;

        public string[] HelpStrings => ["Release Left Mouse Button to place control point"];

        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);

        public AdjustPolygonVertexCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        Microsoft.Xna.Framework.Color color,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            OriginalMosaicPolygon = mosaic_polygon;
            OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);
            _color = color;

            //this.SmoothedVolumePolygon = OriginalVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
            this.success_callback = success_callback;

        }

        private static async Task<PositionColorMeshModel> CreateView(GridPolygon poly, Color color, CancellationToken token) => await Task.Run(() => poly.Smooth(Global.NumClosedCurveInterpolationPointsForDisplay).CreateMeshForPolygon2D(color), token);

        protected void PopulateControlPointIndexIfNeeded(GridVector2 WorldPosition)
        {
            if (ControlPointSelected == false)
            {
                ControlPointSelected = true;
                OriginalVolumePolygon.NearestVertex(WorldPosition, out iOriginalVolumePolyControlPoint);
                AdjustedPolygon = (GridPolygon)iOriginalVolumePolyControlPoint.Polygon(OriginalVolumePolygon).Clone();

                iAdjustedControlPoint = iOriginalVolumePolyControlPoint.IsInner
                    ? iOriginalVolumePolyControlPoint.ReindexToOuter()
                    : iOriginalVolumePolyControlPoint;
            }
        }

        private CancellationTokenSource? UpdatePositionCancellationTokenSource = null;

        protected virtual async Task UpdatePosition(GridVector2 PositionDelta)
        {
            AdjustedPolygon[iAdjustedControlPoint] = AdjustedPolygon[iAdjustedControlPoint] + PositionDelta;

            //If we haven't moved a significant distance, don't update the view
            if (PositionDelta.Round(0) == GridVector2.Zero)
            {
                return;
            }

            CancellationTokenSource newTokenSource = new();
            CancellationTokenSource existingToken = Interlocked.Exchange(ref UpdatePositionCancellationTokenSource, newTokenSource);
            existingToken?.Cancel();

            PositionColorMeshModel result = await CreateView(AdjustedPolygon, _color, newTokenSource.Token);
            if (newTokenSource.IsCancellationRequested == false)
            {
                Interlocked.Exchange(ref polygonView, result);
                ThreadSafeParentInvalidate();
            }
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
            PopulateControlPointIndexIfNeeded(NewPosition);

            //Redraw if we are dragging a location
            if (oldMouse != null)
            {
                if (oldMouse.Button.Left())
                {
                    GridVector2 LastWorldPosition = Parent.ScreenToWorld(oldMouse.X, oldMouse.Y);
                    UpdatePosition(NewPosition - LastWorldPosition);
                }
            }

            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button.Left())
            {
                GridVector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
                PopulateControlPointIndexIfNeeded(NewPosition);

                if (AdjustedPolygon != null)
                {
                    OutputVolumePolygon = (GridPolygon)OriginalVolumePolygon.Clone();
                    OutputVolumePolygon[iOriginalVolumePolyControlPoint] = AdjustedPolygon[iAdjustedControlPoint];
                    Execute();
                }
                else
                {
                    CommandActive = false;
                }
            }

            base.OnMouseUp(sender, e);
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            if (polygonView != null)
            {
                MeshView<VertexPositionColor>.Draw(graphicsDevice, scene, DeviceEffectsStore<PolygonOverlayEffect>.TryGet(graphicsDevice), meshmodels: new PositionColorMeshModel[] { polygonView });
            }
        }

        protected override void Execute()
        {
            GridPolygon mosaic_polygon;
            try
            {
                mosaic_polygon = mapping.TryMapShapeVolumeToSection(OutputVolumePolygon);
            }
            catch (ArgumentOutOfRangeException)
            {
                Trace.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                return;
            }

            success_callback(mosaic_polygon, OutputVolumePolygon);

            base.Execute();
        }
    }
}
