using System;
using System.Text;
using System.Threading;

namespace MorphologyMesh
{
    /// <summary>
    /// Lock-free counters for FirstPass OTV / chord-generation cost. Enable for harness and --timings
    /// long-pole diagnosis (H1–H6); leave off in production paths that do not care.
    /// </summary>
    public static class ChordGenStats
    {
        public static bool Enabled { get; set; }

        private static long _chordPassCalls;
        private static long _chordsAdded;
        private static long _otvRebuilds;
        private static long _otvFullRebuilds;
        private static long _otvDirtyRebuilds;
        private static long _otvVertexEvals;
        private static long _otvStickyHits;
        private static long _findNearestCalls;
        private static long _isSliceChordValidCalls;
        private static long _permanentCacheHits;
        private static long _tryAddSkippedValid;
        private static long _completedVertsEvicted;
        private static long _unchangedOtvEntries;

        public static void Reset()
        {
            Interlocked.Exchange(ref _chordPassCalls, 0);
            Interlocked.Exchange(ref _chordsAdded, 0);
            Interlocked.Exchange(ref _otvRebuilds, 0);
            Interlocked.Exchange(ref _otvFullRebuilds, 0);
            Interlocked.Exchange(ref _otvDirtyRebuilds, 0);
            Interlocked.Exchange(ref _otvVertexEvals, 0);
            Interlocked.Exchange(ref _otvStickyHits, 0);
            Interlocked.Exchange(ref _findNearestCalls, 0);
            Interlocked.Exchange(ref _isSliceChordValidCalls, 0);
            Interlocked.Exchange(ref _permanentCacheHits, 0);
            Interlocked.Exchange(ref _tryAddSkippedValid, 0);
            Interlocked.Exchange(ref _completedVertsEvicted, 0);
            Interlocked.Exchange(ref _unchangedOtvEntries, 0);
        }

        public static void IncChordPassCalls() { if (Enabled) Interlocked.Increment(ref _chordPassCalls); }
        public static void AddChords(int n) { if (Enabled && n != 0) Interlocked.Add(ref _chordsAdded, n); }
        public static void IncOtvRebuild() { if (Enabled) Interlocked.Increment(ref _otvRebuilds); }
        public static void IncOtvFullRebuild() { if (Enabled) Interlocked.Increment(ref _otvFullRebuilds); }
        public static void IncOtvDirtyRebuild() { if (Enabled) Interlocked.Increment(ref _otvDirtyRebuilds); }
        public static void IncOtvVertexEval() { if (Enabled) Interlocked.Increment(ref _otvVertexEvals); }
        public static void IncOtvStickyHit() { if (Enabled) Interlocked.Increment(ref _otvStickyHits); }
        public static void IncFindNearest() { if (Enabled) Interlocked.Increment(ref _findNearestCalls); }
        public static void IncIsSliceChordValid() { if (Enabled) Interlocked.Increment(ref _isSliceChordValidCalls); }
        public static void IncPermanentCacheHit() { if (Enabled) Interlocked.Increment(ref _permanentCacheHits); }
        public static void IncTryAddSkippedValid() { if (Enabled) Interlocked.Increment(ref _tryAddSkippedValid); }
        public static void AddCompletedVertsEvicted(int n) { if (Enabled && n != 0) Interlocked.Add(ref _completedVertsEvicted, n); }
        public static void AddUnchangedOtvEntries(int n) { if (Enabled && n != 0) Interlocked.Add(ref _unchangedOtvEntries, n); }

        public static long ChordPassCalls => Interlocked.Read(ref _chordPassCalls);
        public static long ChordsAdded => Interlocked.Read(ref _chordsAdded);
        public static long OtvRebuilds => Interlocked.Read(ref _otvRebuilds);
        public static long OtvFullRebuilds => Interlocked.Read(ref _otvFullRebuilds);
        public static long OtvDirtyRebuilds => Interlocked.Read(ref _otvDirtyRebuilds);
        public static long OtvVertexEvals => Interlocked.Read(ref _otvVertexEvals);
        public static long OtvStickyHits => Interlocked.Read(ref _otvStickyHits);
        public static long FindNearestCalls => Interlocked.Read(ref _findNearestCalls);
        public static long IsSliceChordValidCalls => Interlocked.Read(ref _isSliceChordValidCalls);
        public static long PermanentCacheHits => Interlocked.Read(ref _permanentCacheHits);
        public static long TryAddSkippedValid => Interlocked.Read(ref _tryAddSkippedValid);
        public static long CompletedVertsEvicted => Interlocked.Read(ref _completedVertsEvicted);
        public static long UnchangedOtvEntries => Interlocked.Read(ref _unchangedOtvEntries);

        /// <summary>
        /// Geometric / annotation failures that cannot become true by adding more chords.
        /// <see cref="SliceChordTestType.ChordIntersection"/> failures are also sticky once failed, but a
        /// previously-valid pair must be re-checked for intersection when the RTree generation advances.
        /// </summary>
        public const SliceChordTestType PermanentFailureMask =
            SliceChordTestType.Correspondance
            | SliceChordTestType.Theorem2
            | SliceChordTestType.Theorem4
            | SliceChordTestType.LineOrientation
            | SliceChordTestType.EdgeType
            | SliceChordTestType.Face
            | SliceChordTestType.ShapeLink
            | SliceChordTestType.ForkPartition;

        public static string Format()
        {
            StringBuilder sb = new();
            sb.Append("ChordGenStats");
            sb.Append($" chordPassCalls={ChordPassCalls}");
            sb.Append($" chordsAdded={ChordsAdded}");
            sb.Append($" otvRebuilds={OtvRebuilds}(full={OtvFullRebuilds},dirty={OtvDirtyRebuilds})");
            sb.Append($" otvVertexEvals={OtvVertexEvals}");
            sb.Append($" stickyHits={OtvStickyHits}");
            sb.Append($" unchangedOtv={UnchangedOtvEntries}");
            sb.Append($" findNearest={FindNearestCalls}");
            sb.Append($" isValid={IsSliceChordValidCalls}");
            sb.Append($" permCacheHits={PermanentCacheHits}");
            sb.Append($" tryAddSkipped={TryAddSkippedValid}");
            sb.Append($" vertsEvicted={CompletedVertsEvicted}");
            return sb.ToString();
        }
    }
}
