using System;
using System.Threading;
using System.Windows.Forms;
using VikingXNAGraphics;
using VikingXNAWinForms;

namespace Viking.UI
{
    /// <summary>
    /// Process-wide DXGI device-removed reporting. OnPaint already catches this HRESULT;
    /// these hooks cover texture/render-target creation that happens outside paint.
    /// Call after EnableVisualStyles and before any form is shown.
    /// </summary>
    public static class GpuExceptionHandling
    {
        public static void Register()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnUiThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        static void OnUiThreadException(object sender, ThreadExceptionEventArgs e)
        {
            if (e?.Exception != null && GpuDeviceRemovedDiagnostics.IsDeviceRemovedError(e.Exception))
            {
                GpuDeviceRemovedDialog.LogAndShowOnce(e.Exception, GraphicsDeviceService.CurrentDevice);
                return;
            }

            try
            {
                using ThreadExceptionDialog dlg = new(e.Exception);
                dlg.ShowDialog();
            }
            catch (Exception)
            {
                MessageBox.Show(e?.Exception?.ToString() ?? "Unhandled UI exception",
                    "Unhandled exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is not Exception ex)
                return;
            if (!GpuDeviceRemovedDiagnostics.IsDeviceRemovedError(ex))
                return;

            GpuDeviceRemovedDialog.LogAndShowOnce(ex, GraphicsDeviceService.CurrentDevice);
        }
    }
}
