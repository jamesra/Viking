using AnnotationVizLib;

using ColladaIO;
using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MorphologyMesh;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;


namespace MonogameTestbed
{
    class BajajMultiOTVAssignmentView
    {
        public readonly Polygon[] Polygons = null;
        public readonly double[] PolyZ = null;
        //public PointSetViewView[] PolyPointsView = null;
        public PointSetView IncompletedVertexView = null;

        //Top-level cell shells are translucent with backface culling so nested children remain visible inside.
        //Depth write stays on so only the nearest shell surface wins; far walls of the same body do not blend through.
        //Store CullClockwiseFace: with --invert-z, CullModeForView flips winding-sensitive cull modes so the
        //outward (front) faces remain. CullCounterClockwiseFace + InvertZ culls the exterior instead.
        //Child sheets stay opaque and double-sided (CullMode.None) - a flat wall between sections has one winding.
        public CullMode CullMode;

        /// <summary>
        /// True when this structure is not nested under another real structure (factory root StructureID 0 does not count).
        /// </summary>
        internal bool IsTopLevelStructure => IsTopLevelMorphologyGraph(Graph);

        /// <summary>
        /// Factory roots use StructureID 0; real parentless cells and cells whose only parent is that root are top-level.
        /// </summary>
        internal static bool IsTopLevelMorphologyGraph(MorphologyGraph graph) =>
            graph?.Parent is null || graph.Parent.StructureID == 0;

        /// <summary>
        /// Top-level meshes draw at half opacity so children inside the shell remain visible.
        /// </summary>
        internal const float TopLevelMeshOpacity = 0.5f;

        public int? iShownLineView = null;
        public List<LineSetView> listLineViews = [];
        public bool ShowLines => iShownLineView.HasValue;

        //private LineSetView lineViews = new LineSetView();
        //private LineSetView unfiltered_lineViews = new LineSetView();
        //List<LineView> polyRingViews = null;
        public PointSetView MeshVertsView = null;

        /// <summary>
        /// The position of this mesh in volume space. 
        /// </summary>
        public Geometry.Vector2 Position => Graph.NodesBoundingBox.CenterPoint.XY();

        readonly PolygonSetView PolyViews;
        readonly List<LineView> OTVTableView = null;

        //BajajGeneratorMesh FirstPassTriangulation = null;

        public List<RegionView> RegionViews = [];

        public int? iShownMesh = null;
        public List<MeshView<VertexPositionColor>> MeshViews = [];
        public bool ShowMesh => iShownMesh.HasValue;


        //MeshModel<VertexPositionColor> meshViewModel = null;


        MeshView<VertexPositionColor> SliceMeshView = null;
        MeshView<VertexPositionNormalColor> CompositeMeshView = null;

        //public SliceGraphMeshModel CompositeMeshModel = null;

        public MeshAssemblyPlanner meshAssemblyPlan = null;
        public MeshAssemblyPlannerCompletedView meshCompletedView = null;

        /// <summary>
        /// Run-wide failed-slice log shared by every structure in the test; each new plan reports into it.
        /// </summary>
        public FailedSliceReport FailedSliceLog { get; set; }

        /// <summary>
        /// GPU mesh built from the assembled composite (same geometry as DAE export).
        /// </summary>
        MeshModel<VertexPositionNormalColor> _assembledDisplayModel = null;
        public MeshAssemblyPlannerIncompleteView meshIncompleteView = null;

        //LineView[] lineViews = null;

        public int? iShownRegion = null;
        readonly List<LineSetView> RegionPolygonViews;
        readonly List<LabelView> RegionLabelViews;

        public bool ShowFaces = false;
        public bool ShowPolygons = true;
        public bool ShowRegionPolygons => iShownRegion.HasValue;

        public bool ShowCompletedVerticies = true;
        public bool ShowAllEdges = false;


        /// <summary>
        /// True if we show composite mesh, false if we show the slice mesh
        /// </summary>
        public bool ShowCompositeMesh = true;

        /// <summary>
        /// Assembly debug overlay: wireframe bounding boxes for incomplete/failed nodes. Boxes vanish as slices
        /// merge, so the overlay thins out on its own and leaves the solid mesh visible once assembly finishes.
        /// Toggle with the right stick or B.
        /// </summary>
        public bool ShowAssemblyBoundingBoxes = true;

        /// <summary>
        /// When false, hides only red (critical / non-manifold) error overlays. Toggle with R or View menu.
        /// Requires <see cref="ShowAssemblyBoundingBoxes"/>. Off by default in non-DEBUG builds.
        /// </summary>
        public bool ShowRedErrorBoxes
        {
            get => ShowCriticalSliceStatus;
            set => ShowCriticalSliceStatus = value;
        }

        bool _showInProgressSliceStatus = true;
        bool _showSectionReadySliceStatus = true;
#if DEBUG
        bool _showMinorIssueSliceStatus = true;
        bool _showWarningSliceStatus = true;
        bool _showCriticalSliceStatus = true;
        bool _showUntiledLinkedPairStatus = true;
#else
        bool _showMinorIssueSliceStatus = false;
        bool _showWarningSliceStatus = false;
        bool _showCriticalSliceStatus = false;
        bool _showUntiledLinkedPairStatus = true;
#endif

        public bool ShowInProgressSliceStatus
        {
            get => meshIncompleteView?.ShowInProgressSliceStatus ?? _showInProgressSliceStatus;
            set
            {
                _showInProgressSliceStatus = value;
                if (meshIncompleteView != null)
                    meshIncompleteView.ShowInProgressSliceStatus = value;
            }
        }

        public bool ShowSectionReadySliceStatus
        {
            get => meshIncompleteView?.ShowSectionReadySliceStatus ?? _showSectionReadySliceStatus;
            set
            {
                _showSectionReadySliceStatus = value;
                if (meshIncompleteView != null)
                    meshIncompleteView.ShowSectionReadySliceStatus = value;
            }
        }

        public bool ShowMinorIssueSliceStatus
        {
            get => meshIncompleteView?.ShowMinorIssueSliceStatus ?? _showMinorIssueSliceStatus;
            set
            {
                _showMinorIssueSliceStatus = value;
                if (meshIncompleteView != null)
                    meshIncompleteView.ShowMinorIssueSliceStatus = value;
            }
        }

        public bool ShowWarningSliceStatus
        {
            get => meshIncompleteView?.ShowWarningSliceStatus ?? _showWarningSliceStatus;
            set
            {
                _showWarningSliceStatus = value;
                if (meshIncompleteView != null)
                    meshIncompleteView.ShowWarningSliceStatus = value;
            }
        }

        public bool ShowCriticalSliceStatus
        {
            get => meshIncompleteView?.ShowCriticalSliceStatus ?? _showCriticalSliceStatus;
            set
            {
                _showCriticalSliceStatus = value;
                if (meshIncompleteView != null)
                    meshIncompleteView.ShowCriticalSliceStatus = value;
            }
        }

        public bool ShowUntiledLinkedPairStatus
        {
            get => meshIncompleteView?.ShowUntiledLinkedPairStatus ?? _showUntiledLinkedPairStatus;
            set
            {
                _showUntiledLinkedPairStatus = value;
                if (meshIncompleteView != null)
                    meshIncompleteView.ShowUntiledLinkedPairStatus = value;
            }
        }

        /// <summary>Alias for <see cref="ShowCriticalSliceStatus"/>.</summary>
        public bool ShowFailedBoundingBoxes
        {
            get => ShowCriticalSliceStatus;
            set => ShowCriticalSliceStatus = value;
        }

        void ApplySliceStatusFiltersToIncompleteView()
        {
            if (meshIncompleteView is null)
                return;
            meshIncompleteView.ShowInProgressSliceStatus = _showInProgressSliceStatus;
            meshIncompleteView.ShowSectionReadySliceStatus = _showSectionReadySliceStatus;
            meshIncompleteView.ShowMinorIssueSliceStatus = _showMinorIssueSliceStatus;
            meshIncompleteView.ShowWarningSliceStatus = _showWarningSliceStatus;
            meshIncompleteView.ShowCriticalSliceStatus = _showCriticalSliceStatus;
            meshIncompleteView.ShowUntiledLinkedPairStatus = _showUntiledLinkedPairStatus;
        }

        public IndexLabelType VertexLabelType
        {
            get => PolyViews is null ? IndexLabelType.NONE : PolyViews.PointLabelType;
            set
            {
                if (PolyViews is not null)
                    PolyViews.PointLabelType = value;
            }
        }

        public bool ShowPolyIndexLabels => PolyViews?.LabelPolygonIndex ?? false;

        public bool ShowMeshIndexLabels => PolyViews?.LabelIndex ?? false;


        public bool ShowPolyPositionLabels => PolyViews?.LabelPosition ?? false;

        public readonly MorphologyGraph Graph;

        /// <summary>
        /// Volume XY SliceGraph subtracts from this structure. Shared with the parent cell so synapses mesh in the cell's frame.
        /// </summary>
        internal readonly Geometry.Vector2 SliceOrigin;

        Vector3? _placementOffset;

        /// <summary>
        /// World translation that puts this mesh back in volume XY. Always the slice-graph frame origin
        /// (parent cell center for synapses), so live placement stays registered for the whole run.
        /// </summary>
        internal Vector3 SliceGraphToVolumeOffset =>
            _placementOffset ?? new Vector3((float)SliceOrigin.X, (float)SliceOrigin.Y, 0f);

        /// <summary>
        /// Color from the structure type recorded on the morphology graph. Shared by the mesh and the BajajMultiTest legend.
        /// Top-level structures use <see cref="TopLevelMeshOpacity"/> so nested children show through the shell.
        /// </summary>
        internal static Color ColorForGraph(MorphologyGraph graph)
        {
            uint argb = graph?.structure?.Type?.Color ?? 0xFF808080u;
            Color color = argb.ToXNAColor();
            if (IsTopLevelMorphologyGraph(graph))
                return color.SetAlpha(TopLevelMeshOpacity);

            if (color.A == 0)
                color.A = 255;
            return color;
        }

        /// <summary>
        /// Used to lock the mesh views for individual slices
        /// </summary>
        private readonly SemaphoreSlim drawlock = new(1);
        private int _meshGeneration;
        private int _generateRunning;

        /// <summary>True while ConvertToMesh is in flight for this view.</summary>
        internal bool IsGeneratingMesh => Volatile.Read(ref _generateRunning) != 0;

        private SliceGraph _sliceGraph;

        /// <summary>Cached volume-space AABB for pick/framing; invalidated when mesh or placement changes.</summary>
        private bool _renderedBoundsValid;
        private Vector3 _renderedBoundsMin;
        private Vector3 _renderedBoundsMax;

        /// <summary>Last translation applied in <see cref="ApplySliceGraphPlacement"/>; skip redundant ModelMatrix writes.</summary>
        private Vector3 _lastAppliedPlacementOffset = new(float.NaN);

        /// <summary>
        /// Incomplete overlays are allocated after the first placement pass; remember which instance we stamped
        /// so a replaced view cannot sit forever in slice-local space under an offset-equality early-out.
        /// </summary>
        private MeshAssemblyPlannerIncompleteView _lastPlacedIncompleteView;

        /// <summary>
        /// Slices that produced no geometry because their topology could not be built.  Surfaced so a run that
        /// quietly dropped part of a cell is not mistaken for a complete one.
        /// </summary>
        internal IReadOnlyDictionary<ulong, string> FailedTopologySlices =>
            _sliceGraph?.FailedTopologySlices ?? EmptyFailedTopology;

        static readonly IReadOnlyDictionary<ulong, string> EmptyFailedTopology =
            new Dictionary<ulong, string>();

        /// <summary>Reusable single-element array for solid-mesh draws (avoids per-frame collection alloc).</summary>
        readonly MeshModel<VertexPositionNormalColor>[] _solidDrawModels = new MeshModel<VertexPositionNormalColor>[1];

        static readonly DepthStencilState OpaqueDepthState = new()
        {
            DepthBufferEnable = true,
            StencilEnable = false,
            DepthBufferWriteEnable = true,
            DepthBufferFunction = CompareFunction.LessEqual
        };

        /// <summary>
        /// Translucent top-level shells: test and write depth so only the nearest fragment along a ray
        /// updates color. Farther shell surfaces (e.g. the far wall of a closed cell) fail the depth test
        /// instead of alpha-blending through. Opaque children are drawn first, so their pixels remain
        /// under the blended shell.
        /// </summary>
        static readonly DepthStencilState TranslucentDepthState = new()
        {
            DepthBufferEnable = true,
            StencilEnable = false,
            DepthBufferWriteEnable = true,
            DepthBufferFunction = CompareFunction.LessEqual
        };

        /// <summary>
        /// Depth-only: write Z without touching color so back faces can occlude exterior children
        /// behind the cell before the translucent front pass runs.
        /// </summary>
        static readonly BlendState DepthOnlyBlendState = new()
        {
            ColorWriteChannels = ColorWriteChannels.None
        };

        /// <summary>Opposite winding cull so a front-face-culled mesh draws its back faces.</summary>
        internal static CullMode FlipCullMode(CullMode mode) => mode switch
        {
            CullMode.CullClockwiseFace => CullMode.CullCounterClockwiseFace,
            CullMode.CullCounterClockwiseFace => CullMode.CullClockwiseFace,
            _ => mode
        };

