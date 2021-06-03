using System;

namespace Jotunn
{
    public static class Global
    {
        internal static void Initialize()
        {
            MathNet.Numerics.Control.UseNativeMKL();
            SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
