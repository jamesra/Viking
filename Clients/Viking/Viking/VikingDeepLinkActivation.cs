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
            if (string.IsNullOrWhiteSpace(vikingUrl))
                return VikingSingleInstance.AckNotReady;

            if (State.volume is null || State.ViewerForm is null || string.IsNullOrWhiteSpace(State.VolumeUrl))
                return VikingSingleInstance.AckNotReady;

            if (!Uri.TryCreate(vikingUrl, UriKind.Absolute, out Uri? uri) || string.IsNullOrEmpty(uri?.Query))
                return VikingSingleInstance.AckNotReady;

            var query = VikingDeepLinkParser.ParseQueryString(uri.Query);
            query.TryGetValue("volume", out string? linkVolumeUrl);

            if (!VikingSingleInstance.VolumeUrlsMatch(linkVolumeUrl, State.VolumeUrl))
                return VikingSingleInstance.AckVolumeMismatch;

            NameValueCollection place = VikingDeepLinkParser.ParsePlaceArguments(query);

            void Navigate()
            {
                try
                {
                    ActivateMainWindow();
                    ApplyPlace(place);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[Viking] Deep-link navigation failed: {ex.Message}", "Viking");
                }
            }

            if (State.MainThreadDispatcher != null)
            {
                State.MainThreadDispatcher.BeginInvoke(new Action(Navigate));
            }
            else if (State.ViewerForm.IsHandleCreated)
            {
                State.ViewerForm.BeginInvoke(new Action(Navigate));
            }
            else
            {
                return VikingSingleInstance.AckNotReady;
            }

            return VikingSingleInstance.AckOk;
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
