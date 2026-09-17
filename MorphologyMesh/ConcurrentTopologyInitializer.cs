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

        /// <summary>
        /// Optional per-slice notification so a caller can show a slice's contours as soon as they exist instead of
        /// waiting for every topology in the graph.
        /// </summary>
        readonly SliceTopologyReadyHandler OnSliceTopologyReady;

        public ConcurrentTopologyInitializer(SliceGraph graph, SliceTopologyReadyHandler onSliceTopologyReady = null)
        {
            Graph = graph;
            OnSliceTopologyReady = onSliceTopologyReady;
            UnprocessedSlices = [.. Graph.Nodes.Keys];
            SliceToTopology = new Dictionary<ulong, SliceTopology>(Graph.Nodes.Count);
        }

        private void OnTopologyComplete(Slice s, SliceTopology st)
        {
            List<ulong> slicesToStart = [];
            bool signalDone = false;

            try
            {
                rwLock.EnterWriteLock();

                SliceToTopology.Add(s.Key, st);

                SlicesWithActiveTasks.Remove(s.Key);
                CompletedSlices.Add(s.Key);

                foreach (ulong adjacent in s.Edges.Keys)
                {
                    if (TryClaimSliceUnlocked(adjacent))
                        slicesToStart.Add(adjacent);
                }

                if (UnprocessedSlices.Count == 0 && SlicesWithActiveTasks.Count == 0)
                    signalDone = true;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }

            //Notify before the adjacent slices start.  Those slices share cached shapes with this one and insert
            //corresponding verticies into them while they build, so reading the contours after they start would
            //race with that mutation.  Nothing else can touch these shapes until the adjacent tasks are launched.
            if (OnSliceTopologyReady is not null)
            {
                try
                {
                    OnSliceTopologyReady(s, st);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.WriteLine($"Slice {s.Key} topology-ready handler threw; topology initialization continues.\n{e}");
                }
            }

            foreach (ulong id in slicesToStart)
                StartSliceTask(id);

            if (signalDone)
                AllDone.TrySetResult(SliceToTopology);
        }

        /// <summary>
        /// Return true if a task can be safely launched for this slice. Caller must hold the write lock.
        /// </summary>
        private bool CanStartSlice(Slice node)
        {
            if (UnprocessedSlices.Contains(node.Key) == false)
                return false;

            return !node.Edges.Keys.Any(key => SlicesWithActiveTasks.Contains(key));
        }

        /// <summary>
        /// Claim eligibility under the write lock without starting the task. Returns false if not eligible.
        /// </summary>
        private bool TryClaimSliceUnlocked(ulong slice_id)
        {
            Slice slice = Graph[slice_id];
            if (CanStartSlice(slice) is false)
                return false;

            UnprocessedSlices.Remove(slice_id);
            SlicesWithActiveTasks.Add(slice_id);
            return true;
        }

        private void StartSliceTask(ulong slice_id)
        {
            Slice slice = Graph[slice_id];
            Task.Run(() =>
            {
                SliceTopology st;
                try
                {
                    st = Graph.GetSliceTopology(slice);
                    OnTopologyComplete(slice, st);
                }
                catch (Exception e)
                {
                    string sectionText = Graph.FormatSectionNumbers(slice);
                    System.Diagnostics.Trace.WriteLine($"Slice {slice.Key} topology initialization failed for {sectionText}. Emitting empty topology.\n{e}");
                    Graph.RecordTopologyFailure(slice.Key, sectionText);
                    OnTopologyComplete(slice, new SliceTopology());
                }
            });
        }

        /// <summary>
        /// Populates the lookup table mapping morph nodes to shapes.  Allows user option to simplify shapes.  Ensures all shapes have matching corresponding verticies if they participate in two or more slices
        /// </summary>
        /// <param name="tolerance"></param>
        public Task<Dictionary<ulong, SliceTopology>> InitializeSliceTopologyAsync(double tolerance = 0)
        {
            List<ulong> slicesToStart = [];
            try
            {
                rwLock.EnterWriteLock();

                ulong[] UnprocessedSlicesArray = [.. UnprocessedSlices];

                for (int iSlice = UnprocessedSlicesArray.Length - 1; iSlice >= 0; iSlice--)
                {
                    ulong id = UnprocessedSlicesArray[iSlice];
                    if (TryClaimSliceUnlocked(id))
                        slicesToStart.Add(id);
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }

            foreach (ulong id in slicesToStart)
                StartSliceTask(id);

            if (slicesToStart.Count == 0)
                AllDone.TrySetResult(SliceToTopology);

            return AllDone.Task;
        }
    }
}
