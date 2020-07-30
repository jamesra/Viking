using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationVizLib
{
    public interface ILocationLink : IEquatable<ILocationLink>
    {
        ulong A { get; }
        ulong B { get; }
    }
}
