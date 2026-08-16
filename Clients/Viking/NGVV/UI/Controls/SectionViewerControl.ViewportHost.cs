using Geometry;
using Viking.Input;

namespace Viking.UI.Controls
{
    public partial class SectionViewerControl : IViewportHost
    {
        int IViewportHost.SectionNumber => Section?.Number ?? 0;

        Rectangle IViewportHost.VisibleWorldBounds =>
            Scene?.VisibleWorldBounds ?? new Rectangle(new Vector2(0, 0), 1, 1);

        double IViewportHost.Downsample => Camera?.Downsample ?? 1;

        int IViewportHost.ViewportWidth => Device?.Viewport.Width ?? 1;

        int IViewportHost.ViewportHeight => Device?.Viewport.Height ?? 1;

        ModifierKeys IViewportHost.CurrentModifiers =>
            ModifierKeysConverter.FromWinFormsKeys((int)System.Windows.Forms.Control.ModifierKeys);

        public event System.EventHandler ViewportChanged;

        Geometry.Vector2 IViewportHost.ScreenToWorld(Geometry.Vector2 screen) =>
            ScreenToWorld(screen.X, screen.Y);

        Geometry.Vector2 IViewportHost.WorldToScreen(Geometry.Vector2 world) =>
            WorldToScreen(world.X, world.Y);

        void IViewportHost.Invalidate() => Invalidate();

        void IViewportHost.CapturePointer() => Capture = true;

        void IViewportHost.ReleasePointer() => Capture = false;

        internal void RaiseViewportChanged() => ViewportChanged?.Invoke(this, System.EventArgs.Empty);
    }
}
