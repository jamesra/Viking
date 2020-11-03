using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitsAndScale
{
    public interface IScale
    {
        IAxisUnits X { get; }
        IAxisUnits Y { get; }
        IAxisUnits Z { get; }
    }
}
