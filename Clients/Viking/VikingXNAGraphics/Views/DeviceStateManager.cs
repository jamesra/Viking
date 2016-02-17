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

        public static void SetRenderStateForShapes(GraphicsDevice graphicsDevice)
        {
            if (ShapeRendererBlendState == null || ShapeRendererBlendState.IsDisposed)
            {
                ShapeRendererBlendState = new BlendState();

                ShapeRendererBlendState.AlphaSourceBlend = Blend.SourceAlpha;
                ShapeRendererBlendState.AlphaDestinationBlend = Blend.InverseSourceAlpha;
                ShapeRendererBlendState.ColorSourceBlend = Blend.SourceAlpha;
                ShapeRendererBlendState.ColorDestinationBlend = Blend.InverseSourceAlpha;
            }

            graphicsDevice.BlendState = ShapeRendererBlendState;
        }

        public static void SetRasterizerStateForShapes(GraphicsDevice graphicsDevice)
        {
            if (ShapeRendererRasterizerState == null || ShapeRendererRasterizerState.IsDisposed)
            {
                ShapeRendererRasterizerState = new RasterizerState();
                ShapeRendererRasterizerState.FillMode = FillMode.Solid;
                ShapeRendererRasterizerState.CullMode = CullMode.None;
            }

            graphicsDevice.RasterizerState = ShapeRendererRasterizerState;
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
                depthstencilState.DepthBufferEnable = true;
                depthstencilState.DepthBufferWriteEnable = true;
                depthstencilState.DepthBufferFunction = CompareFunction.LessEqual;

                depthstencilState.StencilEnable = true;
                depthstencilState.StencilFunction = stencilFunction;
                depthstencilState.ReferenceStencil = StencilValue;
                depthstencilState.StencilPass = StencilOperation.Replace;

                device.DepthStencilState = depthstencilState;
            }
        }

        public static int GetDepthStencilValue(GraphicsDevice device)
        {
            return device.DepthStencilState.ReferenceStencil;
        }
    }
}
