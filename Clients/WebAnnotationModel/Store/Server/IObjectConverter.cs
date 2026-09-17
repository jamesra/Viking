using Geometry;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel.ServerInterface
{
    /// <summary>Creates a new TARGET from SOURCE. Use IObjectUpdater when the instance already exists.</summary>
    public interface IObjectConverter<in SOURCE, out TARGET>
    {
        /// <summary>
        /// Convert the source object to the target object
        /// </summary>
        /// <returns></returns>
        TARGET Convert(SOURCE source);
    }

    /// <summary>
    /// Bounding box for the region-loader RTree. Must match the space of the query rectangle.
    /// Location store wires mosaic; callers pass ApproximateVisibleMosaicBounds.
    /// </summary>
    /// <typeparam name="SOURCE"></typeparam>
    public interface IBoundingBoxConverter<in SOURCE>
    {  
        RTree.Rectangle BoundingRect(SOURCE obj);
    }
}