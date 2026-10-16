using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Interactive console for the running service: show which volumes have corrections and when they were built,
    /// and trigger a manual rebuild of one volume (or all) while watching its progress.
    /// </summary>
    /// <remarks>
    /// Runs as a second process in the container (<c>docker exec -it &lt;container&gt; correction-menu</c>, see the
    /// Dockerfile) and is only a gRPC client of the service in the first process; it never rebuilds on its own, so the
    /// service's one-rebuild-at-a-time rule still holds. The server accepts <c>RebuildVolume</c> only from loopback,
    /// which is where this process connects. Not thread safe; one instance drives one console.
    /// </remarks>
    public sealed class CorrectionMenu
    {
        /// <summary>Address of the service's cleartext HTTP/2 port; override with CORRECTION_MENU_URL.</summary>
        public const string DefaultUrl = "http://127.0.0.1:80";

        const int BarWidth = 30;

        readonly AnnotateSectionCorrections.AnnotateSectionCorrectionsClient _client;
        readonly TextReader _input;
        readonly TextWriter _output;
        readonly Func<bool> _keyPressed;
        readonly Func<TimeSpan, CancellationToken, Task> _delay;
        readonly Func<DateTime> _utcNow;

        /// <summary>
        /// Creates a menu bound to one service connection and one console.
        /// </summary>
        /// <param name="client">Connected client of the local service.</param>
        /// <param name="input">Where choices are read from.</param>
        /// <param name="output">Where the menu is written.</param>
        /// <param name="keyPressed">True when the user has pressed a key, which stops watching a rebuild. Pass
        /// <c>() =&gt; false</c> when input is not a terminal.</param>
        /// <param name="delay">Waits between status polls; replaced in tests.</param>
        /// <param name="utcNow">Clock used for ages and elapsed time; replaced in tests.</param>
        public CorrectionMenu(
            AnnotateSectionCorrections.AnnotateSectionCorrectionsClient client,
            TextReader input,
            TextWriter output,
            Func<bool> keyPressed = null,
            Func<TimeSpan, CancellationToken, Task> delay = null,
            Func<DateTime> utcNow = null)
        {
            _client = client;
            _input = input;
            _output = output;
            _keyPressed = keyPressed ?? (() => false);
            _delay = delay ?? Task.Delay;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>
        /// Entry point for <c>GrpcSectionCorrectionService menu</c>: connects to the local service and runs the menu on
        /// the real console.
        /// </summary>
        /// <param name="args">Arguments after <c>menu</c>; unused.</param>
        /// <returns>Process exit code: 0 after Exit, 1 when the service cannot be reached.</returns>
        public static async Task<int> RunConsoleAsync(string[] args)
        {
            string url = Environment.GetEnvironmentVariable("CORRECTION_MENU_URL");
            if (string.IsNullOrWhiteSpace(url))
                url = DefaultUrl;

            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            var menu = new CorrectionMenu(
                new AnnotateSectionCorrections.AnnotateSectionCorrectionsClient(channel),
                Console.In,
                Console.Out,
                keyPressed: () => !Console.IsInputRedirected && Console.KeyAvailable);
            return await menu.RunAsync(CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Shows the main menu until the user exits or input ends.
        /// </summary>
        /// <param name="cancellationToken">Aborts the menu, for example on Ctrl+C.</param>
        /// <returns>0 after Exit or end of input, 1 when the first status call finds the service unreachable.</returns>
        public async Task<int> RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                RebuildStatus first = await _client.GetRebuildStatusAsync(
                    new GetRebuildStatusRequest(), cancellationToken: cancellationToken);
                _output.WriteLine($"Section correction service {first.ServiceVersion}");
            }
            catch (RpcException ex)
            {
                _output.WriteLine($"Cannot reach the correction service: {ex.Status.Detail}");
                return 1;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                _output.WriteLine();
                _output.WriteLine("  1) Status");
                _output.WriteLine("  2) Refresh volume");
                _output.WriteLine("  3) Exit");
                string choice = Prompt("Choose: ");
                if (choice is null)
                    return 0;

                try
                {
                    switch (choice.Trim().ToLowerInvariant())
                    {
                        case "1":
                        case "s":
                        case "status":
                            await ShowStatusAsync(cancellationToken);
                            break;
                        case "2":
                        case "r":
                        case "refresh":
                            await RefreshVolumeAsync(cancellationToken);
                            break;
                        case "3":
                        case "x":
                        case "q":
                        case "exit":
                            return 0;
                        default:
                            _output.WriteLine("Enter 1, 2 or 3.");
                            break;
                    }
                }
                catch (RpcException ex)
                {
                    _output.WriteLine($"Service call failed: {ex.Status.StatusCode}: {ex.Status.Detail}");
                }
            }

            return 0;
        }

        /// <summary>
        /// Prints the rebuild state, then one line per correction set with its build time. Volumes the service knows
        /// but has not published yet are listed as such.
        /// </summary>
        async Task ShowStatusAsync(CancellationToken cancellationToken)
        {
            RebuildStatus status = await _client.GetRebuildStatusAsync(new GetRebuildStatusRequest(), cancellationToken: cancellationToken);
            ListCorrectionSetsResponse sets = await _client.ListCorrectionSetsAsync(new ListCorrectionSetsRequest(), cancellationToken: cancellationToken);
            ListVolumesResponse volumes = await _client.ListVolumesAsync(new ListVolumesRequest(), cancellationToken: cancellationToken);

            _output.WriteLine();
            _output.WriteLine(status.InProgress
                ? $"Rebuild: RUNNING  {Describe(status)}"
                : "Rebuild: idle");
            if (status.LastStartedUtc is not null)
                _output.WriteLine($"  last started   {FormatTime(status.LastStartedUtc.ToDateTime())}");
            if (status.LastCompletedUtc is not null)
                _output.WriteLine($"  last completed {FormatTime(status.LastCompletedUtc.ToDateTime())}");
            if (!string.IsNullOrEmpty(status.LastError))
                _output.WriteLine($"  last error     {status.LastError}");

            _output.WriteLine();
            var rows = new List<string[]> { new[] { "Volume", "StosGroup", "Built (UTC)", "Age" } };
            foreach (CorrectionSetInfo set in sets.Sets)
            {
                DateTime? built = set.Provenance?.BuiltUtc?.ToDateTime();
                rows.Add(
                [
                    set.VolumeName,
                    string.IsNullOrEmpty(set.StosGroup) ? "(default)" : set.StosGroup,
                    built.HasValue ? FormatTime(built.Value) : "unknown",
                    built.HasValue ? FormatAge(_utcNow() - built.Value) : ""
                ]);
            }

            HashSet<string> published = new(sets.Sets.Select(s => s.VolumeName), StringComparer.OrdinalIgnoreCase);
            foreach (VolumeInfo volume in volumes.Volumes.Where(v => !published.Contains(v.VolumeName)))
                rows.Add([volume.VolumeName, "", "no corrections yet", ""]);

            WriteTable(rows);
        }

        /// <summary>
        /// Lists every volume that has an annotation database, asks which to rebuild (or all), starts it and watches it finish.
        /// </summary>
        async Task RefreshVolumeAsync(CancellationToken cancellationToken)
        {
            ListVolumesResponse response = await _client.ListVolumesAsync(new ListVolumesRequest(), cancellationToken: cancellationToken);
            List<VolumeInfo> volumes = [.. response.Volumes];

            _output.WriteLine();
            if (volumes.Count == 0)
            {
                _output.WriteLine("No volumes with an annotation database are assigned to this service.");
                return;
            }

            for (int i = 0; i < volumes.Count; i++)
                _output.WriteLine($"  {i + 1,2}) {volumes[i].VolumeName}");
            _output.WriteLine("   A) All volumes");
            _output.WriteLine("   B) Back");

            string choice = Prompt("Refresh which volume? ")?.Trim();
            if (string.IsNullOrEmpty(choice) || choice.Equals("b", StringComparison.OrdinalIgnoreCase))
                return;

            string name;
            if (choice.Equals("a", StringComparison.OrdinalIgnoreCase))
                name = "";
            else if (int.TryParse(choice, out int index) && index >= 1 && index <= volumes.Count)
                name = volumes[index - 1].VolumeName;
            else
            {
                _output.WriteLine("Not a volume in the list.");
                return;
            }

            string target = name.Length == 0 ? "all volumes" : name;
            string force = Prompt($"Rebuild {target} even if its corrections are current? [y/N] ");
            bool forced = force is not null && force.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);

            RebuildVolumeResponse started = await _client.RebuildVolumeAsync(
                new RebuildVolumeRequest { VolumeName = name, Force = forced }, cancellationToken: cancellationToken);
            _output.WriteLine(started.Message);
            if (started.Started)
                await WatchAsync(cancellationToken);
        }

        /// <summary>
        /// Polls the status and redraws one progress line until the rebuild ends or the user presses a key. A key
        /// only stops watching; the rebuild carries on in the service.
        /// </summary>
        async Task WatchAsync(CancellationToken cancellationToken)
        {
            _output.WriteLine("Press any key to stop watching (the rebuild keeps running).");
            DateTime began = _utcNow();
            int lastWidth = 0;
            RebuildStatus status;
            while (true)
            {
                status = await _client.GetRebuildStatusAsync(new GetRebuildStatusRequest(), cancellationToken: cancellationToken);
                string line = $"{RenderBar(status.GroupsDone, status.GroupsTotal)} {Describe(status)}  {FormatElapsed(_utcNow() - began)}";
                _output.Write("\r" + line.PadRight(lastWidth));
                lastWidth = line.Length;

                if (!status.InProgress)
                    break;
                if (_keyPressed())
                {
                    _output.WriteLine();
                    _output.WriteLine("Stopped watching. Choose Status to check on it.");
                    return;
                }

                await _delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }

            _output.WriteLine();
            _output.WriteLine(string.IsNullOrEmpty(status.LastError)
                ? "Rebuild finished."
                : $"Rebuild finished with errors: {status.LastError}");
        }

        /// <summary>Text progress bar such as <c>[#####-----]</c>; empty when the total is not yet known.</summary>
        public static string RenderBar(int done, int total)
        {
            if (total <= 0)
                return "[" + new string('.', BarWidth) + "]";
            int filled = Math.Clamp((int)Math.Round((double)done / total * BarWidth), 0, BarWidth);
            return "[" + new string('#', filled) + new string('-', BarWidth - filled) + "]";
        }

        static string Describe(RebuildStatus status)
        {
            var text = new StringBuilder();
            if (status.GroupsTotal > 0)
                text.Append($"{status.GroupsDone}/{status.GroupsTotal} sets");
            if (!string.IsNullOrEmpty(status.CurrentVolume))
                text.Append(text.Length > 0 ? ", " : "").Append(status.CurrentVolume);
            if (!string.IsNullOrEmpty(status.CurrentStep))
                text.Append(" - ").Append(Truncate(status.CurrentStep, 60));
            return text.ToString();
        }

        static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

        static string FormatTime(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm");

        public static string FormatAge(TimeSpan age)
        {
            if (age < TimeSpan.Zero)
                age = TimeSpan.Zero;
            if (age.TotalDays >= 1)
                return $"{(int)age.TotalDays}d {age.Hours}h ago";
            if (age.TotalHours >= 1)
                return $"{(int)age.TotalHours}h {age.Minutes}m ago";
            return $"{(int)age.TotalMinutes}m ago";
        }

        static string FormatElapsed(TimeSpan elapsed) => $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";

        void WriteTable(List<string[]> rows)
        {
            int[] widths = Enumerable.Range(0, rows[0].Length).Select(c => rows.Max(r => r[c].Length)).ToArray();
            foreach (string[] row in rows)
                _output.WriteLine("  " + string.Join("  ", row.Select((cell, c) => cell.PadRight(widths[c]))).TrimEnd());
        }

        /// <summary>Writes a prompt and reads a line; null when input has ended.</summary>
        string Prompt(string text)
        {
            _output.Write(text);
            return _input.ReadLine();
        }
    }
}
