using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebAnnotation
{
    using RTree;
    using Geometry;

    public static class Extensions
    {
        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, float Z)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, int Z)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, (float)Z, (float)Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, float Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, int Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, (float)Z, (float)Z);
        }

        public static Microsoft.SqlServer.Types.SqlGeometry ToGeometryPoint(this GridVector2 p)
        {
            return Microsoft.SqlServer.Types.SqlGeometry.Point(p.X, p.Y, 0);
        }

    }
}
