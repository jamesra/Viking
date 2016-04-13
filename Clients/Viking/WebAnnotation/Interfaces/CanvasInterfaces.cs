using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;
using Geometry;

namespace WebAnnotation
{
    public interface ISelectable
    {
        bool Selected { get; set; }
    }

    public interface ICanvasView
    {
        bool IsVisible(Scene scene);

        /// <summary>
        /// Bounding box of the annotation
        /// </summary>
        GridRectangle BoundingBox
        {
            get;
        }

        bool Intersects(GridVector2 Position);

        /// <summary>
        /// Returns the distance from the position to the nearest point on the annotation, or 0 if the position is inside the annotation
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double Distance(GridVector2 Position);


        double Distance(Microsoft.SqlServer.Types.SqlGeometry Position);

        /// <summary>
        /// Assumes Position is within the annotation.  Returns a number from 0 to 1 indicating how close the position is between the center and edge of the annotation.
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double DistanceFromCenterNormalized(GridVector2 Position);
    }

    /// <summary>
    /// A class that contains multiple ICanvasView objects
    /// </summary>
    public interface ICanvasViewContainer
    {
        ICanvasView GetAnnotationAtPosition(GridVector2 position, out double distanceToCenterNormalized);
    }
}
