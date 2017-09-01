using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd
{

    partial class Point2D
    {

        public Point2D(GridVector2 p)
        {
            this.X = p.X;
            this.Y = p.Y;
        }

        public Point2D(double X, double Y)
        {
            this.X = X;
            this.Y = Y;
        }

        public GridVector2 ToGridVector2()
        {
            return new GridVector2(this.X, this.Y);
        }
    }
}
