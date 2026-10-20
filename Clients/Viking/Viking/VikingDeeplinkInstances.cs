using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Viking
{
    /// <summary>
    /// Hands viking://open links to a Viking that is already running, so clicking a location in the gallery moves the
    /// open session there instead of starting another Viking.
    ///
    /// Every Viking listens on a per-process named pipe (<c>Viking.Deeplink.&lt;pid&gt;</c>, current user only). A Viking
    /// started from a viking:// link first offers the link to the other Vikings of the same Windows session. One whose
    /// open volume has the link's <c>volumeName</c> (the Identity name, e.g. NM) jumps to the location and answers OK;
    /// the new process then exits. If nobody takes it the new process carries on and opens the volume as before, using
    /// the link's launch code. The code is not needed to jump, so an open session never spends it.
    /// </summary>
    internal static class VikingDeeplinkInstances
    {
        private const string PipePrefix = "Viking.Deeplink.";
        private static Thread? _listener;

        internal static string PipeName(int processId) => PipePrefix + processId.ToString(CultureInfo.InvariantCulture);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int processId);

        // ------------------------------------------------------------------ sender (the new process)

        /// <summary>True when another Viking took the link and jumped to it; the caller should then exit.</summary>
        public static bool TryForward(string vikingUrl)
        {
            try
            {
                using Process self = Process.GetCurrentProcess();
                foreach (Process other in Process.GetProcessesByName(self.ProcessName))
                {
                    using (other)
                    {
                        if (other.Id == self.Id || other.SessionId != self.SessionId)
                            continue;
                        if (TrySend(other.Id, vikingUrl))
                            return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("[Viking] Forwarding viking:// link failed: " + ex.Message, "Viking");
            }

            return false;
        }

        private static bool TrySend(int processId, string vikingUrl)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeName(processId), PipeDirection.InOut, PipeOptions.None);
                pipe.Connect(300);

                // The other Viking may raise its window; Windows only lets it do so when we say it may.
                AllowSetForegroundWindow(processId);

                var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
                var reader = new StreamReader(pipe, Encoding.UTF8);
                writer.WriteLine(vikingUrl);

                var reply = Task.Run(reader.ReadLine);
                if (!reply.Wait(TimeSpan.FromSeconds(5)))
                    return false;

                Trace.WriteLine($"[Viking] Open session {processId} answered: {reply.Result}", "Viking");
                return reply.Result?.StartsWith("OK", StringComparison.Ordinal) == true;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or AggregateException)
            {
                // No pipe (not a Viking session, or an older Viking), or not ours to use: try the next one.
                return false;
            }
        }

        // ------------------------------------------------------------------ receiver (the open session)

        /// <summary>Starts answering forwarded links. Safe to call once, early; links are refused until a volume is open.</summary>
        public static void StartListening()
        {
            if (_listener != null)
                return;

            _listener = new Thread(ListenLoop) { IsBackground = true, Name = "Viking deep-link pipe" };
            _listener.Start();
        }

        private static void ListenLoop()
        {
            string name = PipeName(Process.GetCurrentProcess().Id);
            var security = new PipeSecurity();
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.ReadWrite, AccessControlType.Allow));

            while (true)
            {
                try
                {
#if NETFRAMEWORK
                    using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                        PipeOptions.None, 4096, 4096, security);
#else
                    using var server = NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                        PipeOptions.None, 4096, 4096, security);
#endif
                    server.WaitForConnection();

                    var reader = new StreamReader(server, Encoding.UTF8, false, 4096, leaveOpen: true);
                    var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = true };
                    string? url = reader.ReadLine();
                    writer.WriteLine(HandleRequest(url));
                    server.WaitForPipeDrain();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("[Viking] Deep-link pipe error: " + ex.Message, "Viking");
                    Thread.Sleep(500);
                }
            }
        }

        /// <summary>Answers "OK" when this session jumped to the link's position, else "NO &lt;reason&gt;".</summary>
        internal static string HandleRequest(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                || !string.Equals(uri.Scheme, "viking", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(uri.Host, "open", StringComparison.OrdinalIgnoreCase))
                return "NO not a viking://open link";

            var query = Program.ParseQueryString(uri.Query);
            string? wanted = query.TryGetValue("volumeName", out string? v) ? v?.Trim() : null;
            if (string.IsNullOrEmpty(wanted))
                return "NO link names no volume";

            string? open = Viking.UI.State.IdentityVolumeName;
            if (Viking.UI.State.volume == null || string.IsNullOrEmpty(open))
                return "NO no volume open";
            if (!string.Equals(open, wanted, StringComparison.OrdinalIgnoreCase))
                return "NO different volume";

            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher == null || Viking.UI.State.ViewerForm == null)
                return "NO viewer not ready";

            // Reuses the startup parser: location=<id>, location=x,y,z[,ds], or x= y= z= ds=.
            Program.ApplyStartupPlaceArguments(query);
            var place = Viking.UI.State.StartupArguments;
            string? locationText = place["Location"];
            string? x = place["X"], y = place["Y"], z = place["Z"], ds = place["DS"];

            bool hasLocation = long.TryParse(locationText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long locationId);
            bool hasPoint = float.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out float px)
                            & float.TryParse(y, NumberStyles.Float, CultureInfo.InvariantCulture, out float py)
                            & int.TryParse(z, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pz);
            if (!hasLocation && !hasPoint)
                return "NO link has no position";

            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var form = Viking.UI.State.ViewerForm;
                    if (form == null)
                        return;
                    if (form.WindowState == FormWindowState.Minimized)
                        form.WindowState = FormWindowState.Normal;
                    form.Activate();
                    form.BringToFront();

                    if (hasLocation)
                    {
                        var go = Viking.UI.State.GoToLocationIdHandler;
                        if (go != null)
                            await go(locationId);
                    }
                    else if (float.TryParse(ds, NumberStyles.Float, CultureInfo.InvariantCulture, out float downsample))
                    {
                        form.GoToLocation(new Microsoft.Xna.Framework.Vector2(px, py), pz, false, downsample);
                    }
                    else
                    {
                        form.GoToLocation(new Microsoft.Xna.Framework.Vector2(px, py), pz, false);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("[Viking] Jump from forwarded link failed: " + ex.Message, "Viking");
                }
            });

            return "OK";
        }
    }
}
