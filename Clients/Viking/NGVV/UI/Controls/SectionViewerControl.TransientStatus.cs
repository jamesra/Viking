using System;
using System.Drawing;
using System.Windows.Forms;

namespace Viking.UI.Controls
{
    public partial class SectionViewerControl
    {
        private ToolStripStatusLabel? _transientStatus;
        private Timer? _transientStatusTimer;

        /// <summary>
        /// Shows a short non-modal message on the status bar (right of mode chips, left of
        /// ViewerTask). Used for Segment skip reasons so the user is not blocked by a dialog.
        /// Call from any thread; UI work is marshaled.
        /// </summary>
        public void ShowTransientStatus(string message, int durationMs = 6000)
        {
            if (IsDisposed || string.IsNullOrWhiteSpace(message))
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => ShowTransientStatus(message, durationMs)));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            EnsureTransientStatusItem();
            _transientStatus!.Text = message.Trim();
            _transientStatus.ForeColor = Color.FromArgb(160, 60, 0);
            _transientStatus.Visible = true;

            if (_transientStatusTimer is null)
            {
                _transientStatusTimer = new Timer { Interval = Math.Max(1000, durationMs) };
                // The designer Dispose is generated code, so release the timer from the event instead.
                Disposed += (_, _) =>
                {
                    _transientStatusTimer?.Stop();
                    _transientStatusTimer?.Dispose();
                    _transientStatusTimer = null;
                };
            }

            _transientStatusTimer.Stop();
            _transientStatusTimer.Interval = Math.Max(1000, durationMs);
            _transientStatusTimer.Tick -= OnTransientStatusTimerTick;
            _transientStatusTimer.Tick += OnTransientStatusTimerTick;
            _transientStatusTimer.Start();
        }

        private void EnsureTransientStatusItem()
        {
            if (_transientStatus is not null)
                return;

            _transientStatus = new ToolStripStatusLabel
            {
                AutoSize = true,
                Visible = false,
                Margin = new Padding(10, 3, 4, 2),
                Text = string.Empty
            };
            StatusBar.Items.Add(_transientStatus);
            ArrangeStatusBarTrailingItems();
            EnsureViewerChromeZOrder();
        }

        private void OnTransientStatusTimerTick(object? sender, EventArgs e)
        {
            _transientStatusTimer?.Stop();
            if (_transientStatus is null || IsDisposed)
                return;

            _transientStatus.Visible = false;
            _transientStatus.Text = string.Empty;
        }
    }
}
