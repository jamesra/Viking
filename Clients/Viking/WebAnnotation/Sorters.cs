using Geometry;
using System.Collections.Generic;

namespace WebAnnotation
{
    class SortByDistance : IComparer<GridVector3>
    {
        public readonly GridVector3 Origin;
        public SortByDistance(GridVector3 Origin)
        {
            this.Origin = Origin;
        }

        public int Compare(GridVector3 x, GridVector3 y)
        {
            double x_origin_dist = GridVector3.Distance(this.Origin, x);
            double y_origin_dist = GridVector3.Distance(this.Origin, y);

            double delta = x_origin_dist - y_origin_dist;

            if (delta == 0)
                return 0;
            return delta < 0 ? -1 : 1;
        }
    }
}
