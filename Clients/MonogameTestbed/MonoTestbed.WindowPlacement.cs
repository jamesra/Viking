using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private const uint MonitorinfofPrimary = 1;

        /// <summary>
        /// Converts a window size chosen in logical pixels to physical ones.  The process is per-monitor DPI aware
        /// so that captures come out at the display's true resolution, which also means Windows no longer stretches
        /// a fixed pixel size on a scaled display; without this the interactive window would shrink to 1/1.5 of its
        /// former size at 150% scaling.
        /// </summary>
        internal static int ScaleForDisplayDpi(int logicalPixels)
        {
            try
            {
                uint dpi = GetDpiForSystem();
                if (dpi > 0)
                    return (int)Math.Round(logicalPixels * (dpi / 96.0));
            }
            catch (EntryPointNotFoundException)
            {
                //Present since Windows 10 1607; older builds keep the unscaled size.
            }

            return logicalPixels;
        }

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
                frameWidth = ScaleForDisplayDpi(desired_screen_width) + (borderLeft * 2);
                frameHeight = ScaleForDisplayDpi(desired_screen_height) + borderTop + borderLeft;
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
        /// Enters borderless fullscreen at the capture monitor's pixel size and refreshes <see cref="Scene"/>'s
        /// viewport.  Called from Initialize (and again before PNG capture) so dump resolution matches the display.
        /// </summary>
        internal void EnsureExportFullscreen()
        {
            GetExportDisplaySize(out int width, out int height);
            graphics.HardwareModeSwitch = false;

            MoveToCaptureDisplay();

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
        /// Moves the window onto the monitor chosen for capture.  Borderless fullscreen follows whichever monitor
        /// the window sits on, so the window has to be placed there while it still has a position, which means
        /// leaving fullscreen first if it is already in it.
        /// </summary>
        private void MoveToCaptureDisplay()
        {
            if (!TryGetCaptureMonitor(out NativeRect monitor))
                return;

            if (Window.Position.X >= monitor.Left && Window.Position.X < monitor.Right &&
                Window.Position.Y >= monitor.Top && Window.Position.Y < monitor.Bottom)
                return;

            if (graphics.IsFullScreen && GraphicsDevice is not null)
            {
                graphics.IsFullScreen = false;
                graphics.ApplyChanges();
            }

            Window.Position = new Point(monitor.Left, monitor.Top);
            Trace.WriteLine($"Capturing on the display at ({monitor.Left},{monitor.Top}); pass --display to change it.");
        }

        /// <summary>
        /// Copies the live graphics viewport onto the testbed scene so projection matches the fullscreen back buffer.
        /// </summary>
        internal void SyncSceneViewport() => SyncViewport(Scene, GraphicsDevice);

        /// <summary>
        /// Points a scene at the device's current viewport.  Assigning it recomputes the projection, so the
        /// assignment is skipped when nothing changed.
        /// </summary>
        internal static void SyncViewport(VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.GraphicsDevice device)
        {
            if (scene is null || device is null)
                return;

            if (!scene.Viewport.Equals(device.Viewport))
                scene.Viewport = device.Viewport;
        }

        /// <inheritdoc cref="SyncViewport(VikingXNA.Scene, Microsoft.Xna.Framework.Graphics.GraphicsDevice)"/>
        internal static void SyncViewport(VikingXNA.Scene3D scene, Microsoft.Xna.Framework.Graphics.GraphicsDevice device)
        {
            if (scene is null || device is null)
                return;

            if (!scene.Viewport.Equals(device.Viewport))
                scene.Viewport = device.Viewport;
        }

        private void GetExportDisplaySize(out int width, out int height)
        {
            if (TryGetCaptureMonitor(out NativeRect capture))
            {
                width = Math.Max(1, capture.Right - capture.Left);
                height = Math.Max(1, capture.Bottom - capture.Top);
                return;
            }

            if (TryGetMonitorBounds(out NativeRect monitor))
            {
                width = Math.Max(1, monitor.Right - monitor.Left);
                height = Math.Max(1, monitor.Bottom - monitor.Top);
                return;
            }

            width = Math.Max(1, GetSystemMetrics(SmCxscreen));
            height = Math.Max(1, GetSystemMetrics(SmCyscreen));
        }

        /// <summary>
        /// Monitor the capture should use.  Taking over the primary display steals whatever the operator is looking
        /// at, so a capture run defaults to a secondary monitor when one is attached.  <c>--display</c> overrides
        /// the choice by index, or with "primary" to keep the old behaviour.
        /// </summary>
        /// <returns>False when the current monitor should be used, which the callers already handle.</returns>
        private static bool TryGetCaptureMonitor(out NativeRect monitor)
        {
            monitor = default;

            //Only capture runs relocate themselves; an interactive session stays where the operator put it.
            if (Program.options?.Screenshots != true)
                return false;

            List<DisplayInfo> displays = EnumerateDisplays();
            if (displays.Count == 0)
                return false;

            string requested = Program.options?.DisplayParam;
            if (!string.IsNullOrWhiteSpace(requested))
            {
                if (requested.Trim().Equals("primary", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (int.TryParse(requested.Trim(), out int index) && index >= 0 && index < displays.Count)
                {
                    monitor = displays[index].Bounds;
                    return true;
                }

                Trace.WriteLine($"--display '{requested}' is not a monitor index or 'primary'; using the default capture monitor.");
            }

            foreach (DisplayInfo display in displays)
            {
                if (!display.IsPrimary)
                {
                    monitor = display.Bounds;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Writes the attached monitors and their indices to the console, for use with <c>--display</c>.
        /// </summary>
        internal static void PrintDisplays()
        {
            List<DisplayInfo> displays = EnumerateDisplays();
            Console.WriteLine($"{displays.Count} display(s):");
            for (int i = 0; i < displays.Count; i++)
            {
                DisplayInfo d = displays[i];
                Console.WriteLine($"  {i}: {d.Bounds.Right - d.Bounds.Left}x{d.Bounds.Bottom - d.Bounds.Top} " +
                                  $"at ({d.Bounds.Left},{d.Bounds.Top}){(d.IsPrimary ? " [primary]" : "")}");
            }
        }

        private readonly record struct DisplayInfo(NativeRect Bounds, bool IsPrimary);

        private static List<DisplayInfo> EnumerateDisplays()
        {
            List<DisplayInfo> displays = [];

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref NativeRect _, IntPtr _) =>
            {
                NativeMonitorInfo info = new() { cbSize = Marshal.SizeOf<NativeMonitorInfo>() };
                if (GetMonitorInfoW(hMonitor, ref info))
                    displays.Add(new DisplayInfo(info.rcMonitor, (info.dwFlags & MonitorinfofPrimary) != 0));

                return true;
            }, IntPtr.Zero);

            return displays;
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

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref NativeRect lprcClip, IntPtr dwData);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetMonitorInfoW(IntPtr hMonitor, ref NativeMonitorInfo lpmi);

        [LibraryImport("user32.dll")]
        private static partial int GetSystemMetrics(int nIndex);

        [LibraryImport("user32.dll")]
        private static partial uint GetDpiForSystem();

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SystemParametersInfoW(uint uiAction, uint uiParam, ref NativeRect pvParam, uint fWinIni);
    }
}
