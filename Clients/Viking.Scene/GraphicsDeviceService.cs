using Microsoft.Xna.Framework.Graphics;
using RoundCurve;
using RoundLineCode;
using System;
using System.Threading;
using VikingXNAGraphics;
using ServiceContainer = System.ComponentModel.Design.ServiceContainer;

#nullable enable

#pragma warning disable 67

namespace Viking.Rendering
{
    /// <summary>
    /// Shared GraphicsDevice singleton keyed by HWND. No WinForms types.
    /// </summary>
    public class GraphicsDeviceService : IGraphicsDeviceService
    {
        static GraphicsDeviceService? singletonInstance;
        static int referenceCount;

        GraphicsDeviceService(IntPtr windowHandle, int width, int height)
        {
            parameters = new PresentationParameters
            {
                BackBufferWidth = Math.Max(width, 1),
                BackBufferHeight = Math.Max(height, 1),
                BackBufferFormat = SurfaceFormat.Color,
                DepthStencilFormat = DepthFormat.Depth24Stencil8,
                DeviceWindowHandle = windowHandle,
                RenderTargetUsage = RenderTargetUsage.DiscardContents,
                IsFullScreen = false
            };

            GpuSynchronizationManager.Initialize();

            if (GraphicsAdapter.DefaultAdapter.IsProfileSupported(GraphicsProfile.HiDef))
                graphicsDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, parameters);
            else if (GraphicsAdapter.DefaultAdapter.IsProfileSupported(GraphicsProfile.Reach))
                graphicsDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.Reach, parameters);
            else
                throw new InvalidOperationException("Default graphics adapter does not support MonoGame");

            LoadGlobalPrimitivesTextures();
        }

        private void LoadGlobalPrimitivesTextures()
        {
            GlobalPrimitives.CircleTexture = Content.LoadTextureWithAlpha("Circle", "CircleMask");
            GlobalPrimitives.MinusTexture = Content.LoadTextureWithAlpha("CircleMinus", "CircleMask");
            GlobalPrimitives.PlusTexture = Content.LoadTextureWithAlpha("CirclePlus", "CircleMask");
            GlobalPrimitives.ChainTexture = Content.LoadTextureWithAlpha("CircleChain", "CircleChain");
            GlobalPrimitives.UpArrowTexture = Content.LoadTextureWithAlpha("UpArrowV2", "UpArrowMask");
            GlobalPrimitives.DownArrowTexture = Content.LoadTextureWithAlpha("DownArrowV2", "UpArrowMask");
            GlobalPrimitives.ConnectTexture = Content.LoadTextureWithAlpha("CircleConnect", "CircleConnect");
            GlobalPrimitives.CircleXTexture = Content.LoadTextureWithAlpha("CircleX", "CircleX");
        }

        public static GraphicsDeviceService? AddRef(IntPtr windowHandle, int width, int height)
        {
            if (Interlocked.Increment(ref referenceCount) == 1)
            {
                singletonInstance = new GraphicsDeviceService(windowHandle, width, height);
            }

            return singletonInstance;
        }

        public void Release(bool disposing)
        {
            if (Interlocked.Decrement(ref referenceCount) == 0)
            {
                if (disposing)
                {
                    DeviceDisposing?.Invoke(this, EventArgs.Empty);
                    graphicsDevice?.Dispose();
                }

                graphicsDevice = null;
            }
        }

        public void ResetDevice(int width, int height)
        {
            if (graphicsDevice is null)
                throw new InvalidOperationException("Graphics device is not initialized");

            if (graphicsDevice.IsDisposed)
                return;

            ClearDeviceDependentCaches();
            DeviceResetting?.Invoke(this, EventArgs.Empty);

            parameters.BackBufferWidth = Math.Max(width, 1);
            parameters.BackBufferHeight = Math.Max(1, height);
            parameters.DepthStencilFormat = DepthFormat.Depth24Stencil8;
            parameters.IsFullScreen = false;
            parameters.RenderTargetUsage = RenderTargetUsage.DiscardContents;

            graphicsDevice.Reset(parameters);
            LoadGlobalPrimitivesTextures();
            DeviceReset?.Invoke(this, EventArgs.Empty);
        }

        private void ClearDeviceDependentCaches()
        {
            DeviceEffectsStore<RoundLineManager>.ClearAll();
            DeviceEffectsStore<LumaOverlayRoundLineManager>.ClearAll();
            DeviceEffectsStore<CurveManager>.ClearAll();
            DeviceEffectsStore<CurveManagerHSV>.ClearAll();
            DeviceEffectsStore<PolygonOverlayEffect>.ClearAll();
            DeviceEffectsStore<OverlayShaderEffect>.ClearAll();
            DeviceEffectsStore<CircleInstancedEffect>.ClearAll();
            DeviceFontStore.ClearAll();
        }

        public GraphicsDevice? GraphicsDevice => graphicsDevice;

        GraphicsDevice? graphicsDevice;

        Microsoft.Xna.Framework.Content.ContentManager? _Content;

        public Microsoft.Xna.Framework.Content.ContentManager Content
        {
            get
            {
                if (_Content is null)
                {
                    ServiceContainer tempContainer = new();
                    tempContainer.AddService(typeof(IGraphicsDeviceService), this);
                    _Content = new Microsoft.Xna.Framework.Content.ContentManager(tempContainer, "Content");
                    VikingXNAGraphics.Global.Content = _Content;
                }

                return _Content;
            }
        }

        readonly PresentationParameters parameters;

        public event EventHandler<EventArgs>? DeviceCreated;
        public event EventHandler<EventArgs>? DeviceDisposing;
        public event EventHandler<EventArgs>? DeviceReset;
        public event EventHandler<EventArgs>? DeviceResetting;
    }
}
