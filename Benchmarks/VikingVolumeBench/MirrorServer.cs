using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.Benchmarks.VolumeBench
{
    /// <summary>
    /// Serves the primed mirror of a volume over <c>http://localhost</c>, so Viking's real loading code (zip download,
    /// mosaic GET, Last-Modified checks) runs unchanged without touching the volume server or the WAN.
    /// </summary>
    /// <remarks>
    /// The VikingXML file is rewritten as it is served: its <c>path</c> and <c>host</c> attributes point at this server
    /// instead of the original host. Every other file is served byte for byte, with <c>Last-Modified</c> taken from the
    /// mirror file's write time, which prime sets from the original server's header so cache validity matches.
    /// Requests for files that are not mirrored return 404 and are recorded in <see cref="Misses"/>.
    /// </remarks>
    internal sealed class MirrorServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly string _mirrorRoot;
        private readonly byte[] _rewrittenVolumeXml;
        private readonly DateTime _volumeXmlLastModifiedUtc;
        private readonly string _volumeXmlRequestPath;
        private readonly CancellationTokenSource _stop = new();
        private Task _loop;

        /// <summary>Paths requested but not present in the mirror. A non-empty set means prime must be rerun with these sections.</summary>
        public ConcurrentDictionary<string, int> Misses { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int Port { get; }

        /// <summary>URL of the rewritten VikingXML on this server.</summary>
        public string VolumeUrl { get; }

        public MirrorServer(BenchOptions options)
        {
            _mirrorRoot = Path.GetFullPath(options.MirrorRoot);
            Port = FindFreePort();
            string localHost = $"http://localhost:{Port}";
            string localVolumeHost = $"{localHost}/{options.VolumeServerPath}";
            _volumeXmlRequestPath = $"/{options.VolumeServerPath}/{options.VolumeFileName}";
            VolumeUrl = localHost + _volumeXmlRequestPath;

            string xmlPath = MirrorPathFor(_volumeXmlRequestPath);
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException($"Mirror is missing {xmlPath}. Run the prime command first.", xmlPath);

            string xml = File.ReadAllText(xmlPath);
            string originalHost = options.VolumeHost;
            Uri originalHostUri = new(originalHost);
            xml = xml.Replace($"path=\"{originalHost}\"", $"path=\"{localVolumeHost}\"")
                     .Replace($"host=\"{originalHostUri.GetLeftPart(UriPartial.Authority)}/\"", $"host=\"{localHost}/\"");
            _rewrittenVolumeXml = Encoding.UTF8.GetBytes(xml);
            _volumeXmlLastModifiedUtc = File.GetLastWriteTimeUtc(xmlPath);

            _listener.Prefixes.Add($"{localHost}/");
        }

        public void Start()
        {
            _listener.Start();
            _loop = Task.Run(AcceptLoop);
        }

        private string MirrorPathFor(string requestPath)
        {
            string relative = Uri.UnescapeDataString(requestPath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            string full = Path.GetFullPath(Path.Combine(_mirrorRoot, relative));
            if (!full.StartsWith(_mirrorRoot, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException(requestPath);
            return full;
        }

        private async Task AcceptLoop()
        {
            while (!_stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception) when (_stop.IsCancellationRequested)
                {
                    return;
                }
                catch (HttpListenerException)
                {
                    return;
                }

                _ = Task.Run(() => Serve(context));
            }
        }

        private async Task Serve(HttpListenerContext context)
        {
            HttpListenerResponse response = context.Response;
            try
            {
                string requestPath = context.Request.Url.AbsolutePath;
                bool head = string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase);

                if (string.Equals(requestPath, _volumeXmlRequestPath, StringComparison.OrdinalIgnoreCase))
                {
                    response.StatusCode = 200;
                    response.ContentType = "text/xml";
                    response.ContentLength64 = _rewrittenVolumeXml.Length;
                    response.AddHeader("Last-Modified", _volumeXmlLastModifiedUtc.ToString("R", CultureInfo.InvariantCulture));
                    if (!head)
                        await response.OutputStream.WriteAsync(_rewrittenVolumeXml, 0, _rewrittenVolumeXml.Length).ConfigureAwait(false);
                    return;
                }

                string path = MirrorPathFor(requestPath);
                if (!File.Exists(path))
                {
                    Misses.AddOrUpdate(requestPath, 1, (_, n) => n + 1);
                    response.StatusCode = 404;
                    return;
                }

                FileInfo info = new(path);
                response.StatusCode = 200;
                response.ContentLength64 = info.Length;
                response.AddHeader("Last-Modified", info.LastWriteTimeUtc.ToString("R", CultureInfo.InvariantCulture));
                if (!head)
                {
                    using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true);
                    await file.CopyToAsync(response.OutputStream, 1 << 16).ConfigureAwait(false);
                }
            }
            catch (Exception e) when (e is HttpListenerException or IOException or UnauthorizedAccessException)
            {
                try { response.StatusCode = 500; } catch (InvalidOperationException) { }
            }
            finally
            {
                try { response.Close(); } catch (HttpListenerException) { }
            }
        }

        private static int FindFreePort()
        {
            TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public void Dispose()
        {
            _stop.Cancel();
            try { _listener.Stop(); } catch (ObjectDisposedException) { }
            _listener.Close();
            try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
            _stop.Dispose();
        }
    }
}
