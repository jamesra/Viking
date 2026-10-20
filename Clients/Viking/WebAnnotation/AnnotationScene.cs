using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Viking;
using Viking.Rendering;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using Rectangle = Geometry.Rectangle;

namespace WebAnnotation
{
    /// <summary>
    /// Shared draw/hit-test for Jotunn and net10. Sets AnnotationOverlay.SectionViewLookup so Actions can resolve section views without WinForms.
    /// LoadVisibleAsync skips an in-flight load when bounds and LOD match. HitTest is the scene equivalent of overlay ObjectAtPosition.
    /// </summary>
    public sealed class AnnotationScene : IAnnotationScene
    {
        readonly Volume _volume;
        readonly VolumeTransformProvider _transforms;
        readonly Dictionary<int, SectionAnnotationsView> _sections = new();
        readonly Dictionary<int, (Rectangle Bounds, double PixelSize)> _loadTarget = new();
        readonly Dictionary<int, Task> _loadBySection = new();
        readonly Dictionary<int, CancellationTokenSource> _loadCtsBySection = new();
        readonly object _loadLock = new();
        readonly Dictionary<int, long> _lastUsedTicks = new();
        readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

        /// <summary>A section used this recently is on screen (drawn every frame) and is never evicted.</summary>
        static readonly TimeSpan InUseWindow = TimeSpan.FromSeconds(5);
        SpriteBatch _spriteBatch;
        SpriteFont _font;
        BasicEffect _basicEffect;
        BlendState _defaultBlendState;

        public AnnotationScene(Volume volume)
        {
            _volume = volume;
            _transforms = new VolumeTransformProvider(volume);
            // Jotunn has no WinForms overlay; shared actions and views find this scene's section views through the lookup.
            AnnotationOverlay.SectionViewLookup = GetOrCreate;
            if (Store.IsInitialized)
            {
                Store.Locations.CollectionChanged += OnStoreCollectionChanged;
                Store.Locations.CollectionChanged += OnLocationsChanged;
                Store.LocationLinks.CollectionChanged += OnStoreCollectionChanged;
            }
        }

        void OnStoreCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) =>
            TileLoadEnvironment.RequestRender?.Invoke();

