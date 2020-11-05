using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Interface for 2D views that can be repositioned.
    /// </summary>
    public interface IViewPosition2D
    {
        GridVector2 Position { get; set; }
    }
    
    /// <summary>
    /// Interface for 2D views with a bounding box
    /// </summary>
    public interface IViewBoundingRect
    {
        GridRectangle BoundingRect { get; set; }
    }

    /// <summary>
    /// Interface for 3D views that can be repositioned
    /// </summary>
    public interface IViewPosition3D
    {
        GridVector3 Position { get; set; }
    }
}
