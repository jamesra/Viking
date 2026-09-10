using AnnotationVizLib;
using AnnotationVizLib;
using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MorphologyMesh;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbed
{
    /// <summary>
    /// Why a slice ended up in <see cref="MeshAssemblyPlanner.FailedSlicesForRepro"/>.  The three cases need
    /// different fixes, so they must not be lumped together as "dropped" on the HUD.
    /// </summary>
    enum SliceFailureKind
    {
        /// <summary>SliceGraph could not build the topology; the slice never reached face generation.</summary>
        Topology,

        /// <summary>GenerateFaces threw; the slice produced no geometry.</summary>
        FaceGenerationException,

        /// <summary>A mesh exists but the manifold report rejects it (holes, non-manifold, winding).</summary>
        InvalidSurface,
    }

    /// <summary>
    /// One BajajMultiTest slice that failed to mesh, named by its LocationIDs for BajajTest repro.
    /// </summary>
    readonly struct FailedSliceReproRecord(ulong structureId, ulong[] locationIds, SliceFailureKind kind, string reason)
    {
        public ulong StructureId { get; } = structureId;
        public ulong[] LocationIds { get; } = locationIds ?? [];
        public SliceFailureKind Kind { get; } = kind;
        public string Reason { get; } = reason ?? "";

        /// <summary>Space-separated LocationIDs suitable for <c>--repro-locations</c>.</summary>
        public string LocationIdsLine => string.Join(' ', LocationIds);
    }

    /// <summary>
    /// This is a binary treeWithUniqueValues where leaves represent meshes.  Branches represent meshes that should be merged when both leaves have finished mesh generation.  Nodes are merged until only a single root leaf node exists with the final mesh
    /// </summary>
    class MeshAssemblyPlanner
    {
        public readonly IAssemblyPlannerNode Root;

        /// <summary>
        /// Allows mapping a slice key to the original leaf node
        /// </summary>
        public readonly SortedList<ulong, AssemblyPlannerLeaf> Slices;

        /// <summary>
        /// Allows mapping a node key (including slice id or branch id) to the original leaf node
        /// </summary>
        private readonly Dictionary<ulong, IAssemblyPlannerNode> Nodes;

        /// <summary>
        /// Set when the entire assembly plan has completed
        /// </summary>
        public System.Threading.ManualResetEventSlim MeshAssembledEvent = new();

        /// <summary>
        /// Structure color baked into each leaf <see cref="SliceGraphMeshModel"/> at creation.
        /// </summary>
        public Color MeshColor { get; set; } = Color.CornflowerBlue;

        /// <summary>
        /// Slices that produced no usable mesh (topology or face-generation failure), keyed by slice id.
        /// Written at the end of BajajMultiTest as a BajajTest <c>--repro-locations-file</c>.
        /// </summary>
        public ConcurrentDictionary<ulong, FailedSliceReproRecord> FailedSlicesForRepro { get; } = new();

        /// <summary>Source morphology grouping; used when recording failed-slice location IDs.</summary>
        public SliceGraph SliceGraph { get; private set; }

        /// <summary>
        /// Face-generation workers enqueue merge work here; a single consumer per planner runs
        /// <see cref="CheckForMerge"/> so merges do not steal face-gen thread-pool slots.
        /// </summary>
        readonly Channel<AssemblyPlannerBranch> _mergeChannel =
            Channel.CreateUnbounded<AssemblyPlannerBranch>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        readonly Task _mergeConsumer;

        readonly TaskCompletionSource _assembledTcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes when the root composite has been finalized (or the plan had nothing to assemble).</summary>
        public Task AssembledTask => _assembledTcs.Task;

        public delegate void OnNodeMeshCompletedDelegate(IAssemblyPlannerNode node, bool success, MeshManifoldReport? report);

        /// <summary>
        /// Called when a node in the assembly plan either completes mesh generation or knows an error occurred and it will not be generating a mesh
        /// </summary>
        public event OnNodeMeshCompletedDelegate OnNodeCompleted;

        public delegate void OnPlanCompletedDelegate(MeshAssemblyPlanner plan);

        /// <summary>
        /// Called when a node in the assembly plan either completes mesh generation or knows an error occurred and it will not be generating a mesh
        /// </summary>
        public event OnPlanCompletedDelegate OnPlanCompleted;

        /// <summary>
        /// Build the merge tree.  Leaves are ordered by the mean Z of the slice's morphology nodes so neighbouring
        /// slices merge first.  This reads the morphology graph rather than the slice topology so the plan can be
        /// created before <see cref="SliceGraph.InitializeTopologyAsync"/> finishes; asking for a topology at that
        /// point would compute one outside the initializer that keeps shape mutation serialized.
        /// </summary>
        public static MeshAssemblyPlanner Create(SliceGraph sliceGraph)
        {
            MorphologyGraph morphology = sliceGraph.Morphology;
            AssemblyPlannerLeaf[] firstLayer = [.. sliceGraph.Nodes.Values.OrderBy(slice =>
            {
                if (slice.AllNodes.Count == 0)
                    return -1;
                return Math.Round(slice.AllNodes.Average(id => morphology[id].Z));
            }).ThenBy(slice => slice.Key).Select(slice => new AssemblyPlannerLeaf(slice.Key))];

            Dictionary<ulong, IAssemblyPlannerNode> Nodes = new(sliceGraph.Nodes.Count * 2);
            SortedList<ulong, AssemblyPlannerLeaf> Slices = new(firstLayer.Length);
            foreach (var leaf in firstLayer)
            {
                Slices.Add(leaf.Key, leaf);
                Nodes.Add(leaf.Key, leaf);
            }

            IAssemblyPlannerNode[] currentLayer = firstLayer;
            //This isn't a true binary treeWithUniqueValues because branches do not have values.  We build our treeWithUniqueValues from the bottom up. This 
            //always generates a balances treeWithUniqueValues.
            while (currentLayer.Length > 1)
            {
                currentLayer = BuildLayer(currentLayer);

                foreach (var item in currentLayer)
                {
                    //The last node can be a carryover, so use index instead of add to prevent errors
                    Nodes[item.Key] = item;
                }
            }

            return new MeshAssemblyPlanner(currentLayer[0], Nodes, Slices)
            {
                SliceGraph = sliceGraph
            };
        }

        private MeshAssemblyPlanner(IAssemblyPlannerNode root, Dictionary<ulong, IAssemblyPlannerNode> nodes, SortedList<ulong, AssemblyPlannerLeaf> slices)
        {
            Root = root;
            Nodes = nodes;
            Slices = slices;
            _mergeConsumer = Task.Run(MergeConsumerLoop);
        }

        async Task MergeConsumerLoop()
        {
            try
            {
                await foreach (AssemblyPlannerBranch branch in _mergeChannel.Reader.ReadAllAsync().ConfigureAwait(false))
                    CheckForMerge(branch);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"MeshAssemblyPlanner merge consumer failed: {ex}");
            }
        }


        /// <summary>
        /// Given a list of nodes, build branch nodes that connect each odd and even node.  Then append a remainder node to the list.
        /// This returns a list of size N / 2 (rounded up).  This call is repeated until a single root node is returned.
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        private static IAssemblyPlannerNode[] BuildLayer(IAssemblyPlannerNode[] nodes)
        {
            if (nodes.Length < 2)
                return nodes;

            IAssemblyPlannerNode[] layer = new IAssemblyPlannerNode[(nodes.Length / 2) + (nodes.Length % 2)];

            for (int iLayer = 0; iLayer < layer.Length; iLayer++)
            {
                int iChild = iLayer * 2;
                IAssemblyPlannerNode newNode;
                if (iChild + 1 >= nodes.Length)
                {
                    //Add a leaf node to the end of the layer
                    newNode = nodes[iChild];
                }
                else
                {
                    AssemblyPlannerBranch branch = new(nodes[iChild], nodes[iChild + 1]);
                    nodes[iChild].Parent = branch;
                    nodes[iChild + 1].Parent = branch;
                    newNode = branch;
                }

                layer[iLayer] = newNode;
            }

            return layer;
        }

        public IAssemblyPlannerNode this[ulong id] => Nodes[id];

        /// <summary>
        /// Called when a mesh is completed.  Generates a model and attempts to merge that model up the treeWithUniqueValues.
        /// Thread safe
        /// </summary>
        /// <param name="mesh"></param>
        public void OnMeshCompleted(Slice slice, BajajGeneratorMesh mesh, bool Success)
        {
            AssemblyPlannerLeaf leaf = this.Slices[slice.Key];

            //A null mesh means the slice produced no geometry.  The leaf still has to complete, otherwise its
            //branch never merges and the assembly never reaches the root.
            if (mesh is null || Success == false)
            {
                SliceFailureKind kind;
                string reason;
                if (mesh is null)
                {
                    //BajajMeshGenerator reports both an invalid topology and a face-generation exception as a null
                    //mesh; the slice graph knows which slices never had a topology.
                    bool topologyFailed = SliceGraph?.FailedTopologySlices.ContainsKey(slice.Key) == true;
                    kind = topologyFailed ? SliceFailureKind.Topology : SliceFailureKind.FaceGenerationException;
                    reason = topologyFailed ? "topology initialisation failed" : "face generation threw (see trace)";
                }
                else
                {
                    kind = SliceFailureKind.InvalidSurface;
                    reason = mesh.ManifoldReport.ToString();
                }

                if (BajajMeshGenerator.VerboseLogging)
                    Trace.WriteLine($"Slice {slice.Key} merged without a complete mesh ({kind}): {reason}.");

                FailedSlicesForRepro[slice.Key] = new FailedSliceReproRecord(
                    SliceGraph?.Morphology?.StructureID ?? 0,
                    [.. slice.AllNodes],
                    kind,
                    reason);
            }

            leaf.OnMeshCompletion(mesh, MeshColor);
            OnNodeCompleted?.Invoke(leaf, Success, mesh?.ManifoldReport);

            if (leaf.Parent != null)
                _mergeChannel.Writer.TryWrite(leaf.Parent);
            else if (leaf == Root)
                FinalizeRootComposite();
        }

        /// <summary>
        /// Stop the merge consumer when this plan is superseded by a newer GenerateMesh run.
        /// </summary>
        internal void Abandon()
        {
            _mergeChannel.Writer.TryComplete();
            _assembledTcs.TrySetResult();
        }

        private int _rootFinalized;

        /// <summary>
        /// Run composite-level winding fix once the full structure mesh is assembled at the root, and only once.
        ///
        /// Root.MeshComplete latches, so every thread whose CheckForMerge walk subsequently reaches the root sees
        /// it set.  Without this guard the slices that finish last all re-run the winding pass over the same
        /// composite; the second pass tries to reverse faces the first has already removed and re-added, tripping
        /// the face/edge consistency assert in Edge.RemoveFace.
        ///
        /// MeshAssembledEvent is signalled here rather than at the call sites so it cannot be set by a thread that
        /// skipped the guard while the winding pass is still running - consumers treat the event as "the composite
        /// is final" and export from it directly.
        /// </summary>
        private void FinalizeRootComposite()
        {
            if (Interlocked.Exchange(ref _rootFinalized, 1) != 0)
                return;

            try
            {
                Root?.MeshModel?.EnsureCompositeWinding();
            }
            catch (Exception ex)
            {
                //A degenerate face can make the winding pass throw.  The composite is still the assembled mesh;
                //leaving AssembledTask incomplete here would hang GenerateMesh (and a -q run) forever.
                Trace.WriteLine($"Composite winding pass failed for structure {SliceGraph?.Morphology?.StructureID}; publishing the composite as assembled: {ex}");
            }
            finally
            {
                MeshAssembledEvent.Set();
                _assembledTcs.TrySetResult();
                _mergeChannel.Writer.TryComplete();
            }
        }

        /// <summary>
        /// Walk from <paramref name="node"/> toward the root. Claim each ready branch with
        /// <see cref="Interlocked.CompareExchange(ref int, int, int)"/> and merge under a plain lock.
        /// Non-ready ancestors stop the walk (nothing above can merge until this node has a mesh).
        /// </summary>
        public void CheckForMerge(AssemblyPlannerBranch node)
        {
            AssemblyPlannerBranch current = node;
            while (current != null)
            {
                AssemblyPlannerBranch next = current.Parent;

                if (current.CanMergeChildren &&
                    Interlocked.CompareExchange(ref current._mergeClaimed, 1, 0) == 0)
                {
                    lock (current._mergeGate)
                    {
                        if (current.Left?.MeshModel != null && current.Right?.MeshModel != null)
                        {
                            current.Left.MeshModel.Merge(current.Right.MeshModel);
                            current.MeshModel = current.Left.MeshModel;
                            current.Left.MeshModel = null;
                            current.Right.MeshModel = null;
                            OnNodeCompleted?.Invoke(current, true, null);
                        }
                    }
                }

                if (!current.MeshComplete)
                    break;

                if (current == Root)
                {
                    FinalizeRootComposite();
                    break;
                }

                current = next;
            }
        }
    }

    interface IAssemblyPlannerNode
    {
        /// <summary>
        /// A leaf's key matches the slice graph's node key
        /// A branch's key is a generated value starting at max value and decrementing for each branch created
        /// </summary>
        ulong Key { get; }
        int Depth { get; }

        bool IsLeaf { get; }


        /// <summary>
        /// True when this node has a mesh ready to merge with its sibling.  Only leaves with meshes return true.
        /// </summary>
        bool CanMerge { get; }

        /// <summary>
        /// True when this node has or has had a mesh and implies it and all children have or have had a mesh.  The merge is complete below this node.
        /// </summary>
        bool MeshComplete { get; }

        /// <summary>
        /// Parent node in the treeWithUniqueValues, null if the root node
        /// </summary>
        AssemblyPlannerBranch Parent { get; set; }

        /// <summary>
        /// When this mesh is not null we are ready to merge.
        /// </summary>
        SliceGraphMeshModel MeshModel { get; set; }
    }

    interface IAssemblyPlannerBranch : IAssemblyPlannerNode
    {
        IAssemblyPlannerNode Left { get; set; }
        IAssemblyPlannerNode Right { get; set; }
    }

    abstract class AssemblyPlannerNode : IAssemblyPlannerNode
    {
        private SliceGraphMeshModel _MeshModel = null;

        /// <summary>
        /// This mesh is only set once.  Setting it flips MeshComplete to true, even if it is set to null. 
        /// This tracks whether the node has finished its role in assembling the full mesh even if we later
        /// free memory by setting MeshModel to null.
        /// </summary>
        public SliceGraphMeshModel MeshModel
        {
            get => _MeshModel;
            set
            {
                _MeshModel = value;
                MeshComplete = true;
            }
        }

        public bool CanMerge => this.MeshModel != null;

        /// <summary>
        /// True when this node has or has had a mesh and implies it and all children have or have had a mesh.  The merge is complete below this node.
        /// </summary>
        public bool MeshComplete { get; private set; } = false;

        public abstract bool IsLeaf { get; }

        public abstract int Depth { get; }

        public abstract ulong Key { get; }


        /// <summary>
        /// Parent node in the treeWithUniqueValues, null if the root node
        /// </summary>
        public AssemblyPlannerBranch Parent { get; set; }

        public override bool Equals(object obj)
        {
            if (obj as IAssemblyPlannerNode is null)
                return false;

            IAssemblyPlannerNode other = (IAssemblyPlannerNode)obj;
            return other.Key == this.Key;
        }

        public override int GetHashCode() => this.Key.GetHashCode();
    }

    class AssemblyPlannerBranch : AssemblyPlannerNode, IAssemblyPlannerBranch
    {
        /// <summary>0 = unclaimed; 1 = a merge consumer has claimed this branch.</summary>
        internal int _mergeClaimed;

        /// <summary>Serializes the merge body after a successful claim.</summary>
        internal readonly object _mergeGate = new();

        /// <summary>
        /// A branch key is a generated value that begins at maxint and decrements for each branch created
        /// </summary>
        public override ulong Key { get; }

        public override int Depth => Math.Max(Left.Depth + 1, Right.Depth + 1);

        public override bool IsLeaf => false;

        /// <summary>
        /// True if both children are ready to merge
        /// </summary>
        public bool CanMergeChildren
        {
            get
            {
                if (Left != null && Right != null)
                    return Left.CanMerge && Right.CanMerge;
                else if (Left is null && Right != null)
                    return Right.CanMerge;
                else if (Left != null && Right is null)
                    return Left.CanMerge;
                else
                {
                    throw new ArgumentException("Branch node has no children");
                }
            }
        }

        readonly IAssemblyPlannerNode[] Children = new IAssemblyPlannerNode[2];


        public IAssemblyPlannerNode Left
        {
            get => Children[0]; set => Children[0] = value;
        }
        public IAssemblyPlannerNode Right
        {
            get => Children[1]; set => Children[1] = value;
        }

        static ulong NextKey = ulong.MaxValue;

        public AssemblyPlannerBranch(AssemblyPlannerBranch parent = null)
        {
            this.Parent = parent;
            this.Key = NextKey;
            NextKey--;
        }

        public AssemblyPlannerBranch(IAssemblyPlannerNode left = null, IAssemblyPlannerNode right = null, AssemblyPlannerBranch parent = null) : this(parent)
        {
            this.Left = left;
            this.Right = right;
        }

        public override string ToString() => string.Format("Branch: {2}{0}{3} Parent: {1}", Key, Parent is null ? "NULL" : Parent.Key.ToString(), this.MeshModel != null ? "*" : "", this.MeshComplete ? "F" : "");
    }


    class AssemblyPlannerLeaf : AssemblyPlannerNode
    {
        /// <summary>
        /// A leaf's key matches the Slice Graph nodes (Slice object) key
        /// </summary>
        public override ulong Key { get; }

        public override int Depth => 0;

        public override bool IsLeaf { get; } = true;

        public AssemblyPlannerLeaf(ulong sliceKey, AssemblyPlannerBranch parent = null)
        {
            this.Key = sliceKey;
            this.Parent = parent;
        }

        /// <summary>
        /// Call to add a final mesh to the leaf and allow it to merge.  Passing null indicates the mesh could not be generated
        /// but the leaf should still merge.
        /// </summary>
        /// <param name="completedMesh"></param>
        /// <param name="color">Structure color baked into vertices at creation.</param>
        public void OnMeshCompletion(BajajGeneratorMesh completedMesh, Color color)
        {
            // Vertices are stored in volume coordinates; model transform stays at origin.
            SliceGraphMeshModel model = new(color);
            if (completedMesh is null)
            {
                this.MeshModel = model;
                return;
            }

            model.AddSlice(completedMesh);
            this.MeshModel = model;
            return;
        }

        public override string ToString() => string.Format("Leaf: {2}{0}{3} Parent: {1}", Key, Parent is null ? "NULL" : Parent.Key.ToString(), this.MeshModel != null ? "*" : "", this.MeshComplete ? "F" : "");
    }

    abstract class MeshAssemblyPlannerViewBase
    {
        protected readonly MeshAssemblyPlanner Plan;

        public abstract void OnNodeCompleted(IAssemblyPlannerNode node, bool success, MeshManifoldReport? report);

        public MeshAssemblyPlannerViewBase(MeshAssemblyPlanner plan)
        {
            Plan = plan;
            Plan.OnNodeCompleted += this.OnNodeCompleted;
        }
    }


    /// <summary>
    /// Visualize the completed slices of a mesh assembly plan
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    /// <param name="plan"></param>
    /// <param name="position">Where in volume space the world matrix should position the model by default</param>
    class MeshAssemblyPlannerCompletedView(MeshAssemblyPlanner plan) : MeshAssemblyPlannerViewBase(plan), IColorView
    {
        /// <summary>
        /// A mapping of all nodes with completed models we can show as part of an incremental view
        /// </summary>
        public readonly SortedList<ulong, SliceGraphMeshModel> ReadyModels = [];

        private readonly ReaderWriterLockSlim ReadyModelLock = new();

        private MeshModel<VertexPositionNormalColor>[] _MeshModels = null;
        public MeshModel<VertexPositionNormalColor>[] MeshModels
        {
            get
            {
                try
                {
                    ReadyModelLock.EnterReadLock();

                    _MeshModels ??= [.. ReadyModels.Values.Select(rm => rm.model)];

                    return _MeshModels;
                }
                finally
                {
                    ReadyModelLock.ExitReadLock();
                }
            }
        }

        public Color Color { get; set; } = Color.CornflowerBlue;
        public float Alpha
        {
            get => Color.GetAlpha();
            set => Color = Color.SetAlpha(value);
        }

        public override void OnNodeCompleted(IAssemblyPlannerNode node, bool success, MeshManifoldReport? report)
        {
            try
            {
                ReadyModelLock.EnterWriteLock();
                if (node.MeshModel != null)
                {
                    //Color is baked at leaf creation; force a publish so Vertices are visible to Draw.
                    node.MeshModel.PublishNow();
                    if (node.MeshModel.model?.Vertices is { Length: > 0 })
                        ReadyModels.Add(node.Key, node.MeshModel);
                }

                if (node.IsLeaf == false)
                {
                    if (node is IAssemblyPlannerBranch branch)
                    {
                        ReadyModels.Remove(branch.Left.Key);
                        ReadyModels.Remove(branch.Right.Key);
                    }
                }

                _MeshModels = null;
            }
            finally
            {
                ReadyModelLock.ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// Severity of a failed leaf mesh used to color and filter assembly bounding boxes.
    /// </summary>
    enum AssemblyBoxFailureSeverity
    {
        /// <summary>No mesh or non-manifold edges — drawn red.</summary>
        Critical,
        /// <summary>Holes or inconsistent winding — drawn orange.</summary>
        Warning,
        /// <summary>Softer issues such as isolated edges — drawn yellow.</summary>
        Minor
    }

    /// <summary>
    /// Visualize the incomplete nodes of a mesh assembly plan
    /// </summary>
    class MeshAssemblyPlannerIncompleteView : MeshAssemblyPlannerViewBase
    {
        /// <summary>
        /// A mapping of all nodes that are incomplete to a boundingbox
        /// </summary>
        public SortedList<ulong, MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor>> BoundingBoxModels = [];

        /// <summary>
        /// A mapping of all nodes to their bounding box
        /// </summary>
        public Dictionary<ulong, Box> NodeBoundingBox = [];

        private readonly ReaderWriterLockSlim ReadyModelLock = new();
        private readonly SortedSet<ulong> NodesThatFailedMeshing = [];
        private readonly Dictionary<ulong, MeshManifoldReport> NodeFailureReports = [];
        private readonly Dictionary<ulong, AssemblyBoxFailureSeverity> NodeFailureSeverity = [];

        /// <summary>
        /// When false, hides red (critical / non-manifold) error overlays. Prefer the View menu Slice Status
        /// flags for finer control.
        /// </summary>
        public bool ShowRedErrorBoxes
        {
            get => ShowCriticalSliceStatus;
            set => ShowCriticalSliceStatus = value;
        }

        private bool _showInProgressSliceStatus = true;
        private bool _showSectionReadySliceStatus = true;
#if DEBUG
        private bool _showMinorIssueSliceStatus = true;
        private bool _showWarningSliceStatus = true;
        private bool _showCriticalSliceStatus = true;
#else
        private bool _showMinorIssueSliceStatus = false;
        private bool _showWarningSliceStatus = false;
        private bool _showCriticalSliceStatus = false;
#endif

        /// <summary>In-progress leaf contour overlays (gray).</summary>
        public bool ShowInProgressSliceStatus
        {
            get => _showInProgressSliceStatus;
            set
            {
                if (_showInProgressSliceStatus == value)
                    return;
                _showInProgressSliceStatus = value;
                RequestVisibleListRebuild();
            }
        }

        /// <summary>Blue branch / section-ready AABB overlays.</summary>
        public bool ShowSectionReadySliceStatus
        {
            get => _showSectionReadySliceStatus;
            set
            {
                if (_showSectionReadySliceStatus == value)
                    return;
                _showSectionReadySliceStatus = value;
                RequestVisibleListRebuild();
            }
        }

        /// <summary>Yellow minor-failure overlays.</summary>
        public bool ShowMinorIssueSliceStatus
        {
            get => _showMinorIssueSliceStatus;
            set
            {
                if (_showMinorIssueSliceStatus == value)
                    return;
                _showMinorIssueSliceStatus = value;
                RequestVisibleListRebuild();
            }
        }

        /// <summary>Orange warning (holes / winding) overlays.</summary>
        public bool ShowWarningSliceStatus
        {
            get => _showWarningSliceStatus;
            set
            {
                if (_showWarningSliceStatus == value)
                    return;
                _showWarningSliceStatus = value;
                RequestVisibleListRebuild();
            }
        }

        /// <summary>Red critical (non-manifold) overlays.</summary>
        public bool ShowCriticalSliceStatus
        {
            get => _showCriticalSliceStatus;
            set
            {
                if (_showCriticalSliceStatus == value)
                    return;
                _showCriticalSliceStatus = value;
                RequestVisibleListRebuild();
            }
        }

        /// <summary>Backward-compatible alias for <see cref="ShowCriticalSliceStatus"/>. </summary>
        public bool ShowFailedBoundingBoxes
        {
            get => ShowCriticalSliceStatus;
            set => ShowCriticalSliceStatus = value;
        }

        /// <summary>
        /// Local transform for each overlay model before world placement. Branch AABB models carry scale and
        /// centre here (unit box); leaf contour models use identity because vertices are already in slice space.
        /// </summary>
        private readonly Dictionary<ulong, Matrix> BoxLocalTransform = [];

        /// <summary>Source of leaf contour geometry for in-progress overlays.</summary>
        private readonly SliceGraph _sliceGraph;

        /// <summary>
        /// Slices that SliceGraph could not report a valid topology for. They get no box at all, so a run that
        /// silently visualizes fewer slices than it has is visible in the log rather than just looking sparse.
        /// Guarded by <see cref="ReadyModelLock"/>.
        /// </summary>
        private readonly HashSet<ulong> _leavesWithoutTopology = [];

        /// <summary>
        /// Placement last applied by <see cref="ApplyPlacement"/>.  Overlays are inserted while meshing runs, after
        /// the view was already placed, so each insert composes this rather than waiting for the next placement pass.
        /// Guarded by <see cref="ReadyModelLock"/>.
        /// </summary>
        private Matrix _placement = Matrix.Identity;

        /// <summary>Last published snapshot for Draw. Never rebuilt on the draw thread.</summary>
        private MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor>[] _MeshModels =
            Array.Empty<MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor>>();

        /// <summary>
        /// Memo of <see cref="HasIncompleteDescendantLeaf"/> by node key. Cleared along the ancestor chain when a
        /// node completes so unrelated subtrees keep their answers across visible-list rebuilds.
        /// Concurrent because the rebuild task reads while meshing invalidates under write.
        /// </summary>
        private readonly ConcurrentDictionary<ulong, bool> _incompleteDescendantCache = new();

        /// <summary>1 while a rebuild task owns the single-flight slot.</summary>
        private int _rebuildRunning;

        /// <summary>1 if a rebuild was requested while a rebuild was running (or before one started).</summary>
        private int _rebuildRequested;

        /// <summary>1 after <see cref="CancelRebuild"/>; rebuild loop exits and ignores further requests.</summary>
        private int _rebuildCancelled;

        /// <summary>
        /// Published visible-box list. Draw only reads this snapshot; rebuilds run off the draw thread.
        /// </summary>
        public MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor>[] MeshModels
        {
            get
            {
                try
                {
                    ReadyModelLock.EnterReadLock();
                    return _MeshModels;
                }
                finally
                {
                    ReadyModelLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Mark the visible list dirty and ensure exactly one rebuild task is running.
        /// If a rebuild is already in flight, it will notice <see cref="_rebuildRequested"/> when it finishes
        /// and run another pass.
        /// </summary>
        public void RequestVisibleListRebuild()
        {
            if (Volatile.Read(ref _rebuildCancelled) != 0)
                return;

            Volatile.Write(ref _rebuildRequested, 1);

            if (Interlocked.CompareExchange(ref _rebuildRunning, 1, 0) != 0)
                return;

            Task.Run(RebuildVisibleListLoop);
        }

        /// <summary>
        /// Stop scheduling rebuilds (e.g. when this view is replaced by a new mesh generation).
        /// </summary>
        public void CancelRebuild()
        {
            Volatile.Write(ref _rebuildCancelled, 1);
            Volatile.Write(ref _rebuildRequested, 0);
        }

        void RebuildVisibleListLoop()
        {
            try
            {
                while (Volatile.Read(ref _rebuildCancelled) == 0)
                {
                    //Clear the request flag at the start of this pass so completions during the pass set it again.
                    Interlocked.Exchange(ref _rebuildRequested, 0);

                    MeshModel<VertexPositionColor>[] models;
                    try
                    {
                        ReadyModelLock.EnterReadLock();
                        models = [.. GetVisibleBoundingBoxModels()];
                    }
                    finally
                    {
                        ReadyModelLock.ExitReadLock();
                    }

                    if (Volatile.Read(ref _rebuildCancelled) != 0)
                        return;

                    try
                    {
                        ReadyModelLock.EnterWriteLock();
                        _MeshModels = models;
                    }
                    finally
                    {
                        ReadyModelLock.ExitWriteLock();
                    }

                    //Another request arrived while we were building — run again; otherwise leave the single-flight slot.
                    if (Volatile.Read(ref _rebuildRequested) == 0)
                        return;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _rebuildRunning, 0);

                //Request landed after the last requested-check but before we cleared running — start again.
                if (Volatile.Read(ref _rebuildCancelled) == 0
                    && Volatile.Read(ref _rebuildRequested) != 0)
                {
                    RequestVisibleListRebuild();
                }
            }
        }

        /// <summary>
        /// Show bounding box models whose siblings have meshes or no siblings
        /// Branches decide for their children whether to add the children or themselves
        /// </summary>
        private List<MeshModel<VertexPositionColor>> GetVisibleBoundingBoxModels()
        {
            List<MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor>> listModels = new(BoundingBoxModels.Count);
            foreach (var item in BoundingBoxModels)
            {
                if (NodesThatFailedMeshing.Contains(item.Key))
                {
                    AssemblyBoxFailureSeverity severity = NodeFailureSeverity.TryGetValue(item.Key, out AssemblyBoxFailureSeverity s)
                        ? s
                        : AssemblyBoxFailureSeverity.Critical;

                    bool show = severity switch
                    {
                        AssemblyBoxFailureSeverity.Critical => ShowCriticalSliceStatus,
                        AssemblyBoxFailureSeverity.Warning => ShowWarningSliceStatus,
                        _ => ShowMinorIssueSliceStatus
                    };
                    if (!show)
                        continue;

                    listModels.Add(item.Value);
                }
                else if (CanShowBoundingBoxModel(this.Plan[item.Key]))
                {
                    IAssemblyPlannerNode node = this.Plan[item.Key];
                    if (node.IsLeaf)
                    {
                        if (!ShowInProgressSliceStatus)
                            continue;
                    }
                    else if (!ShowSectionReadySliceStatus)
                    {
                        continue;
                    }

                    listModels.Add(item.Value);
                }
            }

            return listModels;
        }

        /// <summary>
        /// True when any descendant leaf has not finished meshing. Results are memoized until
        /// <see cref="InvalidateIncompleteDescendantCache"/> runs for that node or an ancestor.
        /// </summary>
        private bool HasIncompleteDescendantLeaf(IAssemblyPlannerNode node)
        {
            if (_incompleteDescendantCache.TryGetValue(node.Key, out bool cached))
                return cached;

            bool result;
            if (node.IsLeaf)
            {
                result = !node.MeshComplete;
            }
            else if (node is IAssemblyPlannerBranch branch)
            {
                result = (branch.Left != null && HasIncompleteDescendantLeaf(branch.Left))
                    || (branch.Right != null && HasIncompleteDescendantLeaf(branch.Right));
            }
            else
            {
                result = false;
            }

            _incompleteDescendantCache[node.Key] = result;
            return result;
        }

        /// <summary>
        /// Drop memoized incomplete-descendant answers for <paramref name="node"/> and every parent up to the root.
        /// </summary>
        private void InvalidateIncompleteDescendantCache(IAssemblyPlannerNode node)
        {
            for (IAssemblyPlannerNode n = node; n != null; n = n.Parent)
                _incompleteDescendantCache.TryRemove(n.Key, out _);
        }

        /// <summary>
        /// A node is boxed until its own mesh exists. Branch boxes stay hidden while any inner slice box is still visible.
        /// </summary>
        private bool CanShowBoundingBoxModel(IAssemblyPlannerNode node) =>
            !node.MeshComplete && (node.IsLeaf || !HasIncompleteDescendantLeaf(node));

        internal static AssemblyBoxFailureSeverity SeverityForFailure(MeshManifoldReport? report)
        {
            if (report is null || report.Value.NonManifoldEdges > 0)
                return AssemblyBoxFailureSeverity.Critical;

            MeshManifoldReport r = report.Value;
            if (r.UnexpectedBoundaryEdges > 0 || r.InconsistentManifoldEdges > 0)
                return AssemblyBoxFailureSeverity.Warning;

            return AssemblyBoxFailureSeverity.Minor;
        }

        internal static Color ColorForFailure(MeshManifoldReport? report) => ColorForFailure(SeverityForFailure(report));

        internal static Color ColorForFailure(AssemblyBoxFailureSeverity severity) => severity switch
        {
            AssemblyBoxFailureSeverity.Critical => Color.Red.SetAlpha(0.5f),
            AssemblyBoxFailureSeverity.Warning => Color.Orange.SetAlpha(0.5f),
            _ => Color.Yellow.SetAlpha(0.5f)
        };


        /// <summary>
        /// Build every overlay now.  Requires <paramref name="sliceGraph"/> to have finished topology initialization.
        /// Leaves are published as they are built so the first contours draw before the last one exists.
        /// </summary>
        public MeshAssemblyPlannerIncompleteView(MeshAssemblyPlanner plan, SliceGraph sliceGraph)
            : this(plan, sliceGraph, deferLeafContours: false)
        {
        }

        /// <summary>
        /// With <paramref name="deferLeafContours"/> the view starts empty and the caller feeds
        /// <see cref="PublishLeafContour"/> from <see cref="SliceGraph.InitializeTopologyAsync"/> as each slice
        /// topology completes, then calls <see cref="CompleteBranchOverlays"/> once every topology exists.  That lets
        /// a large structure show its contours while its topologies are still being computed.
        /// </summary>
        public MeshAssemblyPlannerIncompleteView(MeshAssemblyPlanner plan, SliceGraph sliceGraph, bool deferLeafContours) : base(plan)
        {
            _sliceGraph = sliceGraph ?? throw new ArgumentNullException(nameof(sliceGraph));

            if (!deferLeafContours)
                CompleteBranchOverlays();
        }

        /// <summary>
        /// Add the in-progress contour overlay for one slice.  Safe to call from the topology worker threads; the
        /// line list is built outside the lock and only the dictionary inserts are serialized.  An invalid
        /// topology records the leaf as having no overlay so <see cref="CompleteBranchOverlays"/> does not try again.
        /// </summary>
        public void PublishLeafContour(ulong sliceKey, SliceTopology topology)
        {
            using var _phase = MeshPhaseTimings.Measure(MeshPhase.IncompleteViewContours);

            if (!topology.IsValid || topology.Shapes is null || topology.Shapes.Length == 0)
            {
                try
                {
                    ReadyModelLock.EnterWriteLock();
                    _leavesWithoutTopology.Add(sliceKey);
                }
                finally
                {
                    ReadyModelLock.ExitWriteLock();
                }

                return;
            }

            //Left in the slice graph's centered frame, the same space the mesh verticies use.  These models
            //are given the view's placement ModelMatrix alongside the mesh models, so translating to volume
            //XY here would apply that offset a second time and draw every box away from its own geometry.
            Rectangle boundingRect = topology.Shapes.BoundingBox();
            Box bbox = new(boundingRect, topology.ShapeZ.Min(), topology.ShapeZ.Max());

            //Leaf fallback when contour build failed.
            MeshModel<VertexPositionColor> model = GenerateLeafContourMesh(topology)
                ?? bbox.ToMeshModelEdgesOnly(Color.LightGray.SetAlpha(0.5f));

            try
            {
                ReadyModelLock.EnterWriteLock();
                NodeBoundingBox[sliceKey] = bbox;

                //Branch overlays are completed while faces are already generating; a leaf that meshed in the
                //meantime has nothing in progress to show and OnNodeCompleted has already run for it.
                if (!Plan[sliceKey].MeshComplete)
                    InsertOverlayModelUnlocked(sliceKey, model);
            }
            finally
            {
                ReadyModelLock.ExitWriteLock();
            }

            RequestVisibleListRebuild();
        }

        /// <summary>
        /// Publish any leaf that has not been fed through <see cref="PublishLeafContour"/>, then build the branch
        /// AABB overlays from the leaf boxes.  Every slice topology must exist by now; a leaf that is still missing
        /// is read from the slice graph's cache.  Branch boxes only draw once all their leaves have meshed, so
        /// deferring them until here costs nothing visible.
        /// </summary>
        public void CompleteBranchOverlays()
        {
            using var _phase = MeshPhaseTimings.Measure(MeshPhase.IncompleteViewContours, Plan.Slices.Count);

            foreach (ulong sliceKey in Plan.Slices.Keys)
            {
                bool havePublished;
                try
                {
                    ReadyModelLock.EnterReadLock();
                    havePublished = NodeBoundingBox.ContainsKey(sliceKey) || _leavesWithoutTopology.Contains(sliceKey);
                }
                finally
                {
                    ReadyModelLock.ExitReadLock();
                }

                if (!havePublished)
                    PublishLeafContour(sliceKey, _sliceGraph.GetTopology(sliceKey));
            }

            Dictionary<ulong, Box> leafBoxes;
            int leavesWithoutTopology;
            try
            {
                ReadyModelLock.EnterReadLock();
                leafBoxes = new Dictionary<ulong, Box>(NodeBoundingBox);
                leavesWithoutTopology = _leavesWithoutTopology.Count;
            }
            finally
            {
                ReadyModelLock.ExitReadLock();
            }

            Dictionary<ulong, Box> branchBoxes = [];
            CalculateBranchBoundingBox(Plan.Root, leafBoxes, branchBoxes);

            List<KeyValuePair<ulong, MeshModel<VertexPositionColor>>> branchModels = new(branchBoxes.Count);
            foreach (KeyValuePair<ulong, Box> item in branchBoxes)
                branchModels.Add(new(item.Key, GenerateBranchBoxMesh(item.Value)));

            try
            {
                ReadyModelLock.EnterWriteLock();
                foreach (KeyValuePair<ulong, Box> item in branchBoxes)
                    NodeBoundingBox[item.Key] = item.Value;

                foreach (KeyValuePair<ulong, MeshModel<VertexPositionColor>> item in branchModels)
                {
                    //Faces are generating while this runs; a branch that already merged owes no overlay.
                    if (!Plan[item.Key].MeshComplete)
                        InsertOverlayModelUnlocked(item.Key, item.Value);
                }
            }
            finally
            {
                ReadyModelLock.ExitWriteLock();
            }

            if (leavesWithoutTopology > 0)
                Trace.WriteLine($"MeshAssemblyPlannerIncompleteView: {leavesWithoutTopology} slices reported invalid topology and have no bounding box. {BoundingBoxModels.Count} boxes generated.");

            RequestVisibleListRebuild();
        }

        /// <summary>
        /// Record the model's own transform and stamp the current placement on it, so a model added after
        /// <see cref="ApplyPlacement"/> ran does not sit in slice-local space until the placement changes again.
        /// Caller holds the write lock.
        /// </summary>
        private void InsertOverlayModelUnlocked(ulong key, MeshModel<VertexPositionColor> model)
        {
            Matrix local = model.ModelMatrix;
            BoxLocalTransform[key] = local;
            model.ModelMatrix = local * _placement;
            BoundingBoxModels[key] = model;
        }

        public override void OnNodeCompleted(IAssemblyPlannerNode node, bool success, MeshManifoldReport? report)
        {
            try
            {
                ReadyModelLock.EnterWriteLock();

                //MeshComplete is set before this callback; ancestor CanShow answers are stale along this chain.
                InvalidateIncompleteDescendantCache(node);

                if (success)
                {
                    BoundingBoxModels.Remove(node.Key);
                    NodesThatFailedMeshing.Remove(node.Key);
                    NodeFailureReports.Remove(node.Key);
                    NodeFailureSeverity.Remove(node.Key);
                }
                else if (BoundingBoxModels.TryGetValue(node.Key, out MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor> model))
                {
                    AssemblyBoxFailureSeverity severity = SeverityForFailure(report);
                    NodesThatFailedMeshing.Add(node.Key);
                    NodeFailureSeverity[node.Key] = severity;
                    if (report.HasValue)
                        NodeFailureReports[node.Key] = report.Value;
                    model.SetColor(ColorForFailure(severity));
                }
            }
            finally
            {
                ReadyModelLock.ExitWriteLock();
            }

            //Keep the last published snapshot for Draw; rebuild coalesces on a background task.
            RequestVisibleListRebuild();
        }

        /// <summary>
        /// Position the boxes in the world without discarding the transform that gives them their size.
        /// The placement is remembered so overlays published later are stamped on insert.
        /// </summary>
        public void ApplyPlacement(Matrix placement)
        {
            try
            {
                ReadyModelLock.EnterWriteLock();
                _placement = placement;

                foreach (var item in BoundingBoxModels)
                {
                    if (BoxLocalTransform.TryGetValue(item.Key, out Matrix local))
                        item.Value.ModelMatrix = local * placement;
                }
            }
            finally
            {
                ReadyModelLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Branch overlay: AABB wireframe scaled slightly so it does not overdraw the leaf geometry it encloses.
        /// </summary>
        private static MeshModel<VertexPositionColor> GenerateBranchBoxMesh(Box bbox)
        {
            Box scaled = bbox.Scale(new Geometry.Vector3(1.02, 1.02, 1));
            return scaled.ToMeshModelEdgesOnly(Color.DarkBlue.SetAlpha(0.5f));
        }

        /// <summary>
        /// Line segments along each contour vertex ring (and polyline edges) in the slice's centered XY frame.
        /// </summary>
        private static MeshModel<VertexPositionColor> GenerateLeafContourMesh(SliceTopology topology)
        {
            if (!topology.IsValid || topology.Shapes is null || topology.Shapes.Length == 0)
                return null;

            Color color = Color.LightGray.SetAlpha(0.75f);
            List<VertexPositionColor> verts = new(256);
            List<int> edges = new(512);

            for (int i = 0; i < topology.Shapes.Length; i++)
            {
                double z = topology.ShapeZ[i];
                AppendShapeContour(topology.Shapes[i], z, color, verts, edges);
            }

            if (edges.Count == 0)
                return null;

            return new MeshModel<VertexPositionColor>
            {
                Vertices = [.. verts],
                Edges = [.. edges],
                Primitive = PrimitiveType.LineList,
                ModelMatrix = Matrix.Identity
            };
        }

        private static void AppendShapeContour(
            IShape2D shape,
            double z,
            Color color,
            List<VertexPositionColor> verts,
            List<int> edges)
        {
            switch (shape)
            {
                case Polygon poly:
                    AppendClosedRing(poly.ExteriorRing, z, color, verts, edges);
                    foreach (Geometry.Vector2[] hole in poly.InteriorRings)
                        AppendClosedRing(hole, z, color, verts, edges);
                    break;

                case Polyline line:
                    AppendOpenPolyline(line.Points, z, color, verts, edges);
                    break;

                case Circle circle:
                    AppendClosedRing(TessellateCircle(circle, segments: 32), z, color, verts, edges);
                    break;
            }
        }

        private static void AppendClosedRing(
            Geometry.Vector2[] ring,
            double z,
            Color color,
            List<VertexPositionColor> verts,
            List<int> edges)
        {
            if (ring is null || ring.Length < 2)
                return;

            //Closed rings store first == last; drop the duplicate for the vertex buffer.
            int count = ring.Length > 1 && ring[0] == ring[ring.Length - 1]
                ? ring.Length - 1
                : ring.Length;
            if (count < 2)
                return;

            int baseIdx = verts.Count;
            for (int i = 0; i < count; i++)
                verts.Add(new VertexPositionColor(ring[i].ToXNAVector3(z), color));

            for (int i = 0; i < count; i++)
            {
                edges.Add(baseIdx + i);
                edges.Add(baseIdx + ((i + 1) % count));
            }
        }

        private static void AppendOpenPolyline(
            IReadOnlyList<IPoint2D> points,
            double z,
            Color color,
            List<VertexPositionColor> verts,
            List<int> edges)
        {
            if (points is null || points.Count < 2)
                return;

            int baseIdx = verts.Count;
            for (int i = 0; i < points.Count; i++)
            {
                IPoint2D p = points[i];
                verts.Add(new VertexPositionColor(new Vector3((float)p.X, (float)p.Y, (float)z), color));
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                edges.Add(baseIdx + i);
                edges.Add(baseIdx + i + 1);
            }
        }

        private static Geometry.Vector2[] TessellateCircle(Circle circle, int segments)
        {
            Geometry.Vector2[] ring = new Geometry.Vector2[segments + 1];
            for (int i = 0; i < segments; i++)
            {
                double angle = 2.0 * Math.PI * i / segments;
                ring[i] = new Geometry.Vector2(
                    circle.Center.X + circle.Radius * Math.Cos(angle),
                    circle.Center.Y + circle.Radius * Math.Sin(angle));
            }

            ring[segments] = ring[0];
            return ring;
        }

        /// <summary>
        /// Union the leaf boxes up the merge tree.  Leaves come from <paramref name="leafBoxes"/> (a snapshot taken
        /// under the lock); branch results go to <paramref name="branchBoxes"/> for the caller to insert.
        /// A leaf with no box (invalid topology) contributes nothing.
        /// </summary>
        private static Box? CalculateBranchBoundingBox(IAssemblyPlannerNode node, IReadOnlyDictionary<ulong, Box> leafBoxes, Dictionary<ulong, Box> branchBoxes)
        {
            if (node is null)
                return null;

            if (node is not IAssemblyPlannerBranch branch)
                return leafBoxes.TryGetValue(node.Key, out Box leafBox) ? leafBox : null;

            Box? lbox = CalculateBranchBoundingBox(branch.Left, leafBoxes, branchBoxes);
            Box? rbox = CalculateBranchBoundingBox(branch.Right, leafBoxes, branchBoxes);

            Box result;
            if (lbox.HasValue && rbox.HasValue)
                result = lbox.Value.Union(rbox.Value, out _);
            else if (lbox.HasValue)
                result = lbox.Value;
            else if (rbox.HasValue)
                result = rbox.Value;
            else
                return null;

            branchBoxes[branch.Key] = result;
            return result;
        }
    }
}
