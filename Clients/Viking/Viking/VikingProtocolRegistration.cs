using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Viking
{
    /// <summary>
    /// Registers the viking:// URL protocol so the OS launches Viking when the user clicks a viking://open?code=... link.
    /// Uses HKCU (no admin required).
    /// </summary>
    public static class VikingProtocolRegistration
    {
        private const string ProtocolName = "viking";
        private const string UrlProtocolValue = "URL:Viking Volume";

        /// <summary>
        /// Registers the viking:// protocol for the current user so that viking:// URLs open this application.
        /// Safe to call on every run; updates the command if the executable path has changed.
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
