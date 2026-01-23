using System;
using System.Net;
using System.Net.Http;

namespace Viking.Common
{
    /// <summary>
    /// Factory for creating HttpClient instances with appropriate credentials based on URI scheme.
    /// Consolidates duplicate HTTP client creation patterns across the codebase.
    /// </summary>
    public static class HttpClientFactory
    {
        /// <summary>
        /// Creates an HttpClientHandler with appropriate credentials based on the URI scheme.
        /// For HTTPS, uses the provided credentials. For HTTP, uses default credentials.
        /// </summary>
        /// <param name="uri">The URI to determine the scheme from</param>
        /// <param name="credentials">The credentials to use for HTTPS requests</param>
        /// <returns>An HttpClientHandler configured with appropriate credentials</returns>
        public static HttpClientHandler CreateHandler(Uri uri, ICredentials? credentials = null)
        {
            if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpClientHandler
                {
                    Credentials = credentials
                };
            }
            else
            {
                return new HttpClientHandler
                {
                    UseDefaultCredentials = true // Use the default credentials for HTTP requests
                };
            }
        }

        /// <summary>
        /// Creates an HttpClient with appropriate credentials based on the URI scheme.
        /// For HTTPS, uses the provided credentials. For HTTP, uses default credentials.
        /// </summary>
        /// <param name="uri">The URI to determine the scheme from</param>
        /// <param name="credentials">The credentials to use for HTTPS requests</param>
        /// <returns>An HttpClient configured with appropriate credentials</returns>
        public static HttpClient CreateClient(Uri uri, ICredentials? credentials = null)
        {
            var handler = CreateHandler(uri, credentials);
            return new HttpClient(handler);
        }
    }
}
