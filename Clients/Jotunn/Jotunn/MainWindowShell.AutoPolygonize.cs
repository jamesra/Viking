using System;
using System.Diagnostics;
using System.Windows;
using Viking.VolumeView;
using WebAnnotation;
using WebAnnotation.Tools;
using WebAnnotation.UI.AutoPolygonize;
using MouseButton = System.Windows.Input.MouseButton;

namespace Jotunn
{
    /// <summary>
    /// Viking's auto-polygonize in Jotunn: while the view is idle, circles in view are sent to the segmentation service
    /// and their proposed outlines drawn over them. A double click on a proposal accepts it; a right click opens its
    /// Accept / Add points / Dismiss menu. Tools > Auto-polygonize circles turns it on and off (Viking's setting).
    /// </summary>
    public partial class MainWindow
    {
        AutoCirclePolygonizeController _autoPolygonize;
        int _autoPolygonizeSection;

        void AttachAutoPolygonize(SectionSceneHost host, AnnotationScene scene, AnnotationToolHost tools)
        {
            try
            {
                _autoPolygonize = new AutoCirclePolygonizeController(host, host.Invalidate);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Auto-polygonize unavailable: {ex.Message}");
                return;
            }

            scene.OverlayDraw = (device, sceneView, sectionNumber) =>
            {
                if (sectionNumber == host.SectionNumber)
                    _autoPolygonize?.Draw(device, sceneView);
            };
            _autoPolygonizeSection = host.SectionNumber;
            host.ViewportChanged += (_, _) =>
            {
                if (_autoPolygonize == null)
                    return;

                if (host.SectionNumber != _autoPolygonizeSection)
                {
                    _autoPolygonizeSection = host.SectionNumber;
                    _autoPolygonize.OnSectionChanged();
                }

                _autoPolygonize.OnCameraChanged();
            };
            host.TransientStatus += (_, message) => _status?.SetMessage(message);
            tools.PointerMoved += (_, world) => _autoPolygonize?.UpdateHover(world);
            tools.PreviewMouseDown = OnProposalMouseDown;
            Closed += (_, _) => _autoPolygonize?.Stop();

            ApplyAutoPolygonizeSetting();
        }

        /// <summary>Viking's double click on a proposal accepts it, even over the circle it proposes for.</summary>
        bool OnProposalMouseDown(Geometry.Vector2 world, MouseButton button, int clickCount)
        {
            if (_autoPolygonize == null || clickCount < 2 || button != MouseButton.Left)
                return false;
            if (!_autoPolygonize.TryHit(world, out AutoPolygonizeProposal proposal, out _))
                return false;

            return proposal.HandleMouseDoubleClick(System.Windows.Forms.MouseButtons.Left, world);
        }

        /// <summary>A right click on a proposal opens its menu instead of the annotation menu.</summary>
        bool TryShowProposalMenu(Geometry.Vector2 world)
        {
            if (_autoPolygonize == null || !_autoPolygonize.TryHit(world, out AutoPolygonizeProposal proposal, out _))
                return false;

            proposal.ShowContextMenuAtCursor();
            return true;
        }

        /// <summary>On when Viking's setting is on and a segmentation service was chosen.</summary>
        void ApplyAutoPolygonizeSetting()
        {
            bool on = WebAnnotation.Global.AnnotationSettings.AutoPolygonizeCircles && WebAnnotation.Global.IsSegmentationServiceAvailable;
            _autoPolygonize?.SetEnabled(on);
            if (AutoPolygonizeMenuItem != null)
                AutoPolygonizeMenuItem.IsChecked = WebAnnotation.Global.AnnotationSettings.AutoPolygonizeCircles;
        }

        void OnToggleAutoPolygonize(object sender, RoutedEventArgs e)
        {
            WebAnnotation.Global.AnnotationSettings.AutoPolygonizeCircles = !WebAnnotation.Global.AnnotationSettings.AutoPolygonizeCircles;
            ApplyAutoPolygonizeSetting();
            if (WebAnnotation.Global.AnnotationSettings.AutoPolygonizeCircles && !WebAnnotation.Global.IsSegmentationServiceAvailable)
                _status?.SetMessage("Auto-polygonize needs a segmentation service; choose one at login");
        }
    }
}
