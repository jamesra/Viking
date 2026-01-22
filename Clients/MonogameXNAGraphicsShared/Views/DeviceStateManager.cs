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

        static BlendState ShapeRendererBlendState = null;
        static RasterizerState ShapeRendererRasterizerState = null;

        static BlendState BackgroundRendererBlendState = null;
        static RasterizerState BackgroundRendererRasterizerState = null;

        static DepthStencilState depthstencilState;

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
            ShapeRendererBlendState?.Dispose();
            ShapeRendererBlendState = null;

            if (ShapeRendererBlendState is null || ShapeRendererBlendState.IsDisposed)
            {
                ShapeRendererBlendState = new BlendState
                {
                    AlphaSourceBlend = Blend.SourceAlpha,
                    AlphaDestinationBlend = Blend.InverseSourceAlpha,
                    ColorSourceBlend = Blend.SourceAlpha,
                    ColorDestinationBlend = Blend.InverseSourceAlpha,
                    Name = "BlendShapes",
                    ColorWriteChannels = colorWriteChannels
                };
            }

            graphicsDevice.BlendState = ShapeRendererBlendState;

        }

        public static void SetRasterizerStateForShapes(GraphicsDevice graphicsDevice)
        {
            ShapeRendererRasterizerState?.Dispose();
            ShapeRendererRasterizerState = null;

            if (ShapeRendererRasterizerState is null || ShapeRendererRasterizerState.IsDisposed)
            {
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


        public static void SetDepthBuffer(GraphicsDevice device, CompareFunction depthFunction = CompareFunction.LessEqual)
        {
            depthstencilState?.Dispose();
            depthstencilState = null;

            if (depthstencilState is null || depthstencilState.IsDisposed)
            {
                depthstencilState = new DepthStencilState();
                CopyStencilSettings(depthstencilState, device.DepthStencilState);
                depthstencilState.DepthBufferEnable = true;
                depthstencilState.DepthBufferWriteEnable = true;
                depthstencilState.DepthBufferFunction = depthFunction;

                device.DepthStencilState = depthstencilState;
            }
        }


        public static void SetDepthStencilValue(GraphicsDevice device, int StencilValue, CompareFunction stencilFunction = CompareFunction.GreaterEqual, bool stencilEnable = true)
        {
            depthstencilState?.Dispose();
            depthstencilState = null;

            if (depthstencilState is null || depthstencilState.IsDisposed)
            {
                depthstencilState = new DepthStencilState();

                CopyDepthSettings(depthstencilState, device.DepthStencilState);

                depthstencilState.StencilEnable = stencilEnable;
                depthstencilState.StencilFunction = stencilFunction;
                depthstencilState.ReferenceStencil = StencilValue;
                depthstencilState.StencilPass = StencilOperation.Replace;
            }

            device.DepthStencilState = depthstencilState;
        }

        public static int GetDepthStencilValue(GraphicsDevice device) => device.DepthStencilState.ReferenceStencil;

        private static void CopyDepthSettings(DepthStencilState DestState, DepthStencilState SrcState)
        {
            if (SrcState is null)
            {
                depthstencilState.DepthBufferEnable = true;
                depthstencilState.DepthBufferWriteEnable = true;
                depthstencilState.DepthBufferFunction = CompareFunction.LessEqual;
                return;
            }

            DestState.CounterClockwiseStencilDepthBufferFail = SrcState.CounterClockwiseStencilDepthBufferFail;
            DestState.CounterClockwiseStencilFail = SrcState.CounterClockwiseStencilFail;
            DestState.CounterClockwiseStencilFunction = SrcState.CounterClockwiseStencilFunction;
            DestState.CounterClockwiseStencilPass = SrcState.CounterClockwiseStencilPass;
            DestState.DepthBufferEnable = SrcState.DepthBufferEnable;
            DestState.DepthBufferFunction = SrcState.DepthBufferFunction;
            DestState.DepthBufferWriteEnable = SrcState.DepthBufferWriteEnable;
        }

        private static void CopyStencilSettings(DepthStencilState DestState, DepthStencilState SrcState)
        {
            if (SrcState is null)
                return;

            DestState.StencilDepthBufferFail = SrcState.StencilDepthBufferFail;
            DestState.StencilEnable = SrcState.StencilEnable;
            DestState.StencilFail = SrcState.StencilFail;
            DestState.StencilFunction = SrcState.StencilFunction;
            DestState.StencilMask = SrcState.StencilMask;
            DestState.StencilPass = SrcState.StencilPass;
            DestState.StencilWriteMask = SrcState.StencilWriteMask;
            DestState.TwoSidedStencilMode = SrcState.TwoSidedStencilMode;
            DestState.ReferenceStencil = SrcState.ReferenceStencil;
        }
    }
}