        static void OnLocationsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            try
            {
                ReviewFeed.ReviewChangeFeedIngress.PushLocationStoreChange(e);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Review feed ingest failed: {ex.Message}");
            }
        }

        public VolumeTransformProvider Transforms => _transforms;

        public bool Visible { get; set; } = true;

        /// <summary>Feedback the active annotation tool draws over the annotations; null when the tool draws none.</summary>
        public WebAnnotation.Tools.IAnnotationToolPreview ToolPreview { get; set; }

        /// <summary>The section <see cref="ToolPreview"/> is drawn over.</summary>
        public int ToolPreviewSection { get; set; }

        /// <summary>
        /// Draws more over a section's annotations (auto-polygonize proposals), given the section number; the host decides
        /// which sections it applies to.
        /// </summary>
        public Action<GraphicsDevice, VikingXNA.Scene, int> OverlayDraw { get; set; }

        internal SectionAnnotationsView GetOrCreate(int sectionNumber)
        {
            lock (_loadLock)
            {
                _lastUsedTicks[sectionNumber] = _clock.ElapsedTicks;
                if (_sections.TryGetValue(sectionNumber, out SectionAnnotationsView existing))
                    return existing;

                if (!_volume.Sections.TryGetValue(sectionNumber, out Section section))
                    return null;

                SectionAnnotationsView view = new(section, _transforms, _volume);
                _sections[sectionNumber] = view;
                EvictLeastRecentlyUsed(Global.AnnotationSettings.NumSectionsInMemory);
                return view;
            }
        }

        /// <summary>
        /// Viking's section view cache limit (<c>NumSectionsInMemory</c>): drops the least recently used section views
        /// beyond <paramref name="maxSections"/>, never one used within <see cref="InUseWindow"/>. Caller holds the lock.
        /// </summary>
        void EvictLeastRecentlyUsed(int maxSections)
        {
            long inUseSince = _clock.ElapsedTicks - (long)(InUseWindow.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);
            while (_sections.Count > Math.Max(1, maxSections))
            {
                int oldest = -1;
                long oldestTicks = long.MaxValue;
                foreach (int candidate in _sections.Keys)
                {
                    long used = _lastUsedTicks.TryGetValue(candidate, out long t) ? t : 0;
                    if (used < oldestTicks)
                    {
                        oldestTicks = used;
                        oldest = candidate;
                    }
                }

                if (oldest < 0 || oldestTicks >= inUseSince)
                    return;

                _sections.Remove(oldest);
                _lastUsedTicks.Remove(oldest);
                _loadTarget.Remove(oldest);
                _loadBySection.Remove(oldest);
                if (_loadCtsBySection.TryGetValue(oldest, out CancellationTokenSource cts))
                {
                    cts.Cancel();
                    _loadCtsBySection.Remove(oldest);
                }

                // An evicted section is no longer polled, so its region cells' stamps would let a revisit skip the server
                // and miss edits made meanwhile; dropping them makes the revisit ask again.
                if (Store.IsInitialized)
                    Store.LocationsByRegion.InvalidateSection(oldest);
            }
        }

        /// <summary>Sections whose annotation views are held; for diagnostics and tests.</summary>
        internal int[] HeldSections
        {
            get
            {
                lock (_loadLock)
                    return _sections.Keys.ToArray();
            }
        }

        /// <summary>
        /// Viking's F5: drops every section's annotation views and the region cells behind them, so the next draw rebuilds
        /// the views and asks the server again.
        /// </summary>
        public void Reload()
        {
            int[] sections;
            lock (_loadLock)
            {
                foreach (CancellationTokenSource cts in _loadCtsBySection.Values)
                    cts.Cancel();

                sections = _sections.Keys.ToArray();
                _sections.Clear();
                _lastUsedTicks.Clear();
                _loadTarget.Clear();
                _loadBySection.Clear();
                _loadCtsBySection.Clear();
            }

            if (Store.IsInitialized)
            {
                foreach (int section in sections)
                    Store.LocationsByRegion.InvalidateSection(section);
            }

            TileLoadEnvironment.RequestRender?.Invoke();
        }

        /// <summary>How often <see cref="PollSections"/> should run: Viking's idle poll interval.</summary>
        public static TimeSpan PollInterval => TimeSpan.FromMilliseconds(SectionLocationPollPolicy.IntervalMilliseconds);

        /// <summary>
        /// Viking's idle poll for other users' edits: for each of <paramref name="sectionNumbers"/> already shown, asks the
        /// server for locations changed since that section's watermark and adds them to its view. Sections still loading,
        /// or not yet seeded by a region load, are skipped as in Viking.
        /// </summary>
        public void PollSections(IEnumerable<int> sectionNumbers)
        {
            if (!Store.IsInitialized || sectionNumbers == null)
                return;

            foreach (int sectionNumber in sectionNumbers.Distinct())
            {
                SectionAnnotationsView view;
                bool loadInFlight;
                lock (_loadLock)
                {
                    if (!_sections.TryGetValue(sectionNumber, out view))
                        continue;
                    loadInFlight = _loadBySection.TryGetValue(sectionNumber, out Task load) && load is { IsCompleted: false };
                }

                if (!SectionLocationPollPolicy.ShouldPollSection(
                        Store.Locations.GetLastQueryTimeForSection(sectionNumber),
                        Store.Locations.HasOutstandingSectionQuery(sectionNumber),
                        loadInFlight))
                    continue;

                _ = Store.Locations.RefreshSectionLocationsAsync(sectionNumber, locations =>
                {
                    if (locations is null || locations.Count == 0)
                        return;
                    // Objects already in the store raise no CollectionChanged; AddLocations is how they reach the view.
                    view.AddLocations(locations);
                    // As in Viking, only rows newer than the Review seed floor enter the change feed.
                    ReviewFeed.ReviewChangeFeedIngress.PushUpsertsFromPoll(locations);
                    TileLoadEnvironment.RequestRender?.Invoke();
                });
            }
        }

        public void LoadVisible(VikingXNA.Scene scene, int sectionNumber)
        {
            _ = LoadVisibleAsync(scene, sectionNumber);
        }

        async Task LoadVisibleAsync(VikingXNA.Scene scene, int sectionNumber)
        {
            if (scene is null)
                return;

            SectionAnnotationsView view = GetOrCreate(sectionNumber);
            if (view is null)
                return;

            Rectangle bounds = scene.VisibleWorldBounds;
            double pixel = scene.ScreenPixelSizeInVolume;
            Task load;
            CancellationTokenSource cts;
            lock (_loadLock)
            {
                if (_loadTarget.TryGetValue(sectionNumber, out (Rectangle Bounds, double PixelSize) previous)
                    && previous.Bounds == bounds
                    && RegionQueryBounds.SameLod(previous.PixelSize, pixel)
                    && _loadBySection.TryGetValue(sectionNumber, out Task inflight)
                    && inflight is { IsCompleted: false }
                    && _loadCtsBySection.TryGetValue(sectionNumber, out CancellationTokenSource live)
                    && live is { IsCancellationRequested: false })
                {
                    return;
                }

                if (_loadCtsBySection.TryGetValue(sectionNumber, out CancellationTokenSource old))
                {
                    old.Cancel();
                    if (!_loadBySection.TryGetValue(sectionNumber, out Task previousLoad) || previousLoad.IsCompleted)
                        old.Dispose();
                }

                cts = new CancellationTokenSource();
                _loadCtsBySection[sectionNumber] = cts;
                _loadTarget[sectionNumber] = (bounds, pixel);
                load = view.LoadAnnotationsInRegion(scene, cts.Token);
                _loadBySection[sectionNumber] = load;
            }

            try
            {
                await load.ConfigureAwait(false);
                lock (_loadLock)
                {
                    if (!_loadCtsBySection.TryGetValue(sectionNumber, out CancellationTokenSource current) || !ReferenceEquals(current, cts))
                        return;
                }

                TileLoadEnvironment.RequestRender?.Invoke();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                lock (_loadLock)
                {
                    if (!_loadCtsBySection.TryGetValue(sectionNumber, out CancellationTokenSource current) || !ReferenceEquals(current, cts))
                        cts.Dispose();
                }
            }
        }

        /// <summary>Draws the tool's feedback over the section the tool works on; other sections in a grid show none.</summary>
        void DrawToolPreview(int sectionNumber, GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect)
        {
            WebAnnotation.Tools.IAnnotationToolPreview preview = ToolPreview;
            if (preview is null || lineManager is null || sectionNumber != ToolPreviewSection)
                return;

            try
            {
                preview.DrawPreview(device, scene, lineManager, overlayEffect);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Annotation tool preview failed: {ex.Message}");
            }
        }

        public object HitTest(int sectionNumber, Geometry.Vector2 worldPosition, out double distance)
        {
            distance = double.MaxValue;
            if (!Visible)
                return null;
            SectionAnnotationsView locView = GetOrCreate(sectionNumber);
            if (locView is null)
                return null;

            List<HitTestResult> listObjects = locView.GetAnnotations(worldPosition);
            HitTestResult bestHit = listObjects.NearestObjectOnCurrentSectionThenAdjacent(sectionNumber);
            if (bestHit is null)
                return null;

            distance = bestHit.Distance;
            ICanvasGeometryView bestObj = bestHit.obj as ICanvasGeometryView;
            if (bestObj is ICanvasViewContainer container)
            {
                bestObj = container.GetAnnotationAtPosition(worldPosition) as ICanvasGeometryView;
                if (bestObj != null)
                    distance = bestObj.DistanceFromCenterNormalized(worldPosition);
            }

            return bestObj;
        }

        public void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, int sectionNumber, Texture backgroundLuma, Texture backgroundColors, ref int nextStencilValue, bool requestLoad = true)
        {
            if (!Visible)
                return;
            SectionAnnotationsView current = GetOrCreate(sectionNumber);
            if (current is null || scene is null)
                return;

            if (requestLoad)
                LoadVisible(scene, sectionNumber);

            ContentManager content = VikingXNAGraphics.Global.Content;
            OverlayShaderEffect overlayEffect = DeviceEffectsStore<OverlayShaderEffect>.GetOrCreateForDevice(graphicsDevice, content);
            RoundLineCode.LumaOverlayRoundLineManager lineManager = DeviceEffectsStore<RoundLineCode.LumaOverlayRoundLineManager>.GetOrCreateForDevice(graphicsDevice, content);
            RoundCurve.CurveManagerHSV curveManager = DeviceEffectsStore<RoundCurve.CurveManagerHSV>.GetOrCreateForDevice(graphicsDevice, content);

            if (backgroundLuma != null && overlayEffect != null)
            {
                overlayEffect.LumaTexture = backgroundLuma;
                overlayEffect.RenderTargetSize = graphicsDevice.Viewport;
            }

            if (backgroundLuma != null && lineManager != null)
            {
                lineManager.LumaTexture = backgroundLuma;
                lineManager.RenderTargetSize = graphicsDevice.Viewport;
            }

            if (_basicEffect is null || _basicEffect.IsDisposed)
                _basicEffect = new BasicEffect(graphicsDevice);
            _basicEffect.SetScene(scene);

            if (_spriteBatch is null || _spriteBatch.GraphicsDevice.IsDisposed)
                _spriteBatch = new SpriteBatch(graphicsDevice);
            _font ??= content?.Load<SpriteFont>("Arial");

            BlendState originalBlendState = graphicsDevice.BlendState;
            RasterizerState originalRaster = graphicsDevice.RasterizerState;
            int startingStencil = nextStencilValue;
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, startingStencil);

            Rectangle bounds = scene.VisibleWorldBounds;
            ICollection<LocationCanvasView> locations = current.GetLocations(bounds);
            List<LocationCanvasView> visible = locations.Where(l => l != null && l.Parent != null && l.Parent.Type != null && l.IsVisible(scene)).ToList();
            ICollection<LocationCanvasView> adjacent = current.AdjacentLocationsNotOverlappedInRegion(bounds);
            List<LocationCanvasView> visibleAdjacent = adjacent.Where(l => l != null && l.IsVisible(scene)).ToList();

            LocationObjRenderer.DrawBackgrounds(visible, graphicsDevice, _basicEffect, overlayEffect, lineManager, curveManager, scene, sectionNumber);
            nextStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice) + 1;
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);
            LocationObjRenderer.DrawBackgrounds(visibleAdjacent, graphicsDevice, _basicEffect, overlayEffect, lineManager, curveManager, scene, sectionNumber);

            nextStencilValue = DeviceStateManager.GetDepthStencilValue(graphicsDevice) + 1;
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, startingStencil);
            LocationLinkView.Draw(graphicsDevice, scene, lineManager, _basicEffect, overlayEffect, current.NonOverlappedLocationLinksInRegion(bounds));

            graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, 1, 0);

            if (_defaultBlendState is null || _defaultBlendState.IsDisposed)
            {
                _defaultBlendState = new BlendState
                {
                    AlphaBlendFunction = BlendFunction.Add,
                    AlphaSourceBlend = Blend.SourceAlpha,
                    AlphaDestinationBlend = Blend.DestinationAlpha,
                    ColorSourceBlend = Blend.SourceColor,
                    ColorDestinationBlend = Blend.DestinationColor,
                    ColorBlendFunction = BlendFunction.Add
                };
            }

            graphicsDevice.BlendState = _defaultBlendState;
            DeviceStateManager.SetRasterizerStateForShapes(graphicsDevice);
            DeviceStateManager.SetRenderStateForShapes(graphicsDevice);
            DeviceStateManager.SetDepthStencilValue(graphicsDevice, nextStencilValue);

            List<StructureLinkViewModelBase> structureLinks = current.VisibleStructureLinks(scene);
            StructureLinkCirclesView.Draw(graphicsDevice, scene, lineManager, structureLinks.OfType<StructureLinkCirclesView>().ToArray());
            StructureLinkCurvesView.Draw(graphicsDevice, scene, lineManager, structureLinks.OfType<StructureLinkCurvesView>().ToArray());

            if (_font != null)
            {
                _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                foreach (LocationCanvasView loc in visible.Concat(visibleAdjacent))
                {
                    if (loc is ILabelView label && label.IsLabelVisible(scene))
                        label.DrawLabel(_spriteBatch, _font, scene);
                }
                _spriteBatch.End();
            }

            DrawToolPreview(sectionNumber, graphicsDevice, scene, lineManager, overlayEffect);
            try
            {
                OverlayDraw?.Invoke(graphicsDevice, scene, sectionNumber);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Annotation overlay draw failed: {ex.Message}");
            }

            if (originalRaster != null && !originalRaster.IsDisposed)
                graphicsDevice.RasterizerState = originalRaster;
            if (originalBlendState != null)
                graphicsDevice.BlendState = originalBlendState;
            nextStencilValue++;
        }
    }
}