        public BajajMultiOTVAssignmentView(MorphologyGraph graph, Geometry.Vector2? sliceOrigin = null)
        {
            ///Takes a set of polygons and Z values and generates a meshView
            Graph = graph;
            SliceOrigin = sliceOrigin ?? graph.NodesBoundingBox.CenterPoint.XY();
            CullMode = IsTopLevelStructure
                ? CullMode.CullClockwiseFace
                : CullMode.None;
            //Same origin SliceGraph subtracts; apply immediately so incomplete overlays are correct
            //before GenerateMesh finishes (children must not wait on parent assembly).
            _placementOffset = new Vector3((float)SliceOrigin.X, (float)SliceOrigin.Y, 0f);

            /*
            Trace.WriteLine("Begin Slice graph construction");
            sliceGraph = SliceGraph.Create(graph, 2.0);
            Trace.WriteLine("End Slice graph construction");
            */

            ResetMesh();
        }


        /// <summary>
        /// Called when the test window is closed
        /// </summary>
        public void OnUnloadContent()
        {
        }

        private void OnSliceCompleted(Slice slice, BajajGeneratorMesh mesh, bool Success) => this.AddMesh(slice, mesh, Success);

        private void AddMesh(Slice slice, BajajGeneratorMesh mesh, bool Success) =>
            meshAssemblyPlan?.OnMeshCompleted(slice, mesh, Success);


        /// <summary>
        /// Called before GenerateMesh to reset the class views.
        /// </summary>
        internal void ResetMesh()
        {
            InvalidateRenderedBoundsCache();
            _lastAppliedPlacementOffset = new Vector3(float.NaN);
            _lastPlacedIncompleteView = null;
            //Keep the live view in the slice-graph frame even across ResetMesh; children share the
            //parent SliceOrigin and must not fall back to a transient null offset mid-regeneration.
            _placementOffset = new Vector3((float)SliceOrigin.X, (float)SliceOrigin.Y, 0f);
            SliceMeshView = new MeshView<VertexPositionColor>
            {
                Name = "Slice Mesh"
            };

            this.RegionViews.Clear();
            this.listLineViews.Clear();

            try
            {
                drawlock.Wait();
                this.MeshViews.Clear();
                MeshViews.Add(SliceMeshView);
            }
            finally
            {
                drawlock.Release();
            }

            CompositeMeshView = new MeshView<VertexPositionNormalColor>
            {
                Name = "Composite Mesh"
            };
            _assembledDisplayModel = null;
        }

        /// <summary>
        /// Rebuilds the slice graph and Bajaj mesh. Overlapping Start clicks are ignored until the current run finishes
        /// so completed slices cannot land on a planner that was replaced mid-run.
        /// </summary>
        /// <param name="prepThrottle">
        /// When set, limits how many structure pipelines run concurrently (prep through assembly wait). Face
        /// generation remains capped by <see cref="MeshParallelism.FaceSlots"/> inside each pipeline.
        /// </param>
        internal async Task GenerateMesh(SemaphoreSlim prepThrottle = null)
        {
            if (Interlocked.CompareExchange(ref _generateRunning, 1, 0) != 0)
                return;

            int generation = Interlocked.Increment(ref _meshGeneration);
            bool IsCurrent() => generation == Volatile.Read(ref _meshGeneration);
            bool holdsPrep = false;

            try
            {
                if (prepThrottle is not null)
                {
                    await prepThrottle.WaitAsync().ConfigureAwait(false);
                    holdsPrep = true;
                }

                if (MeshViews.Count > 0)
                    ResetMesh();

                Trace.WriteLine("Begin Slice graph construction");
                //Two-step creation: the plan and the in-progress overlay exist before any topology does, and the
                //topology initializer feeds contours into the overlay as each slice finishes.  A large cell used to
                //show nothing until every topology was done and then every contour at once.
                SliceGraph sliceGraph = await SliceGraph.CreateWithoutTopology(Graph, Program.options?.ContourSimplify ?? ContourSimplifyOptions.Default, SliceOrigin);

                if (!IsCurrent())
                    return;

                if (!sliceGraph.Nodes.Any())
                {
                    Trace.WriteLine($"No nodes in Slice graph {sliceGraph}");
                    return;
                }

                var plan = MeshAssemblyPlanner.Create(sliceGraph);
                if (!IsCurrent())
                    return;

                plan.MeshColor = ColorForGraph(Graph);
                if (FailedSliceLog is not null)
                    plan.FailedSliceRecorded += FailedSliceLog.Record;
                meshAssemblyPlan = plan;
                _sliceGraph = sliceGraph;
                meshIncompleteView?.CancelRebuild();
                //Quiet/headless runs never draw the in-progress overlay; skip its rebuild traffic.
                bool buildIncompleteOverlay = Program.options?.Quiet != true;
                MeshAssemblyPlannerIncompleteView incompleteView = buildIncompleteOverlay
                    ? new(plan, sliceGraph, deferLeafContours: true)
                    : null;
                meshIncompleteView = incompleteView;
                if (incompleteView is not null)
                {
                    ApplySliceStatusFiltersToIncompleteView();
                    plan.UntiledLinkedOverlay += incompleteView.ApplyUntiledLinkedOverlay;
                }
                meshCompletedView = new MeshAssemblyPlannerCompletedView(meshAssemblyPlan)
                {
                    Color = ColorForGraph(Graph)
                };
                //Overlays and completed models are created after the first Draw may already have cached
                //placement as "applied". Force the next ApplySliceGraphPlacement to stamp ModelMatrix.
                _lastAppliedPlacementOffset = new Vector3(float.NaN);

                //The handler captures this generation's view rather than the field so a restart mid-topology
                //cannot feed a replaced view's contours into the new one.
                await sliceGraph.InitializeTopologyAsync((slice, topology) =>
                {
                    if (!IsCurrent() || incompleteView is null)
                        return;
                    incompleteView.PublishLeafContour(slice.Key, topology);
                }).ConfigureAwait(false);
                Trace.WriteLine("End Slice graph construction");

                if (!IsCurrent())
                    return;

                //Branch AABBs only draw once their leaves have meshed, so they need not delay the first faces.
                Task branchOverlays = incompleteView is null
                    ? Task.CompletedTask
                    : Task.Run(() =>
                    {
                        if (IsCurrent())
                            incompleteView.CompleteBranchOverlays();
                    });

                await BajajMeshGenerator.ConvertToMesh(sliceGraph, (slice, mesh, success) =>
                {
                    if (!IsCurrent())
                        return;
                    OnSliceCompleted(slice, mesh, success);
                }, retainMeshes: false).ConfigureAwait(false);

                await branchOverlays.ConfigureAwait(false);

                if (!IsCurrent())
                {
                    plan.Abandon();
                    return;
                }

                //Merges run on a dedicated consumer; wait until the root composite is final.
                await plan.AssembledTask.ConfigureAwait(false);

                if (!IsCurrent())
                    return;

                ViewIndex.ClampOrClear(ref iShownRegion, RegionViews.Count);
                iShownLineView ??= ViewIndex.LastOrNull(listLineViews.Count);
                ViewIndex.ClampOrClear(ref iShownLineView, listLineViews.Count);

                if (iShownMesh is null)
                {
                    try
                    {
                        await drawlock.WaitAsync();
                        iShownMesh = ViewIndex.LastOrNull(MeshViews.Count);
                    }
                    finally
                    {
                        drawlock.Release();
                    }
                }
                else
                {
                    ViewIndex.ClampOrClear(ref iShownMesh, MeshViews.Count);
                }

                if (meshAssemblyPlan.MeshAssembledEvent.IsSet
                    && meshAssemblyPlan.Root?.MeshModel?.composite is { } composite
                    && composite.Faces.Count > 0)
                {
                    Color meshColor = ColorForGraph(Graph);
                    meshAssemblyPlan.Root.MeshModel.Color = meshColor;
                    _assembledDisplayModel = BuildDisplayModelFromComposite(composite, meshColor);
                    InvalidateRenderedBoundsCache();
                    //New model has Identity ModelMatrix; force placement to re-apply next Draw.
                    _lastAppliedPlacementOffset = new Vector3(float.NaN);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Mesh generation failed for structure {Graph.StructureID}: {ex}");
            }
            finally
            {
                if (holdsPrep)
                    prepThrottle.Release();

                RefreshPlacementOffset();
                Interlocked.Exchange(ref _generateRunning, 0);
            }
        }

        private static MeshModel<VertexPositionNormalColor> BuildDisplayModelFromComposite(
            Mesh3D<MorphMeshVertex> composite, Color color)
        {
            var model = new MeshModel<VertexPositionNormalColor>();
            model.Vertices = [.. composite.Vertices.Select(v => new VertexPositionNormalColor(
                v.Position.ToXNAVector3(), v.Normal.ToXNAVector3(), color))];
            model.Edges = [.. composite.Faces.SelectMany(f => f.iVerts)];
            return model;
        }


        public void Draw(MonoTestbed window, Scene scene)
        {
            //window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, Color.DarkGray, 1.0f, 0);
            StringBuilder ViewLabels = new();

            if (RegionViews != null && ViewIndex.InRange(iShownRegion, RegionViews.Count))
            {
                RegionViews[iShownRegion.Value].Draw(window, scene);
                ViewLabels.AppendLine("Region Pass #" + iShownRegion.Value);
            }


            /*lock (drawlock)
            {
            */
            if (ShowCompositeMesh == false)
            {
                if (MeshViews != null && ViewIndex.InRange(iShownMesh, MeshViews.Count))
                {
                    try
                    {
                        drawlock.Wait();
                        MeshViews[iShownMesh.Value].Draw(window.GraphicsDevice, window.Scene, BajajMultiAssignmentTest.CullModeForView(CullMode));
                        ViewLabels.AppendLine(MeshViews[iShownMesh.Value].Name);
                    }
                    finally
                    {
                        drawlock.Release();
                    }
                }
            }
            else
            {
                if (CompositeMeshView != null)
                {
                    CompositeMeshView.Draw(window.GraphicsDevice, window.Scene, BajajMultiAssignmentTest.CullModeForView(CullMode));
                    ViewLabels.AppendLine(CompositeMeshView.Name);
                }
            }
            //}


            if (listLineViews != null && ViewIndex.InRange(iShownLineView, listLineViews.Count))
            {
                int iShownLine = iShownLineView.Value;
                LineSetView lineView = listLineViews[iShownLine];

                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, 0);
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, [.. lineView.LineViews]);
                window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1.0f, 0);
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 10);
                //CurveLabel.Draw(window.GraphicsDevice, window.Scene, window.spriteBatch, window.fontArial, window.curveManager, lineView.LineLables.ToArray());
                foreach (var labelsByFont in lineView.LineLabels.GroupBy(l => l.font))
                {
                    LabelView.Draw(window.spriteBatch, labelsByFont.Key, window.Scene, [.. labelsByFont]);
                }

