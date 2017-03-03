using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
