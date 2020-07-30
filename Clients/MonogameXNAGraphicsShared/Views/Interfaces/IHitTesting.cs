using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace VikingXNAGraphics
{
    public interface IHitTesting
    { 
        /// <summary>
        /// The bounding box of the region we are interested in hit testing
        /// </summary>
        GridRectangle BoundingBox { get; }

        /// <summary>
        /// True if the passed point falls inside the view, a hit-testing function
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        bool Contains(GridVector2 Position);
    }
}
