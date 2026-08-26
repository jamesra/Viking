using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Jotunn.Common;
using Jotunn.Controls;
using Microsoft.Xna.Framework.Graphics;
using Viking;
using Viking.Input;
using Viking.Rendering;
using Viking.VolumeModel;
using WebAnnotation;
using WebAnnotation.Tools;
using JotunnSectionVM = Viking.VolumeViewModel.SectionViewModel;
using VolumeVM = Viking.VolumeViewModel.VolumeViewModel;

namespace Viking.VolumeView
{
    /// <summary>
    /// Jotunn section viewport. Owns SectionSceneRenderer and implements IViewportHost for annotation input.
    /// </summary>
    public class SectionSceneHost : MonoGameHwndHost, IViewportHost
    {
        readonly SectionSceneRenderer _renderer = new();
        VirtualizingGrid _grid;
        MappingBase _fitMapping;
        int _activeCellIndex;
        bool _effectsReady;
        bool _fittedViewToMapping;
        int _loggedEffectFail;
        Point? _panStartScreen;
        Point? _panStartCenter;
        bool _panning;
        bool _annotating;
        readonly Dictionary<int, CancellationTokenSource> _sectionTextureLoadCts = new();
        readonly object _loadCtsLock = new();
        readonly DispatcherTimer _checkpointTimer;
        bool _drawSinceCheckpoint;
        Geometry.Rectangle? _lastReportedBounds;
        double _lastReportedDownsample;
        int _lastReportedSection = int.MinValue;

        public SectionSceneHost()
        {
            Drawing += OnDrawing;
            Loaded += OnLoaded;
            SizeChanged += OnHostSizeChanged;
            _renderer.MappingReady += OnMappingReady;
            _checkpointTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _checkpointTimer.Tick += (_, _) => RunTileCacheCheckpoint();
        }

        /// <summary>
        /// Source of VisibleRegion and cell bounds. Pan/zoom write Grid.VisibleRegion; ApplyVisibleRegion copies it onto Scene.Camera.
        /// </summary>
        public VirtualizingGrid Grid
        {
            get => _grid;
            set
            {
                _grid = value;
                if (_grid != null)
                {
                    _grid.LayoutUpdated += (_, _) => Invalidate();
                    TryFitViewToMapping();
                }
            }
        }

        /// <summary>
        /// Open volume forwarded to the renderer. Also calls TileLoadEnvironment.BindVolume.
        /// </summary>
        public Volume Volume
        {
            get => _renderer.Volume;
            set
            {
                _renderer.Volume = value;
                _fittedViewToMapping = false;
                _fitMapping = null;
                if (value != null)
                {
                    TileLoadEnvironment.BindVolume(value);
                    if (value.Sections.Count > 0)
                    {
                        Section first = FallbackSection() ?? value.Sections.Values[0];
                        SectionNumber = first.Number;
                        CancellationToken token = GetOrCreateSectionTextureLoadToken(first.Number);
                        _renderer.EnsureMappingInitialized(first, token);
                        int idx = value.Sections.IndexOfKey(first.Number);
                        if (idx > 0)
                        {
                            Section below = value.Sections.Values[idx - 1];
                            _renderer.EnsureMappingInitialized(below, GetOrCreateSectionTextureLoadToken(below.Number));
                        }
                        if (idx >= 0 && idx + 1 < value.Sections.Count)
                        {
                            Section above = value.Sections.Values[idx + 1];
                            _renderer.EnsureMappingInitialized(above, GetOrCreateSectionTextureLoadToken(above.Number));
                        }
                    }
                }
            }
        }

        public IAnnotationScene Annotations
        {
            get => _renderer.Annotations;
            set
            {
                _renderer.Annotations = value;
                RequestRender();
            }
        }

        /// <summary>
        /// Section of the active grid cell. Used by the texture sort timer and annotation hit-test.
        /// </summary>
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

        public new void Invalidate()
        {
            RequestRender();
            InvalidateVisual();
        }

