using Microsoft.Xna.Framework;
using System;
using System.Runtime.InteropServices;

namespace MonogameTestbed
{
    public partial class MonoTestbed
    {
        private const int SmCxframe = 32;
        private const int SmCyframe = 33;
        private const int SmCycaption = 4;
        private const int SmCxpaddedborder = 92;
        private const int SmCxscreen = 0;
        private const int SmCyscreen = 1;
        private const uint MonitorDefaultToNearest = 2;
        private const uint SpiGetWorkArea = 0x0030;

        /// <summary>
        /// Places the window at the top-left of the monitor work area, accounting for the title bar.
        /// MonoGame's Position is the client origin; (0,0) pushes the caption off the top of the screen.
        /// </summary>
        private void PositionWindowFullyOnScreen()
        {
            if (!TryGetWorkArea(out NativeRect work))
                work = new NativeRect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };

            if (!TryGetFrameInsets(out int borderLeft, out int borderTop, out int frameWidth, out int frameHeight))
            {
                borderLeft = GetSystemMetrics(SmCxframe) + GetSystemMetrics(SmCxpaddedborder);
                borderTop = GetSystemMetrics(SmCycaption) + GetSystemMetrics(SmCyframe) + GetSystemMetrics(SmCxpaddedborder);
                frameWidth = desired_screen_width + (borderLeft * 2);
                frameHeight = desired_screen_height + borderTop + borderLeft;
            }

            int frameX = work.Left;
            int frameY = work.Top;
            int maxFrameX = work.Right - frameWidth;
            int maxFrameY = work.Bottom - frameHeight;
            if (maxFrameX >= work.Left)
                frameX = Math.Clamp(frameX, work.Left, maxFrameX);
            if (maxFrameY >= work.Top)
                frameY = Math.Clamp(frameY, work.Top, maxFrameY);

            Window.Position = new Point(frameX + borderLeft, frameY + borderTop);
        }

        /// <summary>
        /// Sets borderless-fullscreen intent and a native-resolution back buffer before the device is created.
        /// Exclusive hardware switching would lock the adapter to 1600×1200 and keep screenshots downsampled.
        /// </summary>
        private void ConfigureExportFullscreen()
        {
            GetExportDisplaySize(out int width, out int height);
            graphics.HardwareModeSwitch = false;
            graphics.IsFullScreen = true;
            graphics.PreferredBackBufferWidth = width;
            graphics.PreferredBackBufferHeight = height;
        }

        /// <summary>
        /// Enters borderless fullscreen at the current monitor's pixel size and refreshes <see cref="Scene"/>'s viewport.
        /// Called from Initialize (and again before PNG capture) so dump resolution matches the display.
        /// </summary>
        internal void EnsureExportFullscreen()
        {
            GetExportDisplaySize(out int width, out int height);
            graphics.HardwareModeSwitch = false;
            graphics.PreferredBackBufferWidth = width;
            graphics.PreferredBackBufferHeight = height;
            graphics.IsFullScreen = true;
            if (GraphicsDevice is not null)
            {
                graphics.ApplyChanges();
                SyncSceneViewport();
            }
        }

        /// <summary>
        /// Copies the live graphics viewport onto the testbed scene so projection matches the fullscreen back buffer.
        /// </summary>
        internal void SyncSceneViewport()
        {
            if (Scene is not null && GraphicsDevice is not null)
                Scene.Viewport = GraphicsDevice.Viewport;
        }

        private void GetExportDisplaySize(out int width, out int height)
        {
            if (TryGetMonitorBounds(out NativeRect monitor))
            {
                width = Math.Max(1, monitor.Right - monitor.Left);
                height = Math.Max(1, monitor.Bottom - monitor.Top);
                return;
            }

            width = Math.Max(1, GetSystemMetrics(SmCxscreen));
            height = Math.Max(1, GetSystemMetrics(SmCyscreen));
        }

        private bool TryGetMonitorBounds(out NativeRect monitor)
        {
            IntPtr hwnd = Window.Handle;
            if (hwnd != IntPtr.Zero)
            {
                IntPtr hMonitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
                if (hMonitor != IntPtr.Zero)
                {
                    NativeMonitorInfo info = new() { cbSize = Marshal.SizeOf<NativeMonitorInfo>() };
                    if (GetMonitorInfoW(hMonitor, ref info))
                    {
                        monitor = info.rcMonitor;
                        return true;
                    }
                }
            }

            monitor = default;
            return false;
        }

        private bool TryGetWorkArea(out NativeRect work)
        {
            IntPtr hwnd = Window.Handle;
            if (hwnd != IntPtr.Zero)
            {
                IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
                if (monitor != IntPtr.Zero)
                {
                    NativeMonitorInfo info = new() { cbSize = Marshal.SizeOf<NativeMonitorInfo>() };
                    if (GetMonitorInfoW(monitor, ref info))
                    {
                        work = info.rcWork;
                        return true;
                    }
                }
            }

            work = default;
            return SystemParametersInfoW(SpiGetWorkArea, 0, ref work, 0);
        }

        private bool TryGetFrameInsets(out int borderLeft, out int borderTop, out int frameWidth, out int frameHeight)
        {
            borderLeft = 0;
            borderTop = 0;
            frameWidth = 0;
            frameHeight = 0;

            IntPtr hwnd = Window.Handle;
            if (hwnd == IntPtr.Zero)
                return false;

            if (!GetWindowRect(hwnd, out NativeRect windowRect))
                return false;

            NativePoint clientOrigin = default;
            if (!ClientToScreen(hwnd, ref clientOrigin))
                return false;

            borderLeft = clientOrigin.X - windowRect.Left;
            borderTop = clientOrigin.Y - windowRect.Top;
            frameWidth = windowRect.Right - windowRect.Left;
            frameHeight = windowRect.Bottom - windowRect.Top;
            return frameWidth > 0 && frameHeight > 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMonitorInfo
        {
            public int cbSize;
            public NativeRect rcMonitor;
            public NativeRect rcWork;
            public uint dwFlags;
        }

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ClientToScreen(IntPtr hWnd, ref NativePoint lpPoint);

        [LibraryImport("user32.dll")]
        private static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetMonitorInfoW(IntPtr hMonitor, ref NativeMonitorInfo lpmi);

        [LibraryImport("user32.dll")]
        private static partial int GetSystemMetrics(int nIndex);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SystemParametersInfoW(uint uiAction, uint uiParam, ref NativeRect pvParam, uint fWinIni);
    }
}
