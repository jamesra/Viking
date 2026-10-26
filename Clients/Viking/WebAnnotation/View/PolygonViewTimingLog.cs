using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace WebAnnotation.View
{
    /// <summary>
    /// Data gathering for how long a polygon view takes to appear after a pen command changes the
    /// boundary of an existing polygon. The pen actions call <see cref="MarkBoundaryChange"/> just before
    /// they write the new shape; the next <see cref="LocationPolygonView"/> built for that location claims
    /// the mark with <see cref="TryBegin"/> and reports its timings. Views that took longer than
    /// <see cref="SlowThreshold"/> are appended to <c>%TEMP%\viking-polygon-view-timing.log</c>.
    /// Intended to size a future intermediate UI that shows the polyline ring while the filled view builds.
    /// </summary>
    internal static class PolygonViewTimingLog
    {
        /// <summary>A view whose measured work exceeds this is written to the log.</summary>
        public static readonly TimeSpan SlowThreshold = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// A label layout taking longer than this is written to the log for any view, pen-edited or not. Layouts are
        /// expected to take a few milliseconds per cell.
        /// </summary>
        public static readonly TimeSpan SlowLabelLayoutThreshold = TimeSpan.FromMilliseconds(25);

        /// <summary>
        /// How long a mark stays claimable. A mark that is never claimed (the location is not drawn, or the
        /// save failed) must not be attributed to an unrelated view built much later.
        /// </summary>
        private static readonly TimeSpan MarkLifetime = TimeSpan.FromSeconds(30);

        /// <summary>Destination file. Settable so tests can redirect it away from the user's temp folder.</summary>
        internal static string LogPath { get; set; } = Path.Combine(Path.GetTempPath(), "viking-polygon-view-timing.log");
        private static readonly object Gate = new();
        private static readonly ConcurrentDictionary<long, BoundaryChangeMark> Pending = new();

        private sealed class BoundaryChangeMark(long timestamp, string reason)
        {
            public readonly long Timestamp = timestamp;
            public readonly string Reason = reason;
            public int Claims;
        }

        /// <summary>
        /// Records that a pen command is about to change the boundary (exterior or hole) of an existing polygon.
        /// Call before the shape is written so the view rebuilt by that write can claim the mark.
        /// </summary>
        /// <param name="locationId">The edited location.</param>
        /// <param name="reason">Short name of the pen operation, written to the log.</param>
        public static void MarkBoundaryChange(long locationId, string reason)
        {
            Pending[locationId] = new BoundaryChangeMark(Stopwatch.GetTimestamp(), reason);
        }

        /// <summary>
        /// Starts timing a view built for a location with a recent pen boundary change. Every view built for the
        /// location during <see cref="MarkLifetime"/> gets its own timing, numbered from 1, because the view that
        /// finally draws is not necessarily the first one built. Returns null for every other view, which is the
        /// common case and costs one dictionary lookup.
        /// </summary>
        public static PolygonViewTiming TryBegin(long locationId)
        {
            if (Pending.IsEmpty || !Pending.TryGetValue(locationId, out BoundaryChangeMark mark))
            {
                return null;
            }

            double sinceCommitMs = ToMilliseconds(Stopwatch.GetTimestamp() - mark.Timestamp);
            if (sinceCommitMs > MarkLifetime.TotalMilliseconds)
            {
                Pending.TryRemove(locationId, out _);
                return null;
            }

            int viewNumber = Interlocked.Increment(ref mark.Claims);
            Write(FormattableString.Invariant($"polygon-view-claimed loc={locationId} view={viewNumber} reason={mark.Reason} since_commit_ms={sinceCommitMs:F1}"));
            if (viewNumber > 1)
            {
                // Which code path rebuilt the view again is the answer to "why is the first view never drawn".
                Write($"polygon-view-caller loc={locationId} view={viewNumber} stack={CompactStack()}");
            }

            return new PolygonViewTiming(locationId, viewNumber, mark.Reason, mark.Timestamp);
        }

        private static string CompactStack()
        {
            string[] frames = new StackTrace(2, false).ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" <- ", System.Linq.Enumerable.Take(System.Linq.Enumerable.Select(frames, f => f.Trim().Replace("at ", string.Empty)), 14));
        }
        /// <summary>
        /// Overwrites <c>%TEMP%\viking-polygon-view-last.txt</c> with the vertices handed to the triangulation: one
        /// <c>ring</c> line per ring (<c>exterior</c> then <c>hole</c>) of <c>x,y</c> pairs in round-trip precision.
        /// Only the most recent pen-edited polygon is kept.
        /// </summary>
        internal static void DumpPolygon(long locationId, Geometry.Polygon polygon)
        {
            try
            {
                System.Text.StringBuilder sb = new();
                sb.AppendLine($"loc={locationId}");
                AppendRing(sb, "exterior", polygon.ExteriorRing);
                foreach (Geometry.Vector2[] hole in polygon.InteriorRings)
                {
                    AppendRing(sb, "hole", hole);
                }

                File.WriteAllText(Path.Combine(Path.GetTempPath(), "viking-polygon-view-last.txt"), sb.ToString());
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void AppendRing(System.Text.StringBuilder sb, string kind, System.Collections.Generic.IEnumerable<Geometry.Vector2> ring)
        {
            sb.Append(kind).Append(':');
            foreach (Geometry.Vector2 p in ring)
            {
                sb.Append(' ').Append(p.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(p.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }

            sb.AppendLine();
        }

        /// <summary>
        /// Times one step of a pen edit (cut computation, shape conversion, save, temporary fill) and writes a
        /// <c>pen-step</c> line when the scope ends, so the slow step shows up in the same log as the view timings.
        /// Steps under a millisecond are not written.
        /// </summary>
        /// <param name="step">Short step name, for example <c>save</c>.</param>
        /// <param name="detail">Optional context such as vertex counts.</param>
        public static PenStepTimer Measure(string step, string detail = "") => new(step, detail);

        internal readonly struct PenStepTimer(string step, string detail) : IDisposable
        {
            private readonly long start = Stopwatch.GetTimestamp();

            public void Dispose()
            {
                double ms = ToMilliseconds(Stopwatch.GetTimestamp() - start);
                if (ms >= 1.0)
                {
                    Write(FormattableString.Invariant($"pen-step step={step} ms={ms:F1} thread={Environment.CurrentManagedThreadId} {detail}").TrimEnd());
                }
            }
        }

        internal static double ToMilliseconds(long stopwatchTicks) => stopwatchTicks * 1000.0 / Stopwatch.Frequency;

        /// <summary>
        /// Reports the label layout of a polygon or closed-curve view: finding the inscribed circle and runs, and the
        /// first fit. Written through <paramref name="timing"/> for a pen-edited view, and for any view when slower than
        /// <see cref="SlowLabelLayoutThreshold"/>.
        /// </summary>
        public static void RecordLabelLayout(PolygonViewTiming? timing, long locationId, long stopwatchTicks, int vertices, string kind)
        {
            string detail = FormattableString.Invariant($"verts={vertices} kind={kind}");
            if (timing != null)
            {
                timing.RecordLabelLayout(stopwatchTicks, detail);
            }
            else if (ToMilliseconds(stopwatchTicks) > SlowLabelLayoutThreshold.TotalMilliseconds)
            {
                Write(FormattableString.Invariant($"label-layout-slow loc={locationId} ms={ToMilliseconds(stopwatchTicks):F1} {detail}"));
            }
        }

        internal static void Write(string line)
        {
            Trace.WriteLine(line);
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// Timings for one polygon view built after a pen boundary change. The view reports from three places:
    /// its constructor, its <c>Initialize</c> (curve smoothing), and the lazy background triangulation. The line is
    /// written once all three have reported, so it appears only if the view is drawn and the mesh build runs.
    /// </summary>
    /// <remarks>
    /// <c>curve_ms</c> is the spline smoothing of the volume polygon. <c>delaunay_ms</c> is the mesh build
    /// (centering, Delaunay triangulation, vertex mesh conversion) and includes the simplify-and-retry path.
    /// <c>other_ms</c> is everything else the view does while being built: mapping the shape into volume space,
    /// control points, labels, and hole views. The cut-fill warm-up runs on its own task and is not counted.
    /// </remarks>
    internal sealed class PolygonViewTiming
    {
        private const int PartsToReport = 3;

        private readonly long locationId;
        private readonly string reason;
        private readonly long commitTimestamp;
        private int partsPending = PartsToReport;

        private long constructionTicks;
        private long smoothingTicks;
        private long initializeOtherTicks;
        private int inputVertices;
        private int holes;
        private int smoothedVertices;
        private double triangulationMs;
        private bool triangulationSucceeded;

        private readonly int viewNumber;

        internal PolygonViewTiming(long locationId, int viewNumber, string reason, long commitTimestamp)
        {
            this.locationId = locationId;
            this.viewNumber = viewNumber;
            this.reason = reason;
            this.commitTimestamp = commitTimestamp;
        }

        /// <summary>Reports the constructor's time and the size of the polygon it mapped into volume space.</summary>
        public void RecordConstruction(long stopwatchTicks, int vertices, int interiorRings)
        {
            constructionTicks = stopwatchTicks;
            inputVertices = vertices;
            holes = interiorRings;
            LogPart("constructed", constructionTicks);
            PartDone();
        }

        /// <summary>
        /// Reports <c>Initialize</c>: <paramref name="smoothingTicks"/> is the curve smoothing,
        /// <paramref name="otherTicks"/> the rest of the method, and <paramref name="smoothedVertexCount"/> the
        /// vertex count the triangulation will receive.
        /// </summary>
        public void RecordInitialization(long smoothingTicks, long otherTicks, int smoothedVertexCount)
        {
            this.smoothingTicks = smoothingTicks;
            initializeOtherTicks = otherTicks;
            smoothedVertices = smoothedVertexCount;
            LogPart("initialized", smoothingTicks + otherTicks,
                FormattableString.Invariant($"curve_ms={PolygonViewTimingLog.ToMilliseconds(smoothingTicks):F1} verts_in={inputVertices} verts_smoothed={smoothedVertexCount} holes={holes}"));
            PartDone();
        }

        /// <summary>
        /// Reports that the background triangulation began, and saves the exact polygon it was given so a build that
        /// never reports back can be reproduced offline. Matches <c>SolidPolygonView.MeshBuildStarted</c>.
        /// </summary>
        public void RecordTriangulationStarted(Geometry.Polygon polygon)
        {
            LogPart("meshstart", 0, FormattableString.Invariant($"verts={polygon.TotalUniqueVertices} holes={polygon.InteriorRings.Count}"));
            PolygonViewTimingLog.DumpPolygon(locationId, polygon);
        }

        /// <summary>
        /// Reports the label layout as its own part. Its time is also inside <c>Initialize</c>'s other time, so it does
        /// not count towards the parts the summary line waits for.
        /// </summary>
        public void RecordLabelLayout(long stopwatchTicks, string detail) => LogPart("labellayout", stopwatchTicks, detail);

        /// <summary>Reports the background mesh build. Matches <c>SolidPolygonView.MeshBuildCompleted</c>.</summary>
        public void RecordTriangulation(TimeSpan elapsed, bool succeeded)
        {
            triangulationMs = elapsed.TotalMilliseconds;
            triangulationSucceeded = succeeded;
            LogPart("meshbuilt", (long)(elapsed.TotalSeconds * Stopwatch.Frequency));
            PartDone();
        }

        /// <summary>
        /// Reports that <c>Initialize</c> threw. The view then never becomes drawable, which looks like a polygon
        /// that takes forever to render, so it is logged immediately.
        /// </summary>
        public void RecordInitializeFailure(Exception ex)
        {
            PolygonViewTimingLog.Write($"polygon-view-init-failed loc={locationId} view={viewNumber} reason={reason} {ex.GetType().Name}: {ex.Message}");
        }

        private void LogPart(string part, long durationTicks, string detail = "")
        {
            double durationMs = PolygonViewTimingLog.ToMilliseconds(durationTicks);
            double sinceCommitMs = PolygonViewTimingLog.ToMilliseconds(Stopwatch.GetTimestamp() - commitTimestamp);
            PolygonViewTimingLog.Write(FormattableString.Invariant($"polygon-view-part loc={locationId} view={viewNumber} part={part} part_ms={durationMs:F1} since_commit_ms={sinceCommitMs:F1} {detail}").TrimEnd());
        }

        private void PartDone()
        {
            if (Interlocked.Decrement(ref partsPending) != 0)
            {
                return;
            }

            double curveMs = PolygonViewTimingLog.ToMilliseconds(smoothingTicks);
            double otherMs = PolygonViewTimingLog.ToMilliseconds(constructionTicks + initializeOtherTicks);
            double totalMs = curveMs + triangulationMs + otherMs;
            // Fast views are written too (tagged differently) so "fast" can be told apart from "mesh never built".
            string tag = totalMs > PolygonViewTimingLog.SlowThreshold.TotalMilliseconds ? "polygon-view-slow" : "polygon-view-fast";

            double sinceCommitMs = PolygonViewTimingLog.ToMilliseconds(Stopwatch.GetTimestamp() - commitTimestamp);
            string line = string.Join(" ",
                tag,
                FormattableString.Invariant($"loc={locationId} view={viewNumber} reason={reason}"),
                FormattableString.Invariant($"verts_in={inputVertices} verts_smoothed={smoothedVertices} holes={holes}"),
                FormattableString.Invariant($"total_ms={totalMs:F1} curve_ms={curveMs:F1} delaunay_ms={triangulationMs:F1} other_ms={otherMs:F1}"),
                FormattableString.Invariant($"since_commit_ms={sinceCommitMs:F1} delaunay_ok={triangulationSucceeded}"));
            PolygonViewTimingLog.Write(line);
        }
    }
}
