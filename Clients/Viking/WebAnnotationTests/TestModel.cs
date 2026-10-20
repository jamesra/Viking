using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests
{
    /// <summary>
    /// Builds annotation model objects as the server would deliver them (DBAction NONE) without a store or server.
    /// </summary>
    internal static class TestModel
    {
        /// <summary>
        /// A location on <paramref name="section"/> whose mosaic and volume shapes are both <paramref name="wkt"/>.
        /// </summary>
        public static LocationObj Location(long id, long parentId, LocationType type, string wkt, long section = 1)
        {
            SqlGeometry shape = SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(wkt), 0);
            LocationObj location = new(id, parentId)
            {
                Section = section,
                TypeCode = type,
                MosaicShape = shape.ToShape2D(),
                VolumeShape = shape.ToShape2D(),
            };
            location.DBAction = DBACTION.NONE;
            return location;
        }

        /// <summary>
        /// A point location at the origin of <paramref name="section"/>, for tests that only need IDs and sections.
        /// </summary>
        public static LocationObj Location(long id, long parentId, long section = 1) =>
            Location(id, parentId, LocationType.POINT, "POINT(0 0)", section);
    }
}
