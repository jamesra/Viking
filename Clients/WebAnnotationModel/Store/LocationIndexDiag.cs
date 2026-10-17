using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace WebAnnotationModel
{
    /// <summary>
    /// Diagnostics for a location that is added to the spatial index twice, the condition <c>LocationRTree.AddObject</c> asserts against.
    /// </summary>
    /// <remarks>
    /// The store and the index record each add and remove of a location in a small ring (the last <see cref="Capacity"/> events, so the
    /// memory is fixed). When the index finds the ID already in it, <see cref="ReportDuplicate"/> writes that ID's recent history, the
    /// thread and the stack to <see cref="LogPath"/> and to <see cref="Trace"/>, before the assertion can block the thread with a dialog.
    /// The history says how the ID got there: an add the store accepted twice, a remove that left the index behind, or two threads racing.
    /// </remarks>
    public static class LocationIndexDiag
    {
        /// <summary>How many recent events are kept.</summary>
        public const int Capacity = 4096;

        /// <summary>The most duplicate reports written in one run, so a stuck loop cannot fill the disk.</summary>
        public const int MaxReports = 50;

        /// <summary>One thing that happened to a location in the store or the index.</summary>
        public readonly struct Entry
        {
            public Entry(long id, string description, int thread, long timestamp)
            {
                Id = id;
                Description = description;
                Thread = thread;
                Timestamp = timestamp;
            }

            public long Id { get; }

            public string Description { get; }

            public int Thread { get; }

            /// <summary><see cref="Stopwatch.GetTimestamp"/> when it happened.</summary>
            public long Timestamp { get; }
        }

        /// <summary>%TEMP%\viking-location-index.log</summary>
        public static string LogPath { get; set; } = Path.Combine(Path.GetTempPath(), "viking-location-index.log");

        private static readonly object Gate = new();
        private static readonly Entry[] Ring = new Entry[Capacity];
        private static int next;
        private static long recorded;
        private static int reports;

        /// <summary>Notes that <paramref name="description"/> happened to location <paramref name="id"/> on this thread.</summary>
        public static void Record(long id, string description)
        {
            Entry entry = new(id, description, Thread.CurrentThread.ManagedThreadId, Stopwatch.GetTimestamp());
            lock (Gate)
            {
                Ring[next] = entry;
                next = (next + 1) % Capacity;
                recorded++;
            }
        }

        /// <summary>The kept events of location <paramref name="id"/>, oldest first.</summary>
        public static IReadOnlyList<Entry> HistoryOf(long id)
        {
            lock (Gate)
            {
                int count = (int)Math.Min(recorded, Capacity);
                int first = recorded < Capacity ? 0 : next;
                List<Entry> found = [];
                for (int i = 0; i < count; i++)
                {
                    Entry entry = Ring[(first + i) % Capacity];
                    if (entry.Id == id)
                        found.Add(entry);
                }

                return found;
            }
        }

        /// <summary>Forgets every event and report count. For tests.</summary>
        public static void Reset()
        {
            lock (Gate)
            {
                Array.Clear(Ring, 0, Ring.Length);
                next = 0;
                recorded = 0;
                reports = 0;
            }
        }

        /// <summary>The report text: what was being added, the ID's history (with how long before <paramref name="now"/> each event was) and the stack.</summary>
        public static string Describe(long id, string detail, IReadOnlyList<Entry> history, long now, string stack, int thread)
        {
            StringBuilder text = new();
            text.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} Location {id} is already in the spatial index (thread {thread}). {detail}");
            if (history.Count == 0)
                text.AppendLine("  No earlier event for this location is kept: it was added before the last " + Capacity + " events, or by a path that does not record.");

            foreach (Entry entry in history)
            {
                double ageMs = (now - entry.Timestamp) * 1000.0 / Stopwatch.Frequency;
                text.AppendLine($"  -{ageMs,10:0.0} ms  thread {entry.Thread,3}  {entry.Description}");
            }

            text.AppendLine("  Stack:");
            foreach (string line in stack.Split(['\n'], StringSplitOptions.RemoveEmptyEntries).Take(25))
                text.AppendLine("  " + line.TrimEnd());

            return text.ToString();
        }

        /// <summary>Writes the report for a location the index already holds. Never throws.</summary>
        public static void ReportDuplicate(long id, string detail)
        {
            try
            {
                if (Interlocked.Increment(ref reports) > MaxReports)
                    return;

                string text = Describe(id, detail, HistoryOf(id), Stopwatch.GetTimestamp(), Environment.StackTrace, Thread.CurrentThread.ManagedThreadId);
                Trace.WriteLine(text, "WebAnnotation");
                File.AppendAllText(LogPath, text);
            }
            catch
            {
            }
        }
    }
}
