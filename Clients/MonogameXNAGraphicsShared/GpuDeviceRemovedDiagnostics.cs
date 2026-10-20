using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

#nullable enable

namespace VikingXNAGraphics
{
    /// <summary>
    /// DXGI device-removed diagnostics. The thrown HRESULT is often DXGI_ERROR_DEVICE_REMOVED
    /// (0x887A0005); ID3D11Device.GetDeviceRemovedReason (SharpDX DeviceRemovedReason) is the
    /// specific cause Windows recorded (hung TDR, reset, driver, invalid call).
    /// </summary>
    public static class GpuDeviceRemovedDiagnostics
    {
        public const int DXGI_ERROR_INVALID_CALL = unchecked((int)0x887A0001);
        public const int DXGI_ERROR_DEVICE_REMOVED = unchecked((int)0x887A0005);
        public const int DXGI_ERROR_DEVICE_HUNG = unchecked((int)0x887A0006);
        public const int DXGI_ERROR_DEVICE_RESET = unchecked((int)0x887A0007);
        public const int DXGI_ERROR_WAS_STILL_DRAWING = unchecked((int)0x887A000A);
        public const int DXGI_ERROR_DRIVER_INTERNAL_ERROR = unchecked((int)0x887A0020);

        public static bool IsDeviceRemovedError(Exception? ex)
        {
            for (Exception walk = ex; walk != null; walk = walk.InnerException)
            {
                if (IsDeviceRemovedHResult(walk.HResult))
                    return true;
            }

            return false;
        }

        public static bool IsDeviceRemovedHResult(int hr) =>
            hr == DXGI_ERROR_DEVICE_REMOVED
            || hr == DXGI_ERROR_DEVICE_HUNG
            || hr == DXGI_ERROR_DEVICE_RESET
            || hr == DXGI_ERROR_DRIVER_INTERNAL_ERROR;

        /// <summary>
        /// Reads SharpDX.Direct3D11.Device.DeviceRemovedReason via GraphicsDevice.Handle
        /// (MonoGame WindowsDX maps this to ID3D11Device::GetDeviceRemovedReason).
        /// </summary>
        public static bool TryGetDeviceRemovedReason(GraphicsDevice? device, out int hresult)
        {
            hresult = 0;
            if (device is null)
                return false;

            try
            {
                object handle = device.Handle;
                if (handle is null)
                    return false;

                object? reason = null;
                PropertyInfo reasonProp = handle.GetType().GetProperty("DeviceRemovedReason", BindingFlags.Instance | BindingFlags.Public);
                if (reasonProp != null)
                    reason = reasonProp.GetValue(handle);

                if (reason is null)
                {
                    MethodInfo reasonMethod = handle.GetType().GetMethod("GetDeviceRemovedReason", Type.EmptyTypes)
                        ?? handle.GetType().GetMethod("DeviceRemovedReason", Type.EmptyTypes);
                    if (reasonMethod != null)
                        reason = reasonMethod.Invoke(handle, null);
                }

                if (reason is null)
                    return false;

                return TryCoerceHResult(reason, out hresult);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"GetDeviceRemovedReason failed: {ex.Message}", "Graphics");
                return false;
            }
        }

        public static string NameForHResult(int hr) => hr switch
        {
            0 => "S_OK",
            DXGI_ERROR_INVALID_CALL => "DXGI_ERROR_INVALID_CALL",
            DXGI_ERROR_DEVICE_REMOVED => "DXGI_ERROR_DEVICE_REMOVED",
            DXGI_ERROR_DEVICE_HUNG => "DXGI_ERROR_DEVICE_HUNG",
            DXGI_ERROR_DEVICE_RESET => "DXGI_ERROR_DEVICE_RESET",
            DXGI_ERROR_WAS_STILL_DRAWING => "DXGI_ERROR_WAS_STILL_DRAWING",
            DXGI_ERROR_DRIVER_INTERNAL_ERROR => "DXGI_ERROR_DRIVER_INTERNAL_ERROR",
            _ => "unknown DXGI/HRESULT"
        };

        /// <summary>Microsoft DXGI_ERROR wording plus what we usually see in Viking after a long session.</summary>
        public static string DescribeHResult(int hr) => hr switch
        {
            0 => "Device is not removed.",
            DXGI_ERROR_DEVICE_HUNG =>
                "GPU stopped responding; Windows reset it (Timeout Detection and Recovery). Common after hours of use with no user action. Check Event Viewer → Windows Logs → System (source Display) for a TDR.",
            DXGI_ERROR_DEVICE_REMOVED =>
                "Adapter removed, driver upgraded/crashed, or Windows reset the GPU. GetDeviceRemovedReason above is the specific cause. Recreate the device (restart Viking).",
            DXGI_ERROR_DEVICE_RESET =>
                "Device reset because of a badly formed GPU command. Recreate the device (restart Viking).",
            DXGI_ERROR_DRIVER_INTERNAL_ERROR =>
                "The display driver hit an internal error and put the device into the removed state. Update or reinstall the GPU driver.",
            DXGI_ERROR_INVALID_CALL =>
                "Direct3D rejected a call as invalid. Often a consequence of using the device after it was already removed.",
            _ => "See https://learn.microsoft.com/windows/win32/direct3ddxgi/dxgi-error"
        };

        public static string FormatOverlayMessage(GraphicsDevice? device, Exception? ex)
        {
            int thrown = ex != null ? FindDeviceRemovedHResult(ex) : 0;
            bool hasReason = TryGetDeviceRemovedReason(device, out int reason);
            string reasonText = hasReason
                ? $"{NameForHResult(reason)} (0x{reason:X8})"
                : "(could not query)";

            return "Graphics device removed\n\n"
                + $"GetDeviceRemovedReason: {reasonText}\n"
                + $"Exception HRESULT: {NameForHResult(thrown)} (0x{thrown:X8})\n"
                + "Restart Viking. A copyable report was written to the log and shown in a dialog.";
        }

