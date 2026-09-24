using System;
using System.Runtime.InteropServices;

namespace VikingXNAWinForms
{
    /// <summary>
    /// Reads whether Windows advanced color (HDR) is enabled for a monitor.
    /// Callers use the result to recreate the swap chain only when that state changes.
    /// </summary>
    static class MonitorAdvancedColor
    {
        const uint QueryOnlyActivePaths = 2;
        const int ErrorSuccess = 0;
        const int ErrorInsufficientBuffer = 122;
        const int GetSourceName = 1;
        const int GetAdvancedColorInfo = 9;
        const uint AdvancedColorEnabledBit = 2;

        /// <summary>
        /// Queries the advanced-color enable bit for <paramref name="monitor"/>.
        /// Returns false when the monitor or the display configuration cannot be read,
        /// so the caller leaves the swap chain alone.
        /// </summary>
        public static bool TryGetEnabled(IntPtr monitor, out bool enabled)
        {
            enabled = false;
            if (monitor == IntPtr.Zero)
                return false;

            if (!TryGetDeviceName(monitor, out string deviceName))
                return false;

            if (!TryQueryActivePaths(out DisplayConfigPathInfo[] paths))
                return false;

            foreach (DisplayConfigPathInfo path in paths)
            {
                if (!TryGetSourceDeviceName(path, out string sourceName))
                    continue;
                if (!string.Equals(sourceName, deviceName, StringComparison.OrdinalIgnoreCase))
                    continue;

                return TryGetTargetAdvancedColorEnabled(path, out enabled);
            }

            return false;
        }

        static bool TryGetDeviceName(IntPtr monitor, out string deviceName)
        {
            deviceName = string.Empty;
            MonitorInfoEx info = new()
            {
                cbSize = Marshal.SizeOf<MonitorInfoEx>()
            };
            if (!GetMonitorInfo(monitor, ref info) || string.IsNullOrEmpty(info.szDevice))
                return false;

            deviceName = info.szDevice;
            return true;
        }

        static bool TryQueryActivePaths(out DisplayConfigPathInfo[] paths)
        {
            paths = Array.Empty<DisplayConfigPathInfo>();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (GetDisplayConfigBufferSizes(QueryOnlyActivePaths, out uint pathCount, out uint modeCount) != ErrorSuccess)
                    return false;
                if (pathCount == 0)
                    return false;

                paths = new DisplayConfigPathInfo[pathCount];
                DisplayConfigModeInfo[] modes = modeCount == 0
                    ? Array.Empty<DisplayConfigModeInfo>()
                    : new DisplayConfigModeInfo[modeCount];

                int result = QueryDisplayConfig(QueryOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (result == ErrorSuccess)
                {
                    if (pathCount != paths.Length)
                        Array.Resize(ref paths, (int)pathCount);
                    return true;
                }

                if (result != ErrorInsufficientBuffer)
                    return false;
            }

            return false;
        }

        static bool TryGetSourceDeviceName(DisplayConfigPathInfo path, out string sourceName)
        {
            sourceName = string.Empty;
            DisplayConfigSourceDeviceName request = new()
            {
                header = new DisplayConfigDeviceInfoHeader
                {
                    type = GetSourceName,
                    size = Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                    adapterId = path.sourceInfo.adapterId,
                    id = path.sourceInfo.id
                }
            };

            if (DisplayConfigGetDeviceInfo(ref request) != ErrorSuccess)
                return false;

            sourceName = request.viewGdiDeviceName ?? string.Empty;
            return sourceName.Length > 0;
        }

        static bool TryGetTargetAdvancedColorEnabled(DisplayConfigPathInfo path, out bool enabled)
        {
            enabled = false;
            DisplayConfigGetAdvancedColorInfo request = new()
            {
                header = new DisplayConfigDeviceInfoHeader
                {
                    type = GetAdvancedColorInfo,
                    size = Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo>(),
                    adapterId = path.targetInfo.adapterId,
                    id = path.targetInfo.id
                }
            };

            if (DisplayConfigGetDeviceInfo(ref request) != ErrorSuccess)
                return false;

            enabled = (request.value & AdvancedColorEnabledBit) != 0;
            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DisplayConfigDeviceInfoHeader
        {
            public int type;
            public int size;
            public Luid adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DisplayConfigSourceDeviceName
        {
            public DisplayConfigDeviceInfoHeader header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DisplayConfigGetAdvancedColorInfo
        {
            public DisplayConfigDeviceInfoHeader header;
            public uint value;
            public uint colorEncoding;
            public uint bitsPerColorChannel;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DisplayConfigPathSourceInfo
        {
            public Luid adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DisplayConfigRational
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DisplayConfigPathTargetInfo
        {
            public Luid adapterId;
            public uint id;
            public uint modeInfoIdx;
            public int outputTechnology;
            public int rotation;
            public int scaling;
            public DisplayConfigRational refreshRate;
            public int scanLineOrdering;
            [MarshalAs(UnmanagedType.Bool)]
            public bool targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DisplayConfigPathInfo
        {
            public DisplayConfigPathSourceInfo sourceInfo;
            public DisplayConfigPathTargetInfo targetInfo;
            public uint flags;
        }

        /// <summary>
        /// Opaque mode buffer. QueryDisplayConfig requires it; this caller does not read the modes.
        /// The native DISPLAYCONFIG_MODE_INFO is 64 bytes.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Size = 64)]
        struct DisplayConfigModeInfo
        {
            public int infoType;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativeRect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MonitorInfoEx
        {
            public int cbSize;
            public NativeRect rcMonitor;
            public NativeRect rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

        [DllImport("user32.dll")]
        static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        static extern int QueryDisplayConfig(
            uint flags,
            ref uint numPathArrayElements,
            [Out] DisplayConfigPathInfo[] pathArray,
            ref uint numModeInfoArrayElements,
            [Out] DisplayConfigModeInfo[] modeInfoArray,
            IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSourceDeviceName requestPacket);

        [DllImport("user32.dll")]
        static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigGetAdvancedColorInfo requestPacket);
    }
}
