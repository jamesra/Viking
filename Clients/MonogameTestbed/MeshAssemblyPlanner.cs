using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbed
{
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

        public static MeshAssemblyPlanner Create(SliceGraph sliceGraph)
        {
            //AssemblyPlannerLeaf[] firstLayer = sliceGraph.Nodes.Keys.OrderBy(k => k).Select(k => new AssemblyPlannerLeaf(k)).ToArray();
            AssemblyPlannerLeaf[] firstLayer = [.. sliceGraph.Nodes.Keys.OrderBy(k => {
                SliceTopology t = sliceGraph.GetTopology(k);
                return t.ShapeZ != null ?
                    t.ShapeZ.Length > 0 ?
                        Math.Round(t.ShapeZ.Average())
                        : -1
                    : -1;
            }).Select(k => new AssemblyPlannerLeaf(k))];

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

            return new MeshAssemblyPlanner(currentLayer[0], Nodes, Slices);
        }

        private MeshAssemblyPlanner(IAssemblyPlannerNode root, Dictionary<ulong, IAssemblyPlannerNode> nodes, SortedList<ulong, AssemblyPlannerLeaf> slices)
        {
            Root = root;
            Nodes = nodes;
            Slices = slices;
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
                Trace.WriteLine($"Slice {slice.Key} merged without a complete mesh{(mesh is null ? " (no mesh was generated)" : $": {mesh.ManifoldReport}")}.");

            leaf.OnMeshCompletion(mesh);
            OnNodeCompleted?.Invoke(leaf, Success, mesh?.ManifoldReport);

            /*
            try
            {
                ReadyModelLock.EnterWriteLock();
                ReadyModels.Add(leaf.Key, leaf.MeshModel);
                _MeshModels = null;
            }
            finally
            {
                ReadyModelLock.ExitWriteLock();
            }
            */

            CheckForMerge(leaf.Parent);


            if (leaf == Root)//This covers the case of a single node mesh plan.
            {
                FinalizeRootComposite();
            }
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

            Root?.MeshModel?.EnsureCompositeWinding();
            MeshAssembledEvent.Set();
        }

        public void CheckForMerge(AssemblyPlannerBranch node)
        {
            if (node is null)
                return;

            //Check if the leaf parents can be merged.
            AssemblyPlannerBranch parent = node;
            while (parent != null)
            {
                bool MergePerformed = false;
                //We try because there is a chance another thread will be running merge before us and we don't want to wait.
                try
                {
                    parent.BranchLock.EnterUpgradeableReadLock();
                    //{
                    //try
                    //{
                    if (parent.CanMergeChildren)
                    {
                        //We try because there is a chance another thread will be running merge before us and we don't want to wait.  
                        //If the write lock is taken we presume the other thread will finish the merge and check any parents upstream.
                        if (parent.BranchLock.TryEnterWriteLock(0))
                        {
                            try
                            {
                                //Merge both children and discard the right model
                                parent.Left.MeshModel.Merge(parent.Right.MeshModel);
                                parent.MeshModel = parent.Left.MeshModel;

                                MergePerformed = true;
                                /*try
                                {
                                    ReadyModelLock.EnterWriteLock();
                                    ReadyModels.Remove(parent.Left.Key);
                                    ReadyModels.Remove(parent.Right.Key);
                                    ReadyModels.Add(parent.Key, parent.MeshModel);
                                    _MeshModels = null;
                                }
                                finally
                                {
                                    ReadyModelLock.ExitWriteLock();
                                }
                                */

                                parent.Left.MeshModel = null; //Free memory
                                parent.Right.MeshModel = null; //Free memory
                            }
                            finally
                            {
                                parent.BranchLock.ExitWriteLock();
                            }
                        }
                    }
                }
                finally
                {
                    parent.BranchLock.ExitUpgradeableReadLock();
                }

                if (MergePerformed && OnNodeCompleted != null)
                {
                    OnNodeCompleted(parent, true, null);
                }
                //}

                if (parent == Root)
                {
                    if (Root.MeshComplete)
                    {
                        FinalizeRootComposite();
                    }
                }

                parent = parent.Parent;
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
        public ReaderWriterLockSlim BranchLock = new();

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
        public void OnMeshCompletion(BajajGeneratorMesh completedMesh)
        {
            // Vertices are stored in volume coordinates; model transform stays at origin.
            SliceGraphMeshModel model = new();
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
                    if (node.MeshModel.model?.Vertices is { Length: > 0 })
                    {
                        node.MeshModel.Color = this.Color;
                        ReadyModels.Add(node.Key, node.MeshModel);
                    }
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

        /// <summary>
        /// When false, problem-colored slice boxes (red/orange/yellow) are hidden while in-progress gray boxes remain.
        /// Toggle with R in BajajMultiTest.
        /// </summary>
        public bool ShowFailedBoundingBoxes
        {
            get => _showFailedBoundingBoxes;
            set
            {
                if (_showFailedBoundingBoxes == value)
                    return;

                _showFailedBoundingBoxes = value;
                try
                {
                    ReadyModelLock.EnterWriteLock();
                    _MeshModels = null;
                }
                finally
                {
                    ReadyModelLock.ExitWriteLock();
                }
            }
        }

        private bool _showFailedBoundingBoxes = true;

        /// <summary>
        /// The scale-and-centre transform each box was built with.  A box is a unit cube that carries all of its
        /// size and position in its ModelMatrix, so placement has to be composed onto this rather than assigned
        /// over it.
        /// </summary>
        private readonly Dictionary<ulong, Matrix> BoxLocalTransform = [];

        /// <summary>
        /// Slices that SliceGraph could not report a valid topology for. They get no box at all, so a run that
        /// silently visualizes fewer slices than it has is visible in the log rather than just looking sparse.
        /// </summary>
        private int _leavesWithoutBoundingBox;

        private MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor>[] _MeshModels = null;
        public MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor>[] MeshModels
        {
            get
            {
                try
                {
                    ReadyModelLock.EnterUpgradeableReadLock();

                    if (_MeshModels != null)
                        return _MeshModels;

                    MeshModel<VertexPositionColor>[] models = [.. GetVisibleBoundingBoxModels()];

                    //Publishing the cache takes the write lock. The getter runs on the draw thread while meshing
                    //threads invalidate it from OnNodeCompleted, so filling it under a read lock let two callers
                    //build and publish concurrently.
                    try
                    {
                        ReadyModelLock.EnterWriteLock();
                        _MeshModels = models;
                    }
                    finally
                    {
                        ReadyModelLock.ExitWriteLock();
                    }

                    return _MeshModels;
                }
                finally
                {
                    ReadyModelLock.ExitUpgradeableReadLock();
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
                    if (ShowFailedBoundingBoxes)
                        listModels.Add(item.Value);
                }
                else if (CanShowBoundingBoxModel(this.Plan[item.Key]))
                {
                    listModels.Add(item.Value);
                }
            }

            return listModels;
        }

        /// <summary>
        /// True when any descendant leaf has not finished meshing.
        /// </summary>
        private static bool HasIncompleteDescendantLeaf(IAssemblyPlannerNode node)
        {
            if (node.IsLeaf)
                return !node.MeshComplete;

            if (node is IAssemblyPlannerBranch branch)
            {
                return (branch.Left != null && HasIncompleteDescendantLeaf(branch.Left))
                    || (branch.Right != null && HasIncompleteDescendantLeaf(branch.Right));
            }

            return false;
        }

        /// <summary>
        /// A node is boxed until its own mesh exists. Branch boxes stay hidden while any inner slice box is still visible.
        /// </summary>
        internal static bool CanShowBoundingBoxModel(IAssemblyPlannerNode node) =>
            !node.MeshComplete && (node.IsLeaf || !HasIncompleteDescendantLeaf(node));

        internal static Color ColorForFailure(MeshManifoldReport? report)
        {
            if (report is null || report.Value.NonManifoldEdges > 0)
                return Color.Red.SetAlpha(0.5f);

            MeshManifoldReport r = report.Value;
            if (r.UnexpectedBoundaryEdges > 0 || r.InconsistentManifoldEdges > 0)
                return Color.Orange.SetAlpha(0.5f);

            return Color.Yellow.SetAlpha(0.5f);
        }


        public MeshAssemblyPlannerIncompleteView(MeshAssemblyPlanner plan, SliceGraph sliceGraph) : base(plan)
        {
            CalculateAllBoundingBoxes(plan, sliceGraph);
            try
            {
                ReadyModelLock.EnterWriteLock();
                GenerateAllBoundingBoxMeshesRecursive(plan.Root);
            }
            finally
            {
                ReadyModelLock.ExitWriteLock();
            }

            if (_leavesWithoutBoundingBox > 0)
                Trace.WriteLine($"MeshAssemblyPlannerIncompleteView: {_leavesWithoutBoundingBox} slices reported invalid topology and have no bounding box. {BoundingBoxModels.Count} boxes generated.");
        }

        public override void OnNodeCompleted(IAssemblyPlannerNode node, bool success, MeshManifoldReport? report)
        {
            try
            {
                ReadyModelLock.EnterWriteLock();

                if (success)
                {
                    BoundingBoxModels.Remove(node.Key);
                    NodesThatFailedMeshing.Remove(node.Key);
                    NodeFailureReports.Remove(node.Key);
                }
                else if (BoundingBoxModels.TryGetValue(node.Key, out MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor> model))
                {
                    NodesThatFailedMeshing.Add(node.Key);
                    if (report.HasValue)
                        NodeFailureReports[node.Key] = report.Value;
                    model.SetColor(ColorForFailure(report));
                }

                this._MeshModels = null;
            }
            finally
            {
                ReadyModelLock.ExitWriteLock();
            }
        }

        private void GenerateAllBoundingBoxMeshesRecursive(IAssemblyPlannerNode node)
        {
            if (node is null)
                return;

            if (node is IAssemblyPlannerBranch branch)
            {
                if (branch.Left != null)
                {
                    GenerateAllBoundingBoxMeshesRecursive(branch.Left);
                }

                if (branch.Right != null)
                {
                    GenerateAllBoundingBoxMeshesRecursive(branch.Right);
                }
            }

            //Generate our bounding box mesh
            var model = GenerateBoundingBoxMesh(node);
            if (model != null)
            {
                BoundingBoxModels[node.Key] = model;
                BoxLocalTransform[node.Key] = model.ModelMatrix;
            }
        }

        /// <summary>
        /// Position the boxes in the world without discarding the transform that gives them their size.
        /// </summary>
        public void ApplyPlacement(Matrix placement)
        {
            try
            {
                ReadyModelLock.EnterReadLock();

                foreach (var item in BoundingBoxModels)
                {
                    if (BoxLocalTransform.TryGetValue(item.Key, out Matrix local))
                        item.Value.ModelMatrix = local * placement;
                }
            }
            finally
            {
                ReadyModelLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Create a 3D Box of triangles showing the boundaries of the node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor> GenerateBoundingBoxMesh(IAssemblyPlannerNode node)
        {
            IAssemblyPlannerBranch branch = node as IAssemblyPlannerBranch;
            if (NodeBoundingBox.TryGetValue(node.Key, out Box bbox) && bbox != default)
            {
                if (node.Depth > 0)
                {
                    //For branches we scale the bounding box visual a bit to prevent overdrawing the leaf bounding box
                    bbox = bbox.Scale(new Geometry.Vector3(1.02, 1.02, 1));
                }

                //We have a bounding box from the cache, now build the mesh
                var Color = node.IsLeaf ? Microsoft.Xna.Framework.Color.LightGray.SetAlpha(0.5f) : Microsoft.Xna.Framework.Color.DarkBlue.SetAlpha(0.5f);
                var model = bbox.ToMeshModelEdgesOnly(Color);

                //Scale the bounding box slightly based on the node depth
                return model;
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="sliceGraph"></param>
        /// <returns></returns>
        private Box? CalculateAllBoundingBoxes(MeshAssemblyPlanner plan, SliceGraph sliceGraph) => CalculateBoundingBox(plan.Root, sliceGraph); //Populate our bounding boxes from the root on down

        private Box? CalculateBoundingBox(IAssemblyPlannerNode node, SliceGraph sliceGraph)
        {
            if (node is IAssemblyPlannerBranch branch)
            {
                Box? lbox = default;
                Box? rbox = default;

                if (branch.Left != null)
                {
                    lbox = CalculateBoundingBox(branch.Left, sliceGraph);
                }

                if (branch.Right != null)
                {
                    rbox = CalculateBoundingBox(branch.Right, sliceGraph);
                }

                Box result = default;
                if (lbox.HasValue && rbox.HasValue)
                {
                    result = lbox.Value.Union(rbox.Value, out _);
                }
                else if (lbox.HasValue)
                {
                    result = lbox.Value;
                }
                else if (rbox.HasValue)
                {
                    result = rbox.Value;
                }
                else
                {
                    return null;
                }

                NodeBoundingBox[branch.Key] = result;
                return result;
            }
            else //Is a leaf
            {
                var topology = sliceGraph.GetTopology(node.Key);
                if (!topology.IsValid || topology.Shapes is null || topology.Shapes.Length == 0)
                {
                    _leavesWithoutBoundingBox++;
                    return null;
                }

                //Left in the slice graph's centered frame, the same space the mesh verticies use.  These models
                //are given the view's placement ModelMatrix alongside the mesh models, so translating to volume
                //XY here would apply that offset a second time and draw every box away from its own geometry.
                Rectangle boundingRect = topology.Shapes.BoundingBox();
                Box bbox = new(boundingRect, topology.ShapeZ.Min(), topology.ShapeZ.Max());
                NodeBoundingBox[node.Key] = bbox;
                return bbox;
            }
        }
    }
}
