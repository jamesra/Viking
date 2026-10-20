using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;


namespace Viking.Common
{
    public static class OCPVolumes
    {
        /// <summary>
        /// Downloads the JSON string array of public volume tokens from an Open Connectome Project server.
        /// Blocks the caller (the volume list's selection handler) until the request finishes.
        /// Never throws for network, HTTP, or JSON failures: it returns a one-element array holding the
        /// error message, which the volume list shows in place of tokens.
        /// </summary>
        /// <remarks>
        /// Uses a bare <see cref="HttpClient"/>, not <see cref="HttpClientFactory"/>, because the factory
        /// sends Windows default credentials to http servers and this public endpoint never received them.
        /// </remarks>
        public static string[] ReadServer(Uri OCPServerURL)
        {
            using HttpClient client = new();
            try
            {
                // Task.Run keeps the WinForms synchronization context out of the wait, so blocking cannot deadlock.
                string responseString = Task.Run(() => client.GetStringAsync(OCPServerURL)).GetAwaiter().GetResult();
                string[]? array = Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(responseString);
                return array ?? [];
            }
            catch (HttpRequestException e)
            {
                // On .NET Framework the connect or DNS failure is an inner WebException; its message matches what WebClient reported.
                if (e.InnerException is WebException inner)
                    return [inner.Message];

                // .NET 10 reports a refused or unreachable connection as a SocketException; keep the .NET Framework wording.
                if (e.InnerException is System.Net.Sockets.SocketException)
                    return ["Unable to connect to the remote server"];

                return [e.Message];
            }
            catch (NotSupportedException e)
            {
                // .NET 10 rejects an ftp: or file: server URL here; .NET Framework throws ArgumentException (below).
                return [e.Message];
            }
            catch (TaskCanceledException e)
            {
                // HttpClient reports its timeout as a cancellation.
                return [e.Message];
            }
            catch (ArgumentException e)
            {
                // A user-added server URL with an ftp: or file: scheme, which WebClient accepted and HttpClient rejects.
                return [e.Message];
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                // Malformed JSON (JsonReaderException) or valid JSON of the wrong shape, such as an error
                // object where the token array was expected (JsonSerializationException).
                return [e.Message];
            }
        }
    }
}