        /// <summary>
        /// Overlay Border owns capture. Capturing this HWND would steal moves from the overlay during annotation drags.
        /// </summary>
        public void CapturePointer()
        {
        }

        public void ReleasePointer()
        {
        }

        /// <summary>
        /// Binds TileLoadEnvironment to this dispatcher and device. App.OnStartup should
        /// already have called StartTexturePipeline; this repeats it after the HWND exists
        /// so the pump has a live GetDevice / GetVisibleWorldBounds.
        /// </summary>
        void OnLoaded(object sender, RoutedEventArgs e)
        {
            TileLoadEnvironment.UiDispatcher = Dispatcher;
            TileLoadEnvironment.GetDevice = () => Device;
            TileLoadEnvironment.GetVisibleWorldBounds = () => VisibleWorldBounds;
            TileLoadEnvironment.GetSectionNumber = () => SectionNumber;
            TileLoadEnvironment.GetDownsample = () => Downsample;
            TileLoadEnvironment.RequestRender = () =>
            {
                if (Dispatcher.CheckAccess())
                    RequestRender();
                else
                    Dispatcher.BeginInvoke(new Action(RequestRender));
            };
            TileLoadEnvironment.StartTexturePipeline();
            if (!_checkpointTimer.IsEnabled)
                _checkpointTimer.Start();
            if (_renderer.Volume != null)
                TileLoadEnvironment.BindVolume(_renderer.Volume);

            if (Grid == null)
                Grid = FindGrid();
        }

        VirtualizingGrid FindGrid()
        {
            DependencyObject current = this;
            while (current != null)
            {
                if (current is SectionGridControl control)
                    return control.GridPanel;
                current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
            }

            return null;
        }

        /// <summary>
        /// One MonoGame backbuffer, one viewport per visible grid cell. Draws the current
        /// section full-viewport when the overlay grid has no realized cells yet so mapping
        /// init and tiles are not blocked on ItemsControl virtualization.
        /// </summary>
        void OnDrawing(object sender, DrawingEventArgs e)
        {
            TileLoadEnvironment.GetDevice = () => e.Device;
            TileLoadEnvironment.UiDispatcher ??= Dispatcher;

            TryInitializeEffects(e.Device);

            if (Grid == null)
                Grid = FindGrid();

            TryFitViewToMapping();
            ApplyVisibleRegion();

            List<(Section section, string channel, Viewport viewport)> draws = CollectDraws(e.Device);
            if (draws.Count == 0)
            {
                Trace.WriteLine("Section view: no section to draw (Volume or grid cells missing)");
                return;
            }

            Viewport originalViewport = e.Device.Viewport;
            RasterizerState originalRaster = e.Device.RasterizerState;

            HashSet<int> keep = new();
            for (int i = 0; i < draws.Count; i++)
                keep.Add(draws[i].section.Number);
            AddAdjacentSectionNumbers(keep);
            PruneTextureLoadTokens(keep);

            VolumeVM volumeVm = Grid?.DataContext as VolumeVM ?? DataContext as VolumeVM;
            if (volumeVm != null)
                _renderer.VolumeTransformName = volumeVm.ActiveVolumeTransform ?? string.Empty;

            bool panning = _panning;
            int activeSectionNumber = ResolveActiveSectionNumber(draws);
            for (int i = 0; i < draws.Count; i++)
            {
                (Section section, string channel, Viewport viewport) = draws[i];
                e.Device.Viewport = viewport;
                if (Scene != null)
                    Scene.Viewport = viewport;

                TileLoadEnvironment.GetSectionNumber = () => section.Number;
                bool active = draws.Count == 1 || section.Number == activeSectionNumber;
                if (active)
                    SectionNumber = section.Number;

                JotunnSectionVM sectionVm = volumeVm != null && volumeVm.SectionViewModels.ContainsKey(section.Number)
                    ? volumeVm.SectionViewModels[section.Number]
                    : null;
                _renderer.SectionTransformName = sectionVm?.SelectedSectionTransform ?? section.DefaultPyramidTransform ?? string.Empty;

                CancellationToken token = GetOrCreateSectionTextureLoadToken(section.Number);
                _renderer.EnsureMappingInitialized(section, token);
                bool loadFullResolution = active;
                bool queueTextureLoads = active || !panning;
                bool loadAnnotations = active;
                _renderer.Draw(e.Device, Scene, section, channel, token, loadFullResolution, queueTextureLoads, loadAnnotations);
            }

            e.Device.Viewport = originalViewport;
            e.Device.RasterizerState = originalRaster;
            if (Scene != null)
                Scene.Viewport = originalViewport;
            TileLoadEnvironment.GetSectionNumber = () => SectionNumber;
            RaiseViewportChangedIfNeeded();
            _drawSinceCheckpoint = true;
        }

