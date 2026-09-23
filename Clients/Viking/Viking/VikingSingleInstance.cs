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
    /// Listens on both the volume URL and Identity volume name so SBFSEM/Identity links match
    /// whichever identifier they send.
    /// </summary>
    public static class VikingSingleInstance
    {
        public const string AckOk = "OK";
        public const string AckNotReady = "NOT_READY";
        public const string AckVolumeMismatch = "VOLUME_MISMATCH";

        private const int ConnectTimeoutMs = 1500;

        private static CancellationTokenSource? _listenCts;
        private static readonly List<Mutex> _mutexes = [];
        private static Func<string, string>? _handler;
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

            foreach (string pipeName in PipeNamesFor(link.VolumeUrl, link.VolumeName))
            {
                if (TryForwardToPipe(pipeName, vikingUrl))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Start accepting viking:// activations for the open volume. No-op for a key another instance already owns.
        /// </summary>
        public static void StartListening(string volumeUrl, string? volumeName, Func<string, string> handler)
        {
            StopListening();

            if (handler is null)
                return;

            _handler = handler;
            _listenCts = new CancellationTokenSource();
            CancellationToken token = _listenCts.Token;

            foreach (string pipeName in PipeNamesFor(volumeUrl, volumeName))
            {
                if (!TryOwnPipe(pipeName))
                    continue;
                string captured = pipeName;
                Task.Run(() => ListenLoop(captured, token), token);
                Trace.WriteLine($"[Viking] Deep-link server listening on pipe {pipeName}", "Viking");
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

            foreach (Mutex mutex in _mutexes)
            {
                try
                {
                    mutex.ReleaseMutex();
                }
                catch { /* ignore */ }
                mutex.Dispose();
            }
            _mutexes.Clear();
        }

        internal static string NormalizeVolumeUrl(string url) => VikingDeepLinkParser.NormalizeVolumeUrl(url);

        internal static bool VolumeUrlsMatch(string? a, string? b) => VikingDeepLinkParser.VolumeUrlsEqual(a, b);

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

        private static bool TryOwnPipe(string pipeName)
        {
            string mutexName = @"Local\" + pipeName;
            try
            {
                var mutex = new Mutex(true, mutexName, out bool createdNew);
                if (!createdNew)
                {
                    Trace.WriteLine($"[Viking] Another instance already listens on {pipeName}.", "Viking");
                    mutex.Dispose();
                    return false;
                }

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

        private static void TryForegroundFromAck(string ack)
        {
            int space = ack.IndexOf(' ');
            if (space < 0)
                return;
            if (!long.TryParse(ack.Substring(space + 1), out long hwndValue) || hwndValue == 0)
                return;

            IntPtr hwnd = new(hwndValue);
            ShowWindow(hwnd, SwRestore);
            SetForegroundWindow(hwnd);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SwRestore = 9;

        private static IEnumerable<string> PipeNamesFor(string? volumeUrl, string? volumeName)
        {
            var names = new List<string>();
            if (!string.IsNullOrWhiteSpace(volumeUrl) && VikingDeepLinkParser.LooksLikeVolumeUrl(volumeUrl))
                names.Add("VikingLegacy.Activation." + HashKey(VikingDeepLinkParser.NormalizeVolumeUrl(volumeUrl!)));
            if (!string.IsNullOrWhiteSpace(volumeName))
                names.Add("VikingLegacy.Activation.Name." + HashKey(volumeName!.Trim().ToLowerInvariant()));
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
