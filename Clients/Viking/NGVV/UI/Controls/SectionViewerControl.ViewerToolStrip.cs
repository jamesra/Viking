using System;
using System.Drawing;
using System.Windows.Forms;
using Viking.UI;

namespace Viking.UI.Controls
{
    public partial class SectionViewerControl
    {
        private ToolStrip? _viewerToolStrip;
        private ToolStripButton? _buttonZoomIn;
        private ToolStripButton? _buttonZoomOut;
        private ToolStripButton? _buttonHomeMag;

        /// <summary>
        /// Tool strip for viewer chrome (zoom) and annotation toggles. Hosted on the parent form
        /// (same pattern as <see cref="menuStrip"/>) so DirectX Present on this control
        /// cannot cover it. Created on first use; magnification buttons are always installed.
        /// </summary>
        public ToolStrip EnsureViewerToolStrip()
        {
            if (_viewerToolStrip is null)
            {
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
            }

            EnsureMagnificationButtons();
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
        /// Installs Zoom In / Zoom Out / Home at the left of the strip once. Four clicks
        /// double or half downsample; Home snaps to the nearest power of two.
        /// </summary>
        private void EnsureMagnificationButtons()
        {
            if (_viewerToolStrip is null)
                return;

            if (_buttonZoomIn is { IsDisposed: false })
                return;

            int iconPixels = Math.Max(24, (int)Math.Round(24 * _viewerToolStrip.DeviceDpi / 96.0));
            _viewerToolStrip.ImageScalingSize = new Size(iconPixels, iconPixels);

            _buttonZoomIn = CreateMagnificationButton(
                "Zoom In",
                ZoomToolbarIcons.ZoomIn(iconPixels),
                "Zoom In — four clicks halves magnification (more detail)",
                (_, _) =>
                {
                    Downsample = ViewerMagnificationSteps.ZoomIn(Downsample);
                    Invalidate();
                });

            _buttonZoomOut = CreateMagnificationButton(
                "Zoom Out",
                ZoomToolbarIcons.ZoomOut(iconPixels),
                "Zoom Out — four clicks doubles magnification (less detail)",
                (_, _) =>
                {
                    Downsample = ViewerMagnificationSteps.ZoomOut(Downsample);
                    Invalidate();
                });

            _buttonHomeMag = CreateMagnificationButton(
                "Home",
                ZoomToolbarIcons.Home(iconPixels),
                "Home — round magnification to the nearest power of 2",
                (_, _) =>
                {
                    Downsample = ViewerMagnificationSteps.NearestPowerOfTwo(Downsample);
                    Invalidate();
                });

            // Keep zoom on the left even if annotation items were added first.
            int insertAt = 0;
            _viewerToolStrip.Items.Insert(insertAt++, _buttonZoomIn);
            _viewerToolStrip.Items.Insert(insertAt++, _buttonZoomOut);
            _viewerToolStrip.Items.Insert(insertAt++, _buttonHomeMag);
            _viewerToolStrip.Items.Insert(insertAt, new ToolStripSeparator());
        }

        private static ToolStripButton CreateMagnificationButton(
            string name,
            Image image,
            string toolTip,
            EventHandler onClick)
        {
            ToolStripButton button = new()
            {
                Name = name,
                Text = name,
                Image = image,
                DisplayStyle = ToolStripItemDisplayStyle.Image,
                AutoToolTip = true,
                ToolTipText = toolTip
            };
            button.Click += onClick;
            return button;
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
