using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VikingCoreTests
{
    /// <summary>
    /// Minimal HTTP/1.1 server on 127.0.0.1 for tests that must exercise a real client stack without
    /// live network or an HttpListener URL reservation. Each accepted connection reads one request,
    /// answers with the current <see cref="Status"/>, <see cref="ContentType"/>, and <see cref="Body"/>,
    /// and closes. Responses are produced on a background thread; set the properties before the request.
    /// </summary>
    internal sealed class LoopbackHttpServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task acceptLoop;

        public string Status { get; set; } = "200 OK";

        public string ContentType { get; set; } = "application/json; charset=utf-8";

        public string Body { get; set; } = "";

        /// <summary>When true, read the request and never answer, so the client hits its own timeout.</summary>
        public bool NeverRespond { get; set; }

        /// <summary>Base URI ending in '/', e.g. http://127.0.0.1:51234/.</summary>
        public Uri BaseUri { get; }

        public LoopbackHttpServer()
        {
            listener.Start();
            BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
            acceptLoop = Task.Run(AcceptLoop);
        }

        /// <summary>
        /// Returns a loopback port with nothing listening, so a connect attempt is refused immediately.
        /// </summary>
        public static int UnusedPort()
        {
            TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private async Task AcceptLoop()
        {
            while (!stop.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }

                using (client)
                {
                    // A client that hangs up early must not end the loop for the next request.
                    try
                    {
                        Respond(client.GetStream());
                    }
                    catch (System.IO.IOException)
                    {
                    }
                }
            }
        }

        private void Respond(NetworkStream stream)
        {
            ReadRequestHead(stream);
            if (NeverRespond)
            {
                stop.Token.WaitHandle.WaitOne();
                return;
            }

            byte[] body = Encoding.UTF8.GetBytes(Body);
            string head = $"HTTP/1.1 {Status}\r\nContent-Type: {ContentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
            byte[] headBytes = Encoding.ASCII.GetBytes(head);
            stream.Write(headBytes, 0, headBytes.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private static void ReadRequestHead(NetworkStream stream)
        {
            int matched = 0;
            byte[] terminator = Encoding.ASCII.GetBytes("\r\n\r\n");
            while (matched < terminator.Length)
            {
                int b = stream.ReadByte();
                if (b < 0)
                    return;
                matched = b == terminator[matched] ? matched + 1 : (b == terminator[0] ? 1 : 0);
            }
        }

        public void Dispose()
        {
            stop.Cancel();
            listener.Stop();
            try
            {
                acceptLoop.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
            }
            stop.Dispose();
        }
    }
}
