using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Interface for views that can be repositioned.
    /// </summary>
    public interface IViewPosition2D
    {
        GridVector2 Position { get; set; }
    }

    public interface IViewPosition3D
    {
        GridVector3 Position { get; set; }
    }
}
