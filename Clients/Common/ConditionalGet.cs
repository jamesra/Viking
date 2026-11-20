#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.Common
{
    /// <summary>What a conditional GET found out about a cached copy.</summary>
    public enum ConditionalGetOutcome
    {
        /// <summary>304: the cached copy is the server's current version.</summary>
        NotModified,

        /// <summary>2xx: the server sent a body; <see cref="ConditionalGetResult.Response"/> holds it, unread.</summary>
        Modified,

        /// <summary>404 or 410: the server no longer has the resource.</summary>
        NotFound,

        /// <summary>Any other status, a network error or a timeout. Nothing is known about the cached copy.</summary>
        Failed,
    }

    /// <summary>
    /// Result of <see cref="ConditionalGet.SendAsync(HttpClient, Uri, DateTimeOffset?, CancellationToken)"/>. Dispose it:
    /// it owns the response when the outcome is <see cref="ConditionalGetOutcome.Modified"/>.
    /// </summary>
    public sealed class ConditionalGetResult : IDisposable
    {
        internal ConditionalGetResult(ConditionalGetOutcome outcome, HttpStatusCode? statusCode, HttpResponseMessage? response)
        {
            Outcome = outcome;
            StatusCode = statusCode;
            Response = response;
        }

        public ConditionalGetOutcome Outcome { get; }

        /// <summary>The status the server answered with; null after a network error or timeout.</summary>
        public HttpStatusCode? StatusCode { get; }

        /// <summary>
        /// The response, headers read and body not yet read, when <see cref="Outcome"/> is
        /// <see cref="ConditionalGetOutcome.Modified"/>; otherwise null.
        /// </summary>
        public HttpResponseMessage? Response { get; }

        /// <summary>The response's <c>Last-Modified</c>, the date to send back as <c>If-Modified-Since</c> next time.</summary>
        public DateTimeOffset? LastModified => Response?.Content?.Headers.LastModified;

        /// <summary>
        /// True when the response carries a validator (<c>Last-Modified</c> or <c>ETag</c>). Without one the server can
        /// never answer 304, so every request downloads the body again.
        /// </summary>
        public bool HasValidator => LastModified.HasValue || Response?.Headers.ETag != null;

        public void Dispose() => Response?.Dispose();
    }

    /// <summary>
    /// One HTTP GET that both checks a cached copy and, when it is out of date, downloads the new version: the request
    /// carries <c>If-Modified-Since</c>, so the server answers 304 with no body when the copy is current.
    /// </summary>
    /// <remarks>
    /// The date sent must be the server's own <c>Last-Modified</c> from the response that produced the cached copy. nginx
    /// (rogue1) answers 304 only when the date matches its file's time exactly, so a later date (such as the time the copy
    /// was downloaded) gets a 200, and a file replaced by one with an older time is still noticed. Keep the server's date
    /// with the copy: <see cref="StampCacheFile"/> makes it the cache file's write time.
    /// </remarks>
    public static class ConditionalGet
    {
        /// <summary>
        /// The <c>If-Modified-Since</c> date for a cache file: its write time when the file exists and is not empty,
        /// otherwise null (send an unconditional GET).
        /// </summary>
        public static DateTimeOffset? ValidatorFor(string? cacheFile)
        {
            if (string.IsNullOrEmpty(cacheFile))
                return null;

            try
            {
                FileInfo info = new(cacheFile);
                if (!info.Exists || info.Length == 0)
                    return null;
                return new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return null;
            }
        }

        /// <summary>
        /// A stored server date as an <c>If-Modified-Since</c> value, or null when it is not a real date
        /// (<see cref="DateTime.MinValue"/> or <see cref="DateTime.MaxValue"/> stand for "unknown").
        /// <see cref="DateTimeKind.Unspecified"/> is read as UTC.
        /// </summary>
        public static DateTimeOffset? ValidatorFor(DateTime serverLastModified)
        {
            if (serverLastModified == DateTime.MinValue || serverLastModified == DateTime.MaxValue)
                return null;

            DateTime utc = serverLastModified.Kind switch
            {
                DateTimeKind.Local => serverLastModified.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(serverLastModified, DateTimeKind.Utc),
                _ => serverLastModified,
            };
            return new DateTimeOffset(utc, TimeSpan.Zero);
        }

        /// <summary>Sends a GET for <paramref name="uri"/> conditional on <paramref name="cacheFile"/> (see <see cref="ValidatorFor(string)"/>).</summary>
        public static Task<ConditionalGetResult> SendAsync(HttpClient client, Uri uri, string? cacheFile, CancellationToken token) =>
            SendAsync(client, uri, ValidatorFor(cacheFile), token);

        /// <summary>
        /// Sends a GET for <paramref name="uri"/> with <c>If-Modified-Since: <paramref name="ifModifiedSince"/></c>, or an
        /// unconditional GET when it is null. Only the headers are read; a <see cref="ConditionalGetOutcome.Modified"/>
        /// result holds the response for the caller to read. Network errors and timeouts become
        /// <see cref="ConditionalGetOutcome.Failed"/>.
        /// </summary>
        /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
        public static async Task<ConditionalGetResult> SendAsync(HttpClient client, Uri uri, DateTimeOffset? ifModifiedSince, CancellationToken token)
        {
            if (client is null)
                throw new ArgumentNullException(nameof(client));
            if (uri is null)
                throw new ArgumentNullException(nameof(uri));

            HttpResponseMessage response;
            using (HttpRequestMessage request = new(HttpMethod.Get, uri))
            {
                if (ifModifiedSince.HasValue)
                    request.Headers.IfModifiedSince = ifModifiedSince;

                try
                {
                    response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    return new ConditionalGetResult(ConditionalGetOutcome.Failed, null, null);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // HttpClient's own timeout, not the caller's cancellation.
                    return new ConditionalGetResult(ConditionalGetOutcome.Failed, null, null);
                }
            }

            HttpStatusCode status = response.StatusCode;
            if (response.IsSuccessStatusCode)
                return new ConditionalGetResult(ConditionalGetOutcome.Modified, status, response);

            response.Dispose();
            ConditionalGetOutcome outcome = status switch
            {
                HttpStatusCode.NotModified => ConditionalGetOutcome.NotModified,
                HttpStatusCode.NotFound or HttpStatusCode.Gone => ConditionalGetOutcome.NotFound,
                _ => ConditionalGetOutcome.Failed,
            };
            return new ConditionalGetResult(outcome, status, null);
        }

        /// <summary>
        /// Sets <paramref name="cacheFile"/>'s write time to the server's <c>Last-Modified</c>, so the next conditional GET
        /// sends exactly that date. Does nothing when <paramref name="lastModified"/> is null. Returns false when the
        /// file is missing or its time cannot be set.
        /// </summary>
        public static bool StampCacheFile(string cacheFile, DateTimeOffset? lastModified)
        {
            if (!lastModified.HasValue || string.IsNullOrEmpty(cacheFile))
                return false;

            try
            {
                if (!File.Exists(cacheFile))
                    return false;
                File.SetLastWriteTimeUtc(cacheFile, lastModified.Value.UtcDateTime);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return false;
            }
        }
    }
}
