using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Viking.Identity.Server.Extensions.Services
{
    public class VikingXmlMetadata
    {
        public string VolumeName { get; set; }
        public string OrgNameSuggestion { get; set; }
        public string Description { get; set; }
        public string SourceUrl { get; set; }
    }

    /// <summary>
    /// Fetches and parses VikingXML metadata for the collaborator onboarding wizard.
    /// </summary>
    public class VikingXmlMetadataService
    {
        private const long MaxResponseBytes = 5 * 1024 * 1024;
        private const int MaxRedirects = 5;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

        private readonly HttpClient _httpClient;

        public VikingXmlMetadataService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = RequestTimeout;
        }

        public async Task<VikingXmlMetadata> FetchAsync(string vikingXmlUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(vikingXmlUrl))
                throw new ArgumentException("VikingXML URL is required.", nameof(vikingXmlUrl));

            if (!Uri.TryCreate(vikingXmlUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("VikingXML URL must be an absolute http or https URL.", nameof(vikingXmlUrl));
            }

            using var response = await GetWithValidatedRedirectsAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > MaxResponseBytes)
                throw new InvalidOperationException("VikingXML response exceeds the maximum allowed size.");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var limited = new LimitedReadStream(stream, MaxResponseBytes);
            using var reader = new StreamReader(limited, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var xml = await reader.ReadToEndAsync(cancellationToken);

            var metadata = Parse(xml);
            metadata.SourceUrl = uri.ToString();
            return metadata;
        }

        /// <summary>
        /// Parses VikingXML content without fetching. Exposed for unit tests.
        /// </summary>
        public static VikingXmlMetadata Parse(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                throw new ArgumentException("VikingXML content is empty.", nameof(xml));

            var doc = XDocument.Parse(xml);
            var volumeElement = FindElement(doc.Root, "Volume") ?? doc.Root;
            if (volumeElement == null)
                throw new InvalidOperationException("VikingXML does not contain a Volume element.");

            var volumeName = GetAttributeCaseInsensitive(volumeElement, "Name")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(volumeName))
                volumeName = "Untitled Volume";

            var firstSection = FindDescendants(volumeElement, "Section").FirstOrDefault();
            var notes = string.Empty;
            if (firstSection != null)
            {
                var notesElement = FindElement(firstSection, "Notes");
                if (notesElement != null && !string.IsNullOrWhiteSpace(notesElement.Value))
                {
                    notes = SafeUnescape(notesElement.Value.Trim());
                }
            }

            var orgSuggestion = ExtractInvestigator(notes);
            if (string.IsNullOrWhiteSpace(orgSuggestion))
                orgSuggestion = volumeName;

            return new VikingXmlMetadata
            {
                VolumeName = volumeName,
                OrgNameSuggestion = orgSuggestion,
                Description = notes
            };
        }

        private async Task<HttpResponseMessage> GetWithValidatedRedirectsAsync(Uri startUri, CancellationToken cancellationToken)
        {
            var current = startUri;
            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                await EnsureHostIsPublicAsync(current.Host, cancellationToken);

                var response = await _httpClient.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if ((int)response.StatusCode < 300 || (int)response.StatusCode > 399)
                    return response;

                var location = response.Headers.Location;
                response.Dispose();
                if (location == null)
                    throw new InvalidOperationException("VikingXML URL redirected without a Location header.");

                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                if (current.Scheme != Uri.UriSchemeHttp && current.Scheme != Uri.UriSchemeHttps)
                    throw new InvalidOperationException("VikingXML redirect must stay on http or https.");
            }

            throw new InvalidOperationException("VikingXML URL exceeded the maximum number of redirects.");
        }

        private static string SafeUnescape(string value)
        {
            try
            {
                return Uri.UnescapeDataString(value);
            }
            catch (UriFormatException)
            {
                return value;
            }
        }

        private static string ExtractInvestigator(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return null;

            foreach (var rawLine in notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                const string prefix = "Investigator:";
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var value = line.Substring(prefix.Length).Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }

            return null;
        }

        private static XElement FindElement(XElement parent, string localName)
        {
            if (parent == null)
                return null;

            if (string.Equals(parent.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
                return parent;

            return parent.Elements()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        }

        private static System.Collections.Generic.IEnumerable<XElement> FindDescendants(XElement parent, string localName)
        {
            return parent.Descendants()
                .Where(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        }

        private static XAttribute GetAttributeCaseInsensitive(XElement element, string name)
        {
            return element.Attributes()
                .FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task EnsureHostIsPublicAsync(string host, CancellationToken cancellationToken)
        {
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("VikingXML URL must not target a private or loopback host.");
            }

            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException($"Unable to resolve host '{host}'.", ex);
            }

            if (addresses.Length == 0)
                throw new InvalidOperationException($"Unable to resolve host '{host}'.");

            foreach (var address in addresses)
            {
                if (IsPrivateOrLoopback(address))
                    throw new InvalidOperationException("VikingXML URL must not target a private or loopback host.");
            }
        }

        private static bool IsPrivateOrLoopback(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            if (IPAddress.IsLoopback(address))
                return true;

            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal)
                return true;

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                // 10.0.0.0/8
                if (bytes[0] == 10)
                    return true;
                // 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    return true;
                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168)
                    return true;
                // 169.254.0.0/16 link-local
                if (bytes[0] == 169 && bytes[1] == 254)
                    return true;
                // 127.0.0.0/8
                if (bytes[0] == 127)
                    return true;
                // 100.64.0.0/10 CGNAT
                if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
                    return true;
            }

            return false;
        }

        private sealed class LimitedReadStream : Stream
        {
            private readonly Stream _inner;
            private readonly long _maxBytes;
            private long _read;

            public LimitedReadStream(Stream inner, long maxBytes)
            {
                _inner = inner;
                _maxBytes = maxBytes;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count)
            {
                var remaining = _maxBytes - _read;
                if (remaining <= 0)
                    throw new InvalidOperationException("VikingXML response exceeds the maximum allowed size.");

                var toRead = (int)Math.Min(count, remaining);
                var n = _inner.Read(buffer, offset, toRead);
                _read += n;
                if (_read > _maxBytes)
                    throw new InvalidOperationException("VikingXML response exceeds the maximum allowed size.");
                return n;
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _inner.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
