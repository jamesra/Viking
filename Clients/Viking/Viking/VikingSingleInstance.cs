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
        private const int IoTimeoutMs = 2000;

        private static CancellationTokenSource? _listenCts;
        private static readonly List<Mutex> _mutexes = [];
        private static Func<string, string>? _handler;

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
                client.ReadMode = PipeTransmissionMode.Byte;

                using var writer = new StreamWriter(client, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(client, Encoding.UTF8, false, 1024, leaveOpen: true);

                writer.WriteLine(vikingUrl.Trim());
                client.WriteTimeout = IoTimeoutMs;
                client.ReadTimeout = IoTimeoutMs;

                string? ack = reader.ReadLine();
                Trace.WriteLine($"[Viking] Deep-link forward ack ({pipeName}): {ack}", "Viking");
                return string.Equals(ack, AckOk, StringComparison.Ordinal);
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
                    server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    IAsyncResult wait = server.BeginWaitForConnection(null, null);
                    while (!wait.IsCompleted)
                    {
                        if (token.WaitHandle.WaitOne(100))
                        {
                            try { server.Dispose(); } catch { /* ignore */ }
                            return;
                        }
                    }

                    server.EndWaitForConnection(wait);

                    using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
                    using var writer = new StreamWriter(server, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };

                    string? url = reader.ReadLine();
                    string ack = AckNotReady;
                    try
                    {
                        Func<string, string>? handler = _handler;
                        if (string.IsNullOrWhiteSpace(url) || handler is null)
                            ack = AckNotReady;
                        else
                            ack = handler(url!) ?? AckNotReady;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[Viking] Deep-link handler error: {ex.Message}", "Viking");
                        ack = AckNotReady;
                    }

                    writer.WriteLine(ack);
                    try { server.WaitForPipeDrain(); } catch { /* ignore */ }
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
