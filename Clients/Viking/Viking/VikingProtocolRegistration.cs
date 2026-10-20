using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Viking
{
    /// <summary>
    /// Registers the channel URL protocol (viking:// or viking-test://) so the OS launches this build
    /// when the user clicks an open link. Uses HKCU (no admin required).
    /// </summary>
    public static class VikingProtocolRegistration
    {
        private static string ProtocolName => VikingChannelIdentity.ProtocolScheme;
        private static string UrlProtocolValue =>
            VikingChannelIdentity.IsTestChannel ? "URL:Viking Test Volume" : "URL:Viking Volume";

        /// <summary>
        /// Registers this channel's protocol for the current user.
        /// Safe to call on every run; updates the command if the executable path has changed.
        /// Test builds register viking-test:// and leave production viking:// alone.
        /// </summary>
        public static void RegisterIfNeeded()
        {
            try
            {
                string exePath = GetExecutablePath();
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return;

                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProtocolName, writable: true);
                if (key == null)
                    return;

                key.SetValue("", UrlProtocolValue);
                key.SetValue("URL Protocol", "");

                using var commandKey = Registry.CurrentUser.CreateSubKey(
                    @"Software\Classes\" + ProtocolName + @"\shell\open\command", writable: true);
                if (commandKey != null)
                {
                    var command = $"\"{exePath}\" \"%1\"";
                    commandKey.SetValue("", command);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Viking] Protocol registration failed: {ex.Message}");
            }
        }

        private static string GetExecutablePath()
        {
            try
            {
                var location = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(location))
                    return location;
                try
                {
                    var processPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(processPath))
                        return processPath;
                }
                catch { }
                return Path.Combine(AppContext.BaseDirectory, "Viking.exe");
            }
            catch
            {
                return Path.Combine(AppContext.BaseDirectory, "Viking.exe");
            }
        }
    }
}
