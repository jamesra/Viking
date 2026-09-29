using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.Common;
using Viking.UI;
using Viking.VolumeModel;
using VikingXNAGraphics;
using VikingXNAWinForms;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    internal class RetraceAndReplacePathCommand : PlaceGeometryWithPenCommandBase, IHelpStrings, IObservableHelpStrings
    {
        //Original Polygons
        private readonly Polygon OriginalMosaicPolygon;
        private readonly Polygon OriginalVolumePolygon;
        public Polygon OriginalSmoothedVolumePolygon;

        public PolygonIndex OriginIndex;

        public PolygonIndex? PolyBeingCut;

        //Meshes of the individual cut pieces of the retrace and replace
        private PositionColorMeshModel _choiceBaseMesh;
        private PositionColorMeshModel _choiceCapMesh;
        private Task<Geometry.Meshing.PolygonCutFill> _fillTask;
        private Geometry.Meshing.PolygonCutFill _fill;
        private Geometry.Vector2[] _cutPath;
        private Polygon _viewSmall;
        private Task _choiceFallback;
        private RetraceCommandAction _viewAction = RetraceCommandAction.NONE;
        private bool _viewSwitch;
        private bool _viewKeepBoth;
        private RetraceCommandAction CutAction = RetraceCommandAction.NONE;
        //Each of the cut pieces in polygon forms
        private Polygon? CounterClockwiseCutPolygon = null;
        private Polygon? ClockwiseCutPolygon = null;

        //The output polygons we create
        public Polygon OutputMosaicPolygon;
        public Polygon OutputVolumePolygon;

        /// <summary>
        /// The piece that is not written onto the original location.
        /// Set only when Shift is held on a shrink cut, so the caller can save it as a second annotation.
        /// </summary>
        public Polygon SplitOffMosaicPolygon;

        /// <summary>
        /// True when Ctrl is held. Swaps which cut piece replaces the original location.
        /// Composes with <see cref="KeepBothPieces"/>: Ctrl still chooses which piece stays on the original.
        /// </summary>
        protected bool SwitchSide => (Control.ModifierKeys & Keys.Control) == Keys.Control;

        /// <summary>
        /// True when Shift is held on a finished exterior shrink, so both cut pieces are saved.
        /// Shift is the pen modifier that is free during this command; Ctrl already picks the kept side.
        /// </summary>
        protected bool KeepBothPieces =>
            (Control.ModifierKeys & Keys.Shift) == Keys.Shift &&
            CutAction == RetraceCommandAction.SHRINK_EXTERIOR_RING &&
            ClockwiseCutPolygon is not null &&
            CounterClockwiseCutPolygon is not null;

        /// <summary>
        /// Pen help for a retrace cut. Ctrl swaps the kept side. Shift keeps both sides as two annotations.
        /// View keys stay. The freehand control-point lines are not included.
        /// </summary>
        public new string[] HelpStrings =>
        [
            .. DefaultKeyHelpStrings,
            "Ctrl: Keep the other side of the cut",
            "Shift: Keep both sides as two annotations"
        ];

        public new ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);

        public bool IsCutComplete => PolyBeingCut.HasValue;

        //Is the command ready to finish if we try to?
        private bool IsReadyToComplete => CutAction switch
        {
            RetraceCommandAction.NONE => false,
            RetraceCommandAction.GROW_EXTERIOR_RING or RetraceCommandAction.GROW_INTERNAL_RING or RetraceCommandAction.SHRINK_EXTERIOR_RING or RetraceCommandAction.SHRINK_INTERNAL_RING or RetraceCommandAction.CREATE_INTERNAL_RING => true,
            _ => throw new ArgumentException("Unknown state, cannot determine if the command can complete."),
        };

        private bool? _CommandExpandsArea;
        private bool CommandExpandsArea
        {
            get
            {
                if (_CommandExpandsArea.HasValue == false)
                {
                    //Check if the first point placed in the path is inside or outside the polygon.  Starting from the inside we can only draw a line that grows the area, and vice versa
                    _CommandExpandsArea = OriginalVolumePolygon.Covers(PenInput.path.Points.First());
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
                                        Polygon mosaic_polygon,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, LineWidth, success_callback)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;

            if (mosaic_polygon is null)
            {
                throw new ArgumentException("mosaic_polygon passed to RetraceAndReplaceCommand was null");
            }

            OriginalMosaicPolygon = mosaic_polygon;
            OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);

            if (OriginalVolumePolygon is null)
            {
                throw new ArgumentException("mosaic_polygon could not be mapped to volume space");
            }

            PathView.Color = color.Invert(1.0f);

            // The choice fill reuses this triangulation. Smoothing the whole ring here blocked the pen before the first point.
            OriginalSmoothedVolumePolygon = OriginalVolumePolygon;
            _fillTask = PolygonCutFillCache.Begin(OriginalVolumePolygon);
        }

        public RetraceAndReplacePathCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Polygon mosaic_polygon,
                                        System.Drawing.Color color,
                                        IReadOnlyList<Geometry.Vector2> path,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : this(parent, mosaic_polygon, color.ToXNAColor(), LineWidth, success_callback)
        {

        }

        protected override void OnPathLoop(object sender, bool HasLoop)
        {
            //TODO: Create an interior hole in the polygon
            Polygon proposed_hole = new(PenInput.SimplifiedFirstLoop.ToArray().EnsureClosedRing());

            Polygon original_copy = (Polygon)OriginalVolumePolygon.Clone();
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


            //return false == Polygon.SegmentsIntersect(this.OriginalVolumePolygon, proposed_hole);
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
            //List<Geometry.Vector2> path = PenInput.Path.InflectionPointIndicies().Select(i => PenInput.Path[i]).ToList();
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
                PathView.Style = LineStyle.Tubular;
                return;
            }

            //If an expansion, then figure out which is larger, make that the green mesh, and set the smaller poly and mesh to null. Otherwise display the two polygons.
            UpdateViews();


        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (IsReadyToComplete)
                FinishCut();

            base.OnMouseUp(sender, e);
        }

        protected override void OnPenLeaveRange(object sender, PenEventArgs e)
        {
            if (IsReadyToComplete)
                FinishCut();

            base.OnPenLeaveRange(sender, e);
        }

        /// <summary>
        /// Maps the kept piece, and the other piece when <see cref="KeepBothPieces"/> is set, then completes.
        /// Called from mouse-up and pen-up on a finished cut.
        /// </summary>
        private void FinishCut()
        {
            OutputVolumePolygon = GenerateOutputVolumePolygon();
            Polygon discardedVolume = KeepBothPieces ? DiscardedVolumePolygon() : null;

            try
            {
                double tolerance = PenInput.SimplifiedPathToleranceInPixels * Parent.Downsample;
                OutputMosaicPolygon = mapping.TryMapShapeVolumeToSection(OutputVolumePolygon).Simplify(tolerance);
                SplitOffMosaicPolygon = discardedVolume is null
                    ? null
                    : mapping.TryMapShapeVolumeToSection(discardedVolume).Simplify(tolerance);
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                return;
            }

            Execute();
        }

        /// <summary>
        /// Writes the kept piece onto <paramref name="location"/>. When Shift was held, also creates
        /// an unlinked location on the same structure and section for the other piece.
        /// Location links cannot join two polygons on one section, so the sibling is not linked.
        /// Called by the pen retrace success callbacks before the location save.
        /// </summary>
        public void ApplyCut(LocationObj location)
        {
            location.SetShapeFromGeometryInSection(mapping, OutputMosaicPolygon.ToSqlGeometry());
            if (SplitOffMosaicPolygon is null)
                return;

            LocationObj sibling = new(location.Parent, location.Section, location.TypeCode);
            if (location.Width.HasValue)
                sibling.Width = location.Width;

            sibling.SetShapeFromGeometryInSection(mapping, SplitOffMosaicPolygon.ToSqlGeometry());
            Store.Locations.Create(sibling);
        }

        private RetraceCommandAction GetRetraceActionForPath(IList<Geometry.Vector2> path, out Polygon clockwise_poly, out Polygon counter_clockwise_poly)
        {
            clockwise_poly = null;
            counter_clockwise_poly = null;
            PolyBeingCut = null;

            if (path.Count <= 1)
            {
                return RetraceCommandAction.NONE;
            }

            SortedDictionary<double, PolygonIndex> intersectedSegments = OriginalVolumePolygon.IntersectingSegmentIndices(path.ToLineSegments());

            if (intersectedSegments.Count < 2)
            {
                return RetraceCommandAction.NONE;
            }

            PolygonIndex FirstIntersection = intersectedSegments.First().Value;

            Polygon PolyToCut = OriginalVolumePolygon;
            if (FirstIntersection.IsInner)
            {
                PolyToCut = OriginalVolumePolygon.InteriorPolygons[FirstIntersection.InnerShapeIndex.Value];
            }

            //Condition Check to make sure pen path exists and is valid
            if (path is null || path.Count < 2 || OriginalVolumePolygon.TotalVertices <= 3)
            {
                return RetraceCommandAction.NONE;
            }

            try
            {
                // Walk splices the pen path into the original ring. A chord reduction drops samples
                // that do not change the outline, without a Catmull-Rom refit of the far side.
                clockwise_poly = Polygon.WalkPolygonCut(PolyToCut, RotationDirection.Clockwise, path)
                    .ReduceExteriorRing(1.0);
                counter_clockwise_poly = Polygon.WalkPolygonCut(PolyToCut, RotationDirection.Counterclockwise, path)
                    .ReduceExteriorRing(1.0);
                _cutPath = [.. path];
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

        public Polygon? GenerateOutputVolumePolygon()
        {
            Polygon output;

            switch (CutAction)
            {
                case RetraceCommandAction.NONE:
                    return null;
                case RetraceCommandAction.GROW_EXTERIOR_RING:
                    return CounterClockwiseCutPolygon.Area > ClockwiseCutPolygon.Area ? CounterClockwiseCutPolygon : ClockwiseCutPolygon;
                case RetraceCommandAction.SHRINK_EXTERIOR_RING:
                    return SwitchSide ? ClockwiseCutPolygon : CounterClockwiseCutPolygon;
                case RetraceCommandAction.GROW_INTERNAL_RING:
                    output = (Polygon)OriginalVolumePolygon.Clone();
                    output.ReplaceInteriorRing(PolyBeingCut.Value.InnerShapeIndex.Value, CounterClockwiseCutPolygon.Area > ClockwiseCutPolygon.Area ? CounterClockwiseCutPolygon : ClockwiseCutPolygon);
                    return output;
                case RetraceCommandAction.SHRINK_INTERNAL_RING:
                    output = (Polygon)OriginalVolumePolygon.Clone();
                    output.ReplaceInteriorRing(PolyBeingCut.Value.InnerShapeIndex.Value, SwitchSide ? ClockwiseCutPolygon : CounterClockwiseCutPolygon);
                    return output;
            }

            return null;
        }

        /// <summary>
        /// The exterior piece <see cref="GenerateOutputVolumePolygon"/> does not keep.
        /// Null unless this is a shrink cut with both pieces.
        /// </summary>
        private Polygon DiscardedVolumePolygon()
        {
            if (CutAction != RetraceCommandAction.SHRINK_EXTERIOR_RING)
                return null;

            return SwitchSide ? CounterClockwiseCutPolygon : ClockwiseCutPolygon;
        }

        protected override void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Control || e.Shift)
                UpdateViews();

            base.OnKeyUp(sender, e);
        }

        protected override void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control || e.Shift)
                UpdateViews();

            base.OnKeyDown(sender, e);
        }
        /// <summary>
        /// Sets meshes for retrace and replace
        /// </summary>
        /// <returns></returns>
        private void UpdateViews()
        {
            PathView.Style = KeepBothPieces ? LineStyle.Dashed : LineStyle.Tubular;

            Microsoft.Xna.Framework.Color kept = Microsoft.Xna.Framework.Color.Green.ConvertToHCL(0.5f);
            Microsoft.Xna.Framework.Color dropped = Microsoft.Xna.Framework.Color.Magenta.ConvertToHCL(0.5f);
            Microsoft.Xna.Framework.Color CCW_Color = KeepBothPieces
                ? kept
                : SwitchSide ? dropped : kept;
            Microsoft.Xna.Framework.Color CW_Color = KeepBothPieces
                ? kept
                : SwitchSide ? kept : dropped;
            Microsoft.Xna.Framework.Color Grow_Color = Microsoft.Xna.Framework.Color.Green.ConvertToHCL(0.5f);

            if (CutAction == RetraceCommandAction.NONE || ClockwiseCutPolygon is null || CounterClockwiseCutPolygon is null)
            {
                _choiceBaseMesh = null;
                _choiceCapMesh = null;
                _viewSmall = null;
                _viewAction = RetraceCommandAction.NONE;
                return;
            }

            bool ccwSmaller = CounterClockwiseCutPolygon.Area <= ClockwiseCutPolygon.Area;
            Polygon small = ccwSmaller ? CounterClockwiseCutPolygon : ClockwiseCutPolygon;
            Microsoft.Xna.Framework.Color smallColor = ccwSmaller ? CCW_Color : CW_Color;
            Microsoft.Xna.Framework.Color largeColor = ccwSmaller ? CW_Color : CCW_Color;
            bool grow = CutAction is RetraceCommandAction.GROW_EXTERIOR_RING or RetraceCommandAction.GROW_INTERNAL_RING;
            if (grow)
            {
                smallColor = Grow_Color;
                largeColor = Grow_Color;
            }

            bool sameCut = ReferenceEquals(_viewSmall, small) && _viewAction == CutAction;
            if (sameCut && _choiceBaseMesh is not null)
            {
                if (_viewSwitch != SwitchSide || _viewKeepBoth != KeepBothPieces)
                {
                    _choiceBaseMesh.Color = largeColor;
                    if (_choiceCapMesh is not null)
                        _choiceCapMesh.Color = smallColor;
                    _viewSwitch = SwitchSide;
                    _viewKeepBoth = KeepBothPieces;
                }

                return;
            }

            if (!sameCut)
            {
                _choiceBaseMesh = null;
                _choiceCapMesh = null;
                _choiceFallback = null;
            }

            _viewSmall = small;
            _viewAction = CutAction;
            _viewSwitch = SwitchSide;
            _viewKeepBoth = KeepBothPieces;
            TryBuildChoiceMeshes(small, largeColor, smallColor, grow);
        }

        /// <summary>
        /// Fills the choice from the cached cell triangulation plus a mesh of the local piece.
        /// Leaves the meshes empty while the cache is still running so the pen path stays responsive.
        /// An interior-ring cut has no triangles in that cache, so it meshes the two pieces without a quality refinement.
        /// </summary>
        private void TryBuildChoiceMeshes(Polygon small, Microsoft.Xna.Framework.Color largeColor, Microsoft.Xna.Framework.Color smallColor, bool grow)
        {
            bool interior = CutAction is RetraceCommandAction.GROW_INTERNAL_RING or RetraceCommandAction.SHRINK_INTERNAL_RING;
            if (!interior && _fill is null && _fillTask is { Status: TaskStatus.RanToCompletion })
                _fill = _fillTask.Result;

            if (!interior && _fill is not null && _cutPath is { Length: >= 2 })
            {
                int[] baseIndices = grow
                    ? _fill.TriangleIndices as int[] ?? [.. _fill.TriangleIndices]
                    : _fill.IndicesExcludingOrdinals(_fill.CapTriangleOrdinals(_cutPath, small.ExteriorRing));
                _choiceBaseMesh = PolygonCutMeshBuilder.FromIndices(_fill, baseIndices, largeColor);
                _choiceCapMesh = PolygonCutMeshBuilder.FromPatch(small, smallColor);
                return;
            }

            if ((interior || _fillTask is { IsFaulted: true }) && _choiceFallback is null)
            {
                // The cached cell has no triangles inside a hole. Mesh off the pen thread, without a Steiner budget.
                Polygon largePiece = grow
                    ? GenerateOutputVolumePolygon()
                    : (CounterClockwiseCutPolygon.Area > ClockwiseCutPolygon.Area ? CounterClockwiseCutPolygon : ClockwiseCutPolygon);
                Polygon capPiece = grow ? null : small;
                _choiceFallback = Task.Run(() =>
                {
                    PositionColorMeshModel baseMesh = PolygonCutMeshBuilder.FromPatch(largePiece, largeColor);
                    PositionColorMeshModel capMesh = PolygonCutMeshBuilder.FromPatch(capPiece, smallColor);
                    _choiceBaseMesh = baseMesh;
                    _choiceCapMesh = capMesh;
                });
            }
        }

        protected override void OnPenPathComplete(object sender, Geometry.Vector2[] Path)
        {

        }

        protected override void OnPenProposedNextSegmentChanged(object sender, LineSegment? segment)
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

        protected override bool ShapeIsValid() =>
            /*
if (this.Verticies.Length < 3 || curve_verticies is null || this.curve_verticies.ControlPoints.Length < 3)
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

            true;

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            if (CutAction != RetraceCommandAction.NONE && _choiceBaseMesh is null)
                UpdateViews();

            if (_choiceBaseMesh is not null || _choiceCapMesh is not null)
            {
                float originalAlphaLuma = Parent.PolygonOverlayEffect.InputLumaAlphaValue;
                Parent.PolygonOverlayEffect.InputLumaAlphaValue = 0.5f;
                PositionColorMeshModel[] meshes = _choiceCapMesh is null
                    ? [_choiceBaseMesh]
                    : _choiceBaseMesh is null
                        ? [_choiceCapMesh]
                        : [_choiceBaseMesh, _choiceCapMesh];
                MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, Parent.PolygonOverlayEffect, meshmodels: meshes);
                Parent.PolygonOverlayEffect.InputLumaAlphaValue = originalAlphaLuma;
            }

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}
