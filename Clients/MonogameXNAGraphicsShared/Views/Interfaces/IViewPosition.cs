using Geometry;
using Rectangle = Geometry.Rectangle;
using System;
using System.Threading.Tasks;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Interface for 2D views that can be repositioned.
    /// </summary>
    public interface IViewPosition2D
    {
        Geometry.Vector2 Position { get; set; }
    }

    /// <summary>
    /// Interface for 2D views with a bounding box
    /// </summary>
    public interface IViewBoundingRect
    {
        Geometry.Rectangle BoundingRect { get; set; }
    }

    /// <summary>
    /// Interface for 3D views that can be repositioned
    /// </summary>
    public interface IViewPosition3D
    {
        Geometry.Vector3 Position { get; set; }
    }
}