        public static string FormatReport(Exception? ex, GraphicsDevice? device)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Viking GPU device-removed report");
            sb.AppendLine($"Local time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            try
            {
                Assembly entry = Assembly.GetEntryAssembly();
                if (entry != null)
                    sb.AppendLine($"Viking version: {entry.GetName().Version}");
            }
            catch (Exception versionEx)
            {
                sb.AppendLine($"Viking version unavailable: {versionEx.Message}");
            }

            try
            {
                sb.AppendLine($"OS: {Environment.OSVersion} ({RuntimeInformation.OSDescription})");
                sb.AppendLine($"64-bit OS / process: {Environment.Is64BitOperatingSystem} / {Environment.Is64BitProcess}");
            }
            catch (Exception osEx)
            {
                sb.AppendLine($"OS info unavailable: {osEx.Message}");
            }

            try
            {
                Process proc = Process.GetCurrentProcess();
                TimeSpan uptime = DateTime.Now - proc.StartTime;
                sb.AppendLine($"Process: {proc.ProcessName} pid {proc.Id}");
                sb.AppendLine($"Process uptime: {uptime} ({uptime.TotalHours:0.00} hours)");
                sb.AppendLine($"Working set: {proc.WorkingSet64 / (1024 * 1024)} MB");
            }
            catch (Exception procEx)
            {
                sb.AppendLine($"Process info unavailable: {procEx.Message}");
            }

            if (device != null)
            {
                try
                {
                    sb.AppendLine($"GraphicsDeviceStatus: {device.GraphicsDeviceStatus}");
                    sb.AppendLine($"GraphicsProfile: {device.GraphicsProfile}");
                    sb.AppendLine($"Device disposed: {device.IsDisposed}");
                }
                catch (Exception statusEx)
                {
                    sb.AppendLine($"GraphicsDevice status unavailable: {statusEx.Message}");
                }

                if (TryGetDeviceRemovedReason(device, out int reasonHr))
                {
                    sb.AppendLine($"GetDeviceRemovedReason: {NameForHResult(reasonHr)} (0x{reasonHr:X8})");
                    sb.AppendLine($"  {DescribeHResult(reasonHr)}");
                }
                else
                {
                    sb.AppendLine("GetDeviceRemovedReason: unavailable (Handle missing or query failed)");
                }
            }
            else
            {
                sb.AppendLine("GraphicsDevice: null");
            }

            try
            {
                GraphicsAdapter adapter = device?.Adapter ?? GraphicsAdapter.DefaultAdapter;
                if (adapter != null)
                {
                    sb.AppendLine($"Adapter: {adapter.Description}");
                    sb.AppendLine($"Adapter DeviceId: 0x{adapter.DeviceId:X4} VendorId: 0x{adapter.VendorId:X4}");
                    sb.AppendLine($"Adapter DeviceName: {adapter.DeviceName}");
                    if (adapter.CurrentDisplayMode != null)
                    {
                        sb.AppendLine($"Display mode: {adapter.CurrentDisplayMode.Width}x{adapter.CurrentDisplayMode.Height} {adapter.CurrentDisplayMode.Format}");
                    }
                }
            }
            catch (Exception adapterEx)
            {
                sb.AppendLine($"Adapter info unavailable: {adapterEx.Message}");
            }

            if (ex != null)
            {
                int thrownHr = FindDeviceRemovedHResult(ex);
                sb.AppendLine($"Exception HRESULT: {NameForHResult(thrownHr)} (0x{thrownHr:X8}) {DescribeHResult(thrownHr)}");
                sb.AppendLine($"Exception type: {ex.GetType().FullName}");
                sb.AppendLine($"Exception message: {ex.Message}");
                sb.AppendLine("Stack trace:");
                sb.AppendLine(ex.ToString());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Writes the report under %LOCALAPPDATA%\Viking\Logs so Release builds still keep a copy
        /// (the normal Viking trace listener is DEBUG-only).
        /// </summary>
        /// <returns>Full path written, or null if the write failed.</returns>
        public static string? TryWriteReportFile(string report)
        {
            if (string.IsNullOrEmpty(report))
                return null;

            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Viking",
                    "Logs");
                Directory.CreateDirectory(logDir);
                string path = Path.Combine(logDir, $"gpu-device-removed-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                File.WriteAllText(path, report);
                return path;
            }
            catch (Exception ioEx)
            {
                Trace.WriteLine($"GPU report file write failed: {ioEx.Message}", "Graphics");
                return null;
            }
        }

        static int FindDeviceRemovedHResult(Exception? ex)
        {
            for (Exception walk = ex; walk != null; walk = walk.InnerException)
            {
                if (IsDeviceRemovedHResult(walk.HResult))
                    return walk.HResult;
            }

            return ex?.HResult ?? 0;
        }

        static bool TryCoerceHResult(object? reason, out int hresult)
        {
            hresult = 0;
            if (reason is null)
                return false;
            if (reason is int i)
            {
                hresult = i;
                return true;
            }
            if (reason is uint u)
            {
                hresult = unchecked((int)u);
                return true;
            }

            PropertyInfo codeProp = reason.GetType().GetProperty("Code", BindingFlags.Instance | BindingFlags.Public);
            if (codeProp != null)
            {
                object code = codeProp.GetValue(reason);
                if (code != null && !ReferenceEquals(code, reason))
                    return TryCoerceHResult(code, out hresult);
            }

            try
            {
                hresult = Convert.ToInt32(reason, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
