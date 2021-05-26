using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitsAndScale
{
    public interface IAxisUnits
    {
        /// <summary>
        /// Size of a single unit along the axis
        /// </summary>
        double Value { get; }

        /// <summary>
        /// Units of measurement that Value represents
        /// </summary>
        string Units { get; }
    }
}
