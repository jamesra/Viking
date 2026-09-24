#region File Description
//-----------------------------------------------------------------------------
// GraphicsDeviceControl.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using Microsoft.Win32;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VikingXNAGraphics;
using ServiceContainer = System.ComponentModel.Design.ServiceContainer;

#endregion

namespace VikingXNAWinForms
{
    // System.Drawing and the XNA Framework both define Color and Rectangle
    // types. To avoid conflicts, we specify which ones to use.
    using Color = System.Drawing.Color;
    using Rectangle = Microsoft.Xna.Framework.Rectangle;


    /// <summary>
    /// Custom control uses the XNA Framework GraphicsDevice to render onto
    /// a Windows Form. Derived classes can override the Initialize and Draw
    /// methods to add their own drawing code.
    /// </summary>
    public class GraphicsDeviceControl : Control
    {
        #region Fields


        // However many GraphicsDeviceControl instances you have, they all share
        // the same underlying GraphicsDevice, managed by this helper service.
        protected GraphicsDeviceService? graphicsDeviceService;

        /// <summary>
        /// The winform is running in an STA thread.  If we take a lock on an STA thread other messages from
        /// the message loop will be processed.  I don't want to handle reentrant paint messages, so this is 
        /// a workaround to drop paint messages if one is already being processed.
        /// </summary>
        protected uint PaintCallRefCount = 0;

        /// <summary>
        /// Gets an IServiceProvider containing our IGraphicsDeviceService.
        /// This can be used with components such as the ContentManager,
        /// which use this service to look up the GraphicsDevice.
        /// </summary>
        public ServiceContainer Services => services;

        readonly ServiceContainer services = new();

        /// <summary>
        /// Output the swap-chain window was last presented on.
        /// </summary>
        IntPtr _outputMonitor;

        /// <summary>
        /// Advanced color (HDR) state the swap chain was last created for.
        /// A move between two monitors with the same state does not reset the device.
        /// </summary>
        bool _knownAdvancedColor;

        bool _advancedColorEnabled;

        /// <summary>
        /// Top-level form whose <see cref="Form.LocationChanged"/> we subscribe to.
        /// Child controls do not receive move messages when the user drags the window.
        /// </summary>
        Form? _trackedForm;

        bool _listeningForDisplaySettings;

        private Microsoft.Xna.Framework.Content.ContentManager? _Content;
        public Microsoft.Xna.Framework.Content.ContentManager Content =>
            /*if (_Content is null)
                {
                    _Content = new Microsoft.Xna.Framework.Content.ContentManager(this.Services);
                    _Content.RootDirectory = "Content";
                }*/
            graphicsDeviceService?.Content ?? throw new InvalidOperationException("GraphicsDeviceService is not initialized");

        #endregion

        #region Properties


        /// <summary>
        /// Gets a GraphicsDevice that can be used to draw onto this control.
        /// </summary>
        public GraphicsDevice? Device
        {
            get
            {
                if (graphicsDeviceService is null)
                    return null;

                return graphicsDeviceService.GraphicsDevice;
            }
        }





        #endregion

        #region Initialization


        /// <summary>
        /// Initializes the control.
        /// </summary>
        protected override void OnCreateControl()
        {
            // Don't initialize the graphics device if we are running in the designer.  
            if (!DesignMode)
            {
                graphicsDeviceService = GraphicsDeviceService.AddRef(Handle,
                                                                     ClientSize.Width,
                                                                     ClientSize.Height);

                // Register the service, so components like ContentManager can find it.
                //services.AddService<IGraphicsDeviceService>(graphicsDeviceService);
                services.AddService(typeof(IGraphicsDeviceService), graphicsDeviceService);

                // Give derived classes a chance to initialize themselves.
                Initialize();

                // Ensure we load a default font
                VikingXNAGraphics.DeviceFontStore.GetOrCreateForDevice(this.Device, this.Content);

                // DWM keeps the HDR color space on the swap chain after the window leaves an HDR
                // monitor. Recreating the chain (GraphicsDevice.Reset) drops that stuck space.
                if (graphicsDeviceService?.DeviceWindowHandle == Handle)
                {
                    NoteOutput(MonitorFromWindow(Handle, MonitorDefaultToNearest));
                    AttachOutputTracking();
                    SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
                    _listeningForDisplaySettings = true;
                }
            }

            base.OnCreateControl();
        }

        /// <summary>
        /// Follows the control onto whichever top-level form currently hosts it.
        /// Called from create and from parent changes, because <see cref="FindForm"/> is often null at create.
        /// </summary>
        void AttachOutputTracking()
        {
            Form? form = FindForm();
            if (ReferenceEquals(form, _trackedForm))
                return;

            if (_trackedForm != null)
                _trackedForm.LocationChanged -= OnFormLocationChanged;

            _trackedForm = form;
            if (_trackedForm != null)
                _trackedForm.LocationChanged += OnFormLocationChanged;
        }

