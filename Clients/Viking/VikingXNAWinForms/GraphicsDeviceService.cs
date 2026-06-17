#region File Description
//-----------------------------------------------------------------------------
// GraphicsDeviceService.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using Microsoft.Xna.Framework.Graphics;
using RoundCurve;
using RoundLineCode;
using System;
using System.Threading;
using VikingXNAGraphics;
using ServiceContainer = System.ComponentModel.Design.ServiceContainer;

#endregion

#nullable enable

// The IGraphicsDeviceService interface requires a DeviceCreated event, but we
// always just create the device inside our constructor, so we have no place to
// raise that event. The C# compiler warns us that the event is never used, but
// we don't care so we just disable this warning.
#pragma warning disable 67

namespace VikingXNAWinForms
{
    /// <summary>
    /// Helper class responsible for creating and managing the GraphicsDevice.
    /// All GraphicsDeviceControl instances share the same GraphicsDeviceService,
    /// so even though there can be many controls, there will only ever be a single
    /// underlying GraphicsDevice. This implements the standard IGraphicsDeviceService
    /// interface, which provides notification events for when the device is reset
    /// or disposed.
    /// </summary>
    public class GraphicsDeviceService : IGraphicsDeviceService
    {
        #region Fields


        // Singleton device service instance.
        static GraphicsDeviceService? singletonInstance;


        // Keep track of how many controls are sharing the singletonInstance.
        static int referenceCount;


        #endregion




        /// <summary>
        /// Constructor is private, because this is a singleton class:
        /// client controls should use the public AddRef method instead.
        /// </summary>
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

            /*PORT XNA 4
            parameters.EnableAutoDepthStencil = true;
            parameters.AutoDepthStencilFormat = DepthFormat.Depth24;
            */
            GpuSynchronizationManager.Initialize();

