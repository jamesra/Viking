using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTree;

namespace WebAnnotationTests
{
    /// <summary>
    /// Rectangle.Equals is used by collections and must follow the Equals contract for null and foreign types.
    /// </summary>
    [TestClass]
    public class RTreeRectangleEqualsTests
    {
        /// <summary>
        /// Equals(null) must return false instead of throwing a NullReferenceException.
        /// </summary>
        [TestMethod]
        public void EqualsNullReturnsFalse()
        {
            var r = new Rectangle(0, 0, 1, 1, 0, 0);
            Assert.IsFalse(r.Equals(null));
        }

        /// <summary>
        /// Rectangles with the same bounds are equal; different bounds or another type are not.
        /// </summary>
        [TestMethod]
        public void EqualsComparesBounds()
        {
            var r = new Rectangle(0, 0, 1, 1, 0, 0);
            Assert.IsTrue(r.Equals(new Rectangle(0, 0, 1, 1, 0, 0)));
            Assert.IsFalse(r.Equals(new Rectangle(0, 0, 2, 1, 0, 0)));
            Assert.IsFalse(r.Equals("x"));
        }
    }
}
