using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Viking.UI.Controls
{
    public partial class SectionViewerControl
    {
        /// <summary>Longest status-bar tooltip line, in characters, before it is broken at a space.</summary>
        internal const int StatusBarToolTipLineLength = 100;

        private ToolTip? _statusBarToolTip;
        private Timer? _statusBarToolTipDelay;
        private ToolStripItem? _statusBarToolTipItem;
        private string? _statusBarToolTipShownText;

        /// <summary>
        /// Shows the status bar's item tooltips above the strip in place of ToolStrip's own item tooltips.
        /// Called once from the constructor, after <see cref="StatusBar"/> exists.
        /// </summary>
        /// <remarks>
        /// ToolStrip shows an item tooltip as a tracking tooltip just right of and below the cursor, and that window
        /// takes mouse input. On this bottom-docked strip the point is below the screen's working area, so
        /// <see cref="ToolTip"/> moves the tooltip back up over the strip, one pixel right of the cursor hotspot. The
        /// smallest mouse movement then lands on the tooltip, the strip hides it on MouseLeave and shows it again on
        /// hover, so it flashes; the long service-error text was unreadable. Above the strip it never covers the
        /// cursor, and it stays up until the mouse leaves the item.
        /// </remarks>
        private void InitializeStatusBarToolTips()
        {
            StatusBar.ShowItemToolTips = false;
            _statusBarToolTip = new ToolTip { ShowAlways = true, UseAnimation = false, UseFading = false };
            _statusBarToolTipDelay = new Timer { Interval = Math.Max(100, SystemInformation.MouseHoverTime) };
            _statusBarToolTipDelay.Tick += OnStatusBarToolTipDelayTick;
            StatusBar.MouseMove += OnStatusBarMouseMoveForToolTip;
            StatusBar.MouseLeave += OnStatusBarMouseLeaveForToolTip;
            // The designer Dispose is generated code, so release the timer and tooltip from the event instead.
            Disposed += (_, _) =>
            {
                _statusBarToolTipDelay?.Dispose();
                _statusBarToolTipDelay = null;
                _statusBarToolTip?.Dispose();
                _statusBarToolTip = null;
            };
        }

        private void OnStatusBarMouseMoveForToolTip(object? sender, MouseEventArgs e)
        {
            // ToolStrip rewrites MouseMove coordinates relative to the item under the mouse, so locate it from the cursor.
            ToolStripItem? item = StatusBar.GetItemAt(StatusBar.PointToClient(Cursor.Position));
            if (ReferenceEquals(item, _statusBarToolTipItem))
                return;

            HideStatusBarToolTip();
            _statusBarToolTipItem = item;
            if (item is not null && !string.IsNullOrEmpty(item.ToolTipText))
                _statusBarToolTipDelay?.Start();
        }

        private void OnStatusBarMouseLeaveForToolTip(object? sender, EventArgs e)
        {
            HideStatusBarToolTip();
            _statusBarToolTipItem = null;
        }

        private void OnStatusBarToolTipDelayTick(object? sender, EventArgs e)
        {
            _statusBarToolTipDelay?.Stop();
            ShowStatusBarToolTip();
        }

        /// <summary>
        /// Shows the new text when <paramref name="item"/> has its tooltip up and its ToolTipText changed, for example
        /// when a service error clears while the mouse rests on its status item. Call after assigning ToolTipText,
        /// on the UI thread.
        /// </summary>
        private void RefreshStatusBarToolTip(ToolStripItem item)
        {
            if (_statusBarToolTipShownText is null || !ReferenceEquals(item, _statusBarToolTipItem))
                return;

            if (!string.Equals(_statusBarToolTipShownText, WrapStatusBarToolTip(item.ToolTipText ?? string.Empty), StringComparison.Ordinal))
                ShowStatusBarToolTip();
        }

        private void ShowStatusBarToolTip()
        {
            ToolStripItem? item = _statusBarToolTipItem;
            string? toolTipText = item?.ToolTipText;
            if (_statusBarToolTip is null || item is null || IsDisposed || !item.Visible || string.IsNullOrEmpty(toolTipText))
            {
                HideStatusBarToolTip();
                return;
            }

            string text = WrapStatusBarToolTip(toolTipText!);
            Size textSize = TextRenderer.MeasureText(text, SystemFonts.StatusFont ?? Control.DefaultFont);
            // The tooltip adds a margin and border around the text; the extra keeps its bottom edge clear of the strip.
            int y = -(textSize.Height + 12);
            // No duration: it stays until the mouse leaves the item, so long errors can be read.
            _statusBarToolTip.Show(text, StatusBar, item.Bounds.Left, y);
            _statusBarToolTipShownText = text;
        }

        private void HideStatusBarToolTip()
        {
            _statusBarToolTipDelay?.Stop();
            if (_statusBarToolTipShownText is not null)
                _statusBarToolTip?.Hide(StatusBar);
            _statusBarToolTipShownText = null;
        }

        /// <summary>
        /// Breaks each line of <paramref name="text"/> at a space so no line is longer than
        /// <see cref="StatusBarToolTipLineLength"/> characters; a single longer word is left whole. The tooltip
        /// control only wraps at the screen width, which leaves a long error as one unreadable line.
        /// </summary>
        internal static string WrapStatusBarToolTip(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            StringBuilder wrapped = new(text.Length + 16);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    wrapped.Append('\n');

                string line = lines[i];
                while (line.Length > StatusBarToolTipLineLength)
                {
                    int split = line.LastIndexOf(' ', StatusBarToolTipLineLength);
                    if (split <= 0)
                        split = line.IndexOf(' ', StatusBarToolTipLineLength);
                    if (split <= 0)
                        break;

                    wrapped.Append(line, 0, split).Append('\n');
                    line = line.Substring(split + 1);
                }

                wrapped.Append(line);
            }

            return wrapped.ToString();
        }
    }
}
