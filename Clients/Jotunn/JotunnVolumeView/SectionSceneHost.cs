using System;
using System.Windows;
using System.Windows.Input;
using Jotunn.Controls;
using Microsoft.Xna.Framework.Graphics;
using Viking;
using Viking.Input;
using Viking.Rendering;
using Viking.VolumeModel;
using WebAnnotation;
using JotunnSectionVM = Viking.VolumeViewModel.SectionViewModel;

namespace Viking.VolumeView
{
    public class SectionSceneHost : MonoGameHwndHost, IViewportHost
    {
        readonly SectionSceneRenderer _renderer = new();
        VirtualizingGrid _grid;
        int _activeCellIndex;
        bool _effectsReady;

        public SectionSceneHost()
        {
            Drawing += OnDrawing;
            Loaded += OnLoaded;
            SizeChanged += (_, _) => Invalidate();
        }

        public VirtualizingGrid Grid
        {
            get => _grid;
            set
            {
                _grid = value;
                if (_grid != null)
                    _grid.LayoutUpdated += (_, _) => Invalidate();
            }
        }

        public Volume Volume
        {
            get => _renderer.Volume;
            set
            {
                _renderer.Volume = value;
                if (value != null)
                    TileLoadEnvironment.BindVolume(value);
            }
        }

        public IAnnotationScene Annotations
        {
            get => _renderer.Annotations;
            set => _renderer.Annotations = value;
        }

        public int SectionNumber { get; private set; }

        public Geometry.Rectangle VisibleWorldBounds =>
            Scene == null
                ? new Geometry.Rectangle(new Geometry.Vector2(0, 0), 1, 1)
                : Scene.VisibleWorldBounds;

        public double Downsample => Scene?.Camera.Downsample ?? 1;

        public int ViewportWidth => Device?.Viewport.Width ?? 1;

        public int ViewportHeight => Device?.Viewport.Height ?? 1;

        public Viking.Input.ModifierKeys CurrentModifiers =>
            Viking.Input.ModifierKeysConverter.FromWpfModifierKeys((int)Keyboard.Modifiers);

        public event EventHandler ViewportChanged;

        public Geometry.Vector2 ScreenToWorld(Geometry.Vector2 screen) =>
            Scene == null ? screen : Scene.ScreenToWorld(screen);

        public Geometry.Vector2 WorldToScreen(Geometry.Vector2 world) =>
            Scene == null ? world : Scene.WorldToScreen(world);

        public new void Invalidate() => InvalidateVisual();

        public void CapturePointer() => CaptureMouse();

        public void ReleasePointer() => ReleaseMouseCapture();

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            TileLoadEnvironment.UiDispatcher = Dispatcher;
            TileLoadEnvironment.GetDevice = () => Device;
            TileLoadEnvironment.GetVisibleWorldBounds = () => VisibleWorldBounds;
            TileLoadEnvironment.GetSectionNumber = () => SectionNumber;
            TileLoadEnvironment.GetDownsample = () => Downsample;
            if (_renderer.Volume != null)
                TileLoadEnvironment.BindVolume(_renderer.Volume);

            if (Grid == null)
                Grid = FindGrid();
        }

        VirtualizingGrid FindGrid()
        {
            SectionGridControl parent = Parent as SectionGridControl;
            return parent?.GridPanel;
        }

