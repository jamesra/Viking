using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Xna.Framework.Graphics;
using VikingXNA;

namespace Viking.Rendering
{
    /// <summary>
    /// WPF HwndHost for a shared MonoGame WindowsDX device. Not WinForms.
    /// </summary>
    public class MonoGameHwndHost : HwndHost
    {
        const int WsChild = 0x40000000;
        const int WsVisible = 0x10000000;
        const int WmPaint = 0x000F;
        const int WmSize = 0x0005;

        IntPtr _hwnd;
        GraphicsDeviceService? _deviceService;
        bool _deviceResetPending;

        public GraphicsDevice? Device => _deviceService?.GraphicsDevice;

        public Microsoft.Xna.Framework.Content.ContentManager Content =>
            _deviceService?.Content ?? throw new InvalidOperationException("Graphics device is not initialized");

        public Camera Camera { get; } = new();

        /// <summary>
        /// Created in BuildWindowCore once the device exists. Null until the HWND is built.
        /// </summary>
        public VikingXNA.Scene? Scene { get; private set; }

        /// <summary>
        /// Hosts draw here; this class Presents after the handler returns.
        /// </summary>
        public event EventHandler<DrawingEventArgs>? Drawing;

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            int width = Math.Max(1, (int)ActualWidth);
            int height = Math.Max(1, (int)ActualHeight);
            _hwnd = CreateWindowEx(0, "static", "", WsChild | WsVisible, 0, 0, width, height, hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            _deviceService = GraphicsDeviceService.AddRef(_hwnd, width, height);
            Scene = new VikingXNA.Scene(_deviceService.GraphicsDevice.Viewport, Camera);
            CompositionTarget.Rendering += OnRendering;
            return new HandleRef(this, _hwnd);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            CompositionTarget.Rendering -= OnRendering;
            _deviceService?.Release(true);
            _deviceService = null;
            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Must call base for unhandled messages. Returning Zero without base swallows WM_MOUSE* and the view cannot pan.
        /// </summary>
        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmSize)
                _deviceResetPending = true;
            else if (msg == WmPaint)
            {
                _renderRequested = true;
                DrawFrame();
                handled = true;
                return IntPtr.Zero;
            }

            // Returning Zero without base.WndProc swallows WM_MOUSE* and the view cannot pan.
            return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            _deviceResetPending = true;
        }

        void OnRendering(object sender, EventArgs e) => DrawFrame();

        /// <summary>
        /// Marks the next CompositionTarget tick as needing Present. Texture uploads and camera changes call this so the loop can idle.
        /// </summary>
        public void RequestRender() => _renderRequested = true;

        bool _renderRequested = true;
        long _lastPresentTicks;
        const long MinFrameIntervalTicks = TimeSpan.TicksPerMillisecond * 16;

        void DrawFrame()
        {
            if (_deviceService?.GraphicsDevice is null || _hwnd == IntPtr.Zero)
                return;

            bool pipelineWork = global::Viking.TileLoadEnvironment.HasTexturePipelineWork;
            if (!_deviceResetPending && !_renderRequested && !pipelineWork)
                return;

            long now = DateTime.UtcNow.Ticks;
            if (!_deviceResetPending && now - _lastPresentTicks < MinFrameIntervalTicks)
                return;

            int width = Math.Max(1, (int)ActualWidth);
            int height = Math.Max(1, (int)ActualHeight);
            if (_deviceResetPending)
            {
                _deviceService.ResetDevice(width, height);
                if (Scene != null)
                    Scene.Viewport = _deviceService.GraphicsDevice.Viewport;
                _deviceResetPending = false;
            }

            _renderRequested = false;
            GraphicsDevice device = _deviceService.GraphicsDevice;
            device.Clear(Microsoft.Xna.Framework.Color.Black);
            try
            {
                Drawing?.Invoke(this, new DrawingEventArgs(device, Scene));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Section view draw failed: {ex}");
            }
            device.Present();
            _lastPresentTicks = DateTime.UtcNow.Ticks;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyWindow(IntPtr hwnd);
    }

    public sealed class DrawingEventArgs : EventArgs
    {
        public DrawingEventArgs(GraphicsDevice device, VikingXNA.Scene scene)
        {
            Device = device;
            Scene = scene;
        }

        public GraphicsDevice Device { get; }

        public VikingXNA.Scene Scene { get; }
    }
}
