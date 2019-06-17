#region File Description
//-----------------------------------------------------------------------------
// GraphicsDeviceService.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using System;
using System.Threading;
using System.ComponentModel.Design;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;
#endregion

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
        static GraphicsDeviceService singletonInstance;


        // Keep track of how many controls are sharing the singletonInstance.
        static int referenceCount;


        #endregion




        /// <summary>
        /// Constructor is private, because this is a singleton class:
        /// client controls should use the public AddRef method instead.
        /// </summary>
        GraphicsDeviceService(IntPtr windowHandle, int width, int height)
        {
            parameters = new PresentationParameters();

            parameters.BackBufferWidth = Math.Max(width, 1);
            parameters.BackBufferHeight = Math.Max(height, 1);
            parameters.BackBufferFormat = SurfaceFormat.Color;
            parameters.DepthStencilFormat = DepthFormat.Depth24;

            parameters.DeviceWindowHandle = windowHandle;
            parameters.RenderTargetUsage = RenderTargetUsage.DiscardContents;
            parameters.IsFullScreen = false;
             
            /*PORT XNA 4
            parameters.EnableAutoDepthStencil = true;
            parameters.AutoDepthStencilFormat = DepthFormat.Depth24;
            */

            if(GraphicsAdapter.DefaultAdapter.IsProfileSupported(GraphicsProfile.HiDef))
                graphicsDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, parameters);
            else if (GraphicsAdapter.DefaultAdapter.IsProfileSupported(GraphicsProfile.Reach))
                graphicsDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.Reach, parameters);
           else
            {
                System.Windows.Forms.MessageBox.Show("Default graphics adapter does not support XNA");
                throw new System.InvalidOperationException("Default graphics adapter does not support XNA");
            }

            // AnnotationCache.parent = parent;
            GlobalPrimitives.CircleTexture = Content.LoadTextureWithAlpha("Circle", "CircleMask"); //parent.Content.Load<Texture2D>("Circle");
            GlobalPrimitives.MinusTexture = Content.LoadTextureWithAlpha("CircleMinus", "CircleMask"); //parent.Content.Load<Texture2D>("Circle");
            GlobalPrimitives.PlusTexture = Content.LoadTextureWithAlpha("CirclePlus", "CircleMask"); //parent.Content.Load<Texture2D>("Circle");
            GlobalPrimitives.UpArrowTexture = Content.LoadTextureWithAlpha("UpArrowV2", "UpArrowMask"); //parent.Content.Load<Texture2D>("Circle");
            GlobalPrimitives.DownArrowTexture = Content.LoadTextureWithAlpha("DownArrowV2", "UpArrowMask"); //parent.Content.Load<Texture2D>("Circle");

        }


        /// <summary>
        /// Gets a reference to the singleton instance.
        /// </summary>
        public static GraphicsDeviceService AddRef(IntPtr windowHandle,
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
                    if (DeviceDisposing != null)
                        DeviceDisposing(this, EventArgs.Empty);

                    graphicsDevice.Dispose();
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
            System.Diagnostics.Debug.Assert(!graphicsDevice.IsDisposed, "Resetting disposed graphics device, why?"); 
            if (graphicsDevice.IsDisposed)
            {
                System.Diagnostics.Trace.WriteLine("Resetting disposed graphics device, why?");
                return;
            }
            if (DeviceResetting != null)
                DeviceResetting(this, EventArgs.Empty);

            parameters.BackBufferWidth = Math.Max(width,1);
            parameters.BackBufferHeight = Math.Max(1, height);
            parameters.DepthStencilFormat = DepthFormat.Depth24Stencil8;
            parameters.IsFullScreen = false;
            parameters.RenderTargetUsage = RenderTargetUsage.DiscardContents;

            graphicsDevice.Reset(parameters);

            if (DeviceReset != null)
                DeviceReset(this, EventArgs.Empty);

            
        }



        /// <summary>
        /// Gets the current graphics device.
        /// </summary>
        public GraphicsDevice GraphicsDevice
        {
            get { return graphicsDevice; }
        }

        GraphicsDevice graphicsDevice;

        //Gets the content
        public Microsoft.Xna.Framework.Content.ContentManager _Content;
        public Microsoft.Xna.Framework.Content.ContentManager Content
        {
            get
            {
                if (_Content == null)
                {
                    ServiceContainer tempContainer = new ServiceContainer();
                    tempContainer.AddService(typeof(IGraphicsDeviceService), this);
                    //tempContainer.AddService<IGraphicsDeviceService>(this);
                    _Content = new Microsoft.Xna.Framework.Content.ContentManager(tempContainer, "Content");
                }

                return _Content;
            }
        }
        


        // Store the current device settings.
        PresentationParameters parameters;


        // IGraphicsDeviceService events.
        public event EventHandler<System.EventArgs> DeviceCreated;
        public event EventHandler<System.EventArgs> DeviceDisposing;
        public event EventHandler<System.EventArgs> DeviceReset;
        public event EventHandler<System.EventArgs> DeviceResetting;
    }
}
