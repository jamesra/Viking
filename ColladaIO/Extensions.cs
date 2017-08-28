using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColladaIO
{
    public static class Extensions
    {
        public static fx_common_float_or_param_typeFloat ToColladaFloat(this double value)
        {
            fx_common_float_or_param_typeFloat reflectivityValue = new fx_common_float_or_param_typeFloat();
            reflectivityValue.Value = value;
            return reflectivityValue;
        }
    }
}
