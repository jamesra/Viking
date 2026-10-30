using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.Common;
using Viking.Input;
using Viking.UI;
using Viking.VolumeModel;
using VikingXNAGraphics;
using VikingXNAWinForms;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

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

        // Temporary fill: the visible part of each cut piece, meshed while the full-polygon triangulation is still running.
        // Display only. The cut and the saved shape always use the full polygon.
        private sealed record TemporaryFillRequest(Func<Polygon> LargePiece, Polygon CapPiece, Microsoft.Xna.Framework.Color LargeColor, Microsoft.Xna.Framework.Color CapColor, Geometry.Rectangle ClipArea);
        private PositionColorMeshModel[] _tempBaseMeshes;
        private PositionColorMeshModel[] _tempCapMeshes;
        private Polygon _tempFor;
        private TemporaryFillRequest _tempLatest;
        private int _tempRunning;
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
        protected bool SwitchSide => ModifierKeysConverter.FromWinFormsKeys((int)Control.ModifierKeys).CtrlPressed();

        /// <summary>
        /// True when Shift is held on a finished exterior shrink, so both cut pieces are saved.
        /// Shift is the pen modifier that is free during this command; Ctrl already picks the kept side.
        /// </summary>
        protected bool KeepBothPieces =>
            ModifierKeysConverter.FromWinFormsKeys((int)Control.ModifierKeys).ShiftPressed() &&
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
            OriginalVolumePolygon = LocationVolumeGeometry.TryMapPolygon(mosaic_polygon, mapping);

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

                // The stored outline keeps its own points; only the new hole is mapped to the section
                Polygon mosaicHole = LocationVolumeGeometry.TryMapPolygonToMosaic(proposed_hole, mapping);
                Polygon? mosaic = null;
                if (mosaicHole is not null)
                {
                    mosaic = (Polygon)OriginalMosaicPolygon.Clone();
                    mosaic.AddInteriorRing(mosaicHole);
                }

                OutputMosaicPolygon = mosaic;
            }
            catch (ArgumentException)
            {
                Console.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                return;
            }

            if (OutputMosaicPolygon is null)
            {
                System.Diagnostics.Trace.WriteLine("RetraceAndReplacePathCommand: polygon with the new hole lies partly outside the section mapping; not applied.", "Command");
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
            //List<Geometry.Vector2> path = PenInput.Path.InflectionPointIndices().Select(i => PenInput.Path[i]).ToList();
            //Update our view of the pen path
            base.OnPenPathChanged(sender, e);

            if (!IsCutComplete && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                //See if the addition to the path finished the cut
                using (WebAnnotation.View.PolygonViewTimingLog.Measure("retrace-cut", $"verts={OriginalVolumePolygon.TotalVertices}"))
                {
                    CutAction = GetRetraceActionForPath(PenInput.SimplifiedPath, out ClockwiseCutPolygon, out CounterClockwiseCutPolygon);
                }
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
        /// When either piece has a vertex outside the section's mapping, nothing is applied and the command stays active.
        /// </summary>
        private void FinishCut()
        {
            using WebAnnotation.View.PolygonViewTimingLog.PenStepTimer timer = WebAnnotation.View.PolygonViewTimingLog.Measure("finish-cut", $"verts={OriginalVolumePolygon.TotalVertices}");
            Polygon keptPiece = ChosenPiece();
            Polygon discardedPiece = KeepBothPieces ? OtherPiece(keptPiece) : null;
            Polygon discardedVolume = KeepBothPieces ? DiscardedVolumePolygon() : null;

            try
            {
                OutputVolumePolygon = GenerateOutputVolumePolygon();
                if (!TrySpliceOutput(keptPiece, discardedPiece))
                {
                    // The stroke could not be spliced into the stored points; save the walked pieces as they are
                    OutputMosaicPolygon = LocationVolumeGeometry.TryMapPolygonToMosaic(OutputVolumePolygon, mapping);
                    SplitOffMosaicPolygon = discardedVolume is null ? null : LocationVolumeGeometry.TryMapPolygonToMosaic(discardedVolume, mapping);
                }
            }
            catch (ArgumentException e)
            {
                // Out of the mapping, or a new hole that crosses the outline or another hole: nothing is applied
                System.Diagnostics.Trace.WriteLine($"RetraceAndReplacePathCommand: cut not applied: {e.Message}", "Command");
                return;
            }

            // A missing split-off piece would silently drop the part the user asked to keep, so it aborts the cut too.
            if (OutputMosaicPolygon is null || (discardedVolume is not null && SplitOffMosaicPolygon is null))
            {
                System.Diagnostics.Trace.WriteLine("RetraceAndReplacePathCommand: cut lies partly outside the section mapping; not applied.", "Command");
                return;
            }

            Execute();
        }

        /// <summary>
        /// Sets the outputs from the stored control points: the kept piece, and the split-off piece when one is given, keep
        /// the location's points on their side exactly as stored and add only the pen stretch (<see cref="ControlPointCut"/>).
        /// Only the new stretch is fitted; re-simplifying the whole outline moved the parts the pen never touched.
        /// </summary>
        /// <param name="keptPiece">The walked piece (<see cref="ClockwiseCutPolygon"/> or <see cref="CounterClockwiseCutPolygon"/>) that is kept.</param>
        /// <param name="splitOffPiece">The other walked piece when both are kept, else null.</param>
        /// <returns>False when the stroke cannot be spliced; the outputs are then unchanged.</returns>
        private bool TrySpliceOutput(Polygon keptPiece, Polygon splitOffPiece)
        {
            if (!PolyBeingCut.HasValue || keptPiece is null ||
                !ControlPointCut.TrySplice(OriginalMosaicPolygon, mapping, PolyBeingCut.Value, PenInput.Points, PenInput.SimplifiedPathTolerance,
                                           out ControlPointCut.Piece afterFirst, out ControlPointCut.Piece afterSecond))
                return false;

            ControlPointCut.PairWithWalk(afterFirst, afterSecond, ClockwiseCutPolygon, CounterClockwiseCutPolygon, out ControlPointCut.Piece clockwise, out ControlPointCut.Piece counterClockwise);
            ControlPointCut.Piece Spliced(Polygon walked) => ReferenceEquals(walked, ClockwiseCutPolygon) ? clockwise : ReferenceEquals(walked, CounterClockwiseCutPolygon) ? counterClockwise : null;

            ControlPointCut.Piece kept = Spliced(keptPiece);
            ControlPointCut.Piece splitOff = splitOffPiece is null ? null : Spliced(splitOffPiece);
            if (kept is null || (splitOffPiece is not null && splitOff is null))
                return false;

            if (PolyBeingCut.Value.IsInner)
            {
                int hole = PolyBeingCut.Value.InnerShapeIndex.Value;
                Polygon volume = (Polygon)OriginalVolumePolygon.Clone();
                Polygon mosaic = (Polygon)OriginalMosaicPolygon.Clone();
                try
                {
                    volume.ReplaceInteriorRing(hole, kept.Volume);
                    mosaic.ReplaceInteriorRing(hole, kept.Mosaic);
                }
                catch (ArgumentException)
                {
                    // The spliced hole crosses the outline or another hole; the walked one is tried instead
                    return false;
                }

                OutputVolumePolygon = volume;
                OutputMosaicPolygon = mosaic;
                SplitOffMosaicPolygon = null;
                return true;
            }

            OutputVolumePolygon = kept.Volume;
            OutputMosaicPolygon = kept.Mosaic;
            SplitOffMosaicPolygon = splitOff?.Mosaic;
            return true;
        }

        /// <summary>
        /// Writes the kept piece onto <paramref name="location"/>. When Shift was held, also creates
        /// an unlinked location on the same structure and section for the other piece.
        /// Location links cannot join two polygons on one section, so the sibling is not linked.
        /// Called by the pen retrace success callbacks before the location save.
        /// </summary>
        /// <returns>
        /// An action that records the edit for Ctrl+Z; the caller invokes it once the save succeeded.
        /// Null when a sibling was created, because restoring only the original's shape would leave the sibling overlapping it.
        /// </returns>
        public Action ApplyCut(LocationObj location)
        {
            Microsoft.SqlServer.Types.SqlGeometry oldShape = location.MosaicShape.ToSqlGeometry();
            LocationType oldType = location.TypeCode;
            WebAnnotation.View.PolygonViewTimingLog.MarkBoundaryChange(location.ID, "Retrace");
            using (WebAnnotation.View.PolygonViewTimingLog.Measure("apply-cut"))
            {
                location.SetShapeFromGeometryInSection(mapping, OutputMosaicPolygon.ToSqlGeometry());
            }

            if (SplitOffMosaicPolygon is null)
                return () => UndoManager.PushLocationShape("Change Boundary", location, mapping, oldShape, oldType);

            LocationObj sibling = new(location.Parent, (int)location.Section, location.TypeCode);
            if (location.Width.HasValue)
                sibling.Width = location.Width;

            sibling.SetShapeFromGeometryInSection(mapping, SplitOffMosaicPolygon.ToSqlGeometry());
            _ = CreateSplitOffLocationAsync(sibling);
            return null;
        }

        /// <summary>
        /// Creates the split-off sibling on the server without blocking the pen callback; a failure is reported, not thrown.
        /// </summary>
        private static async Task CreateSplitOffLocationAsync(LocationObj sibling)
        {
            try
            {
                await Store.Locations.Create(sibling);
            }
            catch (Exception e)
            {
                ExceptionReport.Show("Could not create the split-off annotation.", e);
            }
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
            Polygon piece = ChosenPiece();
            if (piece is null)
                return null;

            if (CutAction is RetraceCommandAction.GROW_INTERNAL_RING or RetraceCommandAction.SHRINK_INTERNAL_RING)
            {
                Polygon output = (Polygon)OriginalVolumePolygon.Clone();
                output.ReplaceInteriorRing(PolyBeingCut.Value.InnerShapeIndex.Value, piece);
                return output;
            }

            return piece;
        }

        /// <summary>
        /// The walked piece that is kept: the larger one for a growth, the counterclockwise one for an outline shrink unless Ctrl
        /// swaps it. For a stroke that splits a hole it is the part that stays a hole, so the other part is filled: the smaller
        /// part is filled unless Ctrl fills the larger one (<see cref="HolePartKept"/>), as in the pen choices.
        /// Null when there is no cut.
        /// </summary>
        private Polygon ChosenPiece() => CutAction switch
        {
            RetraceCommandAction.GROW_EXTERIOR_RING or RetraceCommandAction.GROW_INTERNAL_RING =>
                CounterClockwiseCutPolygon.Area > ClockwiseCutPolygon.Area ? CounterClockwiseCutPolygon : ClockwiseCutPolygon,
            RetraceCommandAction.SHRINK_EXTERIOR_RING =>
                SwitchSide ? ClockwiseCutPolygon : CounterClockwiseCutPolygon,
            RetraceCommandAction.SHRINK_INTERNAL_RING =>
                HolePartKept(ClockwiseCutPolygon, CounterClockwiseCutPolygon, SwitchSide),
            _ => null,
        };

        /// <summary>
        /// For a stroke that splits a hole: the part that stays the hole. By default the larger part stays, so the smaller part
        /// is filled; <paramref name="fillLarger"/> (Ctrl) keeps the smaller part as the hole and fills the larger.
        /// Static so the rule can be tested without a viewer.
        /// </summary>
        internal static Polygon HolePartKept(Polygon clockwise, Polygon counterClockwise, bool fillLarger)
        {
            bool clockwiseIsSmaller = clockwise.Area <= counterClockwise.Area;
            Polygon smaller = clockwiseIsSmaller ? clockwise : counterClockwise;
            Polygon larger = clockwiseIsSmaller ? counterClockwise : clockwise;
            return fillLarger ? smaller : larger;
        }

        private Polygon OtherPiece(Polygon piece) =>
            piece is null ? null : ReferenceEquals(piece, ClockwiseCutPolygon) ? CounterClockwiseCutPolygon : ClockwiseCutPolygon;

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

            if (CutAction == RetraceCommandAction.SHRINK_INTERNAL_RING)
            {
                // Green is the part of the hole that will be filled, magenta the part that stays a hole.
                bool counterClockwiseStaysHole = ReferenceEquals(ChosenPiece(), CounterClockwiseCutPolygon);
                CCW_Color = counterClockwiseStaysHole ? dropped : kept;
                CW_Color = counterClockwiseStaysHole ? kept : dropped;
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
            if (sameCut && (_choiceBaseMesh is not null || _choiceCapMesh is not null))
            {
                // The full-polygon meshes are ready, so the temporary fill is no longer needed.
                _tempBaseMeshes = null;
                _tempCapMeshes = null;
                if (_viewSwitch != SwitchSide || _viewKeepBoth != KeepBothPieces)
                {
                    if (_choiceBaseMesh is not null)
                        _choiceBaseMesh.Color = largeColor;
                    if (_choiceCapMesh is not null)
                        _choiceCapMesh.Color = smallColor;
                    _viewSwitch = SwitchSide;
                    _viewKeepBoth = KeepBothPieces;
                }

                return;
            }

            if (sameCut && (_tempBaseMeshes is not null || _tempCapMeshes is not null) && (_viewSwitch != SwitchSide || _viewKeepBoth != KeepBothPieces))
            {
                foreach (PositionColorMeshModel m in _tempBaseMeshes ?? [])
                    m.Color = largeColor;
                foreach (PositionColorMeshModel m in _tempCapMeshes ?? [])
                    m.Color = smallColor;
                _viewSwitch = SwitchSide;
                _viewKeepBoth = KeepBothPieces;
            }

            if (!sameCut)
            {
                _choiceBaseMesh = null;
                _choiceCapMesh = null;
                _choiceFallback = null;
                _tempBaseMeshes = null;
                _tempCapMeshes = null;
                _tempFor = null;
                _tempLatest = null;
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

            if (!ReferenceEquals(_tempFor, small))
            {
                _tempFor = small;
                RequestTemporaryFill(small, largeColor, smallColor, grow);
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

        /// <summary>
        /// Starts meshing only the part of each cut piece near the visible scene. The pieces were already computed by the cut;
        /// this clips them to the view (flat edges along the clip border) so the triangulation is small.
        /// </summary>
        private void RequestTemporaryFill(Polygon small, Microsoft.Xna.Framework.Color largeColor, Microsoft.Xna.Framework.Color smallColor, bool grow)
        {
            Polygon cw = ClockwiseCutPolygon;
            Polygon ccw = CounterClockwiseCutPolygon;
            RetraceCommandAction action = CutAction;
            PolygonIndex? cutIndex = PolyBeingCut;
            Polygon original = OriginalVolumePolygon;
            Polygon bigger = ccw.Area > cw.Area ? ccw : cw;

            Func<Polygon> largePiece = () =>
            {
                if (action == RetraceCommandAction.GROW_INTERNAL_RING && cutIndex.HasValue)
                {
                    Polygon grown = (Polygon)original.Clone();
                    grown.ReplaceInteriorRing(cutIndex.Value.InnerShapeIndex.Value, bigger);
                    return grown;
                }

                return bigger;
            };

            QueueTemporaryFill(new TemporaryFillRequest(largePiece, grow ? null : small, largeColor, smallColor, PolygonViewportClip.ClipAreaForScene(Parent.Scene)));
        }

        private void QueueTemporaryFill(TemporaryFillRequest request)
        {
            _tempLatest = request;
            if (Interlocked.CompareExchange(ref _tempRunning, 1, 0) != 0)
                return;

            Task.Run(RunTemporaryFill);
        }

        /// <summary>
        /// Meshes the newest request; if a newer one arrives while meshing, the older result is dropped and the newer one is built.
        /// </summary>
        private void RunTemporaryFill()
        {
            TemporaryFillRequest request = _tempLatest;
            TemporaryFillRequest handled = null;
            using (WebAnnotation.View.PolygonViewTimingLog.Measure("temporary-fill"))
            {
                try
                {
                    while (request is not null)
                    {
                        PositionColorMeshModel[] baseMeshes = MeshClippedPieces(request.LargePiece(), request.ClipArea, request.LargeColor);
                        PositionColorMeshModel[] capMeshes = MeshClippedPieces(request.CapPiece, request.ClipArea, request.CapColor);

                        TemporaryFillRequest newest = _tempLatest;
                        if (ReferenceEquals(newest, request))
                        {
                            _tempBaseMeshes = baseMeshes;
                            _tempCapMeshes = capMeshes;
                            handled = request;
                            break;
                        }

                        // Superseded or cleared while meshing; null means the cut changed and nothing should be shown.
                        request = newest;
                    }
                }
                catch (ArgumentException)
                {
                    // A piece the triangulation rejects simply gets no temporary fill. Do not retry the same request.
                    handled = request;
                }
                finally
                {
                    Volatile.Write(ref _tempRunning, 0);
                }
            }

            // A request queued after the last check but before the flag was cleared saw the flag set and returned, so pick it up here.
            TemporaryFillRequest pending = _tempLatest;
            if (pending is not null && !ReferenceEquals(pending, handled) && Interlocked.CompareExchange(ref _tempRunning, 1, 0) == 0)
                Task.Run(RunTemporaryFill);
        }

        private static PositionColorMeshModel[] MeshClippedPieces(Polygon piece, Geometry.Rectangle area, Microsoft.Xna.Framework.Color color)
        {
            if (piece is null)
                return [];

            return [.. PolygonViewportClip.Clip(piece, area)
                .Select(p => PolygonCutMeshBuilder.FromPatch(p, color))
                .Where(m => m is not null)];
        }

        /// <summary>
        /// The pan or zoom has left the area the temporary fill was clipped to, so clip again around the new view.
        /// </summary>
        private void RefreshTemporaryFillIfViewMoved(VikingXNA.Scene scene)
        {
            TemporaryFillRequest latest = _tempLatest;
            if (latest is null || (_tempBaseMeshes is null && _tempCapMeshes is null))
                return;

            if (PolygonViewportClip.Covers(latest.ClipArea, PolygonViewportClip.ClipAreaForScene(scene, 0)))
                return;

            QueueTemporaryFill(latest with { ClipArea = PolygonViewportClip.ClipAreaForScene(scene) });
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
if (this.Vertices.Length < 3 || curve_verticies is null || this.curve_verticies.ControlPoints.Length < 3)
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

            PositionColorMeshModel[] meshes = null;
            if (_choiceBaseMesh is not null || _choiceCapMesh is not null)
            {
                meshes = _choiceCapMesh is null
                    ? [_choiceBaseMesh]
                    : _choiceBaseMesh is null
                        ? [_choiceCapMesh]
                        : [_choiceBaseMesh, _choiceCapMesh];
            }
            else
            {
                RefreshTemporaryFillIfViewMoved(scene);
                PositionColorMeshModel[] tempBase = _tempBaseMeshes;
                PositionColorMeshModel[] tempCap = _tempCapMeshes;
                if (tempBase is { Length: > 0 } || tempCap is { Length: > 0 })
                    meshes = [.. tempBase ?? [], .. tempCap ?? []];
            }

            if (meshes is not null)
            {
                float originalAlphaLuma = Parent.PolygonOverlayEffect.InputLumaAlphaValue;
                Parent.PolygonOverlayEffect.InputLumaAlphaValue = 0.5f;
                MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, Parent.PolygonOverlayEffect, meshmodels: meshes);
                Parent.PolygonOverlayEffect.InputLumaAlphaValue = originalAlphaLuma;
            }

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}
