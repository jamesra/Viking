using System;
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
    /// </summary>
    public static class VikingSingleInstance
    {
        public const string AckOk = "OK";
        public const string AckNotReady = "NOT_READY";
        public const string AckVolumeMismatch = "VOLUME_MISMATCH";

        private const int ConnectTimeoutMs = 1500;
        private const int IoTimeoutMs = 2000;

        private static CancellationTokenSource? _listenCts;
        private static Mutex? _volumeMutex;
        private static Func<string, string>? _handler;

        /// <summary>
        /// If another Viking instance already has this volume open, forward the URL and return true when handled (OK).
        /// </summary>
        public static bool TryForwardToExistingInstance(string vikingUrl)
        {
            if (string.IsNullOrWhiteSpace(vikingUrl))
                return false;

            if (!VikingDeepLinkParser.TryGetVolumeUrl(vikingUrl, out string? volumeUrl) || string.IsNullOrWhiteSpace(volumeUrl))
                return false;

            string pipeName = PipeNameForVolume(volumeUrl!);
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
                Trace.WriteLine($"[Viking] Deep-link forward ack: {ack}", "Viking");
                return string.Equals(ack, AckOk, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Viking] Deep-link forward failed (will start normally): {ex.Message}", "Viking");
                return false;
            }
        }

        /// <summary>
        /// Start accepting viking:// activations for the open volume. No-op if another instance already owns this volume pipe.
        /// </summary>
        public static void StartListening(string volumeUrl, Func<string, string> handler)
        {
            StopListening();

            if (string.IsNullOrWhiteSpace(volumeUrl) || handler is null)
                return;

            _handler = handler;
            string pipeName = PipeNameForVolume(volumeUrl);
            string mutexName = @"Local\VikingLegacy.Vol." + HashKey(NormalizeVolumeUrl(volumeUrl));

            try
            {
                _volumeMutex = new Mutex(true, mutexName, out bool createdNew);
                if (!createdNew)
                {
                    Trace.WriteLine("[Viking] Another instance already listens for this volume; skipping deep-link server.", "Viking");
                    _volumeMutex.Dispose();
                    _volumeMutex = null;
                    return;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Viking] Deep-link mutex failed: {ex.Message}", "Viking");
                return;
            }

            _listenCts = new CancellationTokenSource();
            CancellationToken token = _listenCts.Token;
            Task.Run(() => ListenLoop(pipeName, token), token);
            Trace.WriteLine($"[Viking] Deep-link server listening on pipe {pipeName}", "Viking");
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

            if (_volumeMutex != null)
            {
                try
                {
                    _volumeMutex.ReleaseMutex();
                }
                catch { /* ignore */ }
                _volumeMutex.Dispose();
                _volumeMutex = null;
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

        internal static string NormalizeVolumeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            string normalized = url.Trim();
            try
            {
                normalized = Viking.Common.Util.AppendDefaultVolumeFilenameIfMissing(normalized) ?? normalized;
            }
            catch
            {
                // Keep trimmed URL if append fails (malformed URI).
            }

            return normalized.TrimEnd('/').ToLowerInvariant();
        }

        internal static bool VolumeUrlsMatch(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;
            return string.Equals(NormalizeVolumeUrl(a), NormalizeVolumeUrl(b), StringComparison.Ordinal);
        }

        private static string PipeNameForVolume(string volumeUrl)
            => "VikingLegacy.Activation." + HashKey(NormalizeVolumeUrl(volumeUrl));

        private static string HashKey(string normalizedVolumeUrl)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalizedVolumeUrl));
            var sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
