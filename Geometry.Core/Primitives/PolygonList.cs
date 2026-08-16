using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// A list of polygons with an indexer that understands <see cref="PolygonIndex"/>.
    /// </summary>
    public class PolygonList : List<Polygon>
    {
        public virtual Vector2 this[PolygonIndex index] => index.Point(this);

        public PolygonList() : base()
        {
        }

        public PolygonList(int capacity) : base(capacity)
        {
        }

        public PolygonList(IEnumerable<Polygon> collection) : base(collection)
        {
        }
    }
}
