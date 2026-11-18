using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VikingXNAGraphics
{

    public static class DeviceStateManager
    {
        static BlendState OriginalBlendState;
        static RasterizerState OriginalRasterState;
        static DepthStencilState OriginalDepthState;

        static RasterizerState ShapeRendererRasterizerState = null;

        static BlendState BackgroundRendererBlendState = null;
        static RasterizerState BackgroundRendererRasterizerState = null;

        /// <summary>
        /// Every depth-stencil and shape blend state handed out so far, one per device and full set of settings. The
        /// annotation passes switch stencil reference, compare function and colour writes back and forth many times a
        /// frame; each combination is created once and reused. They are never disposed here: a caller may have saved one
        /// as its "original" to restore later, and the set of combinations in use is small.
        /// </summary>
        private static readonly Dictionary<(GraphicsDevice Device, DepthStencilDescription Description), DepthStencilState> DepthStencilStates = [];
        private static readonly Dictionary<(GraphicsDevice Device, ColorWriteChannels Channels), BlendState> ShapeBlendStates = [];
        private static readonly object StateCacheLock = new();

        /// <summary>The settings of a fresh <see cref="DepthStencilState"/>, which the setters below start from.</summary>
        private static readonly DepthStencilDescription FreshDepthStencil = DepthStencilDescription.Of(new DepthStencilState());

        /// <summary>All settings of a <see cref="DepthStencilState"/>, as a dictionary key.</summary>
        private record struct DepthStencilDescription(
            bool DepthBufferEnable,
            bool DepthBufferWriteEnable,
            CompareFunction DepthBufferFunction,
            bool StencilEnable,
            CompareFunction StencilFunction,
            StencilOperation StencilPass,
            StencilOperation StencilFail,
            StencilOperation StencilDepthBufferFail,
            int StencilMask,
            int StencilWriteMask,
            bool TwoSidedStencilMode,
            int ReferenceStencil,
            CompareFunction CounterClockwiseStencilFunction,
            StencilOperation CounterClockwiseStencilPass,
            StencilOperation CounterClockwiseStencilFail,
            StencilOperation CounterClockwiseStencilDepthBufferFail)
        {
            public static DepthStencilDescription Of(DepthStencilState state) => new(
                state.DepthBufferEnable, state.DepthBufferWriteEnable, state.DepthBufferFunction,
                state.StencilEnable, state.StencilFunction, state.StencilPass, state.StencilFail, state.StencilDepthBufferFail,
                state.StencilMask, state.StencilWriteMask, state.TwoSidedStencilMode, state.ReferenceStencil,
                state.CounterClockwiseStencilFunction, state.CounterClockwiseStencilPass,
                state.CounterClockwiseStencilFail, state.CounterClockwiseStencilDepthBufferFail);

            public DepthStencilState Create() => new()
            {
                DepthBufferEnable = DepthBufferEnable,
                DepthBufferWriteEnable = DepthBufferWriteEnable,
                DepthBufferFunction = DepthBufferFunction,
                StencilEnable = StencilEnable,
                StencilFunction = StencilFunction,
                StencilPass = StencilPass,
                StencilFail = StencilFail,
                StencilDepthBufferFail = StencilDepthBufferFail,
                StencilMask = StencilMask,
                StencilWriteMask = StencilWriteMask,
                TwoSidedStencilMode = TwoSidedStencilMode,
                ReferenceStencil = ReferenceStencil,
                CounterClockwiseStencilFunction = CounterClockwiseStencilFunction,
                CounterClockwiseStencilPass = CounterClockwiseStencilPass,
                CounterClockwiseStencilFail = CounterClockwiseStencilFail,
                CounterClockwiseStencilDepthBufferFail = CounterClockwiseStencilDepthBufferFail,
            };
        }

        /// <summary>The shared state with <paramref name="description"/>'s settings on <paramref name="device"/>.</summary>
        private static DepthStencilState DepthStencilStateFor(GraphicsDevice device, DepthStencilDescription description)
        {
            lock (StateCacheLock)
            {
                if (DepthStencilStates.TryGetValue((device, description), out DepthStencilState state) && !state.IsDisposed)
                    return state;

                state = description.Create();
                DepthStencilStates[(device, description)] = state;
                return state;
            }
        }

        /// <summary>Drops the cached states after a device reset. Called from GraphicsDeviceService.ClearDeviceDependentCaches.</summary>
        public static void ClearDeviceDependentCaches()
        {
            lock (StateCacheLock)
            {
                DepthStencilStates.Clear();
                ShapeBlendStates.Clear();
            }
        }

        public static void SaveDeviceState(GraphicsDevice graphicsDevice)
        {
            OriginalBlendState = graphicsDevice.BlendState;
            OriginalRasterState = graphicsDevice.RasterizerState;
            OriginalDepthState = graphicsDevice.DepthStencilState;
        }

        public static void RestoreDeviceState(GraphicsDevice graphicsDevice)
        {
            if (OriginalBlendState != null && !OriginalBlendState.IsDisposed)
                graphicsDevice.BlendState = OriginalBlendState;

            if (OriginalRasterState != null && !OriginalRasterState.IsDisposed)
                graphicsDevice.RasterizerState = OriginalRasterState;

            if (OriginalDepthState != null && !OriginalDepthState.IsDisposed)
                graphicsDevice.DepthStencilState = OriginalDepthState;
        }

        public static void SetRenderStateForShapes(GraphicsDevice graphicsDevice, ColorWriteChannels colorWriteChannels = ColorWriteChannels.All)
        {
            // BlendState cannot be mutated after use. DrawBackgrounds does a Z-only pass (None) then a colour pass (All),
            // so there is one shared state per ColorWriteChannels value.
            BlendState state;
            lock (StateCacheLock)
            {
                if (!ShapeBlendStates.TryGetValue((graphicsDevice, colorWriteChannels), out state) || state.IsDisposed)
                {
                    state = new BlendState
                    {
                        AlphaSourceBlend = Blend.SourceAlpha,
                        AlphaDestinationBlend = Blend.InverseSourceAlpha,
                        ColorSourceBlend = Blend.SourceAlpha,
                        ColorDestinationBlend = Blend.InverseSourceAlpha,
                        Name = "BlendShapes",
                        ColorWriteChannels = colorWriteChannels
                    };
                    ShapeBlendStates[(graphicsDevice, colorWriteChannels)] = state;
                }
            }

            graphicsDevice.BlendState = state;
        }

        public static void SetRasterizerStateForShapes(GraphicsDevice graphicsDevice)
        {
            if (ShapeRendererRasterizerState is null || ShapeRendererRasterizerState.IsDisposed)
            {
                ShapeRendererRasterizerState?.Dispose();
                ShapeRendererRasterizerState = new RasterizerState
                {
                    FillMode = FillMode.Solid,
                    CullMode = CullMode.None
                };
            }

            graphicsDevice.RasterizerState = ShapeRendererRasterizerState;
        }

        public static void SetRenderStateForBackgrounds(GraphicsDevice graphicsDevice)
        {
            if (BackgroundRendererBlendState is null || BackgroundRendererBlendState.IsDisposed)
            {
                BackgroundRendererBlendState = new BlendState
                {
                    AlphaSourceBlend = Blend.One,
                    AlphaDestinationBlend = Blend.Zero,
                    AlphaBlendFunction = BlendFunction.Add,

                    ColorSourceBlend = Blend.One,
                    ColorDestinationBlend = Blend.Zero,
                    ColorBlendFunction = BlendFunction.Add
                };
            }

            graphicsDevice.BlendState = BackgroundRendererBlendState;
        }

        public static void SetRasterizerStateForBackgrounds(GraphicsDevice graphicsDevice)
        {
            if (BackgroundRendererRasterizerState is null || BackgroundRendererRasterizerState.IsDisposed)
            {
                BackgroundRendererRasterizerState = new RasterizerState
                {
                    FillMode = FillMode.Solid,
                    CullMode = CullMode.None
                };
            }

            graphicsDevice.RasterizerState = BackgroundRendererRasterizerState;
        }


        /// <summary>Depth testing with <paramref name="depthFunction"/> and depth writes on; the stencil settings in use are kept.</summary>
        public static void SetDepthBuffer(GraphicsDevice device, CompareFunction depthFunction = CompareFunction.LessEqual)
        {
            DepthStencilState current = device.DepthStencilState;
            DepthStencilDescription description = current is null
                ? FreshDepthStencil
                : FreshDepthStencil with
                {
                    StencilDepthBufferFail = current.StencilDepthBufferFail,
                    StencilEnable = current.StencilEnable,
                    StencilFail = current.StencilFail,
                    StencilFunction = current.StencilFunction,
                    StencilMask = current.StencilMask,
                    StencilPass = current.StencilPass,
                    StencilWriteMask = current.StencilWriteMask,
                    TwoSidedStencilMode = current.TwoSidedStencilMode,
                    ReferenceStencil = current.ReferenceStencil,
                };

            device.DepthStencilState = DepthStencilStateFor(device, description with
            {
                DepthBufferEnable = true,
                DepthBufferWriteEnable = true,
                DepthBufferFunction = depthFunction,
            });
        }

        /// <summary>Stencil test against <paramref name="StencilValue"/>, replacing on pass; the depth settings in use are kept.</summary>
        public static void SetDepthStencilValue(GraphicsDevice device, int StencilValue, CompareFunction stencilFunction = CompareFunction.GreaterEqual, bool stencilEnable = true)
        {
            DepthStencilState current = device.DepthStencilState;
            DepthStencilDescription description = current is null
                ? FreshDepthStencil with { DepthBufferEnable = true, DepthBufferWriteEnable = true, DepthBufferFunction = CompareFunction.LessEqual }
                : FreshDepthStencil with
                {
                    CounterClockwiseStencilDepthBufferFail = current.CounterClockwiseStencilDepthBufferFail,
                    CounterClockwiseStencilFail = current.CounterClockwiseStencilFail,
                    CounterClockwiseStencilFunction = current.CounterClockwiseStencilFunction,
                    CounterClockwiseStencilPass = current.CounterClockwiseStencilPass,
                    DepthBufferEnable = current.DepthBufferEnable,
                    DepthBufferFunction = current.DepthBufferFunction,
                    DepthBufferWriteEnable = current.DepthBufferWriteEnable,
                };

            device.DepthStencilState = DepthStencilStateFor(device, description with
            {
                StencilEnable = stencilEnable,
                StencilFunction = stencilFunction,
                ReferenceStencil = StencilValue,
                StencilPass = StencilOperation.Replace,
            });
        }

        public static int GetDepthStencilValue(GraphicsDevice device) => device.DepthStencilState.ReferenceStencil;
    }
}
