using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry
{
    interface IShape2D
    {
        GridRectangle BoundingBox { get; }
        double Area { get; } 
        bool Contains(GridVector2 p);
    }
}