        void OnDrawing(object sender, DrawingEventArgs e)
        {
            if (!_effectsReady && e.Device != null && Scene != null)
            {
                try
                {
                    _renderer.InitializeEffects(e.Device, Content, Scene);
                    _effectsReady = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(ex);
                    return;
                }
            }

            if (Grid == null)
                Grid = FindGrid();

            if (!_effectsReady || Grid == null)
                return;

            ApplyVisibleRegion();
            var cells = Grid.GetVisibleCells();
            if (cells.Count == 0)
                return;

            if (_activeCellIndex >= cells.Count)
                _activeCellIndex = 0;

            Viewport originalViewport = e.Device.Viewport;
            RasterizerState originalRaster = e.Device.RasterizerState;

            for (int i = 0; i < cells.Count; i++)
            {
                GridCellLayout cell = cells[i];
                JotunnSectionVM sectionVm = cell.Item as JotunnSectionVM;
                if (sectionVm == null)
                    continue;

                Rect bounds = cell.Bounds;
                int x = Math.Max(0, (int)Math.Round(bounds.X));
                int y = Math.Max(0, (int)Math.Round(bounds.Y));
                int w = Math.Max(1, (int)Math.Round(bounds.Width));
                int h = Math.Max(1, (int)Math.Round(bounds.Height));
                if (x >= originalViewport.Width || y >= originalViewport.Height)
                    continue;

                w = Math.Min(w, originalViewport.Width - x);
                h = Math.Min(h, originalViewport.Height - y);
                if (w <= 0 || h <= 0)
                    continue;

                e.Device.Viewport = new Viewport(x, y, w, h);
                if (Scene != null)
                    Scene.Viewport = e.Device.Viewport;

                TileLoadEnvironment.GetSectionNumber = () => sectionVm.Number;
                if (i == _activeCellIndex)
                    SectionNumber = sectionVm.Number;

                _renderer.Draw(e.Device, Scene, sectionVm.section, sectionVm.DefaultChannel ?? string.Empty, System.Threading.CancellationToken.None);
            }

            e.Device.Viewport = originalViewport;
            e.Device.RasterizerState = originalRaster;
            if (Scene != null)
                Scene.Viewport = originalViewport;
            TileLoadEnvironment.GetSectionNumber = () => SectionNumber;
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }

        void ApplyVisibleRegion()
        {
            if (Grid?.VisibleRegion == null || Scene == null)
                return;

            Scene.Camera.LookAt = new Microsoft.Xna.Framework.Vector2(
                (float)Grid.VisibleRegion.Center.X,
                (float)Grid.VisibleRegion.Center.Y);
            Scene.Camera.Downsample = Grid.VisibleRegion.Downsample;
        }

        public ViewportAnnotationController AnnotationController { get; set; }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (Grid == null)
                return;

            Point p = e.GetPosition(this);
            var cells = Grid.GetVisibleCells();
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].Bounds.Contains(p))
                {
                    _activeCellIndex = i;
                    if (cells[i].Item is JotunnSectionVM sectionVm)
                        SectionNumber = sectionVm.Number;
                    break;
                }
            }

            ApplyActiveCellViewport();
            AnnotationController?.OnMouseDown(ActiveCellScreen(p), MouseButton.Left, e.ClickCount);
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            Point p = e.GetPosition(this);
            ApplyActiveCellViewport();
            AnnotationController?.OnMouseDown(ActiveCellScreen(p), MouseButton.Right, e.ClickCount);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point p = e.GetPosition(this);
                ApplyActiveCellViewport();
                AnnotationController?.OnMouseMove(ActiveCellScreen(p));
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            AnnotationController?.OnMouseUp();
        }

        void ApplyActiveCellViewport()
        {
            if (Grid == null || Scene == null || Device == null)
                return;

            var cells = Grid.GetVisibleCells();
            if (_activeCellIndex < 0 || _activeCellIndex >= cells.Count)
                return;

            Rect bounds = cells[_activeCellIndex].Bounds;
            int x = Math.Max(0, (int)Math.Round(bounds.X));
            int y = Math.Max(0, (int)Math.Round(bounds.Y));
            int w = Math.Max(1, (int)Math.Round(bounds.Width));
            int h = Math.Max(1, (int)Math.Round(bounds.Height));
            Scene.Viewport = new Viewport(x, y, w, h);
        }

        Geometry.Vector2 ActiveCellScreen(Point p)
        {
            if (Grid == null)
                return new Geometry.Vector2(p.X, p.Y);

            var cells = Grid.GetVisibleCells();
            if (_activeCellIndex < 0 || _activeCellIndex >= cells.Count)
                return new Geometry.Vector2(p.X, p.Y);

            Rect bounds = cells[_activeCellIndex].Bounds;
            return new Geometry.Vector2(p.X - bounds.X, p.Y - bounds.Y);
        }
    }
}
