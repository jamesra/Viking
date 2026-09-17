using Geometry;
using System.Collections.Generic;

namespace WebAnnotation
{
    internal class SortByDistance(Vector3 Origin) : IComparer<Vector3>
    {
        public readonly Vector3 Origin = Origin;

        public int Compare(Vector3 x, Vector3 y)
        {
            double x_origin_dist = Vector3.Distance(Origin, x);
            double y_origin_dist = Vector3.Distance(Origin, y);

            double delta = x_origin_dist - y_origin_dist;

            if (delta == 0)
            {
                return 0;
            }

            return delta < 0 ? -1 : 1;
        }
    }
}
