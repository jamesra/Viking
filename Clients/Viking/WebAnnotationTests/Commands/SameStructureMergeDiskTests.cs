using AnnotationService.Types;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotationModel;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// A pen loop around a circle merges the structure's other circles as their disks (<see cref="SameStructureMerge.JoinsAsDisk"/>).
    /// Every location has a position and radius, so a point, line or curve of the same structure must not be swept in as a disk.
    /// </summary>
    [TestClass]
    public class SameStructureMergeDiskTests
    {
        private static LocationObj Location(LocationType type)
        {
            SqlGeometry shape = SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars("POINT(10 10)"), 0).STBuffer(5);
            return new LocationObj(new Location
            {
                ID = 11,
                TypeCode = (short)type,
                Section = 1,
                MosaicShapeWKB = shape.AsBinary(),
                VolumeShapeWKB = shape.AsBinary()
            });
        }

        [TestMethod]
        public void OnlyCirclesJoinAsDisks()
        {
            Assert.IsTrue(SameStructureMerge.JoinsAsDisk(Location(LocationType.CIRCLE), includeCircles: true));

            foreach (LocationType type in new[] { LocationType.POINT, LocationType.POLYLINE, LocationType.OPENCURVE, LocationType.CLOSEDCURVE, LocationType.ELLIPSE })
                Assert.IsFalse(SameStructureMerge.JoinsAsDisk(Location(type), includeCircles: true), type.ToString());
        }

        [TestMethod]
        public void NoDisksWhenCirclesAreExcluded() =>
            Assert.IsFalse(SameStructureMerge.JoinsAsDisk(Location(LocationType.CIRCLE), includeCircles: false));
    }
}
