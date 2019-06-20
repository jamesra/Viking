using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace VikingXNAGraphics
{

    /// <summary>
    /// This interface is implemented by objects that require hit-testing
    /// </summary>
    public interface ICanvasView
    {
        /// <summary>
        /// True if the view is visible in the passed scene
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        bool IsVisible(VikingXNA.Scene scene);

        /// <summary>
        /// Indicates what level the CanvasViewObject occupies for selection and rendering purposes
        /// </summary>
        int VisualHeight { get; }

        /// <summary>
        /// Bounding box of the annotation
        /// </summary>
        GridRectangle BoundingBox
        {
            get;
        }

        /// <summary>
        /// True if the passed point falls inside the view, a hit-testing function
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        bool Intersects(GridVector2 Position);

        /// <summary>
        /// Returns the distance from the position to the nearest point on the annotation, or 0 if the position is inside the annotation
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double Distance(GridVector2 Position); 

        /// <summary>
        /// Assumes Position is within the annotation.  Returns a number from 0 to 1 indicating how close the position is between the center and edge of the annotation.
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double DistanceFromCenterNormalized(GridVector2 Position);
    }
}
