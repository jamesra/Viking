using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;

#nullable enable

namespace VikingXNAWinForms
{
    /// <summary>
    /// One-shot copyable report for DXGI device-removed. Safe to call from OnPaint:
    /// the modal dialog is posted to the UI thread so paint can finish.
    /// </summary>
    public static class GpuDeviceRemovedDialog
    {
        static int _shown;

        /// <summary>
        /// Writes the diagnostic report to the Viking log folder, traces it, and shows a
        /// copyable dialog at most once per process.
        /// </summary>
        public static void LogAndShowOnce(Exception ex, GraphicsDevice? device)
        {
            string report = GpuDeviceRemovedDiagnostics.FormatReport(ex, device);
            string? path = GpuDeviceRemovedDiagnostics.TryWriteReportFile(report);
            if (!string.IsNullOrEmpty(path))
                report += Environment.NewLine + "Report also saved to:" + Environment.NewLine + path;

            Trace.WriteLine(report, "Graphics");
            Debug.WriteLine(report);

            if (Interlocked.Exchange(ref _shown, 1) != 0)
                return;

            string reportForUi = report;
            void show()
            {
                try
                {
                    using Form form = CreateForm(reportForUi);
                    form.ShowDialog();
                }
                catch (Exception dialogEx)
                {
                    Trace.WriteLine($"GPU diagnostic dialog failed: {dialogEx}", "Graphics");
                    try
                    {
                        MessageBox.Show(reportForUi, "GPU device removed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            try
            {
                Form? host = null;
                if (Application.OpenForms.Count > 0)
                    host = Application.OpenForms[0];

                if (host != null && host.IsHandleCreated)
                {
                    host.BeginInvoke(show);
                    return;
                }
            }
            catch (Exception marshalEx)
            {
                Trace.WriteLine($"GPU diagnostic dialog marshal failed: {marshalEx.Message}", "Graphics");
            }

            if (SynchronizationContext.Current != null)
                SynchronizationContext.Current.Post(_ => show(), null);
            else
                show();
        }

        static Form CreateForm(string report)
        {
            Form form = new()
            {
                Text = "GPU device removed — copy this report",
                StartPosition = FormStartPosition.CenterScreen,
                Width = 780,
                Height = 560,
                MinimizeBox = false,
                MaximizeBox = true,
                ShowInTaskbar = true
            };

            Button close = new()
            {
                Text = "Close (restart Viking when convenient)",
                Dock = DockStyle.Bottom,
                Height = 32,
                DialogResult = DialogResult.OK
            };

            Button copy = new()
            {
                Text = "Copy to clipboard",
                Dock = DockStyle.Bottom,
                Height = 32
            };
            copy.Click += (_, __) =>
            {
                try
                {
                    Clipboard.SetText(report);
                }
                catch (Exception)
                {
                    // Clipboard can fail if the user is in a remote session without clipboard.
                }
            };

            TextBox box = new()
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericMonospace, 9f),
                Text = report
            };

            form.Controls.Add(close);
            form.Controls.Add(copy);
            form.Controls.Add(box);
            form.AcceptButton = close;
            box.Select(0, 0);
            return form;
        }
    }
}
