using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{

    public static class DeviceStateManager
    {
        static BlendState OriginalBlendState;
        static RasterizerState OriginalRasterState;

        static BlendState ShapeRendererBlendState = null;
        static RasterizerState ShapeRendererRasterizerState = null;

        static BlendState BackgroundRendererBlendState = null;
        static RasterizerState BackgroundRendererRasterizerState = null;

        static DepthStencilState depthstencilState;

        public static void SaveDeviceState(GraphicsDevice graphicsDevice)
        {
            OriginalBlendState = graphicsDevice.BlendState;
            OriginalRasterState = graphicsDevice.RasterizerState;
        }

        public static void RestoreDeviceState(GraphicsDevice graphicsDevice)
        {
            if (OriginalBlendState != null)
                graphicsDevice.BlendState = OriginalBlendState;

            if (OriginalRasterState != null)
                graphicsDevice.RasterizerState = OriginalRasterState;
        }

        public static void SetRenderStateForShapes(GraphicsDevice graphicsDevice, ColorWriteChannels colorWriteChannels = ColorWriteChannels.All)
        {
            if(ShapeRendererBlendState != null)
            {
                ShapeRendererBlendState.Dispose();
                ShapeRendererBlendState = null;
            }
            
            if (ShapeRendererBlendState == null || ShapeRendererBlendState.IsDisposed)
            {
                ShapeRendererBlendState = new BlendState();

                ShapeRendererBlendState.AlphaSourceBlend = Blend.SourceAlpha;
                ShapeRendererBlendState.AlphaDestinationBlend = Blend.InverseSourceAlpha;
                ShapeRendererBlendState.ColorSourceBlend = Blend.SourceAlpha;
                ShapeRendererBlendState.ColorDestinationBlend = Blend.InverseSourceAlpha;

                ShapeRendererBlendState.ColorWriteChannels = colorWriteChannels;
            }

            graphicsDevice.BlendState = ShapeRendererBlendState;
            
        }

        public static void SetRasterizerStateForShapes(GraphicsDevice graphicsDevice)
        {
            if(ShapeRendererRasterizerState != null)
            {
                ShapeRendererRasterizerState.Dispose();
                ShapeRendererRasterizerState = null;
            }

            if (ShapeRendererRasterizerState == null || ShapeRendererRasterizerState.IsDisposed)
            {
                ShapeRendererRasterizerState = new RasterizerState();
                ShapeRendererRasterizerState.FillMode = FillMode.Solid;
                ShapeRendererRasterizerState.CullMode = CullMode.None;
            }

            graphicsDevice.RasterizerState = ShapeRendererRasterizerState;
        }

        public static void SetRenderStateForBackgrounds(GraphicsDevice graphicsDevice)
        {
            if (BackgroundRendererBlendState == null || BackgroundRendererBlendState.IsDisposed)
            {
                BackgroundRendererBlendState = new BlendState();

                BackgroundRendererBlendState.AlphaSourceBlend = Blend.One;
                BackgroundRendererBlendState.AlphaDestinationBlend = Blend.Zero;
                BackgroundRendererBlendState.AlphaBlendFunction = BlendFunction.Add;

                BackgroundRendererBlendState.ColorSourceBlend = Blend.One;
                BackgroundRendererBlendState.ColorDestinationBlend = Blend.Zero;
                BackgroundRendererBlendState.ColorBlendFunction = BlendFunction.Add;
            }

            graphicsDevice.BlendState = BackgroundRendererBlendState;
        }

        public static void SetRasterizerStateForBackgrounds(GraphicsDevice graphicsDevice)
        {
            if (BackgroundRendererRasterizerState == null || BackgroundRendererRasterizerState.IsDisposed)
            {
                BackgroundRendererRasterizerState = new RasterizerState();
                BackgroundRendererRasterizerState.FillMode = FillMode.Solid;
                BackgroundRendererRasterizerState.CullMode = CullMode.None;
            }

            graphicsDevice.RasterizerState = BackgroundRendererRasterizerState;
        }
         

        public static void SetDepthBuffer(GraphicsDevice device, CompareFunction depthFunction = CompareFunction.LessEqual)
        {
            if (depthstencilState != null)
            {
                depthstencilState.Dispose();
                depthstencilState = null;
            }

            if (depthstencilState == null || depthstencilState.IsDisposed)
            {
                depthstencilState = new DepthStencilState();
                CopyStencilSettings(depthstencilState, device.DepthStencilState);
                depthstencilState.DepthBufferEnable = true;
                depthstencilState.DepthBufferWriteEnable = true;
                depthstencilState.DepthBufferFunction = depthFunction;
                
                device.DepthStencilState = depthstencilState;
            }
        }
        

        public static void SetDepthStencilValue(GraphicsDevice device, int StencilValue, CompareFunction stencilFunction = CompareFunction.GreaterEqual)
        {
            if (depthstencilState != null)
            {
                depthstencilState.Dispose();
                depthstencilState = null;
            }

            if (depthstencilState == null || depthstencilState.IsDisposed)
            {
                depthstencilState = new DepthStencilState();

                CopyDepthSettings(depthstencilState, device.DepthStencilState);
                                  
                depthstencilState.StencilEnable = true;
                depthstencilState.StencilFunction = stencilFunction;
                depthstencilState.ReferenceStencil = StencilValue;
                depthstencilState.StencilPass = StencilOperation.Replace;
            }

            device.DepthStencilState = depthstencilState;
        }

        public static int GetDepthStencilValue(GraphicsDevice device)
        {
            return device.DepthStencilState.ReferenceStencil;
        }

        private static void CopyDepthSettings(DepthStencilState DestState, DepthStencilState SrcState)
        {
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
