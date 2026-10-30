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
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Drags a vertex of the polygon the forward owner draws (<c>LocationVolumeGeometry.ControlPolygon</c>) and saves the
    /// result mapped back to mosaic by the inverse owner.
    /// </summary>
    internal class AdjustPolygonVertexCommand : AnnotationCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        private readonly Polygon OriginalVolumePolygon;

        private Polygon OutputVolumePolygon;
        private PositionColorMeshModel polygonView;
        private Polygon? AdjustedPolygon = null; //The polygon we are adjusting.  This can be an interior polygon.
        private bool ControlPointSelected = false;
        private PolygonIndex iOriginalVolumePolyControlPoint;
        private PolygonIndex iAdjustedControlPoint; //The index of the vertex in the exterior ring to adjust. 

        private Color _color;

        /// <summary>
        /// Returns unsmoothed mosaic and volume polygons with the new point
        /// </summary>
        /// <param name="MosaicPolygon"></param>
        /// <param name="VolumePolygon"></param>
        public delegate void OnCommandSuccess(Polygon MosaicPolygon, Polygon VolumePolygon);

        private readonly OnCommandSuccess success_callback;
        private readonly Viking.VolumeModel.IVolumeToSectionTransform mapping;

        public string[] HelpStrings => ["Release Left Mouse Button to place control point"];

        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);

        /// <param name="volume_polygon">The location's control polygon in volume space, from the forward owner.</param>
        public AdjustPolygonVertexCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Polygon volume_polygon,
                                        Microsoft.Xna.Framework.Color color,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            OriginalVolumePolygon = volume_polygon;
            _color = color;

            //this.SmoothedVolumePolygon = OriginalVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
            this.success_callback = success_callback;

        }

        private static async Task<PositionColorMeshModel> CreateView(Polygon poly, Color color, CancellationToken token) => await Task.Run(() => poly.Smooth(Global.NumClosedCurveInterpolationPointsForDisplay).CreateMeshForPolygon2D(color), token);

        protected void PopulateControlPointIndexIfNeeded(Geometry.Vector2 WorldPosition)
        {
            if (ControlPointSelected == false)
            {
                ControlPointSelected = true;
                OriginalVolumePolygon.NearestVertex(WorldPosition, out iOriginalVolumePolyControlPoint);
                AdjustedPolygon = (Polygon)iOriginalVolumePolyControlPoint.Polygon(OriginalVolumePolygon).Clone();

                iAdjustedControlPoint = iOriginalVolumePolyControlPoint.IsInner
                    ? iOriginalVolumePolyControlPoint.ReindexToOuter()
                    : iOriginalVolumePolyControlPoint;
            }
        }

        private CancellationTokenSource? UpdatePositionCancellationTokenSource = null;

        protected virtual async Task UpdatePosition(Geometry.Vector2 PositionDelta)
        {
            AdjustedPolygon[iAdjustedControlPoint] = AdjustedPolygon[iAdjustedControlPoint] + PositionDelta;

            //If we haven't moved a significant distance, don't update the view
            if (PositionDelta.Round(0) == Geometry.Vector2.Zero)
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
            Geometry.Vector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
            PopulateControlPointIndexIfNeeded(NewPosition);

            //Redraw if we are dragging a location
            if (oldMouse != null)
            {
                if (oldMouse.Button.Left())
                {
                    Geometry.Vector2 LastWorldPosition = Parent.ScreenToWorld(oldMouse.X, oldMouse.Y);
                    _ = UpdatePosition(NewPosition - LastWorldPosition);
                }
            }

            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button.Left())
            {
                Geometry.Vector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
                PopulateControlPointIndexIfNeeded(NewPosition);

                if (AdjustedPolygon != null)
                {
                    OutputVolumePolygon = (Polygon)OriginalVolumePolygon.Clone();
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

        /// <summary>
        /// Passes the polygon with the moved vertex to the success callback and ends the command.
        /// When the vertex was released outside the section's mapping, the callback is skipped and the location keeps its shape.
        /// </summary>
        protected override void Execute()
        {
            try
            {
                TryApplyInSection(mapping, OutputVolumePolygon, mosaic_polygon => success_callback(mosaic_polygon, OutputVolumePolygon), nameof(AdjustPolygonVertexCommand), ShowRefusedEdit);
            }
            catch (ArgumentOutOfRangeException)
            {
                Trace.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                return;
            }

            base.Execute();
        }
    }
}
