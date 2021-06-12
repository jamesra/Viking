using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// A List<GridPolygon> with an indexing operator that understands Polygon indicies
    /// </summary>
    public class PolygonList : List<GridPolygon>
    {
        public virtual GridVector2 this[PolygonIndex index]
        {
            get { return index.Point(this); }
        }

        public PolygonList() : base()
        {
        }

        public PolygonList(int capacity) : base(capacity)
        {
        }

        public PolygonList(IEnumerable<GridPolygon> collection) : base(collection)
        {
        }
    }
}
