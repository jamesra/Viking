using System.Windows.Forms;

namespace Viking.UI.Controls
{
    public partial class SectionViewerControl
    {
        private ToolStrip? _viewerToolStrip;

        /// <summary>
        /// Tool strip for Pen Mode / Auto Polygonize toggles. Hosted on the parent form
        /// (same pattern as <see cref="menuStrip"/>) so DirectX Present on this control
        /// cannot cover it. Created on first use.
        /// </summary>
        public ToolStrip EnsureViewerToolStrip()
        {
            if (_viewerToolStrip is not null)
                return _viewerToolStrip;

            _viewerToolStrip = new ToolStrip
            {
                Name = "viewerToolStrip",
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                RenderMode = ToolStripRenderMode.System,
                Stretch = true
            };

            // Prefer the form host. A Dock.Top child of the graphics control sits in the
            // Present region and, without reliable clipping, blanks the status bar / F1 host.
            Control? host = Parent;
            if (host is not null)
            {
                _viewerToolStrip.Parent = host;
                if (host is Form form && form.MainMenuStrip is null && menuStrip.Parent == host)
                    form.MainMenuStrip = menuStrip;
            }
            else
            {
                Controls.Add(_viewerToolStrip);
            }

            EnsureViewerChromeZOrder();
            return _viewerToolStrip;
        }

        /// <summary>
        /// Appends an item to the viewer tool strip. Called from WebAnnotation when
        /// attaching Pen Mode / Auto Polygonize toggles.
        /// </summary>
        public void AddViewerToolStripItem(ToolStripItem item)
        {
            EnsureViewerToolStrip().Items.Add(item);
        }

        /// <summary>
        /// Keeps the status bar and F1 help host above the swap-chain surface after
        /// chrome is added or re-parented.
        /// </summary>
        private void EnsureViewerChromeZOrder()
        {
            // Bottom-docked controls dock back-to-front: the higher child index docks first and
            // lands at the very bottom. Status must sit below the F1 help host, so raise status
            // first and help last (help keeps the lower index).
            StatusBar.BringToFront();
            if (commandHelpTextScrollerHost is not null)
                commandHelpTextScrollerHost.BringToFront();
        }
    }
}
