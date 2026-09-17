using System;
using Geometry;

namespace Viking.Input
{
    /// <summary>
    /// Toolkit-agnostic viewport: world camera, section number, and pointer capture.
    /// WinForms Viking and WPF Jotunn both implement this.
    /// </summary>
    public interface IViewportHost
    {
        int SectionNumber { get; }

        Rectangle VisibleWorldBounds { get; }

        double Downsample { get; }

        int ViewportWidth { get; }

        int ViewportHeight { get; }

        ModifierKeys CurrentModifiers { get; }

        Vector2 ScreenToWorld(Vector2 screen);

        Vector2 WorldToScreen(Vector2 world);

        void Invalidate();

        void CapturePointer();

        void ReleasePointer();

        event EventHandler ViewportChanged;
    }
}
