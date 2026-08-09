using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;
using WebAnnotation;

namespace WebAnnotationTests
{
    /// <summary>
    /// Cursor mapping for LocationAction — no SectionViewerControl / Viking.UI.State required.
    /// (CreateCommand touches State and is covered by integration/UI smoke instead.)
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
    }
}
