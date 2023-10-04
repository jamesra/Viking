using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MorphologyMesh
{
    /// <summary>
    /// Creates the topology for all nodes in a SliceGraph in parallel while ensuring that at no time is a single shape being modified for two slice nodes at the same time.
    /// </summary>
    internal class ConcurrentTopologyInitializer
    {
        readonly SliceGraph Graph;

        readonly SortedSet<ulong> UnprocessedSlices = null;
        readonly SortedSet<ulong> SlicesWithActiveTasks = new SortedSet<ulong>();
        readonly SortedSet<ulong> CompletedSlices = new SortedSet<ulong>();

        readonly System.Threading.ReaderWriterLockSlim rwLock = new System.Threading.ReaderWriterLockSlim();
        readonly System.Threading.ManualResetEventSlim AllDoneEvent = new System.Threading.ManualResetEventSlim();

        readonly Dictionary<ulong, SliceTopology> SliceToTopology;

        public ConcurrentTopologyInitializer(SliceGraph graph)
        {
            Graph = graph;
            UnprocessedSlices = new SortedSet<ulong>(Graph.Nodes.Keys);
            SliceToTopology = new Dictionary<ulong, SliceTopology>(Graph.Nodes.Count);
        }

        private void OnTopologyComplete(Slice s, SliceTopology st)
        {
            try
            {
                rwLock.EnterWriteLock();

                SliceToTopology.Add(s.Key, st);

                SlicesWithActiveTasks.Remove(s.Key);
                CompletedSlices.Add(s.Key);

                foreach(ulong adjacent in s.Edges.Keys)
                {
                    TryStartSlice(adjacent);
                }

                if(UnprocessedSlices.Count == 0 && SlicesWithActiveTasks.Count == 0)
                {
                    AllDoneEvent.Set();
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Return true if a task can be safely launched for this slice
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private bool CanStartSlice(Slice node)
        { 
            if (UnprocessedSlices.Contains(node.Key) == false)
                return false;

            //Do not process a slice if the adjacent slices are being processed and could change the polygons it would be compared against
            return !node.Edges.Keys.Any(key => SlicesWithActiveTasks.Contains(key));
        }

        /// <summary>
        /// If a slice is eligible to be processed then start a task.
        /// </summary>
        /// <param name="slice_id"></param>
        /// <returns></returns>
        private Task TryStartSlice(ulong slice_id)
        {
            Slice slice = Graph[slice_id];

            if (CanStartSlice(slice) is false)
                return null;

            UnprocessedSlices.Remove(slice_id);
            SlicesWithActiveTasks.Add(slice_id);

            void GetTopologyTask()
            {
                SliceTopology st;
                try
                {
                    st = Graph.GetSliceTopology(slice);
                    this.OnTopologyComplete(slice, st);
                }
                catch (Exception e)
                {
                    this.OnTopologyComplete(slice, new SliceTopology());
                }
            }

            return Task.Run(GetTopologyTask);
        }

        /// <summary>
        /// Populates the lookup table mapping morph nodes to shapes.  Allows user option to simplify shapes.  Ensures all shapes have matching corresponding verticies if they participate in two or more slices
        /// </summary>
        /// <param name="tolerance"></param>
        public Dictionary<ulong, SliceTopology> InitializeSliceTopology(double tolerance = 0)
        {
            var MorphNodeToShape = Graph.MorphNodeToShape;
             
            List<Slice> SlicesToStart = new List<Slice>(UnprocessedSlices.Count);
            bool TasksStarted = false;
            try
            {
                rwLock.EnterWriteLock();

                ulong[] UnprocessedSlicesArray = UnprocessedSlices.ToArray();
                
                for (int iSlice = UnprocessedSlices.Count - 1; iSlice >= 0; iSlice--)
                {
                    var outputTask = TryStartSlice(UnprocessedSlicesArray[iSlice]);
                    TasksStarted = TasksStarted || outputTask != null;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
            
            //We need to ensure there are tasks to wait on. This was an edge case for structures with one annotation.
            if(TasksStarted)
                AllDoneEvent.Wait();

            return this.SliceToTopology;
        }
    }
}