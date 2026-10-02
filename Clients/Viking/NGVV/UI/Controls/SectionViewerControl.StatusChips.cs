using System;
using System.Drawing;
using System.Windows.Forms;

namespace Viking.UI.Controls
{
    public partial class SectionViewerControl
    {
        private ToolStripStatusLabel? _chipPenMode;
        private ToolStripStatusLabel? _chipAutoPolygonize;
        private ToolStripStatusLabel? _chipSegmentation;

        /// <summary>
        /// Fired when the user clicks a status-bar mode chip. WebAnnotation toggles
        /// Pen Mode / Auto Polygonize or opens the segmentation picker.
        /// </summary>
        public event EventHandler<ViewerStatusChipSlot>? ViewerStatusChipClicked;

        /// <summary>
        /// Updates one ambient mode chip. Creates the three chip labels on first use and
        /// keeps them left of any ViewerTask items. Call from the UI thread.
        /// </summary>
        public void SetViewerStatusChip(
            ViewerStatusChipSlot slot,
            string text,
            Color foreColor,
            string? toolTipText,
            bool visible = true)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => SetViewerStatusChip(slot, text, foreColor, toolTipText, visible)));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            EnsureStatusChipItems();
            ToolStripStatusLabel chip = GetStatusChip(slot);
            chip.Text = string.IsNullOrEmpty(text) ? " " : text;
            chip.ForeColor = foreColor;
            // IsLink labels paint with LinkColor, not ForeColor — keep both in sync so
            // Pen On/Off (and other chips) visibly change when toggled.
            chip.LinkColor = foreColor;
            chip.ActiveLinkColor = foreColor;
            chip.VisitedLinkColor = foreColor;
            chip.ToolTipText = toolTipText ?? string.Empty;
            chip.Visible = visible;
            // Do not rearrange the StatusStrip on every text refresh — Remove/Add thrash
            // fights DirectX Present and blanks the position/section labels.
        }

        private void EnsureStatusChipItems()
        {
            if (_chipPenMode is not null)
                return;

            _chipPenMode = CreateStatusChip(ViewerStatusChipSlot.PenMode);
            _chipAutoPolygonize = CreateStatusChip(ViewerStatusChipSlot.AutoPolygonize);
            _chipSegmentation = CreateStatusChip(ViewerStatusChipSlot.Segmentation);

            StatusBar.Items.Add(_chipPenMode);
            StatusBar.Items.Add(_chipAutoPolygonize);
            StatusBar.Items.Add(_chipSegmentation);
            ArrangeStatusBarTrailingItems();
            EnsureViewerChromeZOrder();
        }

        private ToolStripStatusLabel CreateStatusChip(ViewerStatusChipSlot slot)
        {
            ToolStripStatusLabel chip = new()
            {
                AutoSize = true,
                IsLink = true,
                LinkBehavior = LinkBehavior.HoverUnderline,
                Margin = new Padding(8, 3, 0, 2),
                Visible = false,
                Text = " "
            };
            chip.Click += (_, _) => ViewerStatusChipClicked?.Invoke(this, slot);
            return chip;
        }

        private ToolStripStatusLabel GetStatusChip(ViewerStatusChipSlot slot) =>
            slot switch
            {
                ViewerStatusChipSlot.PenMode => _chipPenMode!,
                ViewerStatusChipSlot.AutoPolygonize => _chipAutoPolygonize!,
                ViewerStatusChipSlot.Segmentation => _chipSegmentation!,
                _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null)
            };

        /// <summary>
        /// Places mode chips, then transient skip text, then ViewerTask chrome so channel
        /// labels stay left and job progress stays rightmost.
        /// </summary>
        private void ArrangeStatusBarTrailingItems()
        {
            if (_chipPenMode is not null)
            {
                StatusBar.Items.Remove(_chipPenMode);
                StatusBar.Items.Remove(_chipAutoPolygonize);
                StatusBar.Items.Remove(_chipSegmentation);
            }

            if (_transientStatus is not null)
                StatusBar.Items.Remove(_transientStatus);

            if (_viewerTaskStatus is not null)
            {
                StatusBar.Items.Remove(_viewerTaskStatus);
                StatusBar.Items.Remove(_viewerTaskProgress);
                StatusBar.Items.Remove(_viewerTaskCancel);
            }

            if (_chipPenMode is not null)
            {
                StatusBar.Items.Add(_chipPenMode);
                StatusBar.Items.Add(_chipAutoPolygonize);
                StatusBar.Items.Add(_chipSegmentation);
            }

            if (_transientStatus is not null)
                StatusBar.Items.Add(_transientStatus);

            if (_viewerTaskStatus is not null)
            {
                StatusBar.Items.Add(_viewerTaskStatus);
                StatusBar.Items.Add(_viewerTaskProgress);
                StatusBar.Items.Add(_viewerTaskCancel);
            }
        }
    }
}
