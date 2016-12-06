using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace AnnotationVizLib
{
    public interface IGeometry
    {
        Microsoft.SqlServer.Types.SqlGeometry Geometry { get; set; }

        double Z { get; }

        GridBox BoundingBox { get; }
    }
}