        void TryInitializeEffects(GraphicsDevice device)
        {
            if (_effectsReady || device == null || Scene == null)
                return;

            try
            {
                _renderer.InitializeEffects(device, Content, Scene);
                _effectsReady = true;
            }
            catch (Exception ex)
            {
                if (System.Threading.Interlocked.Exchange(ref _loggedEffectFail, 1) == 0)
                    Trace.WriteLine($"InitializeEffects failed (need Content/TileLayout.xnb next to the exe): {ex}");
            }
        }

        CancellationToken GetOrCreateSectionTextureLoadToken(int sectionNumber)
        {
            lock (_loadCtsLock)
            {
                if (_sectionTextureLoadCts.TryGetValue(sectionNumber, out CancellationTokenSource cts)
                    && !cts.IsCancellationRequested)
                {
                    return cts.Token;
                }

                cts?.Dispose();
                var created = new CancellationTokenSource();
                _sectionTextureLoadCts[sectionNumber] = created;
                return created.Token;
            }
        }

        void AddAdjacentSectionNumbers(HashSet<int> keep)
        {
            if (Volume == null || keep.Count == 0)
                return;

            int current = SectionNumber;
            int iSection = Volume.Sections.IndexOfKey(current);
            if (iSection < 0)
                return;

            int iMin = Math.Max(0, iSection - 2);
            int iMax = Math.Min(Volume.Sections.Count - 1, iSection + 2);
            for (int i = iMin; i <= iMax; i++)
                keep.Add(Volume.Sections.Keys[i]);
        }

        void PruneTextureLoadTokens(HashSet<int> keep)
        {
            List<CancellationTokenSource> dispose = null;
            lock (_loadCtsLock)
            {
                List<int> toRemove = new();
                foreach (KeyValuePair<int, CancellationTokenSource> kv in _sectionTextureLoadCts)
                {
                    if (keep.Contains(kv.Key))
                        continue;
                    kv.Value.Cancel();
                    toRemove.Add(kv.Key);
                    (dispose ??= new List<CancellationTokenSource>()).Add(kv.Value);
                }

                foreach (int key in toRemove)
                    _sectionTextureLoadCts.Remove(key);
            }

            if (dispose != null)
            {
                foreach (CancellationTokenSource cts in dispose)
                    cts.Dispose();
                RunTileCacheCheckpoint();
            }
        }

        void RunTileCacheCheckpoint()
        {
            if (!_drawSinceCheckpoint)
                return;
            _drawSinceCheckpoint = false;
            TileViewModelCache cache = TileLoadEnvironment.TileViewModelCache;
            _ = Task.Run(() =>
            {
                cache.Checkpoint();
                Viking.VolumeModel.Global.TileCache.Checkpoint();
            });
        }

