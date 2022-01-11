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
    class AdjustPolygonVertexCommand : AnnotationCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        GridPolygon OriginalMosaicPolygon;
        GridPolygon OriginalVolumePolygon;

        private GridPolygon OutputVolumePolygon;

        PositionColorMeshModel polygonView;

        GridPolygon AdjustedPolygon = null; //The polygon we are adjusting.  This can be an interior polygon.
        private bool ControlPointSelected = false;
        private PolygonIndex iOriginalVolumePolyControlPoint; 
        private PolygonIndex iAdjustedControlPoint; //The index of the vertex in the exterior ring to adjust. 

        Color _color;

        /// <summary>
        /// Returns unsmoothed mosaic and volume polygons with the new point
        /// </summary>
        /// <param name="MosaicPolygon"></param>
        /// <param name="VolumePolygon"></param>
        public delegate void OnCommandSuccess(GridPolygon MosaicPolygon, GridPolygon VolumePolygon);
        OnCommandSuccess success_callback;

        Viking.VolumeModel.IVolumeToSectionTransform mapping;

        public string[] HelpStrings
        {
            get
            {
                return new string[] { "Release Left Mouse Button to place control point" };
            }
        }

        public ObservableCollection<string> ObservableHelpStrings
        {
            get
            {
                return new ObservableCollection<string>(this.HelpStrings);
            }
        }

        public AdjustPolygonVertexCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        Microsoft.Xna.Framework.Color color,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            this.OriginalMosaicPolygon = mosaic_polygon;
            this.OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);
            _color = color;

            //this.SmoothedVolumePolygon = OriginalVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
            this.success_callback = success_callback;

        }

        private static async Task<PositionColorMeshModel> CreateView(GridPolygon poly, Color color, CancellationToken token)
        {
            return await Task.Run(() => TriangleNetExtensions.CreateMeshForPolygon2D(poly.Smooth(Global.NumClosedCurveInterpolationPointsForDisplay), color), token);
        }

        protected void PopulateControlPointIndexIfNeeded(GridVector2 WorldPosition)
        {
            if (ControlPointSelected == false)
            {
                ControlPointSelected = true;
                OriginalVolumePolygon.NearestVertex(WorldPosition, out iOriginalVolumePolyControlPoint);
                AdjustedPolygon = (GridPolygon)iOriginalVolumePolyControlPoint.Polygon(OriginalVolumePolygon).Clone();

                this.iAdjustedControlPoint = iOriginalVolumePolyControlPoint.IsInner
                    ? iOriginalVolumePolyControlPoint.ReindexToOuter()
                    : iOriginalVolumePolyControlPoint;
            }
        }

        private CancellationTokenSource UpdatePositionCancellationTokenSource = null;

        protected virtual async Task UpdatePosition(GridVector2 PositionDelta)
        {
            AdjustedPolygon[iAdjustedControlPoint] = AdjustedPolygon[iAdjustedControlPoint] + PositionDelta;

            //If we haven't moved a significant distance, don't update the view
            if (PositionDelta.Round(0) == GridVector2.Zero)
                return;

            var newTokenSource = new CancellationTokenSource();
            var existingToken = Interlocked.Exchange(ref UpdatePositionCancellationTokenSource, newTokenSource);
            if(existingToken != null)
                existingToken.Cancel();

            var result = await CreateView(AdjustedPolygon, _color, newTokenSource.Token);
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
            if (this.oldMouse != null)
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

                if (this.AdjustedPolygon != null)
                {
                    OutputVolumePolygon = (GridPolygon)OriginalVolumePolygon.Clone();
                    OutputVolumePolygon[iOriginalVolumePolyControlPoint] = AdjustedPolygon[iAdjustedControlPoint];
                    this.Execute();
                }
                else
                {
                    this.CommandActive = false;
                }
            }

            base.OnMouseUp(sender, e);
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            if (polygonView != null)
                MeshView<VertexPositionColor>.Draw(graphicsDevice, scene, DeviceEffectsStore<PolygonOverlayEffect>.TryGet(graphicsDevice), meshmodels: new PositionColorMeshModel[] { polygonView });
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

            this.success_callback(mosaic_polygon, OutputVolumePolygon);

            base.Execute();
        } 
    }
}
