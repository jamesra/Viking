using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MonogameTestbed;

/// <summary>
/// Per-run log of BajajMultiTest slices that failed to mesh, in the <c>--repro-locations-file</c> format BajajTest
/// reads.  Every failure is appended and flushed the moment it is recorded, so a debug session that is stopped
/// mid-run still leaves a usable list; the report used to be written only after every structure finished, which
/// for a whole cell meant it usually was not written at all.  The file name carries the run timestamp so runs can
/// be diffed to see whether a change introduced new failures.  When the run does complete, a sorted copy is also
/// written under the fixed <see cref="LatestFileName"/> for the launch configurations and skill docs that expect it.
/// </summary>
sealed class FailedSliceReport : IDisposable
{
    /// <summary>Fixed name of the sorted copy written when a run completes.</summary>
    public const string LatestFileName = "bajajmultitest_failed_slices.txt";

    private readonly object _lock = new();
    private readonly List<FailedSliceReproRecord> _records = [];
    private StreamWriter _writer;

    /// <summary>Path of the per-run file being appended to, or null when the file could not be opened.</summary>
    public string Path { get; }

    /// <summary>Directory the report files live in.</summary>
    public string Directory { get; }

    public int Count
    {
        get
        {
            lock (_lock)
                return _records.Count;
        }
    }

    /// <param name="directory">Output folder; created when missing.</param>
    /// <param name="runStamp">Timestamp that also names the trace log, so the two files pair up.</param>
    /// <param name="headerLines">Run description written as comments at the top: command line, structures, endpoint.</param>
    public FailedSliceReport(string directory, string runStamp, IEnumerable<string> headerLines)
    {
        Directory = directory;
        Path = System.IO.Path.Combine(directory, $"bajajmultitest_failed_slices_{runStamp.Replace(' ', '_')}.txt");

        try
        {
            System.IO.Directory.CreateDirectory(directory);
            _writer = new StreamWriter(Path, append: false, Encoding.UTF8) { AutoFlush = true };
            _writer.WriteLine("# BajajMultiTest failed slices — one LocationID list per line, appended as each slice fails");
            _writer.WriteLine("# Feed to BajajTest: --mode BajajTest --repro-locations-file <this file>");
            _writer.WriteLine($"# Run started {runStamp}");
            foreach (string line in headerLines ?? [])
                _writer.WriteLine($"# {line}");
            _writer.WriteLine("#");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not open failed-slice report '{Path}': {ex.Message}.  Failures will only be listed on the console.");
            _writer = null;
        }
    }

    /// <summary>
    /// Append one failure.  Thread safe: face generation completes on many pool threads at once.
    /// </summary>
    public void Record(FailedSliceReproRecord record)
    {
        lock (_lock)
        {
            _records.Add(record);
            if (_writer is null)
                return;

            try
            {
                WriteRecord(_writer, record);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not append to failed-slice report '{Path}': {ex.Message}");
                _writer = null;
            }
        }
    }

    /// <summary>
    /// Write the closing summary to the per-run file and a sorted copy to <see cref="LatestFileName"/>.  Sorting by
    /// structure and location makes two completed runs directly diffable, which the chronological per-run file,
    /// whose order depends on thread scheduling, is not.
    /// </summary>
    public void Complete()
    {
        FailedSliceReproRecord[] records;
        lock (_lock)
        {
            records = [.. _records];
            if (_writer is not null)
            {
                _writer.WriteLine("#");
                _writer.WriteLine($"# Run complete: {Summary(records)}");
                _writer.Flush();
            }
        }

        string latest = System.IO.Path.Combine(Directory, LatestFileName);
        try
        {
            using StreamWriter writer = new(latest, append: false, Encoding.UTF8);
            writer.WriteLine("# BajajMultiTest failed slices — one LocationID list per line (sorted copy of the last completed run)");
            writer.WriteLine("# Feed to BajajTest: --mode BajajTest --repro-locations-file <this file>");
            writer.WriteLine($"# Run started {Program.RunStamp}; per-run log: {System.IO.Path.GetFileName(Path)}");
            writer.WriteLine($"# {Summary(records)}");
            if (records.Length == 0)
                writer.WriteLine("# (none)");

            foreach (FailedSliceReproRecord record in records.OrderBy(r => r.StructureId).ThenBy(r => r.LocationIdsLine))
                WriteRecord(writer, record);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not write '{latest}': {ex.Message}");
        }

        if (records.Length == 0)
            Console.WriteLine($"No failed slices. Empty repro lists written to {Path} and {latest}");
        else
            Console.WriteLine($"WARNING: {Summary(records)}. Repro lists: {Path} (chronological) and {latest} (sorted)");
    }

    private static string Summary(FailedSliceReproRecord[] records)
    {
        if (records.Length == 0)
            return "0 slice(s) failed";

        var byKind = records.GroupBy(r => r.Kind).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}");
        return $"{records.Length} slice(s) failed to mesh ({string.Join(", ", byKind)})";
    }

    private static void WriteRecord(TextWriter writer, FailedSliceReproRecord record)
    {
        //A reason can span lines (exception text, one line per closing problem).  Every line must stay a comment,
        //otherwise BajajTest would read it as a LocationID list.
        string[] reasonLines = (record.Reason ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string firstLine = reasonLines.Length > 0 ? reasonLines[0] : string.Empty;

        string heading = record.LocationIds.Length == 0
            ? $"# structure={record.StructureId} skipped (no locations) — [{record.Kind}] {firstLine}"
            : $"# structure={record.StructureId} — [{record.Kind}] {firstLine}";
        writer.WriteLine(heading);
        foreach (string line in reasonLines.Skip(1))
            writer.WriteLine($"#     {line.TrimEnd()}");

        if (record.LocationIds.Length > 0)
            writer.WriteLine(record.LocationIdsLine);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
