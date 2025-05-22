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

    internal class RetraceAndReplacePathCommand : PlaceGeometryWithPenCommandBase
    {
        //Variables:

        //If we make a wrong intersection, this will track the index of that wrong intersection
        private readonly int WrongIntersectionPoint;

        //Original Polygons
        private readonly GridPolygon OriginalMosaicPolygon;
        private readonly GridPolygon OriginalVolumePolygon;
        public GridPolygon OriginalSmoothedVolumePolygon;

        //Our original polygon plus the origin of retrace and replace and the origin point index
        private readonly GridPolygon VolumePolygonPlusOrigin;

        public PolygonIndex OriginIndex;

        public PolygonIndex? PolyBeingCut;

        //Meshes of the individual cut pieces of the retrace and replace
        private PositionColorMeshModel ClockwiseWalkMesh = null;
        private PositionColorMeshModel CounterClockwiseWalkMesh = null;
        private RetraceCommandAction CutAction = RetraceCommandAction.NONE;
        //Each of the cut pieces in polygon forms
        private GridPolygon CounterClockwiseCutPolygon = null;
        private GridPolygon ClockwiseCutPolygon = null;

        //The output polygons we create
        public GridPolygon OutputMosaicPolygon;
        public GridPolygon OutputVolumePolygon;

        /// <summary>
        /// True if we want to use the opposite polygon as normal
        /// </summary>
        protected bool SwitchSide => Control.ModifierKeys.CtrlPressed();

        public bool IsCutComplete => PolyBeingCut.HasValue;

        //Is the command ready to finish if we try to?
        private bool IsReadyToComplete
        {
            get
            {
                switch (CutAction)
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
        private readonly DrawWhichPoly DrawPoly;

        private bool? _CommandExpandsArea;
        private bool CommandExpandsArea
        {
            get
            {
                if (_CommandExpandsArea.HasValue == false)
                {
                    //Check if the first point placed in the path is inside or outside the polygon.  Starting from the inside we can only draw a line that grows the area, and vice versa
                    _CommandExpandsArea = OriginalVolumePolygon.Contains(PenInput.path.Points.First());
                }

                return _CommandExpandsArea.Value;
            }

        } //Set to true if the commands origin will increase the total area of the polygon if the command completes

        //Curve Interpolations Variable
        public override uint NumCurveInterpolations => Global.NumClosedCurveInterpolationPoints;

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
            {
                throw new ArgumentException("mosaic_polygon passed to RetraceAndReplaceCommand was null");
            }

            OriginalMosaicPolygon = mosaic_polygon;
            OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);

            if (OriginalVolumePolygon == null)
            {
                throw new ArgumentException("mosaic_polygon could not be mapped to volume space");
            }

            PathView.Color = color.Invert(1.0f);

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

            GridPolygon original_copy = (GridPolygon)OriginalVolumePolygon.Clone();
            try
            {
                original_copy.AddInteriorRing(proposed_hole);

            }
            catch (ArgumentException)
            {
                //Interior hole was not valid, do nothing?
                return;
            }

            try
            {
                OutputVolumePolygon = original_copy;
                OutputMosaicPolygon = mapping.TryMapShapeVolumeToSection(OutputVolumePolygon).Simplify(PenInput.SimplifiedPathToleranceInPixels * Parent.Downsample);
            }
            catch (ArgumentException)
            {
                Console.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                return;
            }

            Execute();


            //return false == GridPolygon.SegmentsIntersect(this.OriginalVolumePolygon, proposed_hole);
            Deactivated = true;
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

            if (!IsCutComplete && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                //See if the addition to the path finished the cut
                CutAction = GetRetraceActionForPath(PenInput.SimplifiedPath, out ClockwiseCutPolygon, out CounterClockwiseCutPolygon);
            }
            else if (IsCutComplete && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove || e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            {
                CutAction = GetRetraceActionForPath(PenInput.SimplifiedPath, out ClockwiseCutPolygon, out CounterClockwiseCutPolygon);
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
                    OutputMosaicPolygon = mapping.TryMapShapeVolumeToSection(OutputVolumePolygon).Simplify(PenInput.SimplifiedPathToleranceInPixels * Parent.Downsample);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Console.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                    return;
                }

                Execute();
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
                    OutputMosaicPolygon = mapping.TryMapShapeVolumeToSection(OutputVolumePolygon).Simplify(PenInput.SimplifiedPathToleranceInPixels * Parent.Downsample);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Console.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                    return;
                }

                Execute();
            }

            base.OnPenLeaveRange(sender, e);
        }

        private RetraceCommandAction GetRetraceActionForPath(IList<GridVector2> path, out GridPolygon clockwise_poly, out GridPolygon counter_clockwise_poly)
        {
            clockwise_poly = null;
            counter_clockwise_poly = null;
            PolyBeingCut = null;

            if (path.Count <= 1)
            {
                return RetraceCommandAction.NONE;
            }

            SortedDictionary<double, PolygonIndex> intersectedSegments = OriginalVolumePolygon.IntersectingSegments(path.ToLineSegments());

            if (intersectedSegments.Count < 2)
            {
                return RetraceCommandAction.NONE;
            }

            PolygonIndex FirstIntersection = intersectedSegments.First().Value;

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
                PolyBeingCut = FirstIntersection;
            }
            catch (ArgumentException)
            {
                //Thrown when the polygon cannot be cut using the path
                return RetraceCommandAction.NONE;
            }

            if (FirstIntersection.IsInner)
            {
                return CommandExpandsArea ? RetraceCommandAction.SHRINK_INTERNAL_RING : RetraceCommandAction.GROW_INTERNAL_RING;
            }
            else
            {
                return CommandExpandsArea ? RetraceCommandAction.GROW_EXTERIOR_RING : RetraceCommandAction.SHRINK_EXTERIOR_RING;
            }
        }

        public GridPolygon GenerateOutputVolumePolygon()
        {
            GridPolygon output;

            switch (CutAction)
            {
                case RetraceCommandAction.NONE:
                    return null;
                case RetraceCommandAction.GROW_EXTERIOR_RING:
                    return CounterClockwiseCutPolygon.Area > ClockwiseCutPolygon.Area ? CounterClockwiseCutPolygon : ClockwiseCutPolygon;
                case RetraceCommandAction.SHRINK_EXTERIOR_RING:
                    return SwitchSide ? ClockwiseCutPolygon : CounterClockwiseCutPolygon;
                case RetraceCommandAction.GROW_INTERNAL_RING:
                    output = (GridPolygon)OriginalVolumePolygon.Clone();
                    output.ReplaceInteriorRing(PolyBeingCut.Value.iInnerPoly.Value, CounterClockwiseCutPolygon.Area > ClockwiseCutPolygon.Area ? CounterClockwiseCutPolygon : ClockwiseCutPolygon);
                    return output;
                case RetraceCommandAction.SHRINK_INTERNAL_RING:
                    output = (GridPolygon)OriginalVolumePolygon.Clone();
                    output.ReplaceInteriorRing(PolyBeingCut.Value.iInnerPoly.Value, SwitchSide ? ClockwiseCutPolygon : CounterClockwiseCutPolygon);
                    return output;
            }

            return null;
        }

        protected override void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                UpdateViews();
            }

            base.OnKeyUp(sender, e);
        }

        protected override void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                UpdateViews();
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
            switch (CutAction)
            {
                case RetraceCommandAction.NONE:
                    CounterClockwiseWalkMesh = null;
                    ClockwiseWalkMesh = null;
                    break;
                case RetraceCommandAction.GROW_EXTERIOR_RING:
                    //NextWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(this.CounterClockwiseCutPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f));
                    CounterClockwiseWalkMesh = GenerateOutputVolumePolygon().CreateMeshForPolygon2D(Grow_Color);
                    ClockwiseWalkMesh = null;
                    break;
                case RetraceCommandAction.SHRINK_EXTERIOR_RING:
                    //NextWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(CounterClockwiseCutPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f));
                    //PrevWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(ClockwiseCutPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Red.ConvertToHSL(0.5f));

                    CounterClockwiseWalkMesh = CounterClockwiseCutPolygon.CreateMeshForPolygon2D(CCW_Color);
                    ClockwiseWalkMesh = ClockwiseCutPolygon.CreateMeshForPolygon2D(CW_Color);
                    break;
                case RetraceCommandAction.GROW_INTERNAL_RING:
                    //NextWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(this.CounterClockwiseCutPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f));
                    CounterClockwiseWalkMesh = GenerateOutputVolumePolygon().CreateMeshForPolygon2D(Grow_Color);
                    ClockwiseWalkMesh = null;
                    break;
                case RetraceCommandAction.SHRINK_INTERNAL_RING:
                    //NextWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(CounterClockwiseCutPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f));
                    //PrevWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(ClockwiseCutPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Red.ConvertToHSL(0.5f));

                    CounterClockwiseWalkMesh = CounterClockwiseCutPolygon.CreateMeshForPolygon2D(CCW_Color);
                    ClockwiseWalkMesh = ClockwiseCutPolygon.CreateMeshForPolygon2D(CW_Color);
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
            {
                return false;
            }

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
                {
                    MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, Parent.PolygonOverlayEffect, meshmodels: new PositionColorMeshModel[] { ClockwiseWalkMesh });
                }
                else if (ClockwiseWalkMesh == null)
                {
                    MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, Parent.PolygonOverlayEffect, meshmodels: new PositionColorMeshModel[] { CounterClockwiseWalkMesh });
                }
                else
                {
                    MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, Parent.PolygonOverlayEffect, meshmodels: new PositionColorMeshModel[] { ClockwiseWalkMesh, CounterClockwiseWalkMesh });
                }

                Parent.PolygonOverlayEffect.InputLumaAlphaValue = originalAlphaLuma;
            }

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}