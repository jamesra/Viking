using System;
using System.Diagnostics;

namespace Jotunn
{
    public static class Global
    {
        internal static void Initialize()
        {
            Geometry.Global.TryUseNativeMKL();

            try
            {
                SqlServerTypesLoader.Loader.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SqlServerTypes native assemblies were not loaded: {ex.Message}");
            }
        }
    }
}