                ViewLabels.AppendLine(lineView.Name);
            }
            /*
            if (lineViews != null && ShowPolygons && !ShowRegionPolygons)
            {
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, 0);
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, lineViews.LineViews.ToArray());
                window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1.0f, 0);
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 10);
                CurveLabel.Draw(window.GraphicsDevice, window.Scene, window.spriteBatch, window.fontArial, window.curveManager, lineViews.LineLables.ToArray());
                ViewLabels.AppendLine("Chords");
            }

            if (unfiltered_lineViews != null && ShowAllEdges)
            {
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, unfiltered_lineViews.LineViews.ToArray());
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                CurveLabel.Draw(window.GraphicsDevice, window.Scene, window.spriteBatch, window.fontArial, window.curveManager, unfiltered_lineViews.LineLables.ToArray());
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                ViewLabels.AppendLine("Triangulation");
            }*/

            if (IncompletedVertexView != null && ShowCompletedVerticies)
            {
                IncompletedVertexView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha);
                ViewLabels.AppendLine("Incomplete Vertices");
            }

            if (MeshVertsView != null && (this.VertexLabelType & IndexLabelType.MESH) > 0)
            {
                MeshVertsView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha);
                ViewLabels.AppendLine("Mesh verticies");
            }

            if (RegionPolygonViews != null && ShowRegionPolygons)
            {

                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, [.. RegionPolygonViews.SelectMany(rpv => rpv.LineViews)]);
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, RegionLabelViews);
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                ViewLabels.AppendLine("Region Polygon Views");
            }

            if (OTVTableView != null)
            {
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, [.. OTVTableView]);
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                ViewLabels.AppendLine("OTV Table");
            }

            if (this.PolyViews != null && !ShowRegionPolygons && ((this.VertexLabelType & IndexLabelType.MESH) == 0))
            {
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                PolyViews.Draw(window, scene);
                ViewLabels.AppendLine("Poly Views");
            }

            LabelView label = new(ViewLabels.ToString(), scene.VisibleWorldBounds.UpperLeft, anchor: Anchor.BottomLeft, scaleFontWithScene: false);
            LabelView.Draw(window.spriteBatch, window.fontArial, scene, new LabelView[] { label });
        }

        public void Draw3D(MonoTestbed window, Scene3D scene)
        {
            ApplySliceGraphPlacement();
            scene.Viewport = window.GraphicsDevice.Viewport;

            bool translucent = IsTopLevelStructure;
            window.GraphicsDevice.BlendState = translucent ? BlendState.AlphaBlend : BlendState.Opaque;
            window.GraphicsDevice.DepthStencilState = translucent ? TranslucentDepthState : OpaqueDepthState;
            CullMode cull = BajajMultiAssignmentTest.CullModeForView(CullMode);

            if (ShowCompositeMesh == false)
            {
                MeshView<VertexPositionColor> sliceView = null;
                try
                {
                    drawlock.Wait();
                    if (ViewIndex.InRange(iShownMesh, MeshViews.Count))
                        sliceView = MeshViews[iShownMesh.Value];
                }
                finally
                {
                    drawlock.Release();
                }

                sliceView?.Draw(window.GraphicsDevice, scene, cull);
                return;
            }

            if (CompositeMeshView is null)
                return;

            if (ShowAssemblyBoundingBoxes && meshIncompleteView != null)
            {
                var incompleteModels = meshIncompleteView.MeshModels;
                if (incompleteModels.Length > 0)
                    MeshView<VertexPositionColor>.Draw(window.GraphicsDevice, scene, window.basicEffect,
                        cull, FillMode.WireFrame, incompleteModels);
            }

            DrawSolidCompositeMesh(window, scene, cull);
        }

        /// <summary>
        /// Depth-only back faces of a top-level shell. Run before opaque children so geometry behind the
        /// cell fails the depth test; interior children (in front of that far wall) still draw and remain
        /// visible under the later translucent front pass.
        /// Skipped when cull is <see cref="CullMode.None"/> (both faces already drawn in the color pass).
        /// </summary>
        public void Draw3DShellDepthOccluder(MonoTestbed window, Scene3D scene)
        {
            if (!IsTopLevelStructure || !ShowCompositeMesh || CompositeMeshView is null)
                return;

            CullMode frontCull = BajajMultiAssignmentTest.CullModeForView(CullMode);
            if (frontCull == CullMode.None)
                return;

            ApplySliceGraphPlacement();
            scene.Viewport = window.GraphicsDevice.Viewport;

            var device = window.GraphicsDevice;
            BlendState previousBlend = device.BlendState;
            DepthStencilState previousDepth = device.DepthStencilState;
            try
            {
                device.BlendState = DepthOnlyBlendState;
                device.DepthStencilState = OpaqueDepthState;
                DrawSolidCompositeMesh(window, scene, FlipCullMode(frontCull));
            }
            finally
            {
                device.BlendState = previousBlend;
                device.DepthStencilState = previousDepth;
            }
        }

        void DrawSolidCompositeMesh(MonoTestbed window, Scene3D scene, CullMode cull)
        {
            bool drewSolidMesh = false;
            var rootMeshModel = meshAssemblyPlan?.Root?.MeshModel;
            if (rootMeshModel?.model?.Vertices?.Length > 0)
            {
                _solidDrawModels[0] = rootMeshModel.model;
                MeshView<VertexPositionNormalColor>.Draw(window.GraphicsDevice, scene,
                    window.basicEffect, cull, FillMode.Solid, _solidDrawModels);
                drewSolidMesh = true;
            }

            if (!drewSolidMesh && _assembledDisplayModel != null)
            {
                _solidDrawModels[0] = _assembledDisplayModel;
                MeshView<VertexPositionNormalColor>.Draw(window.GraphicsDevice, scene,
                    window.basicEffect, cull, FillMode.Solid, _solidDrawModels);
            }
            else if (!drewSolidMesh && meshCompletedView != null)
            {
                MeshView<VertexPositionNormalColor>.Draw(window.GraphicsDevice, scene,
                    window.basicEffect, cull, FillMode.Solid, meshCompletedView.MeshModels);
            }
        }

        /// <summary>
        /// Put this mesh at volume XY. SliceGraph subtracted <see cref="SliceOrigin"/> (the parent cell's location center).
        /// </summary>
        private void ApplySliceGraphPlacement()
        {
            Vector3 offset = SliceGraphToVolumeOffset;
            Matrix m = Matrix.CreateTranslation(offset);

            bool offsetChanged = offset != _lastAppliedPlacementOffset;
            bool incompleteChanged = !ReferenceEquals(_lastPlacedIncompleteView, meshIncompleteView);
            if (offsetChanged || incompleteChanged)
            {
                //Bounding boxes keep their size in their own ModelMatrix, so they compose placement.
                meshIncompleteView?.ApplyPlacement(m);
                _lastPlacedIncompleteView = meshIncompleteView;
            }

            //Mesh models are born with Identity ModelMatrix as slices merge. A pure offset-equality early-out
            //left those new models in slice-local space while the camera framed volume bounds (children that
            //had already been stamped looked correct; parent progress sat in a cloud offset by SliceOrigin).
            var root = meshAssemblyPlan?.Root?.MeshModel?.model;
            if (root != null && root.ModelMatrix.Translation != offset)
                root.ModelMatrix = m;
            if (_assembledDisplayModel != null && _assembledDisplayModel.ModelMatrix.Translation != offset)
                _assembledDisplayModel.ModelMatrix = m;
            if (meshCompletedView?.MeshModels != null)
            {
                foreach (var model in meshCompletedView.MeshModels)
                {
                    if (model != null && model.ModelMatrix.Translation != offset)
                        model.ModelMatrix = m;
                }
            }

            _lastAppliedPlacementOffset = offset;
        }

        /// <summary>
        /// Live-view translation is always the inverse of the slice-graph XY origin.
        ///
        /// Children are meshed in the parent cell's frame (<see cref="SliceOrigin"/> = parent center), so
        /// translating by that same origin keeps synapses registered to the cell for the whole run. Remapping
        /// via mesh AABB after assembly made children jump whenever the mesh center disagreed with the
        /// annotation-relative center, and left them misaligned with a parent still using <see cref="SliceOrigin"/>
        /// mid-assembly.
        ///
        /// Collada export still uses <see cref="VolumePlacementCenter"/> (annotation AABB), which is independent
        /// of this live-view offset.
        /// </summary>
        void RefreshPlacementOffset()
        {
            _placementOffset = new Vector3((float)SliceOrigin.X, (float)SliceOrigin.Y, 0f);
            InvalidateRenderedBoundsCache();
            _lastAppliedPlacementOffset = new Vector3(float.NaN);
            _lastPlacedIncompleteView = null;
        }

        /// <summary>
        /// Where this structure belongs in volume coordinates: the center of its own annotations.
        ///
        /// The Collada serializer strips each mesh to its own AABB center, so the node translation is what
        /// decides where the structure lands, and anchoring it to the annotations makes the export independent
        /// of both the frame the mesh was built in and any drift between the mesh and the contours it came from.
        /// It is deliberately not <see cref="SliceGraphToVolumeOffset"/>: that offset is the render-time inverse
        /// of the slice-graph frame, and for a child meshed in its parent's frame it is the parent's center.
        /// </summary>
        internal Geometry.Vector3 VolumePlacementCenter
        {
            get
            {
                if (Graph.Nodes.Count > 0)
                    return Graph.NodesBoundingBox.CenterPoint;

                var composite = meshAssemblyPlan?.Root?.MeshModel?.composite;
                if (composite is not null && composite.Vertices.Count > 0)
                {
                    Geometry.Vector3 center = composite.BoundingBox.CenterPoint;
                    return new Geometry.Vector3(center.X + SliceOrigin.X, center.Y + SliceOrigin.Y, center.Z);
                }

                return new Geometry.Vector3(SliceOrigin.X, SliceOrigin.Y, 0);
            }
        }

        bool TryGetLocalMeshBounds(out Vector3 min, out Vector3 max)
        {
            Vector3 boundsMin = new(float.MaxValue);
            Vector3 boundsMax = new(float.MinValue);
            bool any = false;

            void Include(Vector3 p)
            {
                boundsMin = Vector3.Min(boundsMin, p);
                boundsMax = Vector3.Max(boundsMax, p);
                any = true;
            }

            //Prefer the composite AABB when assembly is finished — O(1) vs scanning every vertex.
            var composite = meshAssemblyPlan?.Root?.MeshModel?.composite;
            if (meshAssemblyPlan?.MeshAssembledEvent.IsSet == true
                && composite is not null
                && composite.Vertices.Count > 0)
            {
                Geometry.Box box = composite.BoundingBox;
                if (box != default)
                {
                    min = box.MinCorner.ToXNAVector3();
                    max = box.MaxCorner.ToXNAVector3();
                    return true;
                }
            }

            if (_assembledDisplayModel?.Vertices != null)
            {
                foreach (var v in _assembledDisplayModel.Vertices)
                    Include(v.Position);
            }

            var rootModel = meshAssemblyPlan?.Root?.MeshModel?.model;
            if (rootModel?.Vertices != null)
            {
                foreach (var v in rootModel.Vertices)
                    Include(v.Position);
            }

            if (meshCompletedView?.MeshModels != null)
            {
                foreach (var model in meshCompletedView.MeshModels)
                {
                    if (model?.Vertices == null)
                        continue;
                    foreach (var v in model.Vertices)
                        Include(v.Position);
                }
            }

            if (composite?.Vertices != null)
            {
                foreach (var v in composite.Vertices)
                    Include(v.Position.ToXNAVector3());
            }

            min = boundsMin;
            max = boundsMax;
            return any;
        }

        void InvalidateRenderedBoundsCache() => _renderedBoundsValid = false;

        /// <summary>
        /// Axis-aligned bounds of mesh geometry actually drawn in 3D (volume XY, slice-graph Z).
        /// </summary>
        public bool TryGetRenderedMeshBounds(out Vector3 min, out Vector3 max)
        {
            if (_renderedBoundsValid)
            {
                min = _renderedBoundsMin;
                max = _renderedBoundsMax;
                return true;
            }

            if (!TryGetLocalMeshBounds(out min, out max))
                return false;

            Vector3 offset = SliceGraphToVolumeOffset;
            min += offset;
            max += offset;
            _renderedBoundsMin = min;
            _renderedBoundsMax = max;
            _renderedBoundsValid = true;
            return true;
        }

        /// <summary>
        /// Nearest composite face struck by <paramref name="volumeRay"/>.  The ray must be expressed in the
        /// same space <see cref="TryGetRenderedMeshBounds"/> reports: model placement applied, the scene
        /// Z-flip not.
        ///
        /// Returns false until this structure's assembly has finished, because the composite is still being
        /// mutated by merge threads before that point.
        /// </summary>
        /// <param name="iVerts">Composite vertex indices of the struck triangle.</param>
        /// <param name="distance">Distance from the ray origin to the hit, in volume units.</param>
        /// <param name="boxEntryDistance">
        /// Ray parameter where the volume AABB is entered. Callers use this to skip structures whose box
        /// is farther than an already-found face hit.
        /// </param>
        public bool TryPickCompositeFace(in Geometry.Ray3D volumeRay, out int[] iVerts, out double distance, out double boxEntryDistance)
        {
            iVerts = null;
            distance = double.MaxValue;
            boxEntryDistance = double.MaxValue;

            //Background merges add faces and vertices to the root composite, so enumerating it mid-assembly threw
            //"Collection was modified after the enumerator was instantiated" on the draw thread.  MeshAssembledEvent
            //is set only after FinalizeRootComposite, so the composite is immutable once it is signalled.
            if (meshAssemblyPlan?.MeshAssembledEvent.IsSet != true)
                return false;

            var composite = meshAssemblyPlan.Root?.MeshModel?.composite;
            if (composite is null || composite.Faces.Count == 0)
                return false;

            if (!TryGetRenderedMeshBounds(out Vector3 boundsMin, out Vector3 boundsMax))
                return false;

            Geometry.Vector3 min = new(boundsMin.X, boundsMin.Y, boundsMin.Z);
            Geometry.Vector3 max = new(boundsMax.X, boundsMax.Y, boundsMax.Z);
            if (!Geometry.RayIntersection.TryIntersectBox(volumeRay, min, max, out boxEntryDistance))
                return false;

            //ApplySliceGraphPlacement is a pure translation, so undoing it leaves the direction unit length
            //and keeps hit distances comparable between structure views.
            Vector3 offset = SliceGraphToVolumeOffset;
            Geometry.Ray3D localRay = new(
                volumeRay.Origin - new Geometry.Vector3(offset.X, offset.Y, offset.Z),
                volumeRay.Direction);

            foreach (IFace face in composite.Faces)
            {
                var verts = face.iVerts;
                if (verts.Length < 3)
                    continue;

                Geometry.Vector3 a = composite[verts[0]].Position;

                //Faces are normally triangles; fan-triangulate the rare quad rather than skipping it.
                for (int i = 1; i + 1 < verts.Length; i++)
                {
                    Geometry.Vector3 b = composite[verts[i]].Position;
                    Geometry.Vector3 c = composite[verts[i + 1]].Position;

                    //Composite winding is not reliable enough to cull back faces during picking.
                    if (!Geometry.RayIntersection.TryIntersectTriangle(localRay, a, b, c, out double hitDistance))
                        continue;

                    if (hitDistance >= distance)
                        continue;

                    distance = hitDistance;
                    iVerts = [verts[0], verts[i], verts[i + 1]];
                }
            }

            return iVerts is not null;
        }

        /// <inheritdoc cref="TryPickCompositeFace(in Geometry.Ray3D, out int[], out double, out double)"/>
        public bool TryPickCompositeFace(in Geometry.Ray3D volumeRay, out int[] iVerts, out double distance) =>
            TryPickCompositeFace(volumeRay, out iVerts, out distance, out _);

        /// <summary>
        /// Annotation provenance of a composite vertex.  Cap and medial-axis vertices carry no
        /// <see cref="Geometry.IShapeIndex"/> and therefore no annotation, which the caller must report
        /// rather than hide.
        /// </summary>
        public bool TryGetVertexLocationID(int iVert, out ulong locationID)
        {
            locationID = 0;
            var composite = meshAssemblyPlan?.Root?.MeshModel?.composite;
            if (composite is null || iVert < 0 || iVert >= composite.Vertices.Count)
                return false;

            //SliceGraphMeshModel reindexes every composite vertex to its MorphologyNode key, and
            //MorphologyNode.ID is the annotation Location.ID.
            var shapeIndex = composite[iVert].ShapeIndex;
            if (shapeIndex is null)
                return false;

            locationID = (ulong)shapeIndex.ShapeIndex;
            return true;
        }

        /// <summary>
        /// GPU mesh currently drawn for picking highlight. Prefer the live root model; fall back to the
        /// post-assembly display copy.  When <paramref name="modelLock"/> is non-null, callers must take a
        /// write lock around vertex color mutations.
        /// </summary>
        public bool TryGetSelectableDisplayModel(
            out MeshModel<VertexPositionNormalColor> model,
            out ReaderWriterLockSlim modelLock)
        {
            model = null;
            modelLock = null;

            var rootMeshModel = meshAssemblyPlan?.Root?.MeshModel;
            if (rootMeshModel?.model?.Vertices?.Length > 0)
            {
                model = rootMeshModel.model;
                modelLock = rootMeshModel.ModelLock;
                return true;
            }

            if (_assembledDisplayModel?.Vertices?.Length > 0)
            {
                model = _assembledDisplayModel;
                return true;
            }

            return false;
        }
    }