        /// <summary>
        /// Notes the monitor under the swap-chain window. Resets only when that monitor's HDR state differs.
        /// </summary>
        void OnFormLocationChanged(object? sender, EventArgs e)
        {
            if (!IsHandleCreated || graphicsDeviceService is null)
                return;
            if (graphicsDeviceService.DeviceWindowHandle != Handle)
                return;

            IntPtr monitor = MonitorFromWindow(Handle, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero || monitor == _outputMonitor)
                return;

            NoteOutput(monitor);
        }

        /// <summary>
        /// Re-reads HDR after a display-settings change, including a toggle on the current monitor.
        /// Raised on a background thread.
        /// </summary>
        void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    IntPtr monitor = MonitorFromWindow(Handle, MonitorDefaultToNearest);
                    if (monitor == IntPtr.Zero)
                        monitor = _outputMonitor;
                    NoteOutput(monitor);
                }));
            }
            catch (InvalidOperationException)
            {
                // Handle torn down during shutdown.
            }
        }

        /// <summary>
        /// Stores <paramref name="monitor"/> and recreates the swap chain only when its advanced-color
        /// (HDR) bit differs from the bit the chain was last created for. The first successful read
        /// is the baseline and does not reset.
        /// </summary>
        void NoteOutput(IntPtr monitor)
        {
            if (monitor == IntPtr.Zero)
                return;

            _outputMonitor = monitor;
            if (!MonitorAdvancedColor.TryGetEnabled(monitor, out bool enabled))
                return;

            if (!_knownAdvancedColor)
            {
                _knownAdvancedColor = true;
                _advancedColorEnabled = enabled;
                return;
            }

            if (enabled == _advancedColorEnabled)
                return;

            _advancedColorEnabled = enabled;
            ResetSwapChainForOutputChange();
        }

        /// <summary>
        /// Recreates the swap chain at the current client size so DWM rebinds the window to the
        /// output it is actually on. Called from the UI thread after a monitor or display change.
        /// </summary>
        void ResetSwapChainForOutputChange()
        {
            if (graphicsDeviceService is null || Device is null || Device.IsDisposed)
                return;
            if (graphicsDeviceService.DeviceWindowHandle != Handle)
                return;
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return;
            if (Device.GraphicsDeviceStatus == GraphicsDeviceStatus.Lost)
                return;

            try
            {
                graphicsDeviceService.ResetDevice(ClientSize.Width, ClientSize.Height);
                Invalidate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Swap chain reset after display change failed: {ex.Message}");
            }
        }


        /// <summary>
        /// Disposes the control.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (_trackedForm != null)
            {
                _trackedForm.LocationChanged -= OnFormLocationChanged;
                _trackedForm = null;
            }

            if (_listeningForDisplaySettings)
            {
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                _listeningForDisplaySettings = false;
            }

            graphicsDeviceService?.Release(disposing);
            graphicsDeviceService = null;
            _Content?.Dispose();
            _Content = null;

            base.Dispose(disposing);
        }

        /// <summary>
        /// Rebinds monitor tracking when the control moves to a different form.
        /// </summary>
        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (!DesignMode && graphicsDeviceService != null && graphicsDeviceService.DeviceWindowHandle == Handle)
                AttachOutputTracking();
        }


        #endregion

        #region Paint


        /// <summary>
        /// Redraws the control in response to a WinForms paint message.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            string? beginDrawError = BeginDraw();

            if (PaintCallRefCount > 0 && string.IsNullOrEmpty(beginDrawError))
                beginDrawError = "Viking is thinking, should be back in a few seconds.";

            if (string.IsNullOrEmpty(beginDrawError))
            {
#if !DEBUG
                try
                {
#endif
                    PaintCallRefCount++;
                    // Draw the control using the GraphicsDevice.
                    Draw();
                    EndDraw();
#if !DEBUG
                }
                catch (Exception except)
                {
                    throw;
                }
                finally
                {
#endif
                    PaintCallRefCount--;
#if !DEBUG
                }
#endif

            }
            else
            {
                // If BeginDraw failed, show an error message using System.Drawing.
                PaintUsingSystemDrawing(e.Graphics, beginDrawError ?? "Unknown error");
            }
        }


        /// <summary>
        /// Attempts to begin drawing the control. Returns an error message string
        /// if this was not possible, which can happen if the graphics device is
        /// lost, or if we are running inside the Form designer.
        /// </summary>
        string? BeginDraw()
        {
            // If we have no graphics device, we must be running in the designer.
            if (graphicsDeviceService is null)
            {
                return Text + "\n\n" + GetType();
            }

            // Make sure the graphics device is big enough, and is not lost.
            string? deviceResetError = HandleDeviceReset();

            if (!string.IsNullOrEmpty(deviceResetError))
            {
                return deviceResetError;
            }

            // Many GraphicsDeviceControl instances can be sharing the same
            // GraphicsDevice. The device backbuffer will be resized to fit the
            // largest of these controls. But what if we are currently drawing
            // a smaller control? To avoid unwanted stretching, we set the
            // viewport to only use the top left portion of the full backbuffer.
            if (Device is null)
                return "Graphics device is not available";

            Viewport viewport = new()
            {
                X = 0,
                Y = 0,

                Width = ClientSize.Width,
                Height = ClientSize.Height,

                MinDepth = 0,
                MaxDepth = 1
            };

            Device.Viewport = viewport;

            return null;
        }


        /// <summary>
        /// Ends drawing the control. This is called after derived classes
        /// have finished their Draw method, and is responsible for presenting
        /// the finished image onto the screen, using the appropriate WinForms
        /// control handle to make sure it shows up in the right place.
        /// </summary>
        void EndDraw()
        {
            //            PaintCallRefCount--; 

#if !DEBUG
            try
            {
#endif
                if (Device is null)
                    return;

                Rectangle sourceRectangle = new(0, 0, ClientSize.Width,
                                                                ClientSize.Height);

                if (Device.GraphicsDeviceStatus == GraphicsDeviceStatus.Normal)
                    Device.Present(); //(sourceRectangle, null, this.Handle);
#if !DEBUG
            }
            catch
            {
                // Present might throw if the device became lost while we were
                // drawing. The lost device will be handled by the next BeginDraw,
                // so we just swallow the exception.
            }
#endif
        }


        /// <summary>
        /// Helper used by BeginDraw. This checks the graphics device status,
        /// making sure it is big enough for drawing the current control, and
        /// that the device is not lost. Returns an error string if the device
        /// could not be reset.
        /// </summary>
        string? HandleDeviceReset()
        {
            if (Device is null)
                return "Graphics device is not available";

            bool deviceNeedsReset = false;

            switch (Device.GraphicsDeviceStatus)
            {
                case GraphicsDeviceStatus.Lost:
                    // If the graphics device is lost, we cannot use it at all.
                    return "Graphics device lost";

                case GraphicsDeviceStatus.NotReset:
                    // If device is in the not-reset state, we should try to reset it.
                    deviceNeedsReset = true;
                    break;

                default:
                    // If the device state is ok, check whether it is big enough.
                    PresentationParameters pp = Device.PresentationParameters;

                    deviceNeedsReset = (ClientSize.Width > pp.BackBufferWidth) ||
                                       (ClientSize.Height > pp.BackBufferHeight);
                    break;
            }

            // Do we need to reset the device?
            if (deviceNeedsReset)
            {
                if (graphicsDeviceService is null)
                    return "Graphics device service is not available";

                try
                {
                    graphicsDeviceService.ResetDevice(ClientSize.Width,
                                                      ClientSize.Height);
                }
                catch (Exception e)
                {
                    return "Graphics device reset failed\n\n" + e;
                }
            }

            return null;
        }


        /// <summary>
        /// If we do not have a valid graphics device (for instance if the device
        /// is lost, or if we are running inside the Form designer), we must use
        /// regular System.Drawing method to display a status message.
        /// </summary>
        protected virtual void PaintUsingSystemDrawing(Graphics graphics, string text)
        {
            graphics.Clear(Color.CornflowerBlue);

            using Brush brush = new SolidBrush(Color.Black);
            using StringFormat format = new();
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;

            graphics.DrawString(text, Font, brush, ClientRectangle, format);
        }


        /// <summary>
        /// Ignores WinForms paint-background messages. The default implementation
        /// would clear the control to the current background color, causing
        /// flickering when our OnPaint implementation then immediately draws some
        /// other color over the top using the XNA Framework GraphicsDevice.
        /// </summary>
        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
        }


        #endregion

        #region Abstract Methods


        /// <summary>
        /// Derived classes override this to initialize their drawing code.
        /// </summary>
        protected virtual void Initialize() => throw new NotImplementedException("GraphicsDeviceControl::Initialize must be implemented");


        /// <summary>
        /// Derived classes override this to draw themselves using the GraphicsDevice.
        /// </summary>
        protected virtual void Draw() => throw new NotImplementedException("GraphicsDeviceControl::Draw must be implemented");


        #endregion

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ResumeLayout(false);

        }

        const uint MonitorDefaultToNearest = 2;

        /// <summary>
        /// Returns the monitor the window is on, or the nearest monitor when the window does not intersect one.
        /// </summary>
        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    }
}
