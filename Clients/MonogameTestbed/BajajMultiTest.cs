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

        public CullMode CullMode = CullMode.None;

        public ConcurrentQueue<BajajGeneratorMesh> CompletedMeshes = new();

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
        /// Assembly debug overlay: wireframe bounding boxes for incomplete/failed nodes.
        /// Off by default so the solid vasculature mesh is visible without scattered wireframes.
        /// </summary>
        public bool ShowAssemblyBoundingBoxes = false;

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
        /// World translation that puts this mesh back in volume XY. Uses the mesh AABB when available so a
        /// synapse centered on the cell origin is not shifted by its own bbox.
        /// </summary>
        internal Vector3 SliceGraphToVolumeOffset =>
            _placementOffset ?? new Vector3((float)SliceOrigin.X, (float)SliceOrigin.Y, 0f);

        /// <summary>
        /// Color from the structure type recorded on the morphology graph. Shared by the mesh and the BajajMultiTest legend.
        /// </summary>
        internal static Color ColorForGraph(MorphologyGraph graph)
        {
            uint argb = graph?.structure?.Type?.Color ?? 0xFF808080u;
            Color color = argb.ToXNAColor();
            if (color.A == 0)
                color.A = 255;
            return color;
        }

        /// <summary>
        /// Used to lock the mesh views for individual slices
        /// </summary>
        private readonly SemaphoreSlim drawlock = new(1);
        readonly System.Threading.Thread BuildCompositeThread = null;
        private int _meshGeneration;
        private int _generateRunning;

        /// <summary>True while ConvertToMesh is in flight for this view.</summary>
        internal bool IsGeneratingMesh => Volatile.Read(ref _generateRunning) != 0;
        //SliceGraph sliceGraph;

        public BajajMultiOTVAssignmentView(MorphologyGraph graph, Geometry.Vector2? sliceOrigin = null)
        {
            ///Takes a set of polygons and Z values and generates a meshView
            Graph = graph;
            SliceOrigin = sliceOrigin ?? graph.NodesBoundingBox.CenterPoint.XY();

            /*
            Trace.WriteLine("Begin Slice graph construction");
            sliceGraph = SliceGraph.Create(graph, 2.0);
            Trace.WriteLine("End Slice graph construction");
            */

            ResetMesh();


            //BuildCompositeThread = new System.Threading.Thread(this.MeshCompositeTask);
            //BuildCompositeThread.IsBackground = true;
            //BuildCompositeThread.Start();
        }


        /// <summary>
        /// Called when the test window is closed
        /// </summary>
        public void OnUnloadContent()
        {
            if (BuildCompositeThread is null)
                return;
        }

        private void OnSliceCompleted(Slice slice, BajajGeneratorMesh mesh, bool Success) => this.AddMesh(slice, mesh, Success);

        private void AddMesh(Slice slice, BajajGeneratorMesh mesh, bool Success) =>
            meshAssemblyPlan?.OnMeshCompleted(slice, mesh, Success);


        /// <summary>
        /// Called before GenerateMesh to reset the class views.
        /// </summary>
        internal void ResetMesh()
        {
            _placementOffset = null;
            SliceMeshView = new MeshView<VertexPositionColor>
            {
                Name = "Slice Mesh"
            };

            this.RegionViews.Clear();
            this.listLineViews.Clear();
            this.MeshViews.Clear();

            try
            {
                drawlock.Wait();
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
        internal async Task GenerateMesh()
        {
            if (Interlocked.CompareExchange(ref _generateRunning, 1, 0) != 0)
                return;

            int generation = Interlocked.Increment(ref _meshGeneration);
            bool IsCurrent() => generation == Volatile.Read(ref _meshGeneration);

            try
            {
                if (MeshViews.Count > 0)
                    ResetMesh();

                Trace.WriteLine("Begin Slice graph construction");
                SliceGraph sliceGraph = await SliceGraph.Create(Graph, 2.0, SliceOrigin);
                Trace.WriteLine("End Slice graph construction");

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

                meshAssemblyPlan = plan;
                meshIncompleteView = new MeshAssemblyPlannerIncompleteView(meshAssemblyPlan, sliceGraph);
                meshCompletedView = new MeshAssemblyPlannerCompletedView(meshAssemblyPlan)
                {
                    Color = ColorForGraph(Graph)
                };

                await BajajMeshGenerator.ConvertToMesh(sliceGraph, (slice, mesh, success) =>
                {
                    if (!IsCurrent())
                        return;
                    OnSliceCompleted(slice, mesh, success);
                }).ConfigureAwait(false);

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
                    _assembledDisplayModel = BuildDisplayModelFromComposite(composite, ColorForGraph(Graph));
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Mesh generation failed for structure {Graph.StructureID}: {ex}");
            }
            finally
            {
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
            //window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, Color.DarkGray, float.MaxValue, 0);
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
                        MeshViews[iShownMesh.Value].Draw(window.GraphicsDevice, window.Scene, CullMode.CullCounterClockwiseFace);
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
                    CompositeMeshView.Draw(window.GraphicsDevice, window.Scene, CullMode.CullCounterClockwiseFace);
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
                window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, float.MaxValue, 0);
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
                window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, float.MaxValue, 0);
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
        /*
        /// <summary>
        /// Dequeues entries from the CompletedMeshes
        /// </summary>
        private void MeshCompositeTask()
        {
            while(true)
            {
                bool NewMesh = false;
                while(CompletedMeshes.TryDequeue(out BajajGeneratorMesh completedMesh))
                {
                    //CompositeMeshModel.AddSlice(completedMesh)
                    //System.Threading.Thread.Sleep(1000); 
                    //var leaf = meshAssemblyPlan.Slices[completedMesh.Slice.Key];

                    NewMesh = true;
                }

                if (NewMesh)
                {
                    lock (drawlock)
                    { 
                        CompositeMeshView.models.Clear();

                        foreach (var model in meshAssemblyPlan.MeshModels)
                        {
                            CompositeMeshView.models.Add(model);
                        }
                    }
                }

                System.Threading.Thread.Sleep(100); //Consume all of the objects in the queue every interval
            }
        }*/

        public void Draw3D(MonoTestbed window, Scene3D scene)
        {
            ApplySliceGraphPlacement();
            scene.Viewport = window.GraphicsDevice.Viewport;


            DepthStencilState dstate = new()
            {
                DepthBufferEnable = true,
                StencilEnable = false,
                DepthBufferWriteEnable = true,
                DepthBufferFunction = CompareFunction.LessEqual
            };

            window.GraphicsDevice.DepthStencilState = dstate;
            CullMode cull = BajajMultiAssignmentTest.CullModeForView(CullMode);
            //window.GraphicsDevice.BlendState = BlendState.Opaque;

            //Expand our model if we can
            try
            {
                drawlock.Wait();
                if (ShowCompositeMesh == false)
                {
                    if (ViewIndex.InRange(iShownMesh, MeshViews.Count))
                    {
                        MeshViews[iShownMesh.Value].Draw(window.GraphicsDevice, scene, cull);
                    }
                }
                else
                {
                    if (CompositeMeshView != null)
                    {
                        try
                        {
                            //CompositeMeshModel.ModelLock.EnterReadLock();
                            //CompositeMeshView.Draw(window.GraphicsDevice, scene, CullMode);
                            //MeshView<VertexPositionNormalColor>.Draw(window.GraphicsDevice, scene, window.basicEffect, CullMode, meshAssemblyPlan.MeshModels);
                            if (ShowAssemblyBoundingBoxes && meshIncompleteView != null)
                            {
                                var incompleteModels = meshIncompleteView.MeshModels;
                                if (incompleteModels.Length > 0)
                                    MeshView<VertexPositionColor>.Draw(window.GraphicsDevice, scene, window.basicEffect,
                                        cull, FillMode.WireFrame, incompleteModels);
                            }

                            bool drewSolidMesh = false;
                            var rootMeshModel = meshAssemblyPlan?.Root?.MeshModel;
                            if (rootMeshModel?.model?.Vertices?.Length > 0)
                            {
                                try
                                {
                                    rootMeshModel.ModelLock.EnterReadLock();
                                    MeshView<VertexPositionNormalColor>.Draw(window.GraphicsDevice, scene,
                                        window.basicEffect, cull, FillMode.Solid, [rootMeshModel.model]);
                                    drewSolidMesh = true;
                                }
                                finally
                                {
                                    rootMeshModel.ModelLock.ExitReadLock();
                                }
                            }

                            if (!drewSolidMesh && _assembledDisplayModel != null)
                            {
                                MeshView<VertexPositionNormalColor>.Draw(window.GraphicsDevice, scene,
                                    window.basicEffect, cull, FillMode.Solid, [_assembledDisplayModel]);
                            }
                            else if (!drewSolidMesh && meshCompletedView != null)
                                MeshView<VertexPositionNormalColor>.Draw(window.GraphicsDevice, scene,
                                    window.basicEffect, cull, FillMode.Solid, meshCompletedView.MeshModels);
                        }
                        finally
                        {
                            //CompositeMeshModel.ModelLock.ExitReadLock();
                        }

                        //ViewLabels.AppendLine(CompositeMeshView.Name);
                    }
                }
            }
            finally
            {
                drawlock.Release();
            }
        }

        /// <summary>
        /// Put this mesh at volume XY. SliceGraph subtracted <see cref="SliceOrigin"/> (the parent cell's location center).
        /// </summary>
        private void ApplySliceGraphPlacement()
        {
            Matrix m = Matrix.CreateTranslation(SliceGraphToVolumeOffset);
            var root = meshAssemblyPlan?.Root?.MeshModel?.model;
            if (root != null)
                root.ModelMatrix = m;
            if (_assembledDisplayModel != null)
                _assembledDisplayModel.ModelMatrix = m;
            if (meshCompletedView?.MeshModels != null)
            {
                foreach (var model in meshCompletedView.MeshModels)
                {
                    if (model != null)
                        model.ModelMatrix = m;
                }
            }

            if (meshIncompleteView?.MeshModels != null)
            {
                foreach (var model in meshIncompleteView.MeshModels)
                {
                    if (model != null)
                        model.ModelMatrix = m;
                }
            }
        }

        /// <summary>
        /// After assembly, translate so this structure's annotation AABB center lands at volume XY
        /// even if the mesh was built in the parent cell's frame.
        /// </summary>
        void RefreshPlacementOffset()
        {
            Geometry.Vector2 target = Graph.Nodes.Count > 0
                ? Graph.NodesBoundingBox.CenterPoint.XY()
                : SliceOrigin;
            if (TryGetLocalMeshXYCenter(out Geometry.Vector2 local))
                _placementOffset = new Vector3((float)(target.X - local.X), (float)(target.Y - local.Y), 0f);
            else
                _placementOffset = new Vector3((float)SliceOrigin.X, (float)SliceOrigin.Y, 0f);
        }

        bool TryGetLocalMeshXYCenter(out Geometry.Vector2 center)
        {
            if (!TryGetLocalMeshBounds(out Vector3 min, out Vector3 max))
            {
                center = default;
                return false;
            }

            center = new Geometry.Vector2((min.X + max.X) * 0.5, (min.Y + max.Y) * 0.5);
            return true;
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

            var composite = meshAssemblyPlan?.Root?.MeshModel?.composite;
            if (composite?.Vertices != null)
            {
                foreach (var v in composite.Vertices)
                    Include(v.Position.ToXNAVector3());
            }

            min = boundsMin;
            max = boundsMax;
            return any;
        }

        /// <summary>
        /// Axis-aligned bounds of mesh geometry actually drawn in 3D (volume XY, slice-graph Z).
        /// </summary>
        public bool TryGetRenderedMeshBounds(out Vector3 min, out Vector3 max)
        {
            if (!TryGetLocalMeshBounds(out min, out max))
                return false;

            Vector3 offset = SliceGraphToVolumeOffset;
            min += offset;
            max += offset;
            return true;
        }
    }

/// <summary>
/// Generates a single mesh for a cell or a subset of a cell based on a Z range.  Used to debug the generation of whole cells and the merging of multiple slice meshes.
/// </summary>
class BajajMultiAssignmentTest : IGraphicsTest, ITestLegend
{
    public string Title => this.GetType().Name;

    public string ModeDescription => string.Empty;

    public string ActiveViewDescription => string.Empty;

    public IReadOnlyList<LegendEntry> LegendEntries
    {
        get
        {
            Dictionary<ulong, LegendEntry> byType = [];
            foreach (var wrapView in wrapViews)
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

            return [.. byType.Values.OrderBy(e => e.Text, StringComparer.OrdinalIgnoreCase)];
        }
    }

    Scene scene;
    Scene3D scene3D;
    MonoTestbed _window;
    readonly GamePadStateTracker Gamepad = new();
    readonly KeyboardStateTracker keyboard = new();

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
    readonly Cursor2DCameraManipulator CameraManipulator = new();
    readonly Camera3DManipulator Camera3DManipulator = new();
    readonly List<BajajMultiOTVAssignmentView> wrapViews = [];

    List<BoundarySurfaceViewModel> boundaryViewModels = [];
    MeshView<VertexPositionNormalColor> boundaryView = null;
    readonly bool Draw3D = true;

    bool _initialized = false;
    public bool Initialized => _initialized;

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

        Gamepad.Update(GamePad.GetState(PlayerIndex.One));
        keyboard.Update(Keyboard.GetState());

        Console.Write("Begin OData fetch");

        Task<MorphologyGraph> boundary_graph_task = null;
        if (Program.options.BoundaryIDs.Any() && Program.options.EndpointUri != null)
        {
            Uri endpoint = Program.options.EndpointUri;
            boundary_graph_task = AnnotationVizLib.OData.ODataMorphologyFactory.FromODataByTypeIDsAsync([.. Program.options.BoundaryIDs.Select(id => (long)id)], endpoint, false);
            var boundary_graph = await boundary_graph_task;
            this.boundaryViewModels = BoundarySurfaceViewModel.CreateBoundarySurfaces(boundary_graph);

            this.boundaryView = CreateViewsForBoundaries(this.boundaryViewModels);

            Console.WriteLine(" Boundary view created");
        }

        if (Program.options.StructureIDs.Any() && Program.options.EndpointUri != null)
        {
            Console.WriteLine(" From command line parameters");

            Uri endpoint = Program.options.EndpointUri;
            graph = await Task.Run(() => AnnotationVizLib.OData.ODataMorphologyFactory.FromOData([.. Program.options.StructureIDs.Select(id => (long)id)], Program.options.IncludeChildren, endpoint));
        }
        else
        {
            Console.WriteLine("From hard coded test case (no command line paramters)");

            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(GlialDebug1, DataSource.EndpointMap[ENDPOINT.RPC1]);

            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new long[] { 180 }, false, DataSource.EndpointMap[ENDPOINT.RC1]);
            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new long[] { 40429 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);

            //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 822, 23082, 23084 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);

            //Becca's paper, first render
            //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 822, 2386, 23084, 23098, 31097, 31108, 23093 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);

            //Becca's paper, 2nd render
            //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] {933, 23122, 31687, 23095, 23017, 23856, 39762 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);

            graph = await Task.Run(() => AnnotationVizLib.OData.ODataMorphologyFactory.FromOData(new long[] { 476 }, Program.options.IncludeChildren, DataSource.EndpointMap[Endpoint.TEST]));

            //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 30804, 2713 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
            //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 933 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
            //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 933 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
            //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 23082 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
            //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 1161 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
            //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 1537 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
            //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 30804 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
        }

        Console.WriteLine("End OData fetch");

        if (Program.options.SmoothProcesses)
        {
            Console.WriteLine("Smoothing unbranched process centroids");
            AnnotationVizLib.MorphologyGraph.SmoothProcesses(graph);
        }

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
            window.Scene.Camera.Downsample = graph.BoundingBox.Width / window.GraphicsDevice.Viewport.Width;
        }

        List<Task> meshGenTasks = [];
        QueueMeshViews(graph, meshGenTasks);

        await Task.WhenAll(meshGenTasks);

        FrameCameraOnRenderedMesh(window);

        //Save the output in a specific place upon request in the command line parameters
        if (string.IsNullOrWhiteSpace(Program.options.OutputPath) == false)
        {
            try
            {
                SaveMeshes("BajajMultitest", Program.options.OutputPath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not save scene output mesh to {Program.options.OutputPath}.\nException:{e}");
            }

            foreach (var wrapView in wrapViews)
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

        Console.WriteLine($"All rendering complete");

        _initialized = true;


        if (Program.options.Quiet)
        {
            window.Exit();
        }
        /*
        A = SqlGeometry.STPolyFromText(PolyA.ToSqlChars(), 0).ToPolygon();
        B = SqlGeometry.STPolyFromText(PolyB.ToSqlChars(), 0).ToPolygon();

        Geometry.Vector2 Centroid = A.Centroid;
        A = A.Translate(-Centroid);
        B = B.Translate(-Centroid);

        Points_A.Points = new MonogameTestbed.PointSet(A.ExteriorRing);
        Points_B.Points = new MonogameTestbed.PointSet(B.ExteriorRing);

        wrapView = new TriangulationShapeWrapView(A, B);
        */
    }

    /// <summary>
    /// Starts Bajaj generation for every nested subgraph. Children share the parent cell's XY origin so their
    /// meshes sit on the cell instead of being recentered on each synapse bbox.
    /// </summary>
    private void QueueMeshViews(MorphologyGraph parent, List<Task> meshGenTasks, Geometry.Vector2? familyOrigin = null)
    {
        foreach (var subgraph in parent.Subgraphs.Values)
        {
            Geometry.Vector2? origin = familyOrigin;
            if (origin is null && subgraph.Nodes.Count > 0)
                origin = subgraph.NodesBoundingBox.CenterPoint.XY();

            if (subgraph.Nodes.Count > 0)
            {
                BajajMultiOTVAssignmentView wrapView = new(subgraph, origin);
                wrapViews.Add(wrapView);
                meshGenTasks.Add(wrapView.GenerateMesh());
            }

            QueueMeshViews(subgraph, meshGenTasks, origin);
        }
    }

    public void Update()
    {
        PlayerIndex? InputSource = GamePadStateTracker.GetFirstConnectedController() ?? PlayerIndex.One;
        GamePadState state = GamePad.GetState(InputSource.Value);
        Gamepad.Update(state);
        keyboard.Update(Keyboard.GetState());

        if (!Draw3D)
            CameraManipulator.Update(scene.Camera);
        else
        {
            Camera3DManipulator.Update(this.scene3D.Camera, scene3D.Viewport.Width, scene3D.Viewport.Height);
            //StandardCameraManipulator.Update(this.scene3D.Camera);
        }

        foreach (var wrapView in wrapViews)
        {

            if (Gamepad.A_Clicked)
            {
                wrapView.iShownMesh = wrapView.iShownMesh.HasValue ? wrapView.iShownMesh.Value + 1 : 0;
                if (wrapView.iShownMesh.HasValue && wrapView.iShownMesh.Value >= wrapView.MeshViews.Count)
                {
                    wrapView.iShownMesh = null;
                }
            }

            if (Gamepad.B_Clicked)
            {
                wrapView.iShownLineView = wrapView.iShownLineView.HasValue ? wrapView.iShownLineView.Value + 1 : 0;
                if (wrapView.iShownLineView.HasValue && wrapView.iShownLineView.Value >= wrapView.listLineViews.Count)
                {
                    wrapView.iShownLineView = null;
                }

                Trace.WriteLine(wrapView.iShownLineView.ToString());

                /*wrapView.ShowPolygons = !wrapView.ShowPolygons;
                wrapView.ShowAllEdges = !wrapView.ShowAllEdges;
                */
            }

            if (Gamepad.Y_Clicked)
            {
                //Cycle throught the various region passes as Y is clicked
                wrapView.iShownRegion = wrapView.iShownRegion.HasValue ? wrapView.iShownRegion.Value + 1 : 0;
                if (wrapView.iShownRegion.HasValue && wrapView.iShownRegion.Value >= wrapView.RegionViews.Count)
                {
                    wrapView.iShownRegion = null;
                }
            }

            if (Gamepad.X_Clicked)
            {
                wrapView.ShowCompletedVerticies = !wrapView.ShowCompletedVerticies;
            }

            if (Gamepad.Start_Clicked && wrapView.IsGeneratingMesh == false)
            {
                _ = wrapView.GenerateMesh();
            }

            if (Gamepad.RightShoulder_Clicked)
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
            if (Gamepad.RightStick_Clicked)
            {
                wrapView.ShowAssemblyBoundingBoxes = !wrapView.ShowAssemblyBoundingBoxes;
            }

            if (Gamepad.LeftStick_Clicked || keyboard.Pressed(Keys.K))
            {
                wrapView.CullMode = wrapView.CullMode == CullMode.None ? CullMode.CullCounterClockwiseFace : CullMode.None;
            }

            if (Gamepad.LeftShoulder_Clicked)
            {
                //this.Draw3D = !this.Draw3D;
                wrapView.ShowCompositeMesh = !wrapView.ShowCompositeMesh;
            }

            if (Gamepad.Back_Clicked || keyboard.Pressed(Keys.PrintScreen))
            {
                if (wrapView.meshAssemblyPlan != null && wrapView.meshAssemblyPlan.MeshAssembledEvent.IsSet)
                    SaveMesh(wrapView.meshAssemblyPlan.Root.MeshModel.composite, PlacementTranslation(wrapView), wrapView.Graph);
            }
        }

        if (keyboard.Pressed(Keys.I) && Program.options != null)
        {
            Program.options.InvertZ = !Program.options.InvertZ;
            if (scene3D != null)
                scene3D.World = ViewZAxisWorld;
            if (_window != null)
                FrameCameraOnRenderedMesh(_window);
        }

        if (Gamepad.Back_Clicked || (keyboard.Pressed(Keys.S) && (keyboard.Pressed(Keys.LeftControl) || keyboard.Pressed(Keys.RightControl))))
        {
            //if (wrapView.meshAssemblyPlan.MeshAssembledEvent.IsSet)
            //SaveMesh(wrapView.meshAssemblyPlan.Root.MeshModel.composite, wrapView.Graph.StructureID);
            SaveMeshes("BajajMultitest");
        }
        /*
        if(Gamepad.RightShoulder_Clicked)
        {
            wrapView.NumLinesToDraw++;
        }

        if (Gamepad.LeftShoulder_Clicked)
        {
            wrapView.NumLinesToDraw--;
        }

        if (Gamepad.Y_Clicked)
        {
            wrapView.ShowFinalLines = !wrapView.ShowFinalLines;
        }*/
    }

    public void Draw(MonoTestbed window)
    {
        scene3D.Viewport = window.GraphicsDevice.Viewport;
        scene3D.World = ViewZAxisWorld;
        window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, Color.DarkGray, 1.0f, 0);


        foreach (var wrapView in wrapViews)
        {
            if (!Draw3D)
            {
                wrapView?.Draw(window, scene);
            }
            else
            {
                wrapView?.Draw3D(window, scene3D);
            }
        }

        if (boundaryView != null)
        {
            MeshView<VertexPositionNormalColor>.Draw(window.GraphicsDevice, scene3D,
                                window.basicEffect, CullMode.None, FillMode.Solid, boundaryView.models);

            //boundaryView.Draw(window.GraphicsDevice, scene, CullMode.CullCounterClockwiseFace);
        }

        if (Draw3D)
        {
            Draw3DDebugHud(window);
        }
    }

    private void Draw3DDebugHud(MonoTestbed window)
    {
        var cam = scene3D.Camera;
        float camDistance = (cam.Position - cam.LookAt).Length();
        StringBuilder hud = new();
        hud.AppendLine($"Cam ({cam.Position.X:F0}, {cam.Position.Y:F0}, {cam.Position.Z:F0})");
        hud.AppendLine($"LookAt ({cam.LookAt.X:F0}, {cam.LookAt.Y:F0}, {cam.LookAt.Z:F0})");
        hud.AppendLine($"Yaw {cam.Yaw * 180 / Math.PI:F1} deg  Pitch {cam.Pitch * 180 / Math.PI:F1} deg  Dist {camDistance:F0}");
        hud.AppendLine(Program.options?.InvertZ == true ? "Z inverted (I to toggle)" : "Z volume (I / --invert-z)");
        if (graph?.BoundingBox != null)
        {
            var bbox = graph.BoundingBox;
            hud.AppendLine($"Mesh XY +/-{bbox.Width / 2:F0}  Z {bbox.MinVals[2]:F0}-{bbox.MaxVals[2]:F0}");
        }

        window.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        window.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        const float hudScale = 0.3f;
        window.spriteBatch.DrawString(
            window.fontArial,
            hud.ToString(),
            new Vector2(8, 8),
            Color.Yellow,
            rotation: 0f,
            origin: Vector2.Zero,
            scale: hudScale,
            effects: SpriteEffects.None,
            layerDepth: 0f);
        window.spriteBatch.End();
    }

    /// <summary>
    /// Aim the 3D camera at the union bounding box of assembled mesh geometry (not volume-space graph bounds).
    /// </summary>
    void FrameCameraOnRenderedMesh(MonoTestbed window)
    {
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        bool any = false;

        foreach (var wrapView in wrapViews)
        {
            if (!wrapView.TryGetRenderedMeshBounds(out Vector3 viewMin, out Vector3 viewMax))
                continue;

            min = Vector3.Min(min, viewMin);
            max = Vector3.Max(max, viewMax);
            any = true;
        }

        if (!any)
            return;

        Vector3 worldMin = Vector3.Transform(min, ViewZAxisWorld);
        Vector3 worldMax = Vector3.Transform(max, ViewZAxisWorld);
        min = Vector3.Min(worldMin, worldMax);
        max = Vector3.Max(worldMin, worldMax);

        Vector3 center = (min + max) * 0.5f;
        Vector3 extent = max - min;
        float maxExtent = Math.Max(extent.X, Math.Max(extent.Y, extent.Z));
        if (maxExtent < float.Epsilon)
            maxExtent = 1f;

        float halfFov = scene3D.FieldOfView * 0.5f;
        float distanceForExtent = (maxExtent * 0.5f) / (float)Math.Tan(halfFov);
        float distance = Math.Max(distanceForExtent * 1.35f, maxExtent * 1.5f);
        distance = Math.Max(distance, 1f);

        scene3D.MaxDrawDistance = distance * 25f;
        scene3D.MinDrawDistance = Math.Max(0.01f, distance * 0.001f);

        Vector3 position = center + new Vector3(-distance, -distance * 0.35f, distance * 0.2f);
        scene3D.Camera.Position = position;
        scene3D.Camera.LookAt = center;
    }

    public void UnloadContent(MonoTestbed window)
    {
        foreach (var wrapView in wrapViews)
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

    public void SaveMeshes(string title, string outputDir = null)
    {
        outputDir = outputDir ?? DefaultOutputPath;

        BasicColladaView ColladaView = new(graph.scale.X, null)
        {
            SceneTitle = title
        };

        ulong max_id = wrapViews.Count == 0 ? 0UL : wrapViews.Max(wv => wv.Graph.StructureID);
        ulong structure_type_id_adjustment = StructureTypeIdOffset(max_id);

        foreach (var boundary in boundaryViewModels)
        {
            System.Drawing.Color color = System.Drawing.Color.FromArgb(0x7F7F7F7F);
            ulong structure_id = boundary.Type.ID + structure_type_id_adjustment;
            StructureModel rootModel = new(structure_id, boundary.Mesh,
                new MaterialLighting(MaterialLighting.CreateKey(COLORSOURCE.STRUCTURETYPE, structure_id), color))
            {
                Translation = boundary.Center * 0.001
            };

            ColladaView.Add(rootModel);
        }

        Dictionary<ulong, StructureModel> modelsById = [];

        foreach (var view in wrapViews)
        {
            if (view.meshAssemblyPlan is null || view.Graph is null)
                continue;

            ulong structure_id = view.Graph.StructureID;
            if (view.meshAssemblyPlan.Root.MeshModel != null)
            {
                var mesh = view.meshAssemblyPlan.Root.MeshModel.composite;
                Vector3 offset = view.SliceGraphToVolumeOffset;
                StructureModel rootModel = new(structure_id, mesh,
                new MaterialLighting(MaterialKey(view.Graph), System.Drawing.Color.CornflowerBlue))
                {
                    Translation = new Geometry.Vector3(offset.X, offset.Y, view.Graph.NodesBoundingBox.CenterPoint.Z) * 0.001
                };

                modelsById[structure_id] = rootModel;
            }
        }

        foreach (var view in wrapViews)
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

    static Geometry.Vector3 PlacementTranslation(BajajMultiOTVAssignmentView view)
    {
        Vector3 offset = view.SliceGraphToVolumeOffset;
        return new Geometry.Vector3(offset.X, offset.Y, view.Graph.NodesBoundingBox.CenterPoint.Z);
    }

    /// <summary>
    /// Collada material key for a structure mesh. Nested children are not in the dummy root's
    /// <see cref="MorphologyGraph.Subgraphs"/> map, so this uses the graph already attached to the view.
    /// </summary>
    static string MaterialKey(MorphologyGraph structureGraph) =>
        structureGraph.structure != null
            ? MaterialLighting.CreateKey(COLORSOURCE.STRUCTURE, structureGraph.structure)
            : MaterialLighting.CreateKey(COLORSOURCE.STRUCTURE, structureGraph.StructureID);

    public void SaveMesh(IReadOnlyMesh3D<IVertex3D> mesh, Geometry.Vector3 Position, MorphologyGraph structureGraph, string outputDir = null)
    {
        outputDir = outputDir ?? DefaultOutputPath;
        ulong structure_id = structureGraph.StructureID;

        BasicColladaView ColladaView = new(graph.scale.X, null)
        {
            SceneTitle = $"Structure #{structure_id}"
        };

        StructureModel rootModel = new(structure_id, mesh,
            new MaterialLighting(MaterialKey(structureGraph), System.Drawing.Color.CornflowerBlue))
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

