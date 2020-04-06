using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Geometry.Meshing;
using MorphologyMesh;
using VikingXNA;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    /// <summary>
    /// This is a binary tree where leaves represent meshes.  Branches represent meshes that should be merged when both leaves have finished mesh generation.  Nodes are merged until only a single root leaf node exists with the final mesh
    /// </summary>
    class MeshAssemblyPlanner
    {
        public IAssemblyPlannerNode Root;

        /// <summary>
        /// Allows mapping a slice key to the original leaf node
        /// </summary>
        public readonly SortedList<ulong, AssemblyPlannerLeaf> Slices;

        private ReaderWriterLockSlim ReadyModelLock = new ReaderWriterLockSlim();

        /// <summary>
        /// A mapping of all nodes with completed models we can show as part of an incremental view
        /// </summary>
        public SortedList<ulong, SliceGraphMeshModel> ReadyModels = new SortedList<ulong, SliceGraphMeshModel>();

        public System.Threading.ManualResetEventSlim MeshAssembledEvent = new ManualResetEventSlim();

        private MeshModel<VertexPositionNormalColor>[] _MeshModels = null;
        public MeshModel<VertexPositionNormalColor>[] MeshModels {
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
 
        public MeshAssemblyPlanner(SliceGraph sliceGraph)
        {
            //AssemblyPlannerLeaf[] firstLayer = sliceGraph.Nodes.Keys.OrderBy(k => k).Select(k => new AssemblyPlannerLeaf(k)).ToArray();
            AssemblyPlannerLeaf[] firstLayer = sliceGraph.Nodes.Keys.OrderBy(k => sliceGraph.GetTopology(k).PolyZ.Average()).Select(k => new AssemblyPlannerLeaf(k)).ToArray();
            Slices = new SortedList<ulong, AssemblyPlannerLeaf>(firstLayer.Length);
            foreach(var leaf in firstLayer)
            {
                Slices.Add(leaf.Key, leaf);
            }

            IAssemblyPlannerNode[] currentLayer = firstLayer;
            //This isn't a true binary tree because branches do not have values.  We build our tree from the bottom up. This 
            //always generates a balances tree.
            while(currentLayer.Length > 1)
            {
                currentLayer = BuildLayer(currentLayer);
            }

            Root = currentLayer[0];
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

        /// <summary>
        /// Called when a mesh is completed.  Generates a model and attempts to merge that model up the tree.
        /// Thread safe
        /// </summary>
        /// <param name="mesh"></param>
        public void OnMeshCompleted(BajajGeneratorMesh mesh, bool Success)
        {
            AssemblyPlannerLeaf leaf = this.Slices[mesh.Slice.Key];
            leaf.OnMeshCompletion(mesh);

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

                                    try
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

    class AssemblyPlannerBranch : AssemblyPlannerNode
    {
        public ReaderWriterLockSlim BranchLock = new ReaderWriterLockSlim();

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

        IAssemblyPlannerNode[] Children = new IAssemblyPlannerNode[2];

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
        /// Key to the slice this leaf represents
        /// </summary>
        public override ulong Key { get; }

        public override int Depth { get { return 0; } }

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
            SliceGraphMeshModel model = new SliceGraphMeshModel();
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
}
