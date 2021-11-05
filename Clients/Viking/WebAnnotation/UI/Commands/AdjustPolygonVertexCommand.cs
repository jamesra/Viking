using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

        private void CreateView(GridPolygon poly, Color color)
        {
            polygonView = TriangleNetExtensions.CreateMeshForPolygon2D(poly.Smooth(Global.NumClosedCurveInterpolationPoints), color);
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

        protected virtual void UpdatePosition(GridVector2 PositionDelta)
        {
            AdjustedPolygon[iAdjustedControlPoint] = AdjustedPolygon[iAdjustedControlPoint] + PositionDelta;
            /*
                
            GridVector2[] newRing = AdjustedPolygon.ExteriorRing.Clone() as GridVector2[];
            newRing[iAdjustedControlPoint.Value.iVertex] += PositionDelta;
            if (iAdjustedControlPoint.Value.FirstInRing() == 0)
            {
                newRing[AdjustedPolygon.ExteriorRing.Length - 1] = newRing[iAdjustedControlPoint];
            }
            else if (iAdjustedControlPoint == AdjustedPolygon.ExteriorRing.Length - 1)
            {
                newRing[0] = newRing[iAdjustedControlPoint];
            }

            AdjustedPolygon.ExteriorRing = newRing;
            */

            //if(polygonView == null)
            CreateView(AdjustedPolygon, _color);

            /*else
            {
                polygonView.Verticies[iAdjustedControlPoint].Position = newRing[iAdjustedControlPoint].ToXNAVector3();
                

            }*/
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
                    Parent.Invalidate();
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
