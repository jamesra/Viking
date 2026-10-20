using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests.Actions
{
    /// <summary>
    /// A contour change whose new volume shape cannot be mapped to the section must leave the location untouched
    /// instead of throwing. Reuses the bounded transform and location setup from <see cref="ChangeTypeActionTests"/>.
    /// </summary>
    [TestClass]
    public class ChangeContourActionTests
    {
        /// <summary>
        /// The action constructor smooths the polyline with WebAnnotation's Global settings, whose type initializer needs a volume name.
        /// </summary>
        [TestInitialize]
        public void EnsureGlobalCanInitialize() => SegmentationServiceSessionTests.EnsureVolumeCachePathAvailable();

        [TestMethod]
        public void Change1DContourWithUnmappableShapeKeepsLocationShape()
        {
            LocationObj location = ChangeTypeActionTests.NewLocation(LocationType.POLYLINE, "LINESTRING(10 10, 50 50, 90 10)");
            string originalShape = location.MosaicShape.ToString();

            Change1DContourAction action = new(location, new Polyline(ChangeTypeActionTests.UnmappableRing()), ChangeTypeActionTests.CreateBoundedTransform());
            action.OnExecute();

            Assert.AreEqual(LocationType.POLYLINE, location.TypeCode);
            Assert.AreEqual(originalShape, location.MosaicShape.ToString());
        }
    }
}
