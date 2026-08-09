using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests
{
    /// <summary>
    /// Command/view helpers that do not require a live SectionViewerControl or gRPC stack.
    /// </summary>
    [TestClass]
    public class LocationActionTests
    {
        [TestMethod]
        public void GetCursor_None_IsDefault()
        {
            Assert.AreEqual(Cursors.Default, LocationAction.NONE.GetCursor());
        }

        [TestMethod]
        public void GetCursor_CreateLinkedLocation_IsCross()
        {
            Assert.AreEqual(Cursors.Cross, LocationAction.CREATELINKEDLOCATION.GetCursor());
        }

        [TestMethod]
        public void GetCursor_Translate_IsHand()
        {
            Assert.AreEqual(Cursors.Hand, LocationAction.TRANSLATE.GetCursor());
        }

        [TestMethod]
        public void CreateCommand_NoneForCircle_ReturnsNull()
        {
            var loc = new LocationObj(parent: null, SectionNumber: 1, shapeType: LocationType.CIRCLE);
            var command = LocationAction.NONE.CreateCommand(Parent: null, loc, GridVector2.Zero);
            Assert.IsNull(command);
        }
    }
}
