using Geometry;
using Rectangle = Geometry.Rectangle;
using System;
using System.Threading.Tasks;

namespace VikingXNAGraphics
{
    public interface IHitTesting
    {
        /// <summary>
        /// The bounding box of the region we are interested in hit testing
        /// </summary>
        Geometry.Rectangle BoundingBox { get; }

        /// <summary>
        /// True if the passed point falls inside the view, a hit-testing function
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        bool Contains(Vector2 Position);
    }
}
