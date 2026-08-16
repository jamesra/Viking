using System;
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
    /// Toolkit-agnostic tile + overlay draw. Viking and Jotunn share this path.
    /// </summary>
    public sealed class SectionSceneRenderer
    {
        readonly Dictionary<int, DepthStencilState> _downsampleDepthStateCache = new();

        public TileLayoutEffect? TileLayoutEffect { get; set; }

        public BasicEffect? BasicEffect { get; set; }

        public bool AsynchTextureLoad { get; set; } = true;

        public bool ColorizeTiles { get; set; }

        public IAnnotationScene? Annotations { get; set; }

        MappingManager? _mappingManager;
        Volume? _volume;

        public Volume? Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                _mappingManager = value == null ? null : new MappingManager(value);
                if (value != null && string.IsNullOrEmpty(VolumeTransformName))
                    VolumeTransformName = value.DefaultVolumeTransform ?? string.Empty;
            }
        }

        public string VolumeTransformName { get; set; } = string.Empty;

        public string SectionTransformName { get; set; } = string.Empty;

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

        public void DrawTiles(
            GraphicsDevice graphicsDevice,
            VikingXNA.Scene scene,
            Section section,
            string channel,
            CancellationToken textureLoadToken)
        {
            if (_volume is null || _mappingManager is null || TileLayoutEffect is null || section is null)
                return;

            string volumeTransform = string.IsNullOrEmpty(VolumeTransformName) ? _volume.DefaultVolumeTransform : VolumeTransformName;
            string sectionTransform = string.IsNullOrEmpty(SectionTransformName) ? section.DefaultPyramidTransform : SectionTransformName;
            string channelName = string.IsNullOrEmpty(channel) ? section.DefaultChannel : channel;
            MappingBase mapping = _mappingManager.GetMapping(volumeTransform, section.Number, channelName, sectionTransform);
            if (mapping is null || mapping.Initialized == false)
                return;

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

            if (!AsynchTextureLoad && downsamplesToRender.Length > 0)
                downsamplesToRender = [downsamplesToRender.Last()];

            for (int iLevel = 0; iLevel < downsamplesToRender.Length; iLevel++)
            {
                int level = mapping.AvailableLevels[downsamplesToRender[iLevel]];
                graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, 1f, 0);
                DeviceStateManager.SetDepthStencilValue(graphicsDevice, iLevel);
                graphicsDevice.DepthStencilState = CreateDepthStateForDownsampleLevel(iLevel);

                SortedDictionary<TileUniqueKey, TileViewModel> tileList = visibleTiles.GetTilesForLevel(level);
                List<TileView> tileViewsToDraw = [];

                foreach (TileViewModel t in tileList.Values)
                {
                    TileView tileView = FetchOrConstructTile(t, section, mapping.Name);
                    if (tileView is null)
                        continue;

                    if (tileView.HasTexture == false && tileView.Downsample > scene.Camera.Downsample * 8 && iLevel < downsamplesToRender.Length - 1)
                        continue;

                    if (tileView.TextureNeedsLoading && !tileView.TextureIsLoading)
                    {
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
                }

                TileLayoutEffect.WorldViewProjMatrix = scene.WorldViewProj;
                foreach (TileView tileView in tileViewsToDraw)
                    tileView.Draw(graphicsDevice, TileLayoutEffect, AsynchTextureLoad, ColorizeTiles);
            }
        }

        public void Draw(
            GraphicsDevice graphicsDevice,
            VikingXNA.Scene scene,
            Section section,
            string channel,
            CancellationToken textureLoadToken)
        {
            DrawTiles(graphicsDevice, scene, section, channel, textureLoadToken);

            if (Annotations != null)
            {
                int nextStencil = 2;
                Annotations.Draw(graphicsDevice, scene, section.Number, null, null, ref nextStencil);
            }
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
