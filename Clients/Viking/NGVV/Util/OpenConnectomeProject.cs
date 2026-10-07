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
                return [e.InnerException is WebException inner ? inner.Message : e.Message];
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
            catch (Newtonsoft.Json.JsonReaderException e)
            {
                return [e.Message];
            }
        }
    }
}
