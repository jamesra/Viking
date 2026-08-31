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
        readonly SortedSet<ulong> SlicesWithActiveTasks = [];
        readonly SortedSet<ulong> CompletedSlices = [];

        readonly System.Threading.ReaderWriterLockSlim rwLock = new();

        /// <summary>
        /// Completed by <see cref="OnTopologyComplete"/> while the write lock is held.  RunContinuationsAsynchronously
        /// keeps the awaiting continuation - which is the remainder of SliceGraph.Create and everything it schedules -
        /// from running inline on the last topology task's thread while that thread still owns the lock.
        /// </summary>
        readonly TaskCompletionSource<Dictionary<ulong, SliceTopology>> AllDone =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        readonly Dictionary<ulong, SliceTopology> SliceToTopology;

        public ConcurrentTopologyInitializer(SliceGraph graph)
        {
            Graph = graph;
            UnprocessedSlices = [.. Graph.Nodes.Keys];
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

                foreach (ulong adjacent in s.Edges.Keys)
                {
                    TryStartSlice(adjacent);
                }

                if (UnprocessedSlices.Count == 0 && SlicesWithActiveTasks.Count == 0)
                {
                    AllDone.TrySetResult(SliceToTopology);
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
        private Task TryStartSlice(in ulong slice_id)
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
                    //Log the failure rather than silently emitting an empty topology.  An empty topology
                    //still has to be reported so dependent slices can proceed, but the cause must be visible.
                    string sectionText = Graph.FormatSectionNumbers(slice);
                    System.Diagnostics.Trace.WriteLine($"Slice {slice.Key} topology initialization failed for {sectionText}. Emitting empty topology.\n{e}");
                    Graph.RecordTopologyFailure(slice.Key, sectionText);
                    this.OnTopologyComplete(slice, new SliceTopology());
                }
            }

            return Task.Run(GetTopologyTask);
        }

        /// <summary>
        /// Populates the lookup table mapping morph nodes to shapes.  Allows user option to simplify shapes.  Ensures all shapes have matching corresponding verticies if they participate in two or more slices
        /// </summary>
        /// <param name="tolerance"></param>
        public Task<Dictionary<ulong, SliceTopology>> InitializeSliceTopologyAsync(double tolerance = 0)
        {
            bool TasksStarted = false;
            try
            {
                rwLock.EnterWriteLock();

                ulong[] UnprocessedSlicesArray = [.. UnprocessedSlices];

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
            if (TasksStarted == false)
                AllDone.TrySetResult(this.SliceToTopology);

            return AllDone.Task;
        }
    }
}