/// <summary>
/// Generates a single mesh for a cell or a subset of a cell based on a Z range.  Used to debug the generation of whole cells and the merging of multiple slice meshes.
/// </summary>
class BajajMultiAssignmentTest : IGraphicsTest, ITestLegend, ITestHotkeyHelp, IViewMenuTarget, IFileMenuTarget
{
    public string Title => this.GetType().Name;

    static readonly HotkeyBinding[] HotkeyBindings =
    [
        new("B / Right stick", "Toggle assembly bounding-box overlay"),
        new("R", "Toggle red (critical) error bounding boxes only"),
        new("K / Left stick", "Toggle backface culling"),
        new("Left click", "Select mesh slice under cursor (empty click clears)"),
        new("F", "Frame camera on rendered mesh"),
        new("I", "Toggle invert-Z in the 3D view"),
        new("Ctrl+S / Back", "Save assembled meshes (also File → Save Mesh)"),
        new("PrintScreen / Back", "Save current structure mesh when assembled"),
        new("Left shoulder", "Toggle composite vs slice mesh"),
        new("Start", "Regenerate mesh for focused structure"),
        new("A / X buttons", "Cycle shown mesh / lines / regions (gamepad)"),
        new("Right shoulder", "Cycle vertex label modes"),
    ];

    public IReadOnlyList<HotkeyBinding> GetHotkeyBindings() => HotkeyBindings;

    public string ModeDescription => string.Empty;

    public string ActiveViewDescription => string.Empty;

    IReadOnlyList<LegendEntry> _cachedLegendEntries;
    int _legendCacheWrapCount = -1;

    public IReadOnlyList<LegendEntry> LegendEntries
    {
        get
        {
            IReadOnlyList<BajajMultiOTVAssignmentView> views = WrapViews;
            if (_cachedLegendEntries != null && _legendCacheWrapCount == views.Count)
                return _cachedLegendEntries;

            Dictionary<ulong, LegendEntry> byType = [];
            foreach (var wrapView in views)
            {
                var type = wrapView.Graph?.structure?.Type;
                if (type is null || byType.ContainsKey(type.ID))
                    continue;

                string name = type.Name;
                if (string.IsNullOrWhiteSpace(name))
                    name = type.Code;
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Type {type.ID}";

                byType[type.ID] = new LegendEntry(name, BajajMultiOTVAssignmentView.ColorForGraph(wrapView.Graph));
            }

            List<LegendEntry> entries = [.. byType.Values.OrderBy(e => e.Text, StringComparer.OrdinalIgnoreCase)];
            entries.Add(new LegendEntry("Left click: select slice under cursor (empty clears)", Color.Magenta));
            entries.Add(new LegendEntry("F: frame camera on the mesh centroid", Color.White));

            _legendCacheWrapCount = views.Count;
            _cachedLegendEntries = entries;
            return entries;
        }
    }

    Scene scene;
    Scene3D scene3D;
    MonoTestbed _window;
    readonly TestInputContext Input = new();
    /// <summary>
    /// World transform that reflects volume Z through the XY plane when <see cref="Program.CommandLineOptions.InvertZ"/> is set.
    /// Camera3D uses +Z as up; this keeps exported DAE in volume coordinates.
    /// </summary>
    internal static Matrix ViewZAxisWorld =>
        Program.options?.InvertZ == true
            ? Matrix.CreateScale(1f, 1f, -1f)
            : Matrix.Identity;

    /// <summary>
    /// Swap clockwise/counterclockwise culling when the view Z reflection reverses winding.
    /// </summary>
    internal static CullMode CullModeForView(CullMode mode)
    {
        if (Program.options?.InvertZ != true)
            return mode;

        return mode switch
        {
            CullMode.CullClockwiseFace => CullMode.CullCounterClockwiseFace,
            CullMode.CullCounterClockwiseFace => CullMode.CullClockwiseFace,
            _ => mode
        };
    }

    AnnotationVizLib.MorphologyGraph graph;

    //Polygon A;
    //Polygon B;

    readonly PointSetViewCollection Points_A = new(Color.Blue, Color.BlueViolet, Color.PowderBlue);
    readonly PointSetViewCollection Points_B = new(Color.Red, Color.Pink, Color.Plum);
    readonly Camera3DManipulator Camera3DManipulator = new();
    readonly List<BajajMultiOTVAssignmentView> _wrapViews = [];

    /// <summary>
    /// Copy-on-write snapshot of <see cref="_wrapViews"/>.  Init reports Initialized as soon as the mesh tasks
    /// start, so the game loop enumerates these views on a different thread than the one that populates them.
    /// Publishing an array keeps that enumeration valid no matter when a view is added.
    /// </summary>
    BajajMultiOTVAssignmentView[] _wrapViewsSnapshot = [];

    IReadOnlyList<BajajMultiOTVAssignmentView> WrapViews => Volatile.Read(ref _wrapViewsSnapshot);

    private void AddWrapView(BajajMultiOTVAssignmentView view)
    {
        view.ShowInProgressSliceStatus = _showInProgressSliceStatus;
        view.ShowSectionReadySliceStatus = _showSectionReadySliceStatus;
        view.ShowMinorIssueSliceStatus = _showMinorIssueSliceStatus;
        view.ShowWarningSliceStatus = _showWarningSliceStatus;
        view.ShowCriticalSliceStatus = _showCriticalSliceStatus;
        view.ShowUntiledLinkedPairStatus = _showUntiledLinkedPairStatus;
        _wrapViews.Add(view);
        _cachedLegendEntries = null;
        _legendCacheWrapCount = -1;
        PublishWrapViewsSnapshot();
    }

    /// <summary>
    /// Opaque (child) structures first, then translucent top-level shells. <see cref="Draw"/> also
    /// inserts a top-level back-face depth occluder pass before children.
    /// </summary>
    void PublishWrapViewsSnapshot()
    {
        BajajMultiOTVAssignmentView[] ordered = new BajajMultiOTVAssignmentView[_wrapViews.Count];
        int write = 0;
        foreach (var view in _wrapViews)
        {
            if (!view.IsTopLevelStructure)
                ordered[write++] = view;
        }

        foreach (var view in _wrapViews)
        {
            if (view.IsTopLevelStructure)
                ordered[write++] = view;
        }

        Volatile.Write(ref _wrapViewsSnapshot, ordered);
    }

    List<BoundarySurfaceViewModel> boundaryViewModels = [];
    MeshView<VertexPositionNormalColor> boundaryView = null;
    readonly bool Draw3D = true;

    bool _initialized = false;
    public bool Initialized => _initialized;

    /// <summary>
    /// Left-button press screen position for click-vs-drag detection. Camera uses left-drag to pan;
    /// a release within <see cref="ClickPickSlopPixels"/> of the press is treated as a mesh pick.
    /// </summary>
    Point? _leftButtonPressScreen;
    bool _leftButtonDragExceededSlop;
    bool _leftButtonWasDown;

    /// <summary>
    /// Right-button press for short-click context menu vs camera orbit drag.
    /// </summary>
    Point? _rightButtonPressScreen;
    bool _rightButtonDragExceededSlop;
    bool _rightButtonWasDown;

    const int ClickPickSlopPixels = 5;

    readonly SliceContextMenu _sliceContextMenu = new();
    string _contextMenuStatus;
    Geometry.Vector3? _selectedHitVolumePoint;
    int[] _selectedHitVerts;

    bool _showInProgressSliceStatus = true;
    bool _showSectionReadySliceStatus = true;
    bool _showMinorIssueSliceStatus = true;
    bool _showWarningSliceStatus = true;
    bool _showCriticalSliceStatus = true;
    bool _showUntiledLinkedPairStatus = true;

    public bool ShowInProgressSliceStatus
    {
        get => _showInProgressSliceStatus;
        set
        {
            _showInProgressSliceStatus = value;
            foreach (var wrapView in WrapViews)
                wrapView.ShowInProgressSliceStatus = value;
        }
    }

    public bool ShowSectionReadySliceStatus
    {
        get => _showSectionReadySliceStatus;
        set
        {
            _showSectionReadySliceStatus = value;
            foreach (var wrapView in WrapViews)
                wrapView.ShowSectionReadySliceStatus = value;
        }
    }

    public bool ShowMinorIssueSliceStatus
    {
        get => _showMinorIssueSliceStatus;
        set
        {
            _showMinorIssueSliceStatus = value;
            foreach (var wrapView in WrapViews)
                wrapView.ShowMinorIssueSliceStatus = value;
        }
    }

    public bool ShowWarningSliceStatus
    {
        get => _showWarningSliceStatus;
        set
        {
            _showWarningSliceStatus = value;
            foreach (var wrapView in WrapViews)
                wrapView.ShowWarningSliceStatus = value;
        }
    }

    public bool ShowCriticalSliceStatus
    {
        get => _showCriticalSliceStatus;
        set
        {
            _showCriticalSliceStatus = value;
            foreach (var wrapView in WrapViews)
                wrapView.ShowCriticalSliceStatus = value;
        }
    }

    public bool ShowUntiledLinkedPairStatus
    {
        get => _showUntiledLinkedPairStatus;
        set
        {
            _showUntiledLinkedPairStatus = value;
            foreach (var wrapView in WrapViews)
                wrapView.ShowUntiledLinkedPairStatus = value;
        }
    }

    /// <summary>
    /// HUD text for the last click pick. Cleared when the click misses every mesh.
    /// </summary>
    string _selectionReadout = null;
    double _lastPickMilliseconds;

    BajajMultiOTVAssignmentView _selectedView;
    SliceGraphMeshModel _selectedSliceGraphModel;
    MeshModel<VertexPositionNormalColor> _selectedAssembledModel;
    Color[] _selectedOriginalColors;
    int? _selectedSliceZ;

    /// <summary>
    /// Ray through a screen pixel, expressed in volume space (model placement applied, scene Z-flip
    /// removed) so it can be tested directly against composite geometry.
    /// </summary>
    bool TryBuildVolumeRayAtScreen(float screenX, float screenY, out Geometry.Ray3D ray)
    {
        ray = default;

        Viewport viewport = scene3D.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return false;

        //MeshView multiplies each model's ModelMatrix by scene.World, so unprojecting with an identity
        //world yields final world space rather than any one model's space.
        Vector3 near = viewport.Unproject(new Vector3(screenX, screenY, 0f), scene3D.Projection, scene3D.View, Matrix.Identity);
        Vector3 far = viewport.Unproject(new Vector3(screenX, screenY, 1f), scene3D.Projection, scene3D.View, Matrix.Identity);

        //Undo the optional Z reflection so the ray lives in the same space as the mesh vertices.  The
        //reflection preserves distance, so hit distances remain meaningful.
        Matrix inverseWorld = Matrix.Invert(scene3D.World);
        near = Vector3.Transform(near, inverseWorld);
        far = Vector3.Transform(far, inverseWorld);

        Vector3 direction = far - near;
        if (direction.LengthSquared() <= float.Epsilon)
            return false;

        ray = new Geometry.Ray3D(
            new Geometry.Vector3(near.X, near.Y, near.Z),
            new Geometry.Vector3(direction.X, direction.Y, direction.Z));
        return true;
    }

