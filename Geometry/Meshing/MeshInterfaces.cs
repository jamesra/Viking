using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace Geometry.Meshing
{
    interface IVertex
    {
        GridVector3 Position { get; set;  }
        GridVector3 Normal { get; set; }

        SortedSet<EdgeKey> Edges { get; }
    }
}
