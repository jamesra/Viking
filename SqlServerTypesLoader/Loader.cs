using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace SqlServerTypesLoader
{
    /// <summary>
    /// Utility methods for loading SQL Server native spatial assemblies
    /// </summary>
    public static class Loader
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        /// <summary>
        /// Loads the required native assemblies for the current architecture (x86 or x64)
        /// </summary>
        /// <param name="rootApplicationPath">
        /// Root path of the current application. Use Server.MapPath(".") for ASP.NET applications
        /// and AppDomain.CurrentDomain.BaseDirectory for desktop applications.
        /// </param>
        public static void LoadNativeAssemblies(string rootApplicationPath)
        {
            var nativeBinaryPath = IntPtr.Size > 4
                ? Path.Combine(rootApplicationPath, @"SqlServerTypes\x64\")
                : Path.Combine(rootApplicationPath, @"SqlServerTypes\x86\");

            // Load the Visual C++ runtime first
            LoadNativeAssembly(nativeBinaryPath, "msvcr120.dll");
            
            // Try to load SqlServerSpatial160.dll first (newer version)
            try
            { 
                LoadNativeAssembly(nativeBinaryPath, "SqlServerSpatial160.dll");
                Trace.WriteLine("Successfully loaded SqlServerSpatial160.dll");
            }
            catch (Exception e)
            {
                // If SqlServerSpatial160.dll is not found, try loading the older version
                Trace.WriteLine($"SqlServerSpatial160.dll loading exception {e.Message}\n\nFalling back to SqlServerSpatial140.dll ");

                try { 
                    LoadNativeAssembly(nativeBinaryPath, "SqlServerSpatial140.dll");
                    Trace.WriteLine("Successfully loaded SqlServerSpatial140.dll");
                }
                catch (Exception etwo)
                {
                    Trace.WriteLine($"SqlServerSpatial140.dll loading exception: {etwo.Message}");
                    throw new DllNotFoundException(
                        $"Failed to load SQL Server Spatial DLLs. Tried both SqlServerSpatial160.dll and SqlServerSpatial140.dll from path: {nativeBinaryPath}", 
                        etwo);
                }
            }
        }

        private static void LoadNativeAssembly(string nativeBinaryPath, string assemblyName)
        {
            var path = Path.Combine(nativeBinaryPath, assemblyName);
            
            // Check if file exists before trying to load
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Native assembly not found: {path}");
            }
            
            var ptr = LoadLibrary(path);
            if (ptr == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new DllNotFoundException(string.Format(
                    "Error loading {0} (ErrorCode: {1})",
                    assemblyName,
                    errorCode));
            }
        }
    }
}