        void RaiseViewportChangedIfNeeded()
        {
            if (Scene == null)
                return;

            Geometry.Rectangle bounds = Scene.VisibleWorldBounds;
            double downsample = Scene.Camera.Downsample;
            if (_lastReportedSection == SectionNumber
                && _lastReportedDownsample == downsample
                && _lastReportedBounds.HasValue
                && _lastReportedBounds.Value.Equals(bounds))
            {
                return;
            }

            _lastReportedSection = SectionNumber;
            _lastReportedDownsample = downsample;
            _lastReportedBounds = bounds;
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Visible grid cells that resolve to a section, or the volume's current section in the
        /// full backbuffer when the overlay has not realized any cells.
        /// </summary>
        List<(Section section, string channel, Viewport viewport)> CollectDraws(GraphicsDevice device)
        {
            var draws = new List<(Section section, string channel, Viewport viewport)>();
            Viewport deviceViewport = device.Viewport;

            if (Grid != null)
            {
                IReadOnlyList<GridCellLayout> cells = Grid.GetVisibleCells();
                if (_activeCellIndex >= cells.Count)
                    _activeCellIndex = 0;

                for (int i = 0; i < cells.Count; i++)
                {
                    JotunnSectionVM sectionVm = ResolveSectionVm(cells[i].Item);
                    if (sectionVm?.section == null)
                        continue;
                    if (!TryToDeviceViewport(cells[i].Bounds, deviceViewport, out Viewport cellViewport))
                        continue;

                    draws.Add((sectionVm.section, sectionVm.SelectedChannel ?? sectionVm.DefaultChannel ?? string.Empty, cellViewport));
                }
            }

            if (draws.Count == 0)
            {
                Section section = FallbackSection();
                if (section != null)
                {
                    string channel = section.DefaultChannel ?? string.Empty;
                    if (Grid?.DataContext is VolumeVM vm && vm.SectionViewModels.ContainsKey(section.Number))
                        channel = vm.SectionViewModels[section.Number].SelectedChannel ?? channel;
                    draws.Add((section, channel, deviceViewport));
                }
            }

            return draws;
        }

        /// <summary>
        /// Section number of the focused grid cell. Falls back to the first realized draw when that cell did not produce a draw.
        /// </summary>
        int ResolveActiveSectionNumber(List<(Section section, string channel, Viewport viewport)> draws)
        {
            if (draws.Count == 0)
                return SectionNumber;

            if (Grid != null)
            {
                IReadOnlyList<GridCellLayout> cells = Grid.GetVisibleCells();
                if (_activeCellIndex >= 0 && _activeCellIndex < cells.Count)
                {
                    JotunnSectionVM vm = ResolveSectionVm(cells[_activeCellIndex].Item);
                    if (vm?.section != null)
                    {
                        for (int i = 0; i < draws.Count; i++)
                        {
                            if (draws[i].section.Number == vm.section.Number)
                                return vm.section.Number;
                        }
                    }
                }
            }

            return draws[0].section.Number;
        }

        static JotunnSectionVM ResolveSectionVm(object item)
        {
            if (item is JotunnSectionVM vm)
                return vm;
            if (item is FrameworkElement fe)
                return fe.DataContext as JotunnSectionVM;
            return null;
        }

        /// <summary>
        /// Section at the grid's CenterNumber (list index into Volume.Sections), used when
        /// overlay cells are empty or did not resolve a SectionViewModel.
        /// </summary>
        Section FallbackSection()
        {
            if (Volume == null || Volume.Sections.Count == 0)
                return null;

            int index = Grid?.CenterNumber ?? 0;
            if (index < 0)
                index = 0;
            if (index >= Volume.Sections.Count)
                index = Volume.Sections.Count - 1;
            return Volume.Sections.Values[index];
        }

        /// <summary>
        /// Maps overlay-grid DIP bounds into the HWND backbuffer. Grid and host are siblings;
        /// without TransformToVisual the cell rect can miss the device viewport entirely.
        /// </summary>
        bool TryToDeviceViewport(Rect dipBounds, Viewport deviceViewport, out Viewport result)
        {
            result = default;
            Rect hostBounds = dipBounds;
            if (Grid != null)
            {
                try
                {
                    hostBounds = Grid.TransformToVisual(this).TransformBounds(dipBounds);
                }
                catch (InvalidOperationException)
                {
                }
            }

            double hostW = Math.Max(ActualWidth, 1);
            double hostH = Math.Max(ActualHeight, 1);
            double scaleX = deviceViewport.Width / hostW;
            double scaleY = deviceViewport.Height / hostH;

            int x = Math.Max(0, (int)Math.Round(hostBounds.X * scaleX));
            int y = Math.Max(0, (int)Math.Round(hostBounds.Y * scaleY));
            int w = Math.Max(1, (int)Math.Round(hostBounds.Width * scaleX));
            int h = Math.Max(1, (int)Math.Round(hostBounds.Height * scaleY));
            if (x >= deviceViewport.Width || y >= deviceViewport.Height)
                return false;

            w = Math.Min(w, deviceViewport.Width - x);
            h = Math.Min(h, deviceViewport.Height - y);
            if (w <= 0 || h <= 0)
                return false;

            result = new Viewport(x, y, w, h);
            return true;
        }

        /// <summary>
        /// First successful mapping: center and downsample to ControlBounds, matching
        /// VikingMain's default-position path. The default VisibleRegion (0,0 / ds 256)
        /// is usually off the mosaic, which looks like a black view with a few labels.
        /// </summary>
        void OnMappingReady(MappingBase mapping)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnMappingReady(mapping)));
                return;
            }

            _fitMapping = mapping;
            TryFitViewToMapping();
            Invalidate();
        }

        void TryFitViewToMapping()
        {
            if (_fittedViewToMapping || _fitMapping == null)
                return;

            Geometry.Rectangle bounds = _fitMapping.ControlBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                Trace.WriteLine($"Mapping ready but ControlBounds are empty: {bounds}");
                return;
            }

            double viewW = Math.Max(ActualWidth, 1);
            double viewH = Math.Max(ActualHeight, 1);
            double downsample = Math.Max(bounds.Width / viewW, bounds.Height / viewH);
            if (downsample < 1)
                downsample = 1;

            if (Scene != null)
            {
                Scene.Camera.LookAt = new Microsoft.Xna.Framework.Vector2((float)bounds.Center.X, (float)bounds.Center.Y);
                Scene.Camera.Downsample = downsample;
            }

            if (Grid == null)
                return;

            Grid.VisibleRegion = new VisibleRegionInfo(
                new Point(bounds.Center.X, bounds.Center.Y),
                HostWorldArea(downsample),
                downsample);
            if (ActualWidth >= 16 && ActualHeight >= 16)
                _fittedViewToMapping = true;
            Trace.WriteLine(
                $"Fitted view to mapping bounds center=({bounds.Center.X:0},{bounds.Center.Y:0}) ds={downsample:0.0}");
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

        /// <summary>
        /// Annotation click/drag handler. Null until the volume view wires it after store init.
        /// </summary>
        public AnnotationToolHost AnnotationTools { get; set; }

        /// <summary>
        /// Viking mouse: left click selects/moves/creates annotations; hold right and drag to pan;
        /// wheel zooms about the cursor.
        /// </summary>
        public void HandleViewMouseDown(Point p, MouseButton button, int clickCount)
        {
            try
            {
                SelectCellAt(p);
                ApplyActiveCellViewport();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Select cell failed: {ex}");
                return;
            }
            Geometry.Vector2 screen = ActiveCellScreen(p);

            if (button == MouseButton.XButton2)
            {
                StepSection(1);
                return;
            }

            if (button == MouseButton.XButton1)
            {
                StepSection(-1);
                return;
            }

            if (button == MouseButton.Right)
            {
                _panStartScreen = p;
                _panStartCenter = Grid?.VisibleRegion?.Center;
                _panning = false;
                return;
            }

            if (button != MouseButton.Left)
                return;

            _annotating = true;
            AnnotationTools?.OnMouseDown(screen, button, clickCount);
        }

        public void HandleViewMouseMove(Point p, MouseButtonState leftButton, MouseButtonState rightButton)
        {
            if (_annotating && leftButton == MouseButtonState.Pressed)
            {
                ApplyActiveCellViewport();
                AnnotationTools?.OnMouseMove(ActiveCellScreen(p));
                return;
            }

            if (rightButton != MouseButtonState.Pressed || !_panStartScreen.HasValue || Grid?.VisibleRegion == null)
                return;

            Vector delta = p - _panStartScreen.Value;
            if (!_panning && delta.Length < 4)
                return;

            _panning = true;
            double ds = Grid.VisibleRegion.Downsample;
            Point center = _panStartCenter ?? Grid.VisibleRegion.Center;
            Point newCenter = new(
                center.X + (_panStartScreen.Value.X - p.X) * ds,
                center.Y - (_panStartScreen.Value.Y - p.Y) * ds);
            Size area = HostWorldArea(ds);
            Grid.VisibleRegion = new VisibleRegionInfo(newCenter, area, ds);
            RequestRender();
        }

        public void HandleViewMouseUp(Point p, MouseButton button)
        {
            if (button == MouseButton.Left && _annotating)
            {
                ApplyActiveCellViewport();
                AnnotationTools?.OnMouseUp();
                _annotating = false;
            }

            if (button == MouseButton.Right)
            {
                _panStartScreen = null;
                _panStartCenter = null;
                if (_panning)
                    RunTileCacheCheckpoint();
                _panning = false;
                RequestRender();
            }
        }

        public void HandleViewMouseWheel(int delta, Point cursor)
        {
            if (Grid?.VisibleRegion == null)
                return;

            bool haveBefore = TryGetWorldPosition(cursor, out Geometry.Vector2 before, out _);

            float multiplier = delta / 120.0f;
            double ds = Grid.VisibleRegion.Downsample;
            ds *= multiplier > 0 ? 0.86956521739130434782608695652174 : 1.15;
            if (ds < 0.25)
                ds = 0.25;

            Grid.VisibleRegion = new VisibleRegionInfo(
                Grid.VisibleRegion.Center,
                HostWorldArea(ds),
                ds);
            ApplyVisibleRegion();

            if (haveBefore && TryGetWorldPosition(cursor, out Geometry.Vector2 after, out _))
            {
                Point center = Grid.VisibleRegion.Center;
                Grid.VisibleRegion = new VisibleRegionInfo(
                    new Point(center.X + (before.X - after.X), center.Y + (before.Y - after.Y)),
                    HostWorldArea(ds),
                    ds);
            }
            RequestRender();
        }

        /// <summary>
        /// World-space size of the host, matching pan/zoom rather than a single grid cell.
        /// </summary>
        Size HostWorldArea(double downsample) =>
            new(Math.Max(ActualWidth, 1) * downsample, Math.Max(ActualHeight, 1) * downsample);

        public bool HandleViewKeyDown(Key key)
        {
            return AnnotationTools != null && AnnotationTools.OnKeyDown(key);
        }

        /// <summary>
        /// Draws the current frame to a PNG. Used by the Export Screenshot command.
        /// </summary>
        public bool TryCaptureScreenshot(string path)
        {
            if (Device == null || string.IsNullOrWhiteSpace(path))
                return false;

            int w = Math.Max(1, Device.PresentationParameters.BackBufferWidth);
            int h = Math.Max(1, Device.PresentationParameters.BackBufferHeight);
            using RenderTarget2D rt = new(Device, w, h, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            RenderTargetBinding[] previous = Device.GetRenderTargets();
            try
            {
                Device.SetRenderTarget(rt);
                Device.Clear(Microsoft.Xna.Framework.Color.Black);
                OnDrawing(this, new DrawingEventArgs(Device, Scene));
            }
            finally
            {
                Device.SetRenderTargets(previous);
            }
            using System.IO.FileStream stream = System.IO.File.Create(path);
            rt.SaveAsPng(stream, w, h);
            return true;
        }
        void StepSection(int delta)
        {
            Window window = Window.GetWindow(this);
            IInputElement target = window ?? (IInputElement)this;
            if (delta > 0)
                GlobalCommands.IncrementSectionNumber.Execute(null, target);
            else
                GlobalCommands.DecrementSectionNumber.Execute(null, target);
        }

        void OnHostSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_fittedViewToMapping)
                TryFitViewToMapping();
            SyncVisibleRegionToHostSize();
            Invalidate();
        }

        /// <summary>
        /// Keeps VisibleRegion's world size in sync with the HWND after a resize. ArrangeOverride no longer writes it.
        /// </summary>
        void SyncVisibleRegionToHostSize()
        {
            if (Grid?.VisibleRegion == null)
                return;

            double ds = Grid.VisibleRegion.Downsample;
            VisibleRegionInfo next = new(Grid.VisibleRegion.Center, HostWorldArea(ds), ds);
            if (!next.Equals(Grid.VisibleRegion))
                Grid.VisibleRegion = next;
        }

        /// <summary>
        /// World coordinates under a host-relative pointer using the grid cell under the pointer.
        /// Does not change the active annotation cell.
        /// </summary>
        public bool TryGetWorldPosition(Point hostPoint, out Geometry.Vector2 world, out int sectionNumber)
        {
            world = default;
            sectionNumber = SectionNumber;
            if (Grid == null || Scene == null)
                return false;

            var cells = Grid.GetVisibleCells();
            int cellIndex = -1;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].Bounds.Contains(hostPoint))
                {
                    cellIndex = i;
                    break;
                }
            }

            if (cellIndex < 0)
                cellIndex = _activeCellIndex;
            if (cellIndex < 0 || cellIndex >= cells.Count)
                return false;

            GridCellLayout cell = cells[cellIndex];
            JotunnSectionVM hitSection = ResolveSectionVm(cell.Item);
            if (hitSection != null)
                sectionNumber = hitSection.Number;

            Rect bounds = cell.Bounds;
            int x = Math.Max(0, (int)Math.Round(bounds.X));
            int y = Math.Max(0, (int)Math.Round(bounds.Y));
            int w = Math.Max(1, (int)Math.Round(bounds.Width));
            int h = Math.Max(1, (int)Math.Round(bounds.Height));
            Scene.Viewport = new Viewport(x, y, w, h);
            world = ScreenToWorld(new Geometry.Vector2(hostPoint.X - bounds.X, hostPoint.Y - bounds.Y));
            return true;
        }

        void SelectCellAt(Point p)
        {
            if (Grid == null)
                return;

            var cells = Grid.GetVisibleCells();
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].Bounds.Contains(p))
                {
                    _activeCellIndex = i;
                    JotunnSectionVM selected = ResolveSectionVm(cells[i].Item);
                    if (selected != null)
                        SectionNumber = selected.Number;
                    break;
                }
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton == MouseButton.XButton1 || e.ChangedButton == MouseButton.XButton2)
            {
                HandleViewMouseDown(e.GetPosition(this), e.ChangedButton, e.ClickCount);
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            HandleViewMouseDown(e.GetPosition(this), MouseButton.Left, e.ClickCount);
            e.Handled = true;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            HandleViewMouseDown(e.GetPosition(this), MouseButton.Right, e.ClickCount);
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            HandleViewMouseMove(e.GetPosition(this), e.LeftButton, e.RightButton);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            HandleViewMouseUp(e.GetPosition(this), MouseButton.Left);
            e.Handled = true;
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            HandleViewMouseUp(e.GetPosition(this), MouseButton.Right);
            e.Handled = true;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            HandleViewMouseWheel(e.Delta, e.GetPosition(this));
            e.Handled = true;
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

        /// <summary>
        /// Converts host coordinates to the active cell's local pixels so ScreenToWorld
        /// matches the viewport set for that cell.
        /// </summary>
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
