using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Viking.UI;
using Viking.VolumeModel;
using VikingXNAGraphics;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{

    public enum DrawWhichPoly
    {
        PREVPOLY,
        NEXTPOLY
    }

    class RetraceAndReplacePathCommand : PlaceGeometryWithPenCommandBase
    {
        //Variables:

        //If we make a wrong intersection, this will track the index of that wrong intersection
        private int WrongIntersectionPoint;

        //Original Polygons
        GridPolygon OriginalMosaicPolygon;
        GridPolygon OriginalVolumePolygon;
        public GridPolygon OriginalSmoothedVolumePolygon;

        //Our original polygon plus the origin of retrace and replace and the origin point index
        GridPolygon VolumePolygonPlusOrigin;

        public PointIndex OriginIndex;

        public PointIndex? PolyBeingCut;

        //Meshes of the individual cut pieces of the retrace and replace
        PositionColorMeshModel ClockwiseWalkMesh = null;
        PositionColorMeshModel CounterClockwiseWalkMesh = null;

        RetraceCommandAction CutAction = RetraceCommandAction.NONE;
        //Each of the cut pieces in polygon forms
        private GridPolygon CounterClockwiseCutPolygon = null;
        private GridPolygon ClockwiseCutPolygon = null;

        //The output polygons we create
        public GridPolygon OutputMosaicPolygon;
        public GridPolygon OutputVolumePolygon;

        /// <summary>
        /// True if we want to use the opposite polygon as normal
        /// </summary>
        protected bool SwitchSide
        {
            get
            {
                return Control.ModifierKeys.CtrlPressed();
            }
        }

        public bool IsCutComplete
        {
            get
            {
                return PolyBeingCut.HasValue;
            }
        }

        //Is the command ready to finish if we try to?
        private bool IsReadyToComplete
        {
            get
            {
                switch (this.CutAction)
                {
                    case RetraceCommandAction.NONE:
                        return false;
                    case RetraceCommandAction.GROW_EXTERIOR_RING:
                    case RetraceCommandAction.GROW_INTERNAL_RING:
                    case RetraceCommandAction.SHRINK_EXTERIOR_RING:
                    case RetraceCommandAction.SHRINK_INTERNAL_RING:
                    case RetraceCommandAction.CREATE_INTERNAL_RING:
                        return true;
                    default:
                        throw new ArgumentException("Unknown state, cannot determine if the command can complete.");
                }
            }
        }

        //False draws the PrevWalkPolygon, true draws the NextWalkPolygon
        private DrawWhichPoly DrawPoly;

        private bool? _CommandExpandsArea;
        private bool CommandExpandsArea
        {
            get
            {
                if (_CommandExpandsArea.HasValue == false)
                {
                    //Check if the first point placed in the path is inside or outside the polygon.  Starting from the inside we can only draw a line that grows the area, and vice versa
                    _CommandExpandsArea = this.OriginalVolumePolygon.Contains(PenInput.path.Points.First());
                }

                return _CommandExpandsArea.Value;
            }

        } //Set to true if the commands origin will increase the total area of the polygon if the command completes

        //Curve Interpolations Variable
        public override uint NumCurveInterpolations
        {
            get
            {
                return Global.NumClosedCurveInterpolationPoints;
            }
        }

        //Section to Volume Mapper
        public Viking.VolumeModel.IVolumeToSectionTransform mapping;

        //Replace and retrace constructor
        public RetraceAndReplacePathCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, LineWidth, success_callback)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;

            if (mosaic_polygon == null)
                throw new ArgumentException("mosaic_polygon passed to RetraceAndReplaceCommand was null");

            this.OriginalMosaicPolygon = mosaic_polygon;
            this.OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);

            if (OriginalVolumePolygon == null)
                throw new ArgumentException("mosaic_polygon could not be mapped to volume space");

            this.PathView.Color = color.Invert(1.0f);

            OriginalSmoothedVolumePolygon = OriginalVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
        }

        public RetraceAndReplacePathCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        System.Drawing.Color color,
                                        IReadOnlyList<GridVector2> path,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : this(parent, mosaic_polygon, color.ToXNAColor(), LineWidth, success_callback)
        {

        }

        protected override void OnPathLoop(object sender, bool HasLoop)
        {
            //TODO: Create an interior hole in the polygon
            GridPolygon proposed_hole = new GridPolygon(PenInput.SimplifiedFirstLoop.ToArray().EnsureClosedRing());

            GridPolygon original_copy = (GridPolygon)this.OriginalVolumePolygon.Clone();
            try
            {
                original_copy.AddInteriorRing(proposed_hole);

            }
            catch (ArgumentException e)
            {
                //Interior hole was not valid, do nothing?
                return;
            }

            try
            {
                OutputVolumePolygon = original_copy;
                OutputMosaicPolygon = mapping.TryMapShapeVolumeToSection(OutputVolumePolygon).Simplify(this.PenInput.SimplifiedPathToleranceInPixels * Parent.Downsample);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                return;
            }

            this.Execute();


            //return false == GridPolygon.SegmentsIntersect(this.OriginalVolumePolygon, proposed_hole);
            this.Deactivated = true;
            return;
        }

        /// <summary>
        /// If the pen path changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void OnPenPathChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //List<GridVector2> path = PenInput.Path.InflectionPointIndicies().Select(i => PenInput.Path[i]).ToList();
            //Update our view of the pen path
            base.OnPenPathChanged(sender, e);

            if (!this.IsCutComplete && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                //See if the addition to the path finished the cut
                this.CutAction = GetRetraceActionForPath(PenInput.SimplifiedPath, out ClockwiseCutPolygon, out CounterClockwiseCutPolygon);
            }
            else if (this.IsCutComplete && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove || e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            {
                this.CutAction = GetRetraceActionForPath(PenInput.SimplifiedPath, out ClockwiseCutPolygon, out CounterClockwiseCutPolygon);
            }

            if (CutAction == RetraceCommandAction.NONE)
            {
                return;
            }

            //If an expansion, then figure out which is larger, make that the green mesh, and set the smaller poly and mesh to null. Otherwise display the two polygons.
            UpdateViews();


        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            //If the command is in a valid state populate the output poly and call execute. Otherwise deactive the command.
            if (IsReadyToComplete)
            {
                OutputVolumePolygon = GenerateOutputVolumePolygon();

                try
                {
                    OutputMosaicPolygon = mapping.TryMapShapeVolumeToSection(OutputVolumePolygon).Simplify(this.PenInput.SimplifiedPathToleranceInPixels * Parent.Downsample);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Console.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                    return;
                }

                this.Execute();
            }

            base.OnMouseUp(sender, e);
        }

        protected override void OnPenLeaveRange(object sender, PenEventArgs e)
        {
            //If the command is in a valid state populate the output poly and call execute. Otherwise deactive the command.
            if (IsReadyToComplete)
            {
                OutputVolumePolygon = GenerateOutputVolumePolygon();

                try
                {
                    OutputMosaicPolygon = mapping.TryMapShapeVolumeToSection(OutputVolumePolygon).Simplify(this.PenInput.SimplifiedPathToleranceInPixels * Parent.Downsample);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Console.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                    return;
                }

                this.Execute();
            }

            base.OnPenLeaveRange(sender, e);
        }

        private RetraceCommandAction GetRetraceActionForPath(IList<GridVector2> path, out GridPolygon clockwise_poly, out GridPolygon counter_clockwise_poly)
        {
            clockwise_poly = null;
            counter_clockwise_poly = null;
            this.PolyBeingCut = null;

            if (path.Count <= 1)
            {
                return RetraceCommandAction.NONE;
            }

            SortedDictionary<double, PointIndex> intersectedSegments = this.OriginalVolumePolygon.IntersectingSegments(path.ToLineSegments());

            if (intersectedSegments.Count < 2)
            {
                return RetraceCommandAction.NONE;
            }

            PointIndex FirstIntersection = intersectedSegments.First().Value;

            GridPolygon PolyToCut = OriginalVolumePolygon;
            if (FirstIntersection.IsInner)
            {
                PolyToCut = OriginalVolumePolygon.InteriorPolygons[FirstIntersection.iInnerPoly.Value];
            }

            //Condition Check to make sure pen path exists and is valid
            if (path == null || path.Count < 2 || OriginalVolumePolygon.TotalVerticies <= 3)
            {
                return RetraceCommandAction.NONE;
            }

            try
            {
                clockwise_poly = GridPolygon.WalkPolygonCut(PolyToCut, RotationDirection.CLOCKWISE, path);
                clockwise_poly.ExteriorRing = CatmullRomControlPointSimplification.IdentifyControlPoints(clockwise_poly.ExteriorRing, 1.0, true).ToArray();
                counter_clockwise_poly = GridPolygon.WalkPolygonCut(PolyToCut, RotationDirection.COUNTERCLOCKWISE, path);
                counter_clockwise_poly.ExteriorRing = CatmullRomControlPointSimplification.IdentifyControlPoints(counter_clockwise_poly.ExteriorRing, 1.0, true).ToArray();
                this.PolyBeingCut = FirstIntersection;
            }
            catch (ArgumentException e)
            {
                //Thrown when the polygon cannot be cut using the path
                return RetraceCommandAction.NONE;
            }

            if (FirstIntersection.IsInner)
            {
                return this.CommandExpandsArea ? RetraceCommandAction.SHRINK_INTERNAL_RING : RetraceCommandAction.GROW_INTERNAL_RING;
            }
            else
            {
                return this.CommandExpandsArea ? RetraceCommandAction.GROW_EXTERIOR_RING : RetraceCommandAction.SHRINK_EXTERIOR_RING;
            }
        }

        public GridPolygon GenerateOutputVolumePolygon()
        {
            GridPolygon output;

            switch (this.CutAction)
            {
                case RetraceCommandAction.NONE:
                    return null;
                case RetraceCommandAction.GROW_EXTERIOR_RING:
                    return this.CounterClockwiseCutPolygon.Area > this.ClockwiseCutPolygon.Area ? this.CounterClockwiseCutPolygon : this.ClockwiseCutPolygon;
                case RetraceCommandAction.SHRINK_EXTERIOR_RING:
                    return this.SwitchSide ? this.ClockwiseCutPolygon : this.CounterClockwiseCutPolygon;
                case RetraceCommandAction.GROW_INTERNAL_RING:
                    output = (GridPolygon)OriginalVolumePolygon.Clone();
                    output.ReplaceInteriorRing(this.PolyBeingCut.Value.iInnerPoly.Value, this.CounterClockwiseCutPolygon.Area > this.ClockwiseCutPolygon.Area ? this.CounterClockwiseCutPolygon : this.ClockwiseCutPolygon);
                    return output;
                case RetraceCommandAction.SHRINK_INTERNAL_RING:
                    output = (GridPolygon)OriginalVolumePolygon.Clone();
                    output.ReplaceInteriorRing(this.PolyBeingCut.Value.iInnerPoly.Value, this.SwitchSide ? this.ClockwiseCutPolygon : this.CounterClockwiseCutPolygon);
                    return output;
            }

            return null;
        }

        protected override void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                this.UpdateViews();
            }

            base.OnKeyUp(sender, e);
        }

        protected override void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                this.UpdateViews();
            }

            base.OnKeyDown(sender, e);
        }
        /// <summary>
        /// Sets meshes for retrace and replace
        /// </summary>
        /// <returns></returns>
        private void UpdateViews()
        {
            Microsoft.Xna.Framework.Color CCW_Color = SwitchSide ? Microsoft.Xna.Framework.Color.Magenta.ConvertToHSL(0.5f) : Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f);
            Microsoft.Xna.Framework.Color CW_Color = SwitchSide ? Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f) : Microsoft.Xna.Framework.Color.Magenta.ConvertToHSL(0.5f);
            Microsoft.Xna.Framework.Color Grow_Color = Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f);
            switch (this.CutAction)
            {
                case RetraceCommandAction.NONE:
                    CounterClockwiseWalkMesh = null;
                    ClockwiseWalkMesh = null;
                    break;
                case RetraceCommandAction.GROW_EXTERIOR_RING:
                    //NextWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(this.CounterClockwiseCutPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f));
                    CounterClockwiseWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(this.GenerateOutputVolumePolygon(), Grow_Color);
                    ClockwiseWalkMesh = null;
                    break;
                case RetraceCommandAction.SHRINK_EXTERIOR_RING:
                    //NextWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(CounterClockwiseCutPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f));
                    //PrevWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(ClockwiseCutPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Red.ConvertToHSL(0.5f));

                    CounterClockwiseWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(CounterClockwiseCutPolygon, CCW_Color);
                    ClockwiseWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(ClockwiseCutPolygon, CW_Color);
                    break;
                case RetraceCommandAction.GROW_INTERNAL_RING:
                    //NextWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(this.CounterClockwiseCutPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f));
                    CounterClockwiseWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(this.GenerateOutputVolumePolygon(), Grow_Color);
                    ClockwiseWalkMesh = null;
                    break;
                case RetraceCommandAction.SHRINK_INTERNAL_RING:
                    //NextWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(CounterClockwiseCutPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f));
                    //PrevWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(ClockwiseCutPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Red.ConvertToHSL(0.5f));

                    CounterClockwiseWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(CounterClockwiseCutPolygon, CCW_Color);
                    ClockwiseWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(ClockwiseCutPolygon, CW_Color);
                    break;
            }
        }

        protected override void OnPenPathComplete(object sender, GridVector2[] Path)
        {

        }

        protected override void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment)
        {

        }


        /// <summary>
        /// Can the command be completed by clicking this point?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanCommandComplete()
        {
            //Does the path self intersect
            if (PenInput.HasSelfIntersection)
                return false;

            return ShapeIsValid();
        }

        protected override bool ShapeIsValid()
        {
            /*
            if (this.Verticies.Length < 3 || curve_verticies == null || this.curve_verticies.ControlPoints.Length < 3)
                return false;

            try
            {
                return this.curve_verticies.ControlPoints.ToPolygon().STIsValid().IsTrue;
            }
            catch (ArgumentException e)
            {
                return false;
            }
            */

            return true;
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            if (ClockwiseWalkMesh != null || CounterClockwiseWalkMesh != null)
            {
                float originalAlphaLuma = Parent.PolygonOverlayEffect.InputLumaAlphaValue;
                Parent.PolygonOverlayEffect.InputLumaAlphaValue = 0.5f;
                if (CounterClockwiseWalkMesh == null)
                    MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, Parent.PolygonOverlayEffect, meshmodels: new PositionColorMeshModel[] { ClockwiseWalkMesh });
                else if (ClockwiseWalkMesh == null)
                    MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, Parent.PolygonOverlayEffect, meshmodels: new PositionColorMeshModel[] { CounterClockwiseWalkMesh });
                else
                    MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, Parent.PolygonOverlayEffect, meshmodels: new PositionColorMeshModel[] { ClockwiseWalkMesh, CounterClockwiseWalkMesh });

                Parent.PolygonOverlayEffect.InputLumaAlphaValue = originalAlphaLuma;
            }

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}