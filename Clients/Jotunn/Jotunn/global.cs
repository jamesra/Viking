using System;

namespace Jotunn
{
    public static class Global
    {
        internal static void Initialize()
        {
            MathNet.Numerics.Control.UseNativeMKL();
                                SqlServerTypesLoader.Loader.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