    /// <summary>
    /// Restores vertex colors on the previously selected mesh and clears the selection HUD.
    /// </summary>
    void ClearMeshSelection()
    {
        if (_selectedOriginalColors != null)
        {
            if (_selectedSliceGraphModel != null)
            {
                Color[] restore = _selectedOriginalColors;
                _selectedSliceGraphModel.EditWorkingVertexColors(_ => restore);
            }
            else if (_selectedAssembledModel?.Vertices != null)
            {
                int n = Math.Min(_selectedAssembledModel.Vertices.Length, _selectedOriginalColors.Length);
                for (int i = 0; i < n; i++)
                    _selectedAssembledModel.Vertices[i].Color = _selectedOriginalColors[i];
                _selectedAssembledModel.InvalidateBuffers();
            }
        }

        _selectedView = null;
        _selectedSliceGraphModel = null;
        _selectedAssembledModel = null;
        _selectedOriginalColors = null;
        _selectedSliceZ = null;
        _selectionReadout = null;
        _selectedHitVolumePoint = null;
        _selectedHitVerts = null;
    }

    static Color InvertColor(Color color) =>
        new((byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B), color.A);

    /// <summary>
    /// Dominant discrete Z among the hit triangle's composite vertices. Wall faces that span two
    /// sections still resolve to one slice so only one band is inverted.
    /// </summary>
    static int ResolveSelectedSliceZ(Mesh3D<MorphMeshVertex> composite, int[] iVerts)
    {
        Dictionary<int, int> counts = [];
        foreach (int iVert in iVerts)
        {
            if (iVert < 0 || iVert >= composite.Vertices.Count)
                continue;

            int z = (int)Math.Round(composite[iVert].Position.Z);
            counts[z] = counts.TryGetValue(z, out int c) ? c + 1 : 1;
        }

        int bestZ = 0;
        int bestCount = -1;
        foreach (var pair in counts)
        {
            if (pair.Value <= bestCount)
                continue;
            bestCount = pair.Value;
            bestZ = pair.Key;
        }

        return bestZ;
    }

    /// <summary>
    /// Invert colors for every display vertex whose composite Z matches the selected slice.
    /// </summary>
    void ApplySliceSelectionHighlight(BajajMultiOTVAssignmentView view, int[] iVerts)
    {
        ClearMeshSelection();

        var composite = view.meshAssemblyPlan?.Root?.MeshModel?.composite;
        if (composite is null)
            return;

        int sliceZ = ResolveSelectedSliceZ(composite, iVerts);
        var rootModel = view.meshAssemblyPlan?.Root?.MeshModel;

        if (rootModel != null)
        {
            Color[] originals = null;
            bool applied = rootModel.EditWorkingVertexColors(working =>
            {
                originals = new Color[working.Count];
                Color[] next = new Color[working.Count];
                int vertCount = Math.Min(working.Count, composite.Vertices.Count);
                for (int i = 0; i < working.Count; i++)
                    originals[i] = working[i].Color;

                for (int i = 0; i < working.Count; i++)
                    next[i] = working[i].Color;

                for (int i = 0; i < vertCount; i++)
                {
                    if ((int)Math.Round(composite[i].Position.Z) != sliceZ)
                        continue;
                    next[i] = InvertColor(originals[i]);
                }

                return next;
            });

            if (!applied || originals is null)
                return;

            _selectedView = view;
            _selectedSliceGraphModel = rootModel;
            _selectedOriginalColors = originals;
            _selectedSliceZ = sliceZ;
            return;
        }

        if (!view.TryGetSelectableDisplayModel(out var model, out _) || model.Vertices is null)
            return;

        int assembledCount = Math.Min(model.Vertices.Length, composite.Vertices.Count);
        Color[] assembledOriginals = new Color[model.Vertices.Length];
        for (int i = 0; i < model.Vertices.Length; i++)
            assembledOriginals[i] = model.Vertices[i].Color;

        for (int i = 0; i < assembledCount; i++)
        {
            if ((int)Math.Round(composite[i].Position.Z) != sliceZ)
                continue;
            model.Vertices[i].Color = InvertColor(assembledOriginals[i]);
        }

        model.InvalidateBuffers();
        _selectedView = view;
        _selectedAssembledModel = model;
        _selectedOriginalColors = assembledOriginals;
        _selectedSliceZ = sliceZ;
    }

    /// <summary>
    /// Hit-test at a screen pixel. Misses clear the selection display.
    /// Prefers nested (non-top-level) meshes when both a translucent parent shell and a child
    /// intersect the ray, matching what the user sees through the shell.
    /// </summary>
    /// <returns>True when a composite face was selected.</returns>
    bool PickMeshAtScreen(float screenX, float screenY)
    {
        if (!TryBuildVolumeRayAtScreen(screenX, screenY, out Geometry.Ray3D ray))
        {
            ClearMeshSelection();
            return false;
        }

        Stopwatch timer = Stopwatch.StartNew();

        BajajMultiOTVAssignmentView hitChildView = null;
        int[] hitChildVerts = null;
        double nearestChild = double.MaxValue;

        BajajMultiOTVAssignmentView hitTopView = null;
        int[] hitTopVerts = null;
        double nearestTop = double.MaxValue;

        foreach (var wrapView in WrapViews)
        {
            if (wrapView is null)
                continue;

            if (!wrapView.TryPickCompositeFace(ray, out int[] iVerts, out double distance))
                continue;

            if (!wrapView.IsTopLevelStructure)
            {
                if (distance >= nearestChild)
                    continue;
                nearestChild = distance;
                hitChildVerts = iVerts;
                hitChildView = wrapView;
            }
            else
            {
                if (distance >= nearestTop)
                    continue;
                nearestTop = distance;
                hitTopVerts = iVerts;
                hitTopView = wrapView;
            }
        }

        BajajMultiOTVAssignmentView hitView = hitChildView ?? hitTopView;
        int[] hitVerts = hitChildView != null ? hitChildVerts : hitTopVerts;
        double hitDistance = hitChildView != null ? nearestChild : nearestTop;

        timer.Stop();
        _lastPickMilliseconds = timer.Elapsed.TotalMilliseconds;

        if (hitView is null || hitVerts is null)
        {
            ClearMeshSelection();
            return false;
        }

        ApplySliceSelectionHighlight(hitView, hitVerts);
        _selectedHitVerts = hitVerts;
        _selectedHitVolumePoint = ray.PointAt(hitDistance);
        _selectionReadout = DescribeHit(hitView, hitVerts, _selectedSliceZ);
        return true;
    }

    /// <summary>
    /// MonoGame's <see cref="Mouse.GetState"/> reports global button state. A click that activates another app
    /// still looks like press→release over whatever client pixel the cursor last mapped to, which was clearing or
    /// changing the mesh selection. Only accept gestures while focused and inside the client area.
    /// </summary>
    bool AcceptClientMouse(MouseState mouse)
    {
        if (_window is not null && !_window.IsActive)
            return false;

        if (scene3D is null)
            return mouse.X >= 0 && mouse.Y >= 0;

        return mouse.X >= 0 && mouse.Y >= 0
            && mouse.X < scene3D.Viewport.Width
            && mouse.Y < scene3D.Viewport.Height;
    }

    /// <summary>
    /// Sync press trackers to the current buttons without arming a click, so a focus-stealing press cannot
    /// complete as a pick when the button is later released.
    /// </summary>
    void DiscardPendingClickGestures(MouseState mouse)
    {
        _leftButtonWasDown = mouse.LeftButton == ButtonState.Pressed;
        _leftButtonPressScreen = null;
        _leftButtonDragExceededSlop = false;
        _rightButtonWasDown = mouse.RightButton == ButtonState.Pressed;
        _rightButtonPressScreen = null;
        _rightButtonDragExceededSlop = false;
    }

    /// <summary>
    /// Treat a short left-button press/release as a pick; longer pans still go to the camera only.
    /// </summary>
    void UpdateClickPickInput(MouseState mouse)
    {
        if (!Draw3D || scene3D is null)
            return;

        if (!AcceptClientMouse(mouse))
        {
            DiscardPendingClickGestures(mouse);
            return;
        }

        bool leftDown = mouse.LeftButton == ButtonState.Pressed;

        if (leftDown && !_leftButtonWasDown)
        {
            _leftButtonPressScreen = new Point(mouse.X, mouse.Y);
            _leftButtonDragExceededSlop = false;
        }

        if (leftDown && _leftButtonPressScreen.HasValue)
        {
            int dx = mouse.X - _leftButtonPressScreen.Value.X;
            int dy = mouse.Y - _leftButtonPressScreen.Value.Y;
            if ((dx * dx) + (dy * dy) > ClickPickSlopPixels * ClickPickSlopPixels)
                _leftButtonDragExceededSlop = true;
        }

        if (!leftDown && _leftButtonWasDown && _leftButtonPressScreen.HasValue && !_leftButtonDragExceededSlop)
        {
            //Pick at the press pixel; camera may have nudged during the click frame.
            Point press = _leftButtonPressScreen.Value;
            PickMeshAtScreen(press.X, press.Y);
        }

        if (!leftDown)
        {
            _leftButtonPressScreen = null;
            _leftButtonDragExceededSlop = false;
        }

        _leftButtonWasDown = leftDown;
    }

    /// <summary>
    /// Short right-click opens a HUD context menu on the picked face; drag still orbits the camera.
    /// </summary>
    void UpdateContextMenuInput(MouseState mouse)
    {
        if (!Draw3D || scene3D is null)
            return;

        if (!AcceptClientMouse(mouse))
        {
            DiscardPendingClickGestures(mouse);
            return;
        }

        int vpW = scene3D.Viewport.Width;
        int vpH = scene3D.Viewport.Height;
        if (_sliceContextMenu.Update(mouse, vpW, vpH))
            return;

        bool rightDown = mouse.RightButton == ButtonState.Pressed;

        if (rightDown && !_rightButtonWasDown)
        {
            _rightButtonPressScreen = new Point(mouse.X, mouse.Y);
            _rightButtonDragExceededSlop = false;
        }

        if (rightDown && _rightButtonPressScreen.HasValue)
        {
            int dx = mouse.X - _rightButtonPressScreen.Value.X;
            int dy = mouse.Y - _rightButtonPressScreen.Value.Y;
            if ((dx * dx) + (dy * dy) > ClickPickSlopPixels * ClickPickSlopPixels)
                _rightButtonDragExceededSlop = true;
        }

        if (!rightDown && _rightButtonWasDown && _rightButtonPressScreen.HasValue && !_rightButtonDragExceededSlop)
        {
            Point press = _rightButtonPressScreen.Value;
            if (PickMeshAtScreen(press.X, press.Y))
                OpenSliceContextMenu(press);
            else
                _sliceContextMenu.Close();
        }

        if (!rightDown)
        {
            _rightButtonPressScreen = null;
            _rightButtonDragExceededSlop = false;
        }

        _rightButtonWasDown = rightDown;
    }

    void OpenSliceContextMenu(Point screenAnchor)
    {
        List<(string Label, Action Action, bool Enabled)> items = [];

        List<ulong> locationIds = CollectHitLocationIds(_selectedView, _selectedHitVerts, _selectedSliceZ);
        bool haveVolume = VikingDeeplinkLauncher.TryGetVolumeUrlFromODataEndpoint(
            Program.options?.EndpointUri, out string volumeUrl);

        if (locationIds.Count > 0)
        {
            foreach (ulong locationId in locationIds)
            {
                ulong id = locationId;
                items.Add((
                    $"Open in Viking: Location {id}",
                    () => LaunchVikingForLocation(volumeUrl, haveVolume, id),
                    haveVolume));
                items.Add((
                    $"Copy Location ID: {id}",
                    () => CopyLocationIdToClipboard(id),
                    true));
            }
        }
        else if (_selectedHitVolumePoint.HasValue)
        {
            Geometry.Vector3 hit = _selectedHitVolumePoint.Value;
            int z = _selectedSliceZ ?? (int)Math.Round(hit.Z);
            items.Add((
                $"Open in Viking: ({hit.X:F0}, {hit.Y:F0}, {z})",
                () => LaunchVikingForCoordinates(volumeUrl, haveVolume, hit.X, hit.Y, z),
                haveVolume));
        }
        else
        {
            items.Add(("Open in Viking (no location)", () => { }, false));
        }

        if (!haveVolume)
            _contextMenuStatus = "No -e endpoint; cannot map volume URL for Viking.";

        _sliceContextMenu.Open(screenAnchor, items);
    }

    void LaunchVikingForLocation(string volumeUrl, bool haveVolume, ulong locationId)
    {
        if (!haveVolume)
        {
            _contextMenuStatus = "No -e endpoint; cannot open Viking.";
            return;
        }

        string url = VikingDeeplinkLauncher.BuildOpenLocationUrl(volumeUrl, locationId);
        if (VikingDeeplinkLauncher.TryLaunch(url, out string error))
            _contextMenuStatus = $"Opened Viking at Location {locationId}";
        else
            _contextMenuStatus = error;
    }

    void LaunchVikingForCoordinates(string volumeUrl, bool haveVolume, double x, double y, double z)
    {
        if (!haveVolume)
        {
            _contextMenuStatus = "No -e endpoint; cannot open Viking.";
            return;
        }

        string url = VikingDeeplinkLauncher.BuildOpenCoordinateUrl(volumeUrl, x, y, z);
        if (VikingDeeplinkLauncher.TryLaunch(url, out string error))
            _contextMenuStatus = $"Opened Viking at ({x:F0}, {y:F0}, {z:F0})";
        else
            _contextMenuStatus = error;
    }

