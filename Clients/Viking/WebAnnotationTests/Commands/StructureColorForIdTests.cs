using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotationModel;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The cell hue is a hash of the structure id. Segmentation and autoseg read the same value.
    /// </summary>
    [TestClass]
    public class StructureColorForIdTests
    {
        [TestMethod]
        public void ColorForId_SameIdIsStableAndDifferentIdsDiffer()
        {
            uint first = StructureObj.ColorForId(42);
            uint again = StructureObj.ColorForId(42);
            uint other = StructureObj.ColorForId(43);

            Assert.AreEqual(first, again);
            Assert.AreNotEqual(first, other);
            Assert.AreEqual(0xFFu, (first >> 24) & 0xFFu);
        }
    }
}
