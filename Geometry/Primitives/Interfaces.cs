using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry
{ 
    public interface IPoint
    {
        double X { get; set; }
        double Y { get; set; }
        double Z { get; set; }
    }

    interface IShape2D
    {
        GridRectangle BoundingBox { get; }
        double Area { get; } 
        bool Contains(GridVector2 p);
    }
}