            if (GraphicsAdapter.DefaultAdapter.IsProfileSupported(GraphicsProfile.HiDef))
                graphicsDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, parameters);
            else if (GraphicsAdapter.DefaultAdapter.IsProfileSupported(GraphicsProfile.Reach))
                graphicsDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.Reach, parameters);
            else
            {
                System.Windows.Forms.MessageBox.Show("Default graphics adapter does not support XNA");
                throw new System.InvalidOperationException("Default graphics adapter does not support XNA");
            }

            LoadGlobalPrimitivesTextures();
        }

        /// <summary>
        /// Loads or reloads all GlobalPrimitives textures. 
        /// Called on device creation and after device reset.
        /// </summary>
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


        /// <summary>
        /// Gets a reference to the singleton instance.
        /// </summary>
        public static GraphicsDeviceService? AddRef(IntPtr windowHandle,
                                                   int width, int height)
        {
            // Increment the "how many controls sharing the device" reference count.
            if (Interlocked.Increment(ref referenceCount) == 1)
            {
                // If this is the first control to start using the
                // device, we must create the singleton instance.
                singletonInstance = new GraphicsDeviceService(windowHandle,
                                                              width, height);
            }

            return singletonInstance;
        }


        /// <summary>
        /// Releases a reference to the singleton instance.
        /// </summary>
        public void Release(bool disposing)
        {
            // Decrement the "how many controls sharing the device" reference count.
            if (Interlocked.Decrement(ref referenceCount) == 0)
            {
                // If this is the last control to finish using the
                // device, we should dispose the singleton instance.
                if (disposing)
                {
                    DeviceDisposing?.Invoke(this, EventArgs.Empty);

                    graphicsDevice?.Dispose();
                }

                graphicsDevice = null;
            }
        }


        /// <summary>
        /// Resets the graphics device to whichever is bigger out of the specified
        /// resolution or its current size. This behavior means the device will
        /// demand-grow to the largest of all its GraphicsDeviceControl clients.
        /// </summary>
        public void ResetDevice(int width, int height)
        {
            if (graphicsDevice is null)
                throw new InvalidOperationException("Graphics device is not initialized");

            System.Diagnostics.Debug.Assert(!graphicsDevice.IsDisposed, "Resetting disposed graphics device, why?");
            if (graphicsDevice.IsDisposed)
            {
                System.Diagnostics.Trace.WriteLine("Resetting disposed graphics device, why?");
                return;
            }

            if (graphicsDevice.GraphicsDeviceStatus == GraphicsDeviceStatus.Lost)
            {
                System.Diagnostics.Trace.WriteLine("Skipping device reset: device is lost");
                return;
            }

            // Clear cached device-dependent resources before reset
            ClearDeviceDependentCaches();

            DeviceResetting?.Invoke(this, EventArgs.Empty);

            parameters.BackBufferWidth = Math.Max(width, 1);
            parameters.BackBufferHeight = Math.Max(1, height);
            parameters.DepthStencilFormat = DepthFormat.Depth24Stencil8;
            parameters.IsFullScreen = false;
            parameters.RenderTargetUsage = RenderTargetUsage.DiscardContents;

            try
            {
                graphicsDevice.Reset(parameters);
            }
            catch (Exception ex) when (IsDeviceRemovedError(ex))
            {
                System.Diagnostics.Trace.WriteLine($"GPU device removed during reset: {ex.Message}");
                return;
            }

            // Reload global textures after reset
            LoadGlobalPrimitivesTextures();

            DeviceReset?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Returns true if the exception represents a GPU device-removed or device-reset error
        /// (DXGI_ERROR_DEVICE_REMOVED 0x887A0005 or DXGI_ERROR_DEVICE_RESET 0x887A0007).
        /// </summary>
        private static bool IsDeviceRemovedError(Exception ex)
        {
            const int DXGI_ERROR_DEVICE_REMOVED = unchecked((int)0x887A0005);
            const int DXGI_ERROR_DEVICE_RESET = unchecked((int)0x887A0007);
            return ex.HResult == DXGI_ERROR_DEVICE_REMOVED || ex.HResult == DXGI_ERROR_DEVICE_RESET;
        }

        /// <summary>
        /// Clears all cached device-dependent resources.
        /// Called before device reset to ensure stale resources are not used.
        /// </summary>
        private void ClearDeviceDependentCaches()
        {
            // Clear effect stores for all effect types used in the application
            DeviceEffectsStore<RoundLineCode.RoundLineManager>.ClearAll();
            DeviceEffectsStore<RoundLineCode.LumaOverlayRoundLineManager>.ClearAll();
            DeviceEffectsStore<RoundCurve.CurveManager>.ClearAll();
            DeviceEffectsStore<RoundCurve.CurveManagerHSV>.ClearAll();
            DeviceEffectsStore<PolygonOverlayEffect>.ClearAll();
            DeviceEffectsStore<OverlayShaderEffect>.ClearAll();

            // Clear font store
            DeviceFontStore.ClearAll();
        }



        /// <summary>
        /// Gets the current graphics device.
        /// </summary>
        public GraphicsDevice? GraphicsDevice => graphicsDevice;

        GraphicsDevice? graphicsDevice;

        //Gets the content
        public Microsoft.Xna.Framework.Content.ContentManager? _Content;
        public Microsoft.Xna.Framework.Content.ContentManager Content
        {
            get
            {
                if (_Content is null)
                {
                    ServiceContainer tempContainer = new();
                    tempContainer.AddService(typeof(IGraphicsDeviceService), this);
                    //tempContainer.AddService<IGraphicsDeviceService>(this);
                    _Content = new Microsoft.Xna.Framework.Content.ContentManager(tempContainer, "Content");
                    VikingXNAGraphics.Global.Content = _Content;
                }

                return _Content;
            }
        }



        // Store the current device settings.
        readonly PresentationParameters parameters;


        // IGraphicsDeviceService events.
        public event EventHandler<System.EventArgs>? DeviceCreated;
        public event EventHandler<System.EventArgs>? DeviceDisposing;
        public event EventHandler<System.EventArgs>? DeviceReset;
        public event EventHandler<System.EventArgs>? DeviceResetting;
    }
}
