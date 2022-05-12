using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    /// <summary>
    /// This is a binary tree where leaves represent meshes.  Branches represent meshes that should be merged when both leaves have finished mesh generation.  Nodes are merged until only a single root leaf node exists with the final mesh
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
        public System.Threading.ManualResetEventSlim MeshAssembledEvent = new ManualResetEventSlim();

        public delegate void OnNodeMeshCompletedDelegate(IAssemblyPlannerNode node, bool success);
         
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
            AssemblyPlannerLeaf[] firstLayer = sliceGraph.Nodes.Keys.OrderBy(k => {
                SliceTopology t = sliceGraph.GetTopology(k);
                return t.PolyZ != null ?
                    t.PolyZ.Length > 0 ?
                        Math.Round(t.PolyZ.Average())
                        : -1
                    : -1;
            }).Select(k => new AssemblyPlannerLeaf(k, sliceGraph.BoundingBox.CenterPoint)).ToArray();
            
            var Nodes = new Dictionary<ulong, IAssemblyPlannerNode>(sliceGraph.Nodes.Count * 2);
            var Slices = new SortedList<ulong, AssemblyPlannerLeaf>(firstLayer.Length);
            foreach (var leaf in firstLayer)
            {
                Slices.Add(leaf.Key, leaf);
                Nodes.Add(leaf.Key, leaf);
            }

            IAssemblyPlannerNode[] currentLayer = firstLayer;
            //This isn't a true binary tree because branches do not have values.  We build our tree from the bottom up. This 
            //always generates a balances tree.
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

            for(int iLayer = 0; iLayer < layer.Length; iLayer++)
            {
                int iChild = iLayer * 2;
                IAssemblyPlannerNode newNode;
                if(iChild + 1 >= nodes.Length)
                {
                    //Add a leaf node to the end of the layer
                    newNode = nodes[iChild];
                }
                else
                {
                    var branch = new AssemblyPlannerBranch(nodes[iChild], nodes[iChild+1]);
                    nodes[iChild].Parent = branch;
                    nodes[iChild+1].Parent = branch;
                    newNode = branch;
                }

                layer[iLayer] = newNode;
            }

            return layer;
        }

        public IAssemblyPlannerNode this[ulong id] => Nodes[id];

        /// <summary>
        /// Called when a mesh is completed.  Generates a model and attempts to merge that model up the tree.
        /// Thread safe
        /// </summary>
        /// <param name="mesh"></param>
        public void OnMeshCompleted(BajajGeneratorMesh mesh, bool Success)
        {
            AssemblyPlannerLeaf leaf = this.Slices[mesh.Slice.Key];
            leaf.OnMeshCompletion(mesh);
            OnNodeCompleted?.Invoke(leaf, Success);

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
        }

        public void CheckForMerge(AssemblyPlannerBranch node)
        {
            if (node == null)
                return;

            //Check if the leaf parents can be merged.
            AssemblyPlannerBranch parent = node;
            while(parent != null)
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
                    OnNodeCompleted(parent, true);
                }
                //}

                if (parent == Root)
                {
                    if (Root.MeshComplete)
                    {
                        MeshAssembledEvent.Set();
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
        /// Parent node in the tree, null if the root node
        /// </summary>
        AssemblyPlannerBranch Parent { get; set; }

        /// <summary>
        /// When this mesh is not null we are ready to merge.
        /// </summary>
        SliceGraphMeshModel MeshModel { get; set; }
    }

    interface IAssemblyPlannerBranch : IAssemblyPlannerNode
    {  
        IAssemblyPlannerNode Left { get; set;  }
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
        public SliceGraphMeshModel MeshModel { get { return _MeshModel; }
            set {
                _MeshModel = value;
                MeshComplete = true;
            }
        }

        public bool CanMerge
        {
            get { return this.MeshModel != null; }
        }

        /// <summary>
        /// True when this node has or has had a mesh and implies it and all children have or have had a mesh.  The merge is complete below this node.
        /// </summary>
        public bool MeshComplete { get; private set; } = false;

        public abstract bool IsLeaf { get; }

        public abstract int Depth { get; }

        public abstract ulong Key { get; }


        /// <summary>
        /// Parent node in the tree, null if the root node
        /// </summary>
        public AssemblyPlannerBranch Parent { get; set; }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj as IAssemblyPlannerNode, null))
                return false;

            IAssemblyPlannerNode other = (IAssemblyPlannerNode)obj;
            return other.Key == this.Key;
        }

        public override int GetHashCode()
        {
            return this.Key.GetHashCode();
        }
    }

    class AssemblyPlannerBranch : AssemblyPlannerNode, IAssemblyPlannerBranch
    {
        public ReaderWriterLockSlim BranchLock = new ReaderWriterLockSlim();

        /// <summary>
        /// A branch key is a generated value that begins at maxint and decrements for each branch created
        /// </summary>
        public override ulong Key { get; }

        public override int Depth
        {
            get
            {
                return Math.Max(Left.Depth + 1, Right.Depth + 1);
            }
        }

        public override bool IsLeaf => false;

        /// <summary>
        /// True if both children are ready to merge
        /// </summary>
        public bool CanMergeChildren {
            get
            {
                if (Left != null && Right != null)
                    return Left.CanMerge && Right.CanMerge;
                else if (Left == null && Right != null)
                    return Right.CanMerge;
                else if (Left != null && Right == null)
                    return Left.CanMerge;
                else
                {
                    throw new ArgumentException("Branch node has no children");
                }
            }
        }

        readonly IAssemblyPlannerNode[] Children = new IAssemblyPlannerNode[2];
         

        public IAssemblyPlannerNode Left { get => Children[0]; set { Children[0] = value; } }
        public IAssemblyPlannerNode Right { get => Children[1]; set { Children[1] = value; } }
        
        static ulong NextKey = ulong.MaxValue;

        public AssemblyPlannerBranch(AssemblyPlannerBranch parent = null)
        {
            this.Parent = parent;
            this.Key = NextKey;
            NextKey = NextKey - 1;
        }

        public AssemblyPlannerBranch(IAssemblyPlannerNode left=null, IAssemblyPlannerNode right=null, AssemblyPlannerBranch parent = null) : this(parent)
        {
            this.Left = left;
            this.Right = right;
        }

        public override string ToString()
        {
            return string.Format("Branch: {2}{0}{3} Parent: {1}", Key, Parent == null ? "NULL" : Parent.Key.ToString(), this.MeshModel != null ? "*" : "", this.MeshComplete ? "F" : "");
        }
    }


    class AssemblyPlannerLeaf : AssemblyPlannerNode
    {
        /// <summary>
        /// A leaf's key matches the Slice Graph nodes (Slice object) key
        /// </summary>
        public override ulong Key { get; }

        public override int Depth { get { return 0; } }

        public override bool IsLeaf { get; } = true;

        /// <summary>
        /// Where to position the mesh model this leaf generates in world space;
        /// </summary>
        public readonly GridVector3 Position;
        
        public AssemblyPlannerLeaf(ulong sliceKey, GridVector3 Position, AssemblyPlannerBranch parent = null)
        {
            this.Key = sliceKey;
            this.Parent = parent;
            this.Position = Position;
        }

        /// <summary>
        /// Call to add a final mesh to the leaf and allow it to merge.  Passing null indicates the mesh could not be generated
        /// but the leaf should still merge.
        /// </summary>
        /// <param name="completedMesh"></param>
        public void OnMeshCompletion(BajajGeneratorMesh completedMesh)
        {
            SliceGraphMeshModel model = new SliceGraphMeshModel(Position.XY().ToGridVector3(0));
            if (completedMesh == null)
            {   
                this.MeshModel = model;
                return;
            }

            model.AddSlice(completedMesh);
            this.MeshModel = model;
            return;
        }

        public override string ToString()
        {
            return string.Format("Leaf: {2}{0}{3} Parent: {1}", Key, Parent == null ? "NULL" : Parent.Key.ToString(), this.MeshModel != null ? "*" : "", this.MeshComplete ? "F" : "" );
        }
    }

    abstract class MeshAssemblyPlannerViewBase
    {
        protected MeshAssemblyPlanner Plan;

        public abstract void OnNodeCompleted(IAssemblyPlannerNode node, bool success);

        public MeshAssemblyPlannerViewBase(MeshAssemblyPlanner plan)
        {
            Plan = plan;
            Plan.OnNodeCompleted += this.OnNodeCompleted;  
        }
    }


    /// <summary>
    /// Visualize the completed slices of a mesh assembly plan
    /// </summary>
    class MeshAssemblyPlannerCompletedView : MeshAssemblyPlannerViewBase, IColorView
    {
        /// <summary>
        /// A mapping of all nodes with completed models we can show as part of an incremental view
        /// </summary>
        public SortedList<ulong, SliceGraphMeshModel> ReadyModels = new SortedList<ulong, SliceGraphMeshModel>();

        private readonly ReaderWriterLockSlim ReadyModelLock = new ReaderWriterLockSlim();

        private MeshModel<VertexPositionNormalColor>[] _MeshModels = null;
        public MeshModel<VertexPositionNormalColor>[] MeshModels
        {
            get
            {
                try
                {
                    ReadyModelLock.EnterReadLock();

                    if (_MeshModels == null)
                    {
                        _MeshModels = ReadyModels.Values.Select(rm => rm.model).ToArray();
                    }

                    return _MeshModels;
                }
                finally
                {
                    ReadyModelLock.ExitReadLock();
                }
            }
        }

        public Color Color { get; set; } = Color.CornflowerBlue;
        public float Alpha { get { return Color.GetAlpha(); } set { Color = Color.SetAlpha(value); } }

        /// <summary>
        /// The default position to translate our completed mesh model to
        /// </summary>
        readonly GridVector3 Position;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="position">Where in volume space the world matrix should position the model by default</param>
        public MeshAssemblyPlannerCompletedView(MeshAssemblyPlanner plan, GridVector3 position) : base(plan)
        {
            Position = position;
        }

        public override void OnNodeCompleted(IAssemblyPlannerNode node, bool success)
        {
            try
            {
                ReadyModelLock.EnterWriteLock();
                if (node.MeshModel != null)
                {
                    node.MeshModel.Color = this.Color;
                    node.MeshModel.model.Position = Position.XY().ToGridVector3(0);
                    ReadyModels.Add(node.Key, node.MeshModel);
                }
                
                if(node.IsLeaf == false)
                {
                    IAssemblyPlannerBranch branch = node as IAssemblyPlannerBranch;
                    ReadyModels.Remove(branch.Left.Key);
                    ReadyModels.Remove(branch.Right.Key);
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
        public SortedList<ulong, MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor>> BoundingBoxModels = new SortedList<ulong, MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor>>();

        /// <summary>
        /// A mapping of all nodes to their bounding box
        /// </summary>
        public Dictionary<ulong, GridBox> NodeBoundingBox = new Dictionary<ulong, GridBox>();

        private readonly ReaderWriterLockSlim ReadyModelLock = new ReaderWriterLockSlim(); 
        private readonly SortedSet<ulong> NodesThatFailedMeshing = new SortedSet<ulong>();

        private MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor>[] _MeshModels = null;
        public MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor>[] MeshModels
        {
            get
            {
                try
                {
                    ReadyModelLock.EnterReadLock();

                    if (_MeshModels == null)
                    {
                        //_MeshModels = BoundingBoxModels.Values.ToArray();
                        _MeshModels = GetVisibleBoundingBoxModels().ToArray();
                    }

                    return _MeshModels;
                }
                finally
                {
                    ReadyModelLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Show bounding box models whose siblings have meshes or no siblings
        /// Branches decide for their children whether to add the children or themselves
        /// </summary>
        private List<MeshModel<VertexPositionColor>> GetVisibleBoundingBoxModels()
        {
            List<MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor>> listModels = new List<MeshModel<VertexPositionColor>>(BoundingBoxModels.Count);
            foreach (var item in BoundingBoxModels)
            {
                if (NodesThatFailedMeshing.Contains(item.Key))
                    listModels.Add(item.Value);
                else if(CanShowBoundingBoxModel(this.Plan[item.Key]))
                {
                    listModels.Add(item.Value);
                }    
            }

            return listModels;
        }

        /// <summary>
        /// Determine which bounding box models to show based on whether children are completed. 
        /// </summary>
        private bool CanShowBoundingBoxModel(IAssemblyPlannerNode node)
        {
            if (node.Parent == null)
                return !node.MeshComplete; //Only show the root bounding box if the mesh isn't done

            //if (node.MeshComplete)
            //    return false;

            var parent = node.Parent;
            bool showLeft = !parent.Left.MeshComplete;
            bool showRight = !parent.Right.MeshComplete;

            return showLeft ^ showRight;
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
        }

        public override void OnNodeCompleted(IAssemblyPlannerNode node, bool success)
        {
            try
            {
                ReadyModelLock.EnterWriteLock();

                if (success)
                {
                    BoundingBoxModels.Remove(node.Key);
                    this._MeshModels = null;
                }
                else if (BoundingBoxModels.TryGetValue(node.Key, out MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor> model))
                {
                    NodesThatFailedMeshing.Add(node.Key);
                    Color color = success ? Color.LightGreen.SetAlpha(0.33f) : Color.Red.SetAlpha(0.5f); 
                    model.SetColor(color);
                }
            }
            finally
            {
                ReadyModelLock.ExitWriteLock();
            }
        }

        private void GenerateAllBoundingBoxMeshesRecursive(IAssemblyPlannerNode node)
        {
            if (node == null)
                return;

            IAssemblyPlannerBranch branch = node as IAssemblyPlannerBranch;
            if (branch != null)
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
                BoundingBoxModels[node.Key] = model;
        }
         
        /// <summary>
        /// Create a 3D Box of triangles showing the boundaries of the node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private MeshModel<Microsoft.Xna.Framework.Graphics.VertexPositionColor> GenerateBoundingBoxMesh(IAssemblyPlannerNode node)
        {
            IAssemblyPlannerBranch branch = node as IAssemblyPlannerBranch;
            if(NodeBoundingBox.TryGetValue(node.Key, out GridBox bbox))
            {
                
                if (node.Depth > 0)
                {
                    //For branches we scale the bounding box visual a bit to prevent overdrawing the leaf bounding box
                    bbox = bbox.Scale(new GridVector3(1.02, 1.02, 1));
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
        private GridBox? CalculateAllBoundingBoxes(MeshAssemblyPlanner plan, SliceGraph sliceGraph)
        {
            return CalculateBoundingBox(plan.Root, sliceGraph); //Populate our bounding boxes from the root on down
        }

        private GridBox? CalculateBoundingBox(IAssemblyPlannerNode node, SliceGraph sliceGraph)
        {
            IAssemblyPlannerBranch branch = node as IAssemblyPlannerBranch;
            if(branch != null)
            {
                GridBox? lbox = default;
                GridBox? rbox = default;

                if(branch.Left != null)
                {
                    lbox = CalculateBoundingBox(branch.Left, sliceGraph);
                }

                if (branch.Right != null)
                {
                    rbox = CalculateBoundingBox(branch.Right, sliceGraph);
                }

                GridBox result = default;
                if (lbox.HasValue && rbox.HasValue)
                {
                    result = lbox.Value.Union(rbox.Value, out _);
                }
                else if(lbox.HasValue)
                {
                    result = lbox.Value;
                }
                else if(rbox.HasValue)
                {
                    result = rbox.Value;
                }
                else
                {
                    throw new ArgumentException($"Both branches have no bounding box");
                }
                  
                NodeBoundingBox[branch.Key] = result;
                return result; 
            }
            else //Is a leaf
            {
                var topology = sliceGraph.GetTopology(node.Key);
                if (topology.Polygons is null)
                {
                    Debug.Assert(topology.Polygons != null, "Expected topology for node");
                    NodeBoundingBox[node.Key] = default;
                    return default;
                }
                else
                {
                    GridRectangle boundingRect = topology.Polygons.BoundingBox().Translate(sliceGraph.BoundingBox.CenterPoint.XY());
                    GridBox bbox = new GridBox(boundingRect, topology.PolyZ.Min(), topology.PolyZ.Max());
                    NodeBoundingBox[node.Key] = bbox;
                    return bbox;
                }

            }
        }
    }
}
