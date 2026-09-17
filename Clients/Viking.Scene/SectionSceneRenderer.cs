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
        readonly ConcurrentDictionary<int, Task> _mappingInitTasks = new();
        int _mappingReadyNotified;
        int _loggedNullMapping;
        int _loggedEffectNull;
        int _emptyVisiblePasses;
        ChannelOverlayEffect? ChannelOverlayEffect;
        RenderTarget2D? _sectionTarget;
        DepthStencilState? _overlayBackgroundDepthState;
        readonly Dictionary<int, DepthStencilState> _rtDownsampleDepthStateCache = new();
        bool _drawTilesToSectionTarget;
        int _activeDrawSection = int.MinValue;
        static readonly short[] OverlayIndices = [0, 1, 2, 2, 1, 3];

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
                _mappingReadyNotified = 0;
                _loggedNullMapping = 0;
                _loggedEffectNull = 0;
                _emptyVisiblePasses = 0;
                _activeDrawSection = int.MinValue;
                _sectionTarget?.Dispose();
                _sectionTarget = null;
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
            try
            {
                Effect overlay = content.Load<Effect>("ChannelOverlayShader");
                ChannelOverlayEffect = new ChannelOverlayEffect(overlay)
                {
                    WorldViewProjMatrix = scene.WorldViewProj
                };
            }
            catch (Exception ex)
            {
                ChannelOverlayEffect = null;
                Trace.WriteLine($"ChannelOverlayShader not loaded; tiles will draw to the HWND backbuffer: {ex.GetBaseException().Message}");
            }
            TileGridMappingBase.TileMeshCreated = () => TileLoadEnvironment.RequestRender?.Invoke();
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
            if (_volume is null || _mappingManager is null || section is null)
            {
                return -1;
            }

            PrepareDeviceForTiles(graphicsDevice);
            if (TileLayoutEffect != null)
            {
                TileLayoutEffect.RenderToGreyscale();
                if (!ColorizeTiles)
                    TileLayoutEffect.TileColor = Microsoft.Xna.Framework.Color.White;
            }

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
                TileLoadEnvironment.FollowUpPresents = 1;
                TileLoadEnvironment.RequestRender?.Invoke();
                return -1;
            }

            NotifyMappingReady(mapping);

            if (TileLayoutEffect is null)
            {
                if (Interlocked.Exchange(ref _loggedEffectNull, 1) == 0)
                    Trace.WriteLine("DrawTiles: TileLayoutEffect is null (TileLayout.xnb not loaded)");
                return -1;
            }

            if (_activeDrawSection != section.Number)
            {
                _activeDrawSection = section.Number;
                _emptyVisiblePasses = 0;
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
            int totalVisible = 0;
            int totalQueued = 0;
            int totalNotFound = 0;
            bool complete = true;

            for (int iLevel = 0; iLevel < downsamplesToRender.Length; iLevel++)
            {
                int level = mapping.AvailableLevels[downsamplesToRender[iLevel]];
                graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, 1f, 0);
                DeviceStateManager.SetDepthStencilValue(graphicsDevice, iLevel);
                graphicsDevice.DepthStencilState = CreateDepthStateForDownsampleLevel(iLevel);

                SortedDictionary<TileUniqueKey, TileViewModel> tileList = visibleTiles.GetTilesForLevel(level);
                totalVisible += tileList.Count;
                List<TileView> tileViewsToDraw = [];
                int queuedLoad = 0;
                int hasTex = 0;
                int notFound = 0;

                foreach (TileViewModel t in tileList.Values)
                {
                    TileView tileView = FetchOrConstructTile(t, section, mapping.Name);
                    if (tileView is null)
                        continue;

                    if (tileView.HasTexture == false && tileView.Downsample > scene.Camera.Downsample * 8 && iLevel < downsamplesToRender.Length - 1)
                        continue;
                    if (tileView.ServerTextureNotFound && iLevel < downsamplesToRender.Length - 1)
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
                        if (tileView.HasTexture)
                            hasTex++;
                        if (tileView.ServerTextureNotFound)
                            notFound++;
                        tileViewsToDraw.Add(tileView);
                    }
                    else
                    {
                        complete = false;
                    }
                }

                if (queuedLoad > 0)
                    complete = false;
                totalQueued += queuedLoad;
                totalNotFound += notFound;

                TileLayoutEffect.WorldViewProjMatrix = scene.WorldViewProj;
                foreach (TileView tileView in tileViewsToDraw)
                    tileView.Draw(graphicsDevice, TileLayoutEffect, AsynchTextureLoad, ColorizeTiles, ignoreEffectDepth: !_drawTilesToSectionTarget);
                texturedDrawn += hasTex;
            }

            int uncovered = Math.Max(0, totalVisible - texturedDrawn - totalNotFound);
            RequestFollowUpPresentIfNeeded(totalQueued, texturedDrawn, complete, uncovered, mapping.HasPendingTileConstruction);
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
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            graphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Microsoft.Xna.Framework.Color.Black, 1f, 0);
        }

        /// <summary>
        /// Viking DrawSection: tiles into a Depth24Stencil8 RT, then ChannelOverlayEffect
        /// composites that RT onto the HWND backbuffer as a world-space quad. WM_PAINT/Present
        /// copies the backbuffer to the control. If ChannelOverlayShader is missing, tiles
        /// draw directly to the backbuffer (Taurine path).
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
            bool blitFromSectionTarget = ChannelOverlayEffect != null && scene != null;
            _drawTilesToSectionTarget = blitFromSectionTarget;
            if (blitFromSectionTarget)
            {
                EnsureSectionTarget(graphicsDevice, destViewport);
                graphicsDevice.SetRenderTarget(_sectionTarget);
            }
            else
                graphicsDevice.SetRenderTarget(null);

            graphicsDevice.Viewport = destViewport;
            if (scene != null)
                scene.Viewport = destViewport;
            if (blitFromSectionTarget)
                graphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);

            DrawTiles(graphicsDevice, scene, section, channel, textureLoadToken, loadFullResolution, queueTextureLoads);

            graphicsDevice.SetRenderTarget(null);
            graphicsDevice.Viewport = destViewport;
            if (scene != null)
                scene.Viewport = destViewport;
            _drawTilesToSectionTarget = false;

            if (blitFromSectionTarget)
            {
                graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Microsoft.Xna.Framework.Color.Black, 1f, 0);
                CompositeSectionOntoBackbuffer(graphicsDevice, scene);
            }

            PrepareDeviceForOverlay(graphicsDevice);

            if (Annotations != null)
            {
                try
                {
                    int nextStencil = 2;
                    Texture luma = blitFromSectionTarget ? _sectionTarget : null;
                    Annotations.Draw(graphicsDevice, scene, section.Number, luma, null, ref nextStencil, loadAnnotations);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Annotation draw failed: {ex}");
                }
            }
        }

        void EnsureSectionTarget(GraphicsDevice device, Viewport vp)
        {
            int w = Math.Max(1, vp.Width);
            int h = Math.Max(1, vp.Height);
            if (_sectionTarget != null && !_sectionTarget.IsDisposed && _sectionTarget.Width == w && _sectionTarget.Height == h)
                return;
            _sectionTarget?.Dispose();
            _sectionTarget = new RenderTarget2D(device, w, h, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);
        }

        void CompositeSectionOntoBackbuffer(GraphicsDevice device, VikingXNA.Scene scene)
        {
            ChannelOverlayEffect.WorldViewProjMatrix = scene.WorldViewProj;
            ChannelOverlayEffect.SetEffectTextures(_sectionTarget, null);
            if (_overlayBackgroundDepthState is null || _overlayBackgroundDepthState.IsDisposed)
            {
                _overlayBackgroundDepthState = new DepthStencilState
                {
                    DepthBufferEnable = false,
                    DepthBufferWriteEnable = true,
                    DepthBufferFunction = CompareFunction.LessEqual,
                    StencilEnable = true,
                    StencilFunction = CompareFunction.Greater,
                    ReferenceStencil = 1
                };
            }

            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = _overlayBackgroundDepthState;
            DeviceStateManager.SetDepthStencilValue(device, 1);
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.PointClamp;

            Rectangle bounds = scene.VisibleWorldBounds;
            double halfWidth = bounds.Width / 2;
            double halfHeight = bounds.Height / 2;
            Geometry.Vector2 botLeft = new(bounds.Center.X - halfWidth, bounds.Center.Y + halfHeight);
            Geometry.Vector2 topRight = new(bounds.Center.X + halfWidth, bounds.Center.Y - halfHeight);
            VertexPositionNormalTexture[] mesh =
            [
                new(new Vector3((float)botLeft.X, (float)botLeft.Y, 0), Vector3.UnitZ, new Vector2(0, 0)),
                new(new Vector3((float)topRight.X, (float)botLeft.Y, 0), Vector3.UnitZ, new Vector2(1, 0)),
                new(new Vector3((float)botLeft.X, (float)topRight.Y, 0), Vector3.UnitZ, new Vector2(0, 1)),
                new(new Vector3((float)topRight.X, (float)topRight.Y, 0), Vector3.UnitZ, new Vector2(1, 1))
            ];
            foreach (EffectPass pass in ChannelOverlayEffect.effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, mesh, 0, 4, OverlayIndices, 0, 2);
            }
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

        /// <summary>
        /// Per-level stencil so coarser tiles do not overwrite finer ones. Depth is off:
        /// tiles rasterized into a DepthFormat.None RT (GetData luma 156) but were invisible
        /// on the Depth24Stencil8 backbuffer with LessEqual.
        /// </summary>
        DepthStencilState CreateDepthStateForDownsampleLevel(int stencilValue)
        {
            Dictionary<int, DepthStencilState> cache = _drawTilesToSectionTarget
                ? _rtDownsampleDepthStateCache
                : _downsampleDepthStateCache;
            if (cache.TryGetValue(stencilValue, out var cached) && cached != null && !cached.IsDisposed)
                return cached;

            cached?.Dispose();
            var state = new DepthStencilState
            {
                DepthBufferEnable = _drawTilesToSectionTarget,
                DepthBufferWriteEnable = _drawTilesToSectionTarget,
                DepthBufferFunction = CompareFunction.LessEqual,
                StencilEnable = true,
                StencilFunction = CompareFunction.GreaterEqual,
                ReferenceStencil = stencilValue,
                StencilPass = StencilOperation.Replace
            };
            cache[stencilValue] = state;
            return state;
        }

        /// <summary>
        /// The on-demand present loop idles when HasTexturePipelineWork is false. Task.Run
        /// loads are not in that flag until EnqueueRequest, and VisibleTiles can return 0
        /// on the first pass, so we must request another Present until tiles actually draw.
        /// </summary>
        void RequestFollowUpPresentIfNeeded(int totalQueued, int texturedDrawn, bool complete, int uncovered, bool pendingTileConstruction)
        {
            bool needsFollowUp;
            if (totalQueued > 0 || !complete || uncovered > 0 || pendingTileConstruction)
            {
                _emptyVisiblePasses = 0;
                needsFollowUp = true;
            }
            else if (texturedDrawn == 0)
            {
                _emptyVisiblePasses++;
                needsFollowUp = _emptyVisiblePasses <= 600;
            }
            else
            {
                _emptyVisiblePasses = 0;
                needsFollowUp = false;
            }

            if (!needsFollowUp)
            {
                TileLoadEnvironment.FollowUpPresents = 0;
                return;
            }

            TileLoadEnvironment.FollowUpPresents = 1;
            TileLoadEnvironment.RequestRender?.Invoke();
        }
    }
}
