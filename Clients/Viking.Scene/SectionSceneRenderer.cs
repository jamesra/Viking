using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Viking.ViewModels;
using Viking.VolumeModel;
using VikingXNA;
using VikingXNAGraphics;
using Rectangle = Geometry.Rectangle;

namespace Viking.Rendering
{
    /// <summary>
    /// Toolkit-agnostic tile and overlay draw used by Viking's SectionViewerControl and Jotunn's SectionSceneHost.
    /// </summary>
    public sealed class SectionSceneRenderer
    {
        readonly Dictionary<int, DepthStencilState> _downsampleDepthStateCache = new();

        /// <summary>
        /// Shader for mosaic tiles. DrawTiles returns immediately while this is null.
        /// </summary>
        public TileLayoutEffect? TileLayoutEffect { get; set; }

        public BasicEffect? BasicEffect { get; set; }

        /// <summary>
        /// When true, texture decode/upload is queued; when false only the coarsest visible level is drawn.
        /// </summary>
        public bool AsynchTextureLoad { get; set; } = true;

        public bool ColorizeTiles { get; set; }

        /// <summary>
        /// Optional overlay. Drawn after tiles; null skips annotation draw.
        /// </summary>
        public IAnnotationScene? Annotations { get; set; }

        /// <summary>
        /// Fired once when the first mapping used for draw is Initialized.
        /// Jotunn uses this to fit VisibleRegion to ControlBounds (VikingMain does the same on startup).
        /// May arrive on a thread-pool thread after StartMappingInitIfNeeded.
        /// </summary>
        public event Action<MappingBase>? MappingReady;

        MappingManager? _mappingManager;
        Volume? _volume;
        RenderTarget2D? _tileTarget;
        SpriteBatch? _tileBlitBatch;
        readonly ConcurrentDictionary<int, Task> _mappingInitTasks = new();
        int _mappingReadyNotified;
        int _loggedNullMapping;
        int _loggedNoTextures;
        int _loggedEffectNull;
        RenderTarget2D? _cachedComposite;
        int _cachedSection = int.MinValue;
        Rectangle _cachedBounds;
        double _cachedDownsample;
        int _cachedVpW;
        int _cachedVpH;
        bool _lastDrawTilesComplete;

        /// <summary>
        /// Open volume. Replaces MappingManager; seeds VolumeTransformName from DefaultVolumeTransform when empty.
        /// </summary>
        public Volume? Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                _mappingManager = value == null ? null : new MappingManager(value);
                _mappingInitTasks.Clear();
                _cachedComposite?.Dispose();
                _cachedComposite = null;
                _cachedSection = int.MinValue;
                _mappingReadyNotified = 0;
                _loggedNullMapping = 0;
                _loggedNoTextures = 0;
                _loggedEffectNull = 0;
                if (value != null && string.IsNullOrEmpty(VolumeTransformName))
                    VolumeTransformName = value.DefaultVolumeTransform ?? string.Empty;
            }
        }

        /// <summary>
        /// Volume-space transform name passed to MappingManager. Empty uses Volume.DefaultVolumeTransform.
        /// </summary>
        public string VolumeTransformName { get; set; } = string.Empty;

        /// <summary>
        /// Section pyramid/stos transform. Empty uses Section.DefaultPyramidTransform.
        /// </summary>
        public string SectionTransformName { get; set; } = string.Empty;

        /// <summary>
        /// Loads TileLayout from the host Content folder. Call once the device exists;
        /// DrawTiles returns immediately while TileLayoutEffect is null.
        /// </summary>
        public void InitializeEffects(GraphicsDevice device, Microsoft.Xna.Framework.Content.ContentManager content, VikingXNA.Scene scene)
        {
            BasicEffect = new BasicEffect(device)
            {
                AmbientLightColor = new Vector3(1f, 1f, 1f)
            };

            Effect effectTileLayout = content.Load<Effect>("TileLayout");
            TileLayoutEffect = new TileLayoutEffect(effectTileLayout)
            {
                WorldViewProjMatrix = scene.WorldViewProj
            };
        }

        /// <summary>
        /// Draws visible tiles for one section. Returns without drawing when the mapping
        /// is missing or still initializing; annotations are drawn by Draw after this returns.
        /// </summary>
        /// <returns>Textured tiles drawn this pass, or -1 when mapping/effect is not ready.</returns>
        public int DrawTiles(
            GraphicsDevice graphicsDevice,
            VikingXNA.Scene scene,
            Section section,
            string channel,
            CancellationToken textureLoadToken,
            bool loadFullResolution = true,
            bool queueTextureLoads = true)
        {
            _lastDrawTilesComplete = false;
            if (_volume is null || _mappingManager is null || section is null)
            {
                return -1;
            }

            PrepareDeviceForTiles(graphicsDevice);

            string volumeTransform = string.IsNullOrEmpty(VolumeTransformName) ? _volume.DefaultVolumeTransform : VolumeTransformName;
            string sectionTransform = string.IsNullOrEmpty(SectionTransformName) ? section.DefaultPyramidTransform : SectionTransformName;
            string channelName = string.IsNullOrEmpty(channel) ? section.DefaultChannel : channel;
            MappingBase mapping = _mappingManager.GetMapping(volumeTransform, section.Number, channelName, sectionTransform);
            if (mapping is null)
            {
                if (Interlocked.Exchange(ref _loggedNullMapping, 1) == 0)
                    Trace.WriteLine($"GetMapping returned null for section {section.Number} volume='{volumeTransform}' channel='{channelName}' sectionTransform='{sectionTransform}'");
                return -1;
            }

            if (!mapping.Initialized)
            {
                StartMappingInitIfNeeded(section.Number, mapping, textureLoadToken);
                return -1;
            }

            NotifyMappingReady(mapping);

            if (TileLayoutEffect is null)
            {
                if (Interlocked.Exchange(ref _loggedEffectNull, 1) == 0)
                    Trace.WriteLine("DrawTiles: TileLayoutEffect is null (TileLayout.xnb not loaded)");
                return -1;
            }

            int[] downsamplesToRender = CalculateDownsamplesToRender(mapping, scene.Camera.Downsample);
            var visibleTiles = mapping.VisibleTiles(scene.VisibleWorldBounds, scene.Camera.Downsample);
            if (downsamplesToRender.Length == 0)
            {
                int[] available = mapping.AvailableLevels;
                var fallback = new List<int>(available.Length);
                for (int i = 0; i < available.Length; i++)
                {
                    if (visibleTiles.GetTilesForLevel(available[i]).Count > 0)
                        fallback.Add(i);
                }

                if (fallback.Count > 0)
                    downsamplesToRender = fallback.ToArray();
            }

            if (!loadFullResolution && downsamplesToRender.Length > 0)
                downsamplesToRender = [CoarsestDownsampleIndex(mapping, downsamplesToRender)];
            else if (!AsynchTextureLoad && downsamplesToRender.Length > 0)
                downsamplesToRender = [downsamplesToRender.Last()];

            int texturedDrawn = 0;
            bool complete = true;

            if (downsamplesToRender.Length == 0 && Interlocked.Exchange(ref _loggedNoTextures, 1) == 0)
                Trace.WriteLine($"DrawTiles: mapping ready for section {section.Number} but 0 tiles in view bounds={scene.VisibleWorldBounds} ds={scene.Camera.Downsample}");

            for (int iLevel = 0; iLevel < downsamplesToRender.Length; iLevel++)
            {
                int level = mapping.AvailableLevels[downsamplesToRender[iLevel]];
                graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, 1f, 0);
                DeviceStateManager.SetDepthStencilValue(graphicsDevice, iLevel);
                graphicsDevice.DepthStencilState = CreateDepthStateForDownsampleLevel(iLevel);

                SortedDictionary<TileUniqueKey, TileViewModel> tileList = visibleTiles.GetTilesForLevel(level);
                List<TileView> tileViewsToDraw = [];
                int queuedLoad = 0;

                foreach (TileViewModel t in tileList.Values)
                {
                    TileView tileView = FetchOrConstructTile(t, section, mapping.Name);
                    if (tileView is null)
                        continue;

                    if (tileView.HasTexture == false && tileView.Downsample > scene.Camera.Downsample * 8 && iLevel < downsamplesToRender.Length - 1)
                        continue;

                    if (queueTextureLoads && tileView.TextureNeedsLoading && !tileView.TextureIsLoading)
                    {
                        queuedLoad++;
                        var tile = tileView;
                        tile.MarkLoadQueued();
                        _ = Task.Run(async () => await tile.GetOrLoadTextureAsync(graphicsDevice, textureLoadToken).ConfigureAwait(false))
                            .ContinueWith(tt =>
                            {
                                if (tt.IsFaulted && tt.Exception != null)
                                    Trace.WriteLine($"DrawTiles texture load failed: {tt.Exception.GetBaseException().Message}");
                            }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                    else if (tileView.TextureReadComplete)
                    {
                        tileViewsToDraw.Add(tileView);
                    }
                    else
                    {
                        complete = false;
                    }
                }

                if (queuedLoad > 0)
                    complete = false;

                if (tileViewsToDraw.Count == 0 && tileList.Count > 0 && Interlocked.Exchange(ref _loggedNoTextures, 1) == 0)
                    Trace.WriteLine($"DrawTiles: {tileList.Count} visible tiles at level {level} but none have textures yet");

                TileLayoutEffect.WorldViewProjMatrix = scene.WorldViewProj;
                foreach (TileView tileView in tileViewsToDraw)
                    tileView.Draw(graphicsDevice, TileLayoutEffect, AsynchTextureLoad, ColorizeTiles);
                texturedDrawn += tileViewsToDraw.Count;
            }

            _lastDrawTilesComplete = complete && texturedDrawn > 0;
            return texturedDrawn;
        }

        /// <summary>
        /// Starts mosaic/stos init without waiting for a draw. Pass the section texture-load token so leaving the keep-set cancels init.
        /// </summary>
        public void EnsureMappingInitialized(Section section, CancellationToken token = default)
        {
            if (_volume is null || _mappingManager is null || section is null)
                return;

            string volumeTransform = string.IsNullOrEmpty(VolumeTransformName) ? _volume.DefaultVolumeTransform : VolumeTransformName;
            string sectionTransform = string.IsNullOrEmpty(SectionTransformName) ? section.DefaultPyramidTransform : SectionTransformName;
            string channelName = section.DefaultChannel ?? string.Empty;
            MappingBase mapping = _mappingManager.GetMapping(volumeTransform, section.Number, channelName, sectionTransform);
            if (mapping is null)
            {
                if (Interlocked.Exchange(ref _loggedNullMapping, 1) == 0)
                    Trace.WriteLine($"GetMapping returned null for section {section.Number} volume='{volumeTransform}' channel='{channelName}' sectionTransform='{sectionTransform}'");
                return;
            }

            if (!mapping.Initialized)
                StartMappingInitIfNeeded(section.Number, mapping, token);
            else
                NotifyMappingReady(mapping);
        }

        /// <summary>
        /// Annotation draw leaves BlendState/ColorWriteChannels and stencil dirty. Tiles drawn
        /// on a later frame would write no color without this reset.
        /// </summary>
        static void PrepareDeviceForTiles(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            graphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Microsoft.Xna.Framework.Color.Black, 1f, 0);
        }

        /// <summary>
        /// Draws tiles into an off-screen target (luma for overlay shaders), restores the
        /// caller's render targets, blits in viewport-local space, then draws annotations.
        /// Matches Viking SectionViewerControl: save GetRenderTargets, draw tiles, restore,
        /// then overlay with a live depth/stencil buffer. Called per visible grid cell.
        /// </summary>
        public void Draw(
            GraphicsDevice graphicsDevice,
            VikingXNA.Scene scene,
            Section section,
            string channel,
            CancellationToken textureLoadToken,
            bool loadFullResolution = true,
            bool queueTextureLoads = true,
            bool loadAnnotations = true)
        {
            Viewport destViewport = graphicsDevice.Viewport;
            RenderTargetBinding[] originalTargets = graphicsDevice.GetRenderTargets();
            bool usedCache = loadFullResolution
                && scene != null
                && TryBlitCachedComposite(graphicsDevice, destViewport, section.Number, scene);

            if (!usedCache)
            {
                RenderTarget2D tileTarget = EnsureTileTarget(graphicsDevice, destViewport);

                graphicsDevice.SetRenderTarget(tileTarget);
                graphicsDevice.Viewport = new Viewport(0, 0, tileTarget.Width, tileTarget.Height);
                graphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);
                DrawTiles(graphicsDevice, scene, section, channel, textureLoadToken, loadFullResolution, queueTextureLoads);

                graphicsDevice.SetRenderTargets(originalTargets);
                graphicsDevice.Viewport = destViewport;
                if (scene != null)
                    scene.Viewport = destViewport;

                BlitTileTarget(graphicsDevice, tileTarget, destViewport);
                if (loadFullResolution && _lastDrawTilesComplete && scene != null)
                    CaptureComposite(graphicsDevice, tileTarget, destViewport, section.Number, scene);
            }
            else
            {
                graphicsDevice.SetRenderTargets(originalTargets);
                graphicsDevice.Viewport = destViewport;
                if (scene != null)
                    scene.Viewport = destViewport;
            }

            PrepareDeviceForOverlay(graphicsDevice);

            if (Annotations != null)
            {
                try
                {
                    int nextStencil = 2;
                    Texture luma = _tileTarget;
                    Annotations.Draw(graphicsDevice, scene, section.Number, luma, null, ref nextStencil, loadAnnotations);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Annotation draw failed: {ex}");
                }
            }
        }

        bool TryBlitCachedComposite(GraphicsDevice graphicsDevice, Viewport destViewport, int sectionNumber, VikingXNA.Scene scene)
        {
            if (_cachedComposite is null || _cachedComposite.IsDisposed)
                return false;
            if (_cachedSection != sectionNumber
                || _cachedVpW != destViewport.Width
                || _cachedVpH != destViewport.Height
                || _cachedDownsample != scene.Camera.Downsample
                || !_cachedBounds.Equals(scene.VisibleWorldBounds))
            {
                return false;
            }

            BlitTileTarget(graphicsDevice, _cachedComposite, destViewport);
            return true;
        }

        void CaptureComposite(GraphicsDevice graphicsDevice, RenderTarget2D source, Viewport destViewport, int sectionNumber, VikingXNA.Scene scene)
        {
            int width = Math.Max(1, destViewport.Width);
            int height = Math.Max(1, destViewport.Height);
            if (_cachedComposite is null || _cachedComposite.IsDisposed
                || _cachedComposite.Width != width || _cachedComposite.Height != height)
            {
                _cachedComposite?.Dispose();
                _cachedComposite = new RenderTarget2D(
                    graphicsDevice,
                    width,
                    height,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.Depth24Stencil8,
                    0,
                    RenderTargetUsage.PreserveContents);
            }

            RenderTargetBinding[] original = graphicsDevice.GetRenderTargets();
            graphicsDevice.SetRenderTarget(_cachedComposite);
            graphicsDevice.Viewport = new Viewport(0, 0, width, height);
            graphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);
            BlitTileTarget(graphicsDevice, source, new Viewport(0, 0, width, height));
            graphicsDevice.SetRenderTargets(original);

            _cachedSection = sectionNumber;
            _cachedBounds = scene.VisibleWorldBounds;
            _cachedDownsample = scene.Camera.Downsample;
            _cachedVpW = width;
            _cachedVpH = height;
        }

        RenderTarget2D EnsureTileTarget(GraphicsDevice graphicsDevice, Viewport viewport)
        {
            int width = Math.Max(1, viewport.Width);
            int height = Math.Max(1, viewport.Height);
            if (_tileTarget != null && !_tileTarget.IsDisposed
                && _tileTarget.Width == width && _tileTarget.Height == height)
            {
                return _tileTarget;
            }

            _tileTarget?.Dispose();
            _tileTarget = new RenderTarget2D(
                graphicsDevice,
                width,
                height,
                false,
                SurfaceFormat.Color,
                DepthFormat.Depth24Stencil8,
                0,
                RenderTargetUsage.PreserveContents);
            return _tileTarget;
        }

        /// <summary>
        /// Copies the tile target into the current viewport. SpriteBatch projection is
        /// viewport-local, so the destination rect is (0,0,width,height) — using destViewport.X/Y
        /// double-offsets cells that are not at the origin.
        /// </summary>
        void BlitTileTarget(GraphicsDevice graphicsDevice, RenderTarget2D tileTarget, Viewport destViewport)
        {
            if (_tileBlitBatch is null || _tileBlitBatch.GraphicsDevice.IsDisposed)
                _tileBlitBatch = new SpriteBatch(graphicsDevice);

            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            _tileBlitBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone);
            _tileBlitBatch.Draw(
                tileTarget,
                new Microsoft.Xna.Framework.Rectangle(0, 0, destViewport.Width, destViewport.Height),
                Microsoft.Xna.Framework.Color.White);
            _tileBlitBatch.End();
        }

        /// <summary>
        /// SpriteBatch leaves DepthStencilState.None. Circle backgrounds need a writable
        /// depth/stencil buffer (Z-only pass then color where depth matches).
        /// </summary>
        static void PrepareDeviceForOverlay(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            graphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Microsoft.Xna.Framework.Color.Black, 1f, 0);
        }

        void NotifyMappingReady(MappingBase mapping)
        {
            if (Interlocked.Exchange(ref _mappingReadyNotified, 1) != 0)
                return;
            MappingReady?.Invoke(mapping);
        }

        /// <summary>
        /// Mosaic/stos mappings stay uninitialized until this runs. Viking's SectionViewerControl
        /// starts the same work; without it DrawTiles returns every frame and the view stays black.
        /// Tileset mappings report Initialized immediately and skip this.
        /// </summary>
        void StartMappingInitIfNeeded(int sectionNumber, MappingBase mapping, CancellationToken token)
        {
            if (_mappingInitTasks.TryGetValue(sectionNumber, out Task existing) && !existing.IsCompleted)
                return;

            Task started = Task.Run(async () =>
            {
                try
                {
                    await mapping.Initialize(token).ConfigureAwait(false);
                    if (mapping.Initialized)
                        NotifyMappingReady(mapping);
                    else
                        Trace.WriteLine($"Mapping init finished but Initialized=false for section {sectionNumber}");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Mapping init failed for section {sectionNumber}: {ex}");
                }
            }, token);
            _mappingInitTasks.AddOrUpdate(sectionNumber, started, (_, prior) =>
                prior.IsCompleted ? started : prior);
        }

        public static TileView FetchOrConstructTile(TileViewModel t, Section section, string mappingName)
        {
            string tileFileName = ResolveTileFullPath(t, section);
            return TileLoadEnvironment.TileViewModelCache.FetchOrConstructTile(
                t,
                tileFileName,
                TileCacheFullPath(section, t.TextureCacheFilePath),
                mappingName,
                0);
        }

        public static string TileCacheFullPath(Section section, string textureFileName) =>
            System.IO.Path.Combine(TileLoadEnvironment.TextureCachePath, section.SectionSubPath, textureFileName);

        public static string ResolveTileFullPath(TileViewModel t, Section section)
        {
            if (t.TextureFullPath.StartsWith(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                t.TextureFullPath.StartsWith(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return t.TextureFullPath;
            }

            return $"{section.Path}{System.IO.Path.DirectorySeparatorChar}{t.TextureFullPath}";
        }

        int[] CalculateDownsamplesToRender(MappingBase mapping, double downsample)
        {
            if (mapping is null)
                return [];

            int roundedDownsample = mapping.NearestAvailableLevel(downsample);
            if (roundedDownsample == int.MaxValue)
                return [];

            List<int> downsamplesToRender = new(mapping.AvailableLevels.Length);
            for (int i = 0; i < mapping.AvailableLevels.Length; i++)
            {
                if (roundedDownsample == mapping.AvailableLevels[i])
                    downsamplesToRender.Add(i);
                else if (roundedDownsample < mapping.AvailableLevels[i] && AsynchTextureLoad)
                    downsamplesToRender.Add(i);
            }

            downsamplesToRender.Reverse();
            return [.. downsamplesToRender];
        }

        /// <summary>
        /// Index into AvailableLevels for the coarsest (highest downsample) entry in <paramref name="levelIndexes"/>.
        /// Neighbor cells use this so they do not enqueue the full pyramid.
        /// </summary>
        static int CoarsestDownsampleIndex(MappingBase mapping, int[] levelIndexes)
        {
            int best = levelIndexes[0];
            int bestLevel = mapping.AvailableLevels[best];
            for (int i = 1; i < levelIndexes.Length; i++)
            {
                int idx = levelIndexes[i];
                int level = mapping.AvailableLevels[idx];
                if (level > bestLevel)
                {
                    best = idx;
                    bestLevel = level;
                }
            }

            return best;
        }

        DepthStencilState CreateDepthStateForDownsampleLevel(int stencilValue)
        {
            if (_downsampleDepthStateCache.TryGetValue(stencilValue, out var cached) && cached != null && !cached.IsDisposed)
                return cached;

            cached?.Dispose();
            var state = new DepthStencilState
            {
                DepthBufferEnable = true,
                DepthBufferWriteEnable = true,
                DepthBufferFunction = CompareFunction.LessEqual,
                StencilEnable = true,
                StencilFunction = CompareFunction.GreaterEqual,
                ReferenceStencil = stencilValue,
                StencilPass = StencilOperation.Replace
            };
            _downsampleDepthStateCache[stencilValue] = state;
            return state;
        }
    }
}
