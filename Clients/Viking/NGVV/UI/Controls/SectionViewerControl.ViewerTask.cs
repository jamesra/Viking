using System;
using System.Threading;
using System.Windows.Forms;

namespace Viking.UI.Controls
{
    public partial class SectionViewerControl
    {
        private ToolStripStatusLabel? _viewerTaskStatus;
        private ToolStripProgressBar? _viewerTaskProgress;
        private ToolStripButton? _viewerTaskCancel;
        private CancellationTokenSource? _viewerTaskCancellation;
        private ViewerTaskHandle? _viewerTaskHandle;

        /// <summary>
        /// Shows one background job on the status bar: message, progress, and a Cancel button.
        /// Returns null when a job is already visible. The caller disposes the handle when the job ends.
        /// Called from menu commands on the UI thread.
        /// </summary>
        /// <param name="title">Initial status text, shown until the first progress report.</param>
        /// <param name="cancellation">Cancelled by the status-bar Cancel button. The caller still owns and disposes it.</param>
        public ViewerTaskHandle? TryBeginViewerTask(string title, CancellationTokenSource cancellation)
        {
            if (cancellation is null)
                throw new ArgumentNullException(nameof(cancellation));

            if (IsDisposed)
                return null;

            if (InvokeRequired)
                return (ViewerTaskHandle?)Invoke(new Func<ViewerTaskHandle?>(() => TryBeginViewerTask(title, cancellation)));

            if (_viewerTaskHandle is not null)
                return null;

            EnsureViewerTaskItems();
            _viewerTaskCancellation = cancellation;
            _viewerTaskCancel!.Enabled = true;
            _viewerTaskStatus!.Text = string.IsNullOrWhiteSpace(title) ? "Working" : title;
            _viewerTaskProgress!.Style = ProgressBarStyle.Marquee;
            _viewerTaskProgress.MarqueeAnimationSpeed = 30;
            MoveViewerTaskItemsToEnd();
            SetViewerTaskVisible(true);

            var progress = new Progress<ViewerTaskProgress>(ApplyViewerTaskProgress);
            _viewerTaskHandle = new ViewerTaskHandle(progress, EndViewerTask);
            return _viewerTaskHandle;
        }

        private void EnsureViewerTaskItems()
        {
            if (_viewerTaskStatus is not null)
                return;

            _viewerTaskStatus = new ToolStripStatusLabel
            {
                Visible = false,
                AutoSize = true,
                Text = "Working"
            };
            _viewerTaskProgress = new ToolStripProgressBar
            {
                Visible = false,
                Width = 140,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 1
            };
            _viewerTaskCancel = new ToolStripButton
            {
                Visible = false,
                Text = "Cancel",
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                AutoSize = true,
                Margin = new Padding(4, 1, 4, 2)
            };
            _viewerTaskCancel.Click += OnViewerTaskCancel;
            StatusBar.Items.Add(_viewerTaskStatus);
            StatusBar.Items.Add(_viewerTaskProgress);
            StatusBar.Items.Add(_viewerTaskCancel);
        }

        private void OnViewerTaskCancel(object? sender, EventArgs e)
        {
            _viewerTaskCancellation?.Cancel();
            if (_viewerTaskCancel is not null)
                _viewerTaskCancel.Enabled = false;
        }

        private void ApplyViewerTaskProgress(ViewerTaskProgress progress)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => ApplyViewerTaskProgress(progress)));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            if (_viewerTaskStatus is null || _viewerTaskProgress is null)
                return;

            if (StatusBar.Items.Count == 0 || StatusBar.Items[StatusBar.Items.Count - 1] != _viewerTaskCancel)
                MoveViewerTaskItemsToEnd();

            if (!string.IsNullOrWhiteSpace(progress.Message))
                _viewerTaskStatus.Text = progress.Message;

            if (progress.Total <= 0)
            {
                _viewerTaskProgress.Style = ProgressBarStyle.Marquee;
                return;
            }

            int completed = progress.Completed;
            if (completed < 0)
                completed = 0;
            if (completed > progress.Total)
                completed = progress.Total;

            _viewerTaskProgress.Style = ProgressBarStyle.Continuous;
            if (_viewerTaskProgress.Value > progress.Total)
                _viewerTaskProgress.Value = 0;

            _viewerTaskProgress.Maximum = progress.Total;
            _viewerTaskProgress.Value = completed;
        }

        private void EndViewerTask()
        {
            if (IsDisposed)
            {
                _viewerTaskHandle = null;
                _viewerTaskCancellation = null;
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(EndViewerTask));
                }
                catch (ObjectDisposedException)
                {
                    _viewerTaskHandle = null;
                    _viewerTaskCancellation = null;
                }

                return;
            }

            _viewerTaskHandle = null;
            _viewerTaskCancellation = null;
            SetViewerTaskVisible(false);
        }

        private void MoveViewerTaskItemsToEnd()
        {
            if (_viewerTaskStatus is null || _viewerTaskProgress is null || _viewerTaskCancel is null)
                return;

            StatusBar.Items.Remove(_viewerTaskStatus);
            StatusBar.Items.Remove(_viewerTaskProgress);
            StatusBar.Items.Remove(_viewerTaskCancel);
            StatusBar.Items.Add(_viewerTaskStatus);
            StatusBar.Items.Add(_viewerTaskProgress);
            StatusBar.Items.Add(_viewerTaskCancel);
        }

        private void SetViewerTaskVisible(bool visible)
        {
            if (_viewerTaskStatus is not null)
                _viewerTaskStatus.Visible = visible;
            if (_viewerTaskProgress is not null)
                _viewerTaskProgress.Visible = visible;
            if (_viewerTaskCancel is not null)
                _viewerTaskCancel.Visible = visible;
        }
    }
}
