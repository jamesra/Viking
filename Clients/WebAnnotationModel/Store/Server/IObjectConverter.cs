using Geometry;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel.ServerInterface
{
    public interface IObjectConverter<in SOURCE, out TARGET>
    {
        /// <summary>
        /// Convert the source object to the target object
        /// </summary>
        /// <returns></returns>
        TARGET Convert(SOURCE source);
    }

    /// <summary>
    /// Provides spatial information for an object
    /// </summary>
    /// <typeparam name="SOURCE"></typeparam>
    public interface IBoundingBoxConverter<in SOURCE>
    {  
        RTree.Rectangle BoundingRect(SOURCE obj);
    }
}