using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Viking.UI;

namespace Viking
{
    /// <summary>
    /// Handles viking:// activations in the already-running primary instance (same volume only).
    /// </summary>
    public static class VikingDeepLinkActivation
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SwRestore = 9;

        public static string HandleIncomingUrl(string vikingUrl)
        {
            if (VikingSingleInstance.IsShuttingDown || string.IsNullOrWhiteSpace(vikingUrl))
            {
                Trace.WriteLine($"[Viking] Deep-link NOT_READY: shuttingDown={VikingSingleInstance.IsShuttingDown} emptyUrl={string.IsNullOrWhiteSpace(vikingUrl)}", "Viking");
                return VikingSingleInstance.AckNotReady;
            }

            Form? form = (Form?)State.Appwindow ?? State.ViewerForm;
            if (State.volume is null || form is null || form.IsDisposed || !form.IsHandleCreated
                || string.IsNullOrWhiteSpace(State.VolumeUrl))
            {
                Trace.WriteLine($"[Viking] Deep-link NOT_READY: volume={State.volume is not null} form={form is not null} handle={form?.IsHandleCreated == true} volumeUrl={State.VolumeUrl}", "Viking");
                return VikingSingleInstance.AckNotReady;
            }

            if (!VikingDeepLinkParser.TryParse(vikingUrl, out VikingDeepLink? link) || link is null)
                return VikingSingleInstance.AckNotReady;

            string? openName = State.IdentityVolumeName ?? State.volume?.Name;
            if (!VikingDeepLinkParser.VolumeTargetsMatch(link.VolumeUrl, link.VolumeName, State.VolumeUrl, openName))
                return VikingSingleInstance.AckVolumeMismatch;

            NameValueCollection place = link.Place;

            // WinForms BeginInvoke is pumped by Application.Run. The WPF dispatcher queue was not,
            // so a tools-page jump sat until close and then built UI after the token was gone.
            form.BeginInvoke(new Action(() =>
            {
                if (VikingSingleInstance.IsShuttingDown || form.IsDisposed)
                    return;

                try
                {
                    ActivateMainWindow();
                    ApplyPlace(place);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[Viking] Deep-link navigation failed: {ex.Message}", "Viking");
                }
            }));

            return VikingSingleInstance.AckOk + " " + form.Handle.ToInt64().ToString(CultureInfo.InvariantCulture);
        }

        private static void ApplyPlace(NameValueCollection place)
        {
            string? locStr = place["Location"];
            if (!string.IsNullOrWhiteSpace(locStr)
                && long.TryParse(locStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long locId))
            {
                if (State.GoToAnnotationLocation != null)
                {
                    State.GoToAnnotationLocation(locId);
                    return;
                }

                Trace.WriteLine("[Viking] GoToAnnotationLocation not registered; cannot navigate to Location ID.", "Viking");
                return;
            }

            string? strX = place["X"];
            string? strY = place["Y"];
            string? strZ = place["Z"];
            if (strX is null || strY is null || strZ is null || State.ViewerForm is null)
                return;

            float x = Convert.ToSingle(strX, CultureInfo.InvariantCulture);
            float y = Convert.ToSingle(strY, CultureInfo.InvariantCulture);
            int z = Convert.ToInt32(strZ, CultureInfo.InvariantCulture);
            State.ViewerForm.GoToLocation(new Vector2(x, y), z, false);

            string? strDs = place["DS"];
            if (strDs != null)
                State.ViewerForm.CameraDownsample = Convert.ToSingle(strDs, CultureInfo.InvariantCulture);
        }

        private static void ActivateMainWindow()
        {
            Form? form = (Form?)State.Appwindow ?? State.ViewerForm;
            if (form is null)
                return;

            if (form.WindowState == FormWindowState.Minimized)
                ShowWindow(form.Handle, SwRestore);

            form.BringToFront();
            form.Activate();
            SetForegroundWindow(form.Handle);
        }
    }
}