    static void CopyLocationIdToClipboard(ulong locationId)
    {
        try
        {
            System.Windows.Clipboard.SetText(locationId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[SliceContextMenu] Clipboard failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Location IDs on the hit face. Prefers vertices on the selected slice Z when any are annotated.
    /// </summary>
    static List<ulong> CollectHitLocationIds(BajajMultiOTVAssignmentView view, int[] iVerts, int? preferredSliceZ)
    {
        List<ulong> all = [];
        List<ulong> preferred = [];
        if (view is null || iVerts is null)
            return all;

        var composite = view.meshAssemblyPlan?.Root?.MeshModel?.composite;
        foreach (int iVert in iVerts)
        {
            if (!view.TryGetVertexLocationID(iVert, out ulong locationID))
                continue;

            if (!all.Contains(locationID))
                all.Add(locationID);

            if (preferredSliceZ.HasValue
                && composite is not null
                && iVert >= 0
                && iVert < composite.Vertices.Count
                && (int)Math.Round(composite[iVert].Position.Z) == preferredSliceZ.Value
                && !preferred.Contains(locationID))
            {
                preferred.Add(locationID);
            }
        }

        List<ulong> result = preferred.Count > 0 ? preferred : all;
        result.Sort();
        return result;
    }

    /// <summary>
    /// Human readable provenance of a picked face.  Cap and medial-axis vertices are named explicitly:
    /// they have no annotation, and silently dropping them would make a two-ID wall triangle
    /// indistinguishable from a triangle that touches a cap.
    /// </summary>
    static string DescribeHit(BajajMultiOTVAssignmentView view, int[] iVerts, int? sliceZ)
    {
        List<ulong> locationIDs = [];
        int unannotated = 0;

        foreach (int iVert in iVerts)
        {
            if (view.TryGetVertexLocationID(iVert, out ulong locationID))
            {
                if (!locationIDs.Contains(locationID))
                    locationIDs.Add(locationID);
            }
            else
            {
                unannotated++;
            }
        }

        locationIDs.Sort();

        StringBuilder text = new();
        text.Append($"Structure {view.Graph?.StructureID}");
        if (sliceZ.HasValue)
            text.Append($"  Slice Z {sliceZ.Value}");
        text.Append("  Locations ");
        text.Append(locationIDs.Count == 0 ? "none" : string.Join(", ", locationIDs));
        if (unannotated > 0)
            text.Append($" (+{unannotated} cap/medial)");

        return text.ToString();
    }

    public async Task Init(MonoTestbed window)
    {
        _window = window;
        this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

        this.scene3D = new Scene3D(window.GraphicsDevice.Viewport, new Camera3D())
        {
            MaxDrawDistance = 1000000,
            MinDrawDistance = 1,
            World = ViewZAxisWorld
        };

        Input.UpdateTrackers();
        Input.Keyboard.Update(Keyboard.GetState());

        Console.Write("Begin OData fetch");

        if (Program.options.Timings)
        {
            MeshPhaseTimings.Reset();
            await MeshPhaseTimings.RecordCpuSnapshotAsync("pre-OData");
        }

        Task<MorphologyGraph> boundary_graph_task = null;
        Task<MorphologyGraph> structure_graph_task = null;
        using (MeshPhaseTimings.Measure(MeshPhase.ODataFetch))
        {
        if (Program.options.BoundaryIDs.Any() && Program.options.EndpointUri != null)
        {
            Uri endpoint = Program.options.EndpointUri;
            boundary_graph_task = AnnotationVizLib.OData.ODataMorphologyFactory.FromODataByTypeIDsAsync(
                [.. Program.options.BoundaryIDs.Select(id => (long)id)], endpoint, false);
        }

        if (Program.options.StructureIDs.Any() && Program.options.EndpointUri != null)
        {
            Console.WriteLine(" From command line parameters");

            Uri endpoint = Program.options.EndpointUri;
            structure_graph_task = Task.Run(() => AnnotationVizLib.OData.ODataMorphologyFactory.FromOData(
                [.. Program.options.StructureIDs.Select(id => (long)id)], Program.options.IncludeChildren, endpoint));
        }
        else
        {
            Console.WriteLine("From hard coded test case (no command line paramters)");

            //Endpoint.TEST (webdev.codepharm.net) has no DNS address record any more, so the previous default of
            //structure 476 there could not load at all. Structure 180 on RC1 is the whole-cell case this mode is
            //usually exercised against, and it is reachable.
            structure_graph_task = Task.Run(() => AnnotationVizLib.OData.ODataMorphologyFactory.FromOData(
                new long[] { 180 }, Program.options.IncludeChildren, DataSource.EndpointMap[Endpoint.RC1]));
        }

        //Overlap boundary and structure fetches when both are requested.
        if (boundary_graph_task is not null)
        {
            await Task.WhenAll(boundary_graph_task, structure_graph_task);
            MorphologyGraph boundary_graph = await boundary_graph_task;
            this.boundaryViewModels = BoundarySurfaceViewModel.CreateBoundarySurfaces(boundary_graph);
            this.boundaryView = CreateViewsForBoundaries(this.boundaryViewModels);
            Console.WriteLine(" Boundary view created");
            graph = await structure_graph_task;
        }
        else
        {
            graph = await structure_graph_task;
        }

        Console.WriteLine("End OData fetch");
        }

        if (Program.options.Timings)
        {
            await MeshPhaseTimings.RecordCpuSnapshotAsync("post-OData");
            MeshPhaseTimings.StartContinuousSampling();
        }

        await MorphologyRegistration.ApplyAsync(graph, Program.options);

        //graph = graph.Subgraphs.Values.First();


        //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(BasicBranchInteriorHole, DataSource.EndpointMap[ENDPOINT.RPC1]);
        //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(BasicBranchTroubleIDS, DataSource.EndpointMap[ENDPOINT.RPC1]);

        //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(BasicInteriorHoleOverAdjacentExteriorRing, DataSource.EndpointMap[ENDPOINT.RPC1]);
        //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(HorseshoeInteriorHoleOverAdjacentExteriorRing, DataSource.EndpointMap[ENDPOINT.RPC1]);

        /////////////
        ///This is the major test of mesh generation that covers as many cases as I could think of
        //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(NightmareTroubleIDS, DataSource.EndpointMap[ENDPOINT.TEST]);
        //////////////

        //BajajMeshGenerator.ConvertToMeshGraph(graph);

        /*
        double MaxZ = 750;//graph.Nodes.Values.Max(n => n.Z);
        double MinZ = 500;//graph.Nodes.Values.Min(n => n.Z);

        Debug.Assert(MaxZ > MinZ);

        MaxZ = MaxZ * graph.scale.Z.Value;
        MinZ = MinZ * graph.scale.Z.Value;

        foreach ( var subgraph in graph.Subgraphs.Values)
        {
            foreach (var node in subgraph.Nodes.Values.ToList())
            {
                if (node.Z < MinZ || node.Z > MaxZ)
                {
                    subgraph.RemoveNode(node.ID);
                }
            }
        }
        */

        if (window.Scene.RestoreCamera(TestMode.BAJAJMULTITEST) == false)
        {
            // 2D overlays use the same centered XY space as SliceGraph mesh geometry.
            window.Scene.Camera.LookAt = Vector2.Zero;

            //Fit both axes.  Scaling to width alone crops a cell taller than the viewport aspect allows.
            var viewport = window.GraphicsDevice.Viewport;
            window.Scene.Camera.Downsample = Math.Max(graph.BoundingBox.Width / Math.Max(1, viewport.Width),
                                                     graph.BoundingBox.Height / Math.Max(1, viewport.Height));
        }

        _failedSliceLog = new FailedSliceReport(ResolveFailedSlicesReproDirectory(), Program.RunStamp, DescribeRunForReport(graph));
        //Trace reaches the run's log file; without -v it does not reach the console, so say it there too.
        Trace.WriteLine($"Failed slices are appended to {_failedSliceLog.Path} as they occur");
        if (Program.options?.Verbose != true)
            Console.WriteLine($"Failed slices are appended to {_failedSliceLog.Path} as they occur");

        List<Task> meshGenTasks = [];
        QueueMeshViews(graph, meshGenTasks);

        //MonoTestbed skips Draw and Update entirely until this flag is set.  Setting it only after meshing
        //finished meant the first frame was drawn once every node was already complete, so the assembly bounding
        //box overlay - which exists to show meshing in progress - had removed every box before it was ever drawn.
        //The annotations are downloaded and the mesh tasks are running by this point, so the views the game loop
        //reads all exist and only their contents change as slices complete.
        FrameCameraOnRenderedMesh(window);
        _initialized = true;

        await Task.WhenAll(meshGenTasks);

        if (Program.options.Timings)
        {
            MeshPhaseTimings.StopContinuousSampling();
            await MeshPhaseTimings.RecordCpuSnapshotAsync("mesh complete");
        }

        FrameCameraOnRenderedMesh(window);

        //Save the output in a specific place upon request in the command line parameters
        if (string.IsNullOrWhiteSpace(Program.options.OutputPath) == false)
        {
            using (MeshPhaseTimings.Measure(MeshPhase.Export))
            {
            try
            {
                SaveMeshes("BajajMultitest", Program.options.OutputPath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not save scene output mesh to {Program.options.OutputPath}.\nException:{e}");
            }

            foreach (var wrapView in WrapViews)
            {
                try
                {
                    if (wrapView.meshAssemblyPlan != null && wrapView.meshAssemblyPlan.MeshAssembledEvent.IsSet)
                        SaveMesh(wrapView.meshAssemblyPlan.Root.MeshModel.composite, PlacementTranslation(wrapView), wrapView.Graph, Program.options.OutputPath);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Could not save Structure #{wrapView?.Graph?.StructureID} output mesh to {Program.options.OutputPath}.\nException:{e}");

                }
            }
            }
        }

        _failedSliceLog.Complete();

        if (Program.options.Timings)
            await MeshPhaseTimings.RecordCpuSnapshotAsync("end");

        Console.WriteLine($"All rendering complete");
        Console.WriteLine(MeshPhaseTimings.Report());

        if (Program.options.Quiet)
        {
            window.Exit();
        }
    }

    /// <summary>
    /// A whole cell can contain thousands of children, and each pipeline is itself internally parallel: it fans
    /// slice topology and Bajaj generation out over the thread pool. Cap concurrent pipelines so the shared
    /// <see cref="MeshParallelism.DegreeOfParallelism"/> budget is not multiplied by every child structure.
    /// Releasing the throttle before face generation (WS2) was measured slower on RC1 410 — keep the full hold.
    /// </summary>
    private static int MaxConcurrentMeshPipelines => Math.Max(1, MeshParallelism.DegreeOfParallelism);

    /// <summary>
    /// Starts Bajaj generation for every nested subgraph. Children share the parent cell's XY origin so their
    /// meshes sit on the cell instead of being recentered on each synapse bbox.
    ///
    /// Structures are queued largest-first (by location count) so the cell claims a pipeline and FaceSlots before
    /// a flood of tiny children. Pipelines are independent, so the shared semaphore cannot deadlock: nothing a
    /// holder waits on is itself queued behind the semaphore.
    /// </summary>
    private void QueueMeshViews(MorphologyGraph parent, List<Task> meshGenTasks, Geometry.Vector2? familyOrigin = null, SemaphoreSlim throttle = null)
    {
        throttle ??= new SemaphoreSlim(MaxConcurrentMeshPipelines);

        List<(MorphologyGraph Graph, Geometry.Vector2? Origin)> work = [];
        CollectMeshGraphs(parent, familyOrigin, work);

        foreach ((MorphologyGraph graph, Geometry.Vector2? origin) in work.OrderByDescending(w => w.Graph.Nodes.Count))
        {
            BajajMultiOTVAssignmentView wrapView = new(graph, origin) { FailedSliceLog = _failedSliceLog };
            AddWrapView(wrapView);
            meshGenTasks.Add(GenerateMeshThrottled(wrapView, throttle));
        }
    }

    private static void CollectMeshGraphs(MorphologyGraph parent, Geometry.Vector2? familyOrigin, List<(MorphologyGraph Graph, Geometry.Vector2? Origin)> work)
    {
        foreach (var subgraph in parent.Subgraphs.Values)
        {
            Geometry.Vector2? origin = familyOrigin;
            if (origin is null && subgraph.Nodes.Count > 0)
                origin = subgraph.NodesBoundingBox.CenterPoint.XY();

            if (subgraph.Nodes.Count > 0)
                work.Add((subgraph, origin));

            CollectMeshGraphs(subgraph, origin, work);
        }
    }

    private static Task GenerateMeshThrottled(BajajMultiOTVAssignmentView wrapView, SemaphoreSlim throttle) =>
        wrapView.GenerateMesh(throttle);

    public void Update()
    {
        PlayerIndex? InputSource = GamePadStateTracker.GetFirstConnectedController() ?? PlayerIndex.One;
        GamePadState gamePadState = GamePad.GetState(InputSource.Value);
        KeyboardState keyboardState = Keyboard.GetState();
        MouseState mouseState = Mouse.GetState();

        Input.Gamepad.Update(gamePadState);
        Input.Keyboard.Update(keyboardState);

        bool focused = _window is null || _window.IsActive;
        if (!focused)
            DiscardPendingClickGestures(mouseState);

        UpdateContextMenuInput(mouseState);

        if (!Draw3D)
            Input.CameraManipulator.Update(scene.Camera);
        else if (!_sliceContextMenu.IsOpen && focused)
        {
            Camera3DManipulator.Update(
                this.scene3D.Camera,
                scene3D.Viewport.Width,
                scene3D.Viewport.Height,
                keyboardState,
                mouseState,
                gamePadState);
        }

        UpdateClickPickInput(mouseState);

        bool toggleBoxes = Input.Gamepad.RightStick_Clicked || Input.Keyboard.Pressed(Keys.B);
        bool toggleCull = Input.Gamepad.LeftStick_Clicked || Input.Keyboard.Pressed(Keys.K);
        bool saveCurrent = Input.Gamepad.Back_Clicked || Input.Keyboard.Pressed(Keys.PrintScreen);
        bool aClicked = Input.Gamepad.A_Clicked;
        bool bClicked = Input.Gamepad.B_Clicked;
        bool yClicked = Input.Gamepad.Y_Clicked;
        bool xClicked = Input.Gamepad.X_Clicked;
        bool startClicked = Input.Gamepad.Start_Clicked;
        bool rightShoulder = Input.Gamepad.RightShoulder_Clicked;
        bool leftShoulder = Input.Gamepad.LeftShoulder_Clicked;

        foreach (var wrapView in WrapViews)
        {
            if (aClicked)
            {
                wrapView.iShownMesh = wrapView.iShownMesh.HasValue ? wrapView.iShownMesh.Value + 1 : 0;
                if (wrapView.iShownMesh.HasValue && wrapView.iShownMesh.Value >= wrapView.MeshViews.Count)
                {
                    wrapView.iShownMesh = null;
                }
            }

            if (bClicked)
            {
                wrapView.iShownLineView = wrapView.iShownLineView.HasValue ? wrapView.iShownLineView.Value + 1 : 0;
                if (wrapView.iShownLineView.HasValue && wrapView.iShownLineView.Value >= wrapView.listLineViews.Count)
                {
                    wrapView.iShownLineView = null;
                }

                Trace.WriteLine(wrapView.iShownLineView.ToString());
            }

            if (yClicked)
            {
                wrapView.iShownRegion = wrapView.iShownRegion.HasValue ? wrapView.iShownRegion.Value + 1 : 0;
                if (wrapView.iShownRegion.HasValue && wrapView.iShownRegion.Value >= wrapView.RegionViews.Count)
                {
                    wrapView.iShownRegion = null;
                }
            }

            if (xClicked)
            {
                wrapView.ShowCompletedVerticies = !wrapView.ShowCompletedVerticies;
            }

            if (startClicked && wrapView.IsGeneratingMesh == false)
            {
                if (ReferenceEquals(_selectedView, wrapView))
                    ClearMeshSelection();
                _ = wrapView.GenerateMesh();
            }

            if (rightShoulder)
            {
                if ((wrapView.VertexLabelType & (IndexLabelType.MESH | IndexLabelType.POLYGON)) == 0)
                {
                    wrapView.VertexLabelType |= IndexLabelType.MESH;
                }
                else if ((wrapView.VertexLabelType & IndexLabelType.POLYGON) > 0)
                {
                    wrapView.VertexLabelType = IndexLabelType.NONE;
                }
                else if ((wrapView.VertexLabelType & IndexLabelType.MESH) == 0)
                {
                    wrapView.VertexLabelType |= IndexLabelType.MESH;
                    wrapView.VertexLabelType ^= IndexLabelType.POLYGON;
                }
                else if ((wrapView.VertexLabelType & IndexLabelType.POLYGON) == 0)
                {
                    wrapView.VertexLabelType |= IndexLabelType.POLYGON;
                    wrapView.VertexLabelType ^= IndexLabelType.MESH;
                }
            }

            if (toggleBoxes)
            {
                wrapView.ShowAssemblyBoundingBoxes = !wrapView.ShowAssemblyBoundingBoxes;
                _hudDirty = true;
            }

            if (toggleCull)
            {
                wrapView.CullMode = wrapView.CullMode == CullMode.None ? CullMode.CullClockwiseFace : CullMode.None;
            }

            if (leftShoulder)
            {
                wrapView.ShowCompositeMesh = !wrapView.ShowCompositeMesh;
            }

            if (saveCurrent)
            {
                if (wrapView.meshAssemblyPlan != null && wrapView.meshAssemblyPlan.MeshAssembledEvent.IsSet)
                    SaveMesh(wrapView.meshAssemblyPlan.Root.MeshModel.composite, PlacementTranslation(wrapView), wrapView.Graph);
            }
        }

        if (Input.Keyboard.Pressed(Keys.R))
        {
            ShowCriticalSliceStatus = !ShowCriticalSliceStatus;
            _hudDirty = true;
        }

        if (Input.Keyboard.Pressed(Keys.F) && _window != null)
        {
            FrameCameraOnRenderedMesh(_window);
        }

        if (Input.Keyboard.Pressed(Keys.I) && Program.options != null)
        {
            Program.options.InvertZ = !Program.options.InvertZ;
            if (scene3D != null)
                scene3D.World = ViewZAxisWorld;
            if (_window != null)
                FrameCameraOnRenderedMesh(_window);
            _hudDirty = true;
        }

        if (Input.Gamepad.Back_Clicked || (Input.Keyboard.Pressed(Keys.S) && (Input.Keyboard.Pressed(Keys.LeftControl) || Input.Keyboard.Pressed(Keys.RightControl))))
        {
            TrySaveMesh();
        }
    }

    public void Draw(MonoTestbed window)
    {
        MonoTestbed.SyncViewport(scene, window.GraphicsDevice);
        MonoTestbed.SyncViewport(scene3D, window.GraphicsDevice);
        scene3D.World = ViewZAxisWorld;
        window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, MonoTestbed.DefaultBackground, 1.0f, 0);

        if (Draw3D)
        {
            //1) Top-level back faces write depth only so exterior children behind the cell are occluded.
            //2) Opaque children.
            //3) Translucent top-level front faces (nearest shell surface + alpha over interiors).
            IReadOnlyList<BajajMultiOTVAssignmentView> views = WrapViews;
            foreach (var wrapView in views)
            {
                if (wrapView != null && wrapView.IsTopLevelStructure)
                    wrapView.Draw3DShellDepthOccluder(window, scene3D);
            }

            foreach (var wrapView in views)
            {
                if (wrapView != null && !wrapView.IsTopLevelStructure)
                    wrapView.Draw3D(window, scene3D);
            }

            foreach (var wrapView in views)
            {
                if (wrapView != null && wrapView.IsTopLevelStructure)
                    wrapView.Draw3D(window, scene3D);
            }
        }
        else
        {
            foreach (var wrapView in WrapViews)
                wrapView?.Draw(window, scene);
        }

        if (boundaryView != null)
        {
            MeshView<VertexPositionNormalColor>.Draw(window.GraphicsDevice, scene3D,
                                window.basicEffect, CullMode.None, FillMode.Solid, boundaryView.models);
        }

        if (Draw3D)
        {
            Draw3DDebugHud(window);
        }
    }

    string _hudText;
    Vector3 _hudCamPosition;
    Vector3 _hudLookAt;
    double _hudYaw;
    double _hudPitch;
    string _hudSelectionReadout;
    string _hudContextMenuStatus;
    bool _hudInvertZ;
    SliceFailureCounts _hudDropped;
    bool _hudShowBoxes;
    bool _hudDirty = true;

    private void Draw3DDebugHud(MonoTestbed window)
    {
        var cam = scene3D.Camera;
        bool invertZ = Program.options?.InvertZ == true;
        bool showBoxes = false;
        foreach (var wrapView in WrapViews)
        {
            if (wrapView.ShowAssemblyBoundingBoxes)
            {
                showBoxes = true;
                break;
            }
        }

        SliceFailureCounts dropped = CountFailedSlices();

        bool cameraChanged = cam.Position != _hudCamPosition
            || cam.LookAt != _hudLookAt
            || cam.Yaw != _hudYaw
            || cam.Pitch != _hudPitch;
        bool contentChanged = _hudDirty
            || cameraChanged
            || !ReferenceEquals(_selectionReadout, _hudSelectionReadout)
            || !ReferenceEquals(_contextMenuStatus, _hudContextMenuStatus)
            || invertZ != _hudInvertZ
            || dropped != _hudDropped
            || showBoxes != _hudShowBoxes;

        if (contentChanged || _hudText is null)
        {
            float camDistance = (cam.Position - cam.LookAt).Length();
            StringBuilder hud = new();
            hud.AppendLine($"Cam ({cam.Position.X:F0}, {cam.Position.Y:F0}, {cam.Position.Z:F0})");
            hud.AppendLine($"LookAt ({cam.LookAt.X:F0}, {cam.LookAt.Y:F0}, {cam.LookAt.Z:F0})");
            hud.AppendLine($"Yaw {cam.Yaw * 180 / Math.PI:F1} deg  Pitch {cam.Pitch * 180 / Math.PI:F1} deg  Dist {camDistance:F0}");
            hud.AppendLine(invertZ ? "Z inverted (I to toggle)" : "Z volume (I / --invert-z)");
            if (graph?.BoundingBox != null)
            {
                var bbox = graph.BoundingBox;
                hud.AppendLine($"Mesh XY +/-{bbox.Width / 2:F0}  Z {bbox.MinVals[2]:F0}-{bbox.MaxVals[2]:F0}");
            }

            if (_selectionReadout != null)
            {
                hud.AppendLine(_selectionReadout);
                hud.AppendLine($"Pick {_lastPickMilliseconds:F1} ms");
            }

            if (_contextMenuStatus != null)
                hud.AppendLine(_contextMenuStatus);

            if (showBoxes)
            {
                hud.AppendLine("Slice status: View menu or B=master  R=red");
                hud.AppendLine("  gray=in progress  blue=section ready");
                hud.AppendLine("  yellow=minor  orange=holes/winding  red=non-manifold");
            }

            if (dropped.Total > 0)
                hud.AppendLine($"WARNING: {dropped.Total} slice(s) failed: {dropped}");

            _hudText = hud.ToString();
            _hudCamPosition = cam.Position;
            _hudLookAt = cam.LookAt;
            _hudYaw = cam.Yaw;
            _hudPitch = cam.Pitch;
            _hudSelectionReadout = _selectionReadout;
            _hudContextMenuStatus = _contextMenuStatus;
            _hudInvertZ = invertZ;
            _hudDropped = dropped;
            _hudShowBoxes = showBoxes;
            _hudDirty = false;
        }

        window.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        window.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        const float hudScale = 0.3f;
        window.spriteBatch.DrawString(
            window.fontArial,
            _hudText,
            new Vector2(8, window.MenuBarHeight + 8),
            Color.Yellow,
            rotation: 0f,
            origin: Vector2.Zero,
            scale: hudScale,
            effects: SpriteEffects.None,
            layerDepth: 0f);

        window.spriteBatch.End();

        _sliceContextMenu.Draw(window.spriteBatch, window.fontArial, window.WhitePixel, window.GraphicsDevice.Viewport.Height);
    }

    /// <summary>
    /// Per-kind failed slice counts across every structure.  Before a plan exists only topology failures are known.
    /// </summary>
    readonly record struct SliceFailureCounts(int Topology, int FaceGenerationException, int InvalidSurface, int UntiledLinkedPair)
    {
        public int Total => Topology + FaceGenerationException + InvalidSurface + UntiledLinkedPair;

        public override string ToString()
        {
            List<string> parts = new(4);
            if (Topology > 0)
                parts.Add($"{Topology} no topology");
            if (FaceGenerationException > 0)
                parts.Add($"{FaceGenerationException} face-gen threw");
            if (InvalidSurface > 0)
                parts.Add($"{InvalidSurface} invalid surface");
            if (UntiledLinkedPair > 0)
                parts.Add($"{UntiledLinkedPair} untiled linked");
            return string.Join(", ", parts);
        }
    }

    SliceFailureCounts CountFailedSlices()
    {
        int topology = 0, threw = 0, invalid = 0, untiled = 0;
        foreach (var wrapView in WrapViews)
        {
            var plan = wrapView.meshAssemblyPlan;
            if (plan is null)
            {
                topology += wrapView.FailedTopologySlices.Count;
                continue;
            }

            foreach (FailedSliceReproRecord record in plan.FailedSlicesForRepro.Values)
            {
                switch (record.Kind)
                {
                    case SliceFailureKind.Topology: topology++; break;
                    case SliceFailureKind.FaceGenerationException: threw++; break;
                    case SliceFailureKind.UntiledLinkedPair: untiled++; break;
                    default: invalid++; break;
                }
            }
        }

        return new SliceFailureCounts(topology, threw, invalid, untiled);
    }

    /// <summary>
    /// Failed-slice log for this run; created in <see cref="Init"/> before any mesh task starts so no failure is
    /// missed, and completed after the last structure finishes.
    /// </summary>
    FailedSliceReport _failedSliceLog;

    /// <summary>
    /// Header comments for <see cref="FailedSliceReport"/>: what was meshed and how, so two report files can be
    /// compared knowing whether the inputs were the same.
    /// </summary>
    static IEnumerable<string> DescribeRunForReport(MorphologyGraph graph)
    {
        yield return $"Command line: {string.Join(' ', Environment.GetCommandLineArgs().Skip(1))}";
        yield return $"Endpoint: {Program.options?.EndpointUri?.ToString() ?? "(hard coded test case)"}";

        ulong[] roots = [.. graph.Subgraphs.Keys.OrderBy(id => id)];
        yield return $"Structures: {string.Join(' ', roots)}" + (Program.options?.IncludeChildren == false ? " (children excluded)" : " (with children)");

        int structureCount = CountStructures(graph);
        yield return $"Structure count incl. children: {structureCount}";
        ContourSimplifyOptions simplify = Program.options?.ContourSimplify ?? ContourSimplifyOptions.Default;
        yield return $"Contour simplify: 1 vertex per {simplify.MinNmPerVertex} nm gate, tolerance {simplify.ToleranceNm} nm";
    }

    static int CountStructures(MorphologyGraph graph) =>
        graph.Subgraphs.Values.Sum(sub => 1 + CountStructures(sub));

    static string ResolveFailedSlicesReproDirectory() =>
        string.IsNullOrWhiteSpace(Program.options?.OutputPath)
            ? Directory.GetCurrentDirectory()
            : Program.options.OutputPath;

    /// <summary>
    /// Union bounding box of assembled mesh geometry, in volume space.
    ///
    /// Falls back to the annotation bounds when no mesh exists yet.  The camera is framed as soon as meshing
    /// starts, which is before the first slice completes, and an unframed camera makes an empty view
    /// indistinguishable from geometry that is off screen.
    /// </summary>
    bool TryGetSceneBounds(out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);
        bool any = false;

        foreach (var wrapView in WrapViews)
        {
            if (!wrapView.TryGetRenderedMeshBounds(out Vector3 viewMin, out Vector3 viewMax))
                continue;

            min = Vector3.Min(min, viewMin);
            max = Vector3.Max(max, viewMax);
            any = true;
        }

        if (any)
            return true;

        if (graph is null)
            return false;

        var bbox = graph.BoundingBox;
        min = new Vector3((float)bbox.MinVals[0], (float)bbox.MinVals[1], (float)bbox.MinVals[2]);
        max = new Vector3((float)bbox.MaxVals[0], (float)bbox.MaxVals[1], (float)bbox.MaxVals[2]);
        return true;
    }

    /// <summary>
    /// Aim the 3D camera at the centroid of the scene bounds and back it off far enough to frame them.
    /// </summary>
    void FrameCameraOnRenderedMesh(MonoTestbed window)
    {
        if (!TryGetSceneBounds(out Vector3 min, out Vector3 max))
            return;

        MonoTestbed.SyncViewport(scene3D, window.GraphicsDevice);

        Vector3 worldMin = Vector3.Transform(min, ViewZAxisWorld);
        Vector3 worldMax = Vector3.Transform(max, ViewZAxisWorld);
        min = Vector3.Min(worldMin, worldMax);
        max = Vector3.Max(worldMin, worldMax);

        Vector3 center = (min + max) * 0.5f;
        Vector3 halfExtent = (max - min) * 0.5f;
        if (halfExtent.LengthSquared() < float.Epsilon)
            halfExtent = Vector3.One;

        //Same viewing direction as before; the camera sits on this ray at the fitted distance.
        Vector3 direction = Vector3.Normalize(new Vector3(-1f, -0.35f, 0.2f));

        //Match the basis CreateLookAt will use once Position and LookAt are set below.  zaxis points from the
        //target toward the camera; x/y span the screen horizontally and vertically.
        Vector3 zaxis = direction;
        Vector3 cameraUp = Vector3.UnitZ;
        Vector3 xaxis = Vector3.Cross(cameraUp, zaxis);
        if (xaxis.LengthSquared() < 1e-6f)
        {
            cameraUp = Vector3.UnitY;
            xaxis = Vector3.Cross(cameraUp, zaxis);
        }
        xaxis = Vector3.Normalize(xaxis);
        Vector3 yaxis = Vector3.Normalize(Vector3.Cross(zaxis, xaxis));

        //Project the box half-extents onto the view axes.  The old bounding-sphere fit divided by sin(fov/2),
        //which backs the camera off by about 2x for compact shapes at the default 60 deg vertical field of view.
        float extentHorizontal = Math.Abs(halfExtent.X * xaxis.X) + Math.Abs(halfExtent.Y * xaxis.Y) + Math.Abs(halfExtent.Z * xaxis.Z);
        float extentVertical = Math.Abs(halfExtent.X * yaxis.X) + Math.Abs(halfExtent.Y * yaxis.Y) + Math.Abs(halfExtent.Z * yaxis.Z);

        //FieldOfView is vertical. A window wider than it is tall constrains vertically, but a narrow one
        //constrains horizontally, so fit each screen axis separately and take the larger distance.
        float halfFovVertical = scene3D.FieldOfView * 0.5f;
        float aspect = scene3D.Viewport.Height > 0
            ? scene3D.Viewport.Width / (float)scene3D.Viewport.Height
            : 1f;
        float halfFovHorizontal = (float)Math.Atan(Math.Tan(halfFovVertical) * aspect);

        const float FrameMargin = 1.05f;
        float distanceVertical = extentVertical / (float)Math.Tan(halfFovVertical);
        float distanceHorizontal = extentHorizontal / (float)Math.Tan(halfFovHorizontal);
        float distance = Math.Max(Math.Max(distanceVertical, distanceHorizontal), 1f) * FrameMargin;

        float enclosingRadius = Math.Max(extentHorizontal, extentVertical);

        //Leave the near plane where the scene was configured.  Pulling it out to bracket the geometry buys depth
        //precision but clips away everything in front of the fitted distance, so flying in toward a surface makes
        //it vanish long before the camera reaches it.
        scene3D.MaxDrawDistance = Math.Max(scene3D.MaxDrawDistance, (distance + enclosingRadius) * 2f);

        scene3D.Camera.Position = center + (direction * distance);
        scene3D.Camera.LookAt = center;
    }

    public void UnloadContent(MonoTestbed window)
    {
        foreach (var wrapView in WrapViews)
        {
            wrapView?.OnUnloadContent();
        }

        window.Scene?.SaveCamera(TestMode.BAJAJMULTITEST);
    }

    private string CleanOutputPath(string outputPath) => throw new NotImplementedException();

    private static string DefaultOutputPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Morphology");

    /// <summary>
    /// Offset applied to boundary type IDs so they cannot collide with cell structure IDs on Collada export.
    /// Two extra decimal digits past the largest structure ID.
    /// </summary>
    private static ulong StructureTypeIdOffset(ulong maxId)
    {
        if (maxId == 0)
            return 100UL;

        int digits = (int)Math.Floor(Math.Log10(maxId)) + 1;
        return (ulong)Math.Pow(10, digits + 2);
    }

    public bool TrySaveMesh()
    {
        if (!Initialized)
            return false;

        SaveMeshes("BajajMultitest");
        return true;
    }

    public void SaveMeshes(string title, string outputDir = null)
    {
        outputDir = outputDir ?? DefaultOutputPath;

        BasicColladaView ColladaView = new(graph.scale.X, null)
        {
            SceneTitle = title
        };

        ulong max_id = WrapViews.Count == 0 ? 0UL : WrapViews.Max(wv => wv.Graph.StructureID);
        ulong structure_type_id_adjustment = StructureTypeIdOffset(max_id);

        foreach (var boundary in boundaryViewModels)
        {
            RgbaColor color = RgbaColor.FromArgb(0x7F, 0x7F, 0x7F, 0x7F);
            ulong structure_id = boundary.Type.ID + structure_type_id_adjustment;
            StructureModel rootModel = new(structure_id, boundary.Mesh,
                new MaterialLighting(MaterialLighting.CreateKey(COLORSOURCE.STRUCTURETYPE, structure_id), color))
            {
                Translation = boundary.Center * 0.001
            };

            ColladaView.Add(rootModel);
        }

        Dictionary<ulong, StructureModel> modelsById = [];

        foreach (var view in WrapViews)
        {
            if (view.meshAssemblyPlan is null || view.Graph is null)
                continue;

            ulong structure_id = view.Graph.StructureID;
            if (view.meshAssemblyPlan.Root.MeshModel != null)
            {
                var mesh = view.meshAssemblyPlan.Root.MeshModel.composite;
                StructureModel rootModel = new(structure_id, mesh,
                new MaterialLighting(MaterialKey(view.Graph), ColladaColorForGraph(view.Graph)),
                StructureDisplayName(view.Graph))
                {
                    Translation = PlacementTranslation(view) * 0.001
                };

                modelsById[structure_id] = rootModel;
            }
        }

        foreach (var view in WrapViews)
        {
            if (!modelsById.TryGetValue(view.Graph.StructureID, out StructureModel model))
                continue;

            MorphologyGraph parentGraph = view.Graph.Parent;
            if (parentGraph != null && parentGraph.StructureID != 0 && modelsById.TryGetValue(parentGraph.StructureID, out StructureModel parentModel))
                parentModel.AddChild(model);
            else
                ColladaView.Add(model);
        }

        DirectoryInfo fInfo = new(outputDir);
        if (fInfo.Exists == false)
            fInfo.Create();

        var outputFile = System.IO.Path.Combine(outputDir, title + ".dae");
        DynamicRenderMeshColladaSerializer.SerializeToFile(ColladaView, outputFile);
    }

    /// <summary>
    /// Collada node translation for a structure, in volume coordinates and before the micron scaling the
    /// caller applies.
    /// </summary>
    static Geometry.Vector3 PlacementTranslation(BajajMultiOTVAssignmentView view) => view.VolumePlacementCenter;

    /// <summary>
    /// Collada material key: one shared material per structure type so Blender can select-linked-by-material.
    /// </summary>
    static string MaterialKey(MorphologyGraph structureGraph)
    {
        var type = structureGraph?.structure?.Type;
        if (type != null)
            return MaterialLighting.CreateKey(COLORSOURCE.STRUCTURETYPE, type.ID);
        return MaterialLighting.CreateKey(COLORSOURCE.STRUCTURE, structureGraph.StructureID);
    }

    /// <summary>Diffuse color matching the live BajajMultiTest view (<see cref="BajajMultiOTVAssignmentView.ColorForGraph"/>).</summary>
    static RgbaColor ColladaColorForGraph(MorphologyGraph structureGraph)
    {
        Color c = BajajMultiOTVAssignmentView.ColorForGraph(structureGraph);
        return RgbaColor.FromArgb(c.A, c.R, c.G, c.B);
    }

    /// <summary>Outliner-friendly name: TypeName-StructureID when type is known.</summary>
    static string StructureDisplayName(MorphologyGraph structureGraph)
    {
        ulong id = structureGraph.StructureID;
        string typeName = structureGraph?.structure?.Type?.Name;
        if (string.IsNullOrWhiteSpace(typeName))
            return $"Struct-{id}";
        return $"{typeName}-{id}";
    }

    public void SaveMesh(IReadOnlyMesh3D<IVertex3D> mesh, Geometry.Vector3 Position, MorphologyGraph structureGraph, string outputDir = null)
    {
        outputDir = outputDir ?? DefaultOutputPath;
        ulong structure_id = structureGraph.StructureID;

        BasicColladaView ColladaView = new(graph.scale.X, null)
        {
            SceneTitle = $"Structure #{structure_id}"
        };

        StructureModel rootModel = new(structure_id, mesh,
            new MaterialLighting(MaterialKey(structureGraph), ColladaColorForGraph(structureGraph)),
            StructureDisplayName(structureGraph))
        {
            Translation = Position * 0.001
        };

        ColladaView.Add(rootModel);

        DirectoryInfo fInfo = new(outputDir);
        if (fInfo.Exists == false)
            fInfo.Create();

        var outputFile = System.IO.Path.Combine(outputDir ?? DefaultOutputPath, $"Morphology-{structure_id}.dae");

        DynamicRenderMeshColladaSerializer.SerializeToFile(ColladaView, outputFile);
    }

    private static MeshView<VertexPositionNormalColor> CreateViewsForBoundaries(List<BoundarySurfaceViewModel> boundary_models)
    {
        MeshView<VertexPositionNormalColor> meshView = new();
        if (!boundary_models.Any())
            return null;

        foreach (var bm in boundary_models)
        {
            meshView.models.Add(CreateMeshModelForBoundary(bm));
        }

        return meshView;
    }

    private static PositionColorNormalMeshModel CreateMeshModelForBoundary(BoundarySurfaceViewModel bm)
    {
        var color = bm.Type.Name.GetHashCode().ToXNAColor(0.1f);
        //var verts = bm.BoundaryMarkers.Select(m => new VertexPositionNormalColor(m.ToXNAVector3(), Vector3.UnitZ, color).ToArray();

        PositionColorNormalMeshModel mesh_model = new()
        {
            ModelMatrix = Matrix.CreateTranslation(bm.Center.ToXNAVector3()),
            Vertices = [.. bm.Mesh.Vertices.Select(v => new VertexPositionNormalColor(v.Position.ToXNAVector3(), v.Normal.ToXNAVector3(), color))],
            Edges = [.. bm.TriangulationMesh.Faces.SelectMany(f => f.iVerts)]
        };
        return mesh_model;
    }
}
}

