using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jotunn
{
    public static class Global
    {
        static Global()
        {
            MathNet.Numerics.Control.UseNativeMKL();
        }
    }
}
