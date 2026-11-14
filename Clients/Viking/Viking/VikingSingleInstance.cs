using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Viking
{
    /// <summary>
    /// Per-volume single-instance activation for viking:// deep links via named pipe + mutex.
    /// Listens on the annotation context id, the volume URL and the Identity volume name, so a link reaches the Viking
    /// that has its volume open by whichever identifier it sends. The context id comes first: several contexts share a
    /// name (RC1 has three VikingXMLs), so a link that names its context is never sent by name.
    /// <para>
    /// A Viking started by a link listens on that link's keys from the start (<see cref="ListenWhileStarting"/>) and
    /// queues what arrives until the volume is open, so a second click during sign-in does not start a third Viking.
    /// A key another Viking owns is retried every few seconds, so when that Viking closes this one takes the key over.
    /// </para>
    /// </summary>
    public static class VikingSingleInstance
    {
        public const string AckOk = "OK";
        public const string AckNotReady = "NOT_READY";
        public const string AckVolumeMismatch = "VOLUME_MISMATCH";

        private const int ConnectTimeoutMs = 1500;
        private static readonly TimeSpan TakeoverRetry = TimeSpan.FromSeconds(3);

        private static CancellationTokenSource? _listenCts;
        private static readonly List<Mutex> _mutexes = [];
        private static readonly object _mutexLock = new();
        private static Func<string, string>? _handler;
        private static string? _queuedUrl;
        private static int _shuttingDown;

        /// <summary>
        /// True after the primary instance starts teardown. In-flight deep links must not open UI.
        /// </summary>
        public static bool IsShuttingDown => Volatile.Read(ref _shuttingDown) != 0;

        /// <summary>
        /// Called from the main-window close path before viewers are destroyed, so a tools-page
        /// launch that arrives during exit is rejected instead of creating tabs with no token.
        /// </summary>
        public static void BeginShutdown()
        {
            Volatile.Write(ref _shuttingDown, 1);
        }

        /// <summary>
        /// If another Viking instance already has this volume open, forward the URL and return true when handled (OK).
        /// Tries the volume-URL pipe first, then the Identity volume-name pipe.
        /// </summary>
        public static bool TryForwardToExistingInstance(string vikingUrl)
        {
            if (string.IsNullOrWhiteSpace(vikingUrl))
                return false;

            if (!VikingDeepLinkParser.TryParse(vikingUrl, out VikingDeepLink? link) || link is null)
                return false;

            // A link that names its context is not sent by name: another context of the same name may own that pipe.
            foreach (string pipeName in PipeNamesFor(link.ContextId ?? 0, link.VolumeUrl, link.ContextId > 0 ? null : link.VolumeName))
            {
                if (TryForwardToPipe(pipeName, vikingUrl))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Called by a Viking that a link started, once forwarding found no Viking with that volume: listen on the link's
        /// keys while signing in and loading, and queue the links that arrive (the latest wins) for
        /// <see cref="TakeQueuedActivation"/>. <see cref="StartListening"/> replaces this once the volume is open.
        /// </summary>
        public static void ListenWhileStarting(VikingDeepLink link)
        {
            if (link is null)
                return;

            Listen(link.ContextId ?? 0, link.VolumeUrl, link.VolumeName, QueueUntilOpen);
        }

        /// <summary>
        /// Start accepting viking:// activations for the open volume. A key another instance owns is retried until that
        /// instance exits. Call <see cref="TakeQueuedActivation"/> afterwards for a link that arrived while starting.
        /// </summary>
        public static void StartListening(long contextId, string volumeUrl, string? volumeName, Func<string, string> handler)
        {
            if (handler is null)
                return;

            Listen(contextId, volumeUrl, volumeName, handler);
        }

        /// <summary>The last link that arrived while this Viking was starting, or null; each link is returned once.</summary>
        public static string? TakeQueuedActivation() => Interlocked.Exchange(ref _queuedUrl, null);

        private static string QueueUntilOpen(string vikingUrl)
        {
            if (IsShuttingDown)
                return AckNotReady;

            Interlocked.Exchange(ref _queuedUrl, vikingUrl);
            Trace.WriteLine("[Viking] Deep-link queued until the volume is open.", "Viking");
            return AckOk;
        }

        private static void Listen(long contextId, string? volumeUrl, string? volumeName, Func<string, string> handler)
        {
            StopListening();

            _handler = handler;
            _listenCts = new CancellationTokenSource();
            CancellationToken token = _listenCts.Token;

            var waiting = new List<string>();
            foreach (string pipeName in PipeNamesFor(contextId, volumeUrl, volumeName))
            {
                if (TryOwnPipe(pipeName))
                    StartListenLoop(pipeName, token);
                else
                    waiting.Add(pipeName);
            }

            if (waiting.Count > 0)
                Task.Run(() => TakeOverWhenFree(waiting, token), token);
        }

        private static void StartListenLoop(string pipeName, CancellationToken token)
        {
            Task.Run(() => ListenLoop(pipeName, token), token);
            Trace.WriteLine($"[Viking] Deep-link server listening on pipe {pipeName}", "Viking");
        }

        /// <summary>
        /// Retries the keys another Viking owns. Without this, when the owner closed nobody listened on its keys and the
        /// next click started yet another Viking although this one had the volume open.
        /// </summary>
        private static async Task TakeOverWhenFree(List<string> waiting, CancellationToken token)
        {
            try
            {
                while (waiting.Count > 0 && !token.IsCancellationRequested)
                {
                    await Task.Delay(TakeoverRetry, token).ConfigureAwait(false);
                    for (int i = waiting.Count - 1; i >= 0; i--)
                    {
                        if (token.IsCancellationRequested || !TryOwnPipe(waiting[i], quiet: true))
                            continue;

                        Trace.WriteLine($"[Viking] Took over deep-link pipe {waiting[i]} from a Viking that closed.", "Viking");
                        StartListenLoop(waiting[i], token);
                        waiting.RemoveAt(i);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public static void StopListening()
        {
            // Do not set IsShuttingDown here. StartListening calls this to drop a previous
            // listener, and a stuck shutdown flag made every tools-page handoff return NOT_READY.
            try
            {
                _listenCts?.Cancel();
            }
            catch { /* ignore */ }

            _listenCts?.Dispose();
            _listenCts = null;
            _handler = null;

            lock (_mutexLock)
            {
                foreach (Mutex mutex in _mutexes)
                    mutex.Dispose();
                _mutexes.Clear();
            }
        }

        internal static string NormalizeVolumeUrl(string url) => VikingDeepLinkParser.NormalizeVolumeUrl(url);

        private static bool TryForwardToPipe(string pipeName, string vikingUrl)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                client.Connect(ConnectTimeoutMs);

                WritePipeLine(client, vikingUrl.Trim());
                string? ack = ReadPipeLine(client);
                Trace.WriteLine($"[Viking] Deep-link forward ack ({pipeName}): {ack}", "Viking");
                if (ack is null || !ack.StartsWith(AckOk, StringComparison.Ordinal))
                    return false;

                // This process was started by the tools-page click, so it may foreground the open window.
                // The listening instance often cannot: it did not receive the user input.
                TryForegroundFromAck(ack);
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Viking] Deep-link forward to {pipeName} failed: {ex.Message}", "Viking");
                return false;
            }
        }

        /// <summary>
        /// The key belongs to whichever process created its named mutex; it is freed when the last handle closes (the
        /// owner disposing it or exiting). The mutex is never acquired, only held open, so the takeover task may claim
        /// it on a pool thread and <see cref="StopListening"/> drop it on any thread.
        /// </summary>
        private static bool TryOwnPipe(string pipeName, bool quiet = false)
        {
            string mutexName = @"Local\" + pipeName;
            try
            {
                var mutex = new Mutex(false, mutexName, out bool createdNew);
                if (!createdNew)
                {
                    if (!quiet)
                        Trace.WriteLine($"[Viking] Another instance already listens on {pipeName}; retrying until it closes.", "Viking");
                    mutex.Dispose();
                    return false;
                }

                lock (_mutexLock)
                    _mutexes.Add(mutex);
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Viking] Deep-link mutex failed for {pipeName}: {ex.Message}", "Viking");
                return false;
            }
        }

        private static void ListenLoop(string pipeName, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    // Synchronous byte mode. The previous async wait plus StreamReader/StreamWriter
                    // accepted the tools-page client and then broke the pipe before an ack, so the
                    // new process started a second Viking instead of jumping this one.
                    server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    Task connect = Task.Factory.FromAsync(
                        server.BeginWaitForConnection,
                        server.EndWaitForConnection,
                        null);
                    connect.ContinueWith(
                        t => { _ = t.Exception; },
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    try
                    {
                        connect.Wait(token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    string? url = ReadPipeLine(server);
                    string ack = AckNotReady;
                    try
                    {
                        Func<string, string>? handler = _handler;
                        if (IsShuttingDown || string.IsNullOrWhiteSpace(url) || handler is null)
                        {
                            Trace.WriteLine(
                                $"[Viking] Deep-link NOT_READY on {pipeName}: shuttingDown={IsShuttingDown} urlEmpty={string.IsNullOrWhiteSpace(url)} handlerMissing={handler is null}",
                                "Viking");
                            ack = AckNotReady;
                        }
                        else
                            ack = handler(url!) ?? AckNotReady;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[Viking] Deep-link handler error: {ex.Message}", "Viking");
                        ack = AckNotReady;
                    }

                    WritePipeLine(server, ack);
                    try { server.WaitForPipeDrain(); } catch { /* client may already have read the ack */ }
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                        Trace.WriteLine($"[Viking] Deep-link server error: {ex.Message}", "Viking");
                }
                finally
                {
                    try { server?.Dispose(); } catch { /* ignore */ }
                }
            }
        }

        /// <summary>
        /// One UTF-8 line, no BOM. Do not set <see cref="Stream.ReadTimeout"/> or
        /// <see cref="Stream.WriteTimeout"/>: named-pipe streams throw
        /// "Timeouts are not supported on this stream" and the tools-page process then opens a second Viking.
        /// </summary>
        private static void WritePipeLine(Stream stream, string line)
        {
            byte[] payload = Encoding.UTF8.GetBytes(line + "\n");
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        private static string? ReadPipeLine(Stream stream)
        {
            var buffer = new List<byte>(256);
            var one = new byte[1];
            while (buffer.Count < 8192)
            {
                int read = stream.Read(one, 0, 1);
                if (read == 0)
                    break;
                if (one[0] == (byte)'\n')
                    break;
                if (one[0] != (byte)'\r')
                    buffer.Add(one[0]);
            }

            if (buffer.Count == 0)
                return null;

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        /// <summary>
        /// Brings the already-open Viking window forward after a successful pipe ack.
        /// Called by the short-lived process that forwarded the viking:// link; that process
        /// still holds the user-input foreground right the listener often lacks.
        /// <c>SW_RESTORE</c> returns a maximized window to its restored rectangle, so it runs
        /// only while the window is minimized.
        /// </summary>
        private static void TryForegroundFromAck(string ack)
        {
            int space = ack.IndexOf(' ');
            if (space < 0)
                return;
            if (!long.TryParse(ack.Substring(space + 1), out long hwndValue) || hwndValue == 0)
                return;

            IntPtr hwnd = new(hwndValue);
            if (IsIconic(hwnd))
                ShowWindow(hwnd, SwRestore);
            SetForegroundWindow(hwnd);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SwRestore = 9;

        internal static IReadOnlyList<string> PipeNamesFor(long contextId, string? volumeUrl, string? volumeName)
        {
            var names = new List<string>();
            if (contextId > 0)
                names.Add(VikingChannelIdentity.PipeContextPrefix + contextId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(volumeUrl) && VikingDeepLinkParser.LooksLikeVolumeUrl(volumeUrl))
                names.Add(VikingChannelIdentity.PipePrefix + HashKey(VikingDeepLinkParser.NormalizeVolumeUrl(volumeUrl!)));
            if (!string.IsNullOrWhiteSpace(volumeName))
                names.Add(VikingChannelIdentity.PipeNamePrefix + HashKey(volumeName!.Trim().ToLowerInvariant()));
            return names;
        }

        private static string HashKey(string normalized)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            var sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
