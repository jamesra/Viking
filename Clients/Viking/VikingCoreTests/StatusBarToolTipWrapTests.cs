using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.Controls;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins how status-bar tooltip text is wrapped before SectionViewerControl shows it above the strip: lines break
    /// at spaces within the limit, existing line breaks stay, and an unbreakable word is never cut.
    /// </summary>
    [TestClass]
    public class StatusBarToolTipWrapTests
    {
        private static int Limit => SectionViewerControl.StatusBarToolTipLineLength;

        [TestMethod]
        public void ShortText_IsUnchanged()
        {
            Assert.AreEqual("Pen Mode is on.", SectionViewerControl.WrapStatusBarToolTip("Pen Mode is on."));
        }

        [TestMethod]
        public void LongServiceError_BreaksAtSpacesWithinTheLimit()
        {
            string error = "The segmentation service is not working: Unavailable: failed to connect to all addresses; " +
                "last error: UNKNOWN: ipv4:50.38.99.147:443: Ssl handshake failed: SSL_ERROR_SSL: error:1000007d:SSL routines:" +
                "OPENSSL_internal:CERTIFICATE_VERIFY_FAILED\nViking tries again with the next request.";

            string wrapped = SectionViewerControl.WrapStatusBarToolTip(error);
            string[] lines = wrapped.Split('\n');

            Assert.IsTrue(lines.Length > 2, wrapped);
            Assert.IsTrue(lines.All(l => l.Length <= Limit || !l.Contains(' ')), wrapped);
            Assert.AreEqual("Viking tries again with the next request.", lines.Last());
            Assert.AreEqual(error.Replace("\n", " "), wrapped.Replace("\n", " "));
        }

        [TestMethod]
        public void WordLongerThanTheLimit_IsLeftWhole()
        {
            string word = new string('x', Limit + 20);
            Assert.AreEqual("ab\n" + word + "\ncd", SectionViewerControl.WrapStatusBarToolTip("ab " + word + " cd"));
        }

        [TestMethod]
        public void CarriageReturnLineFeeds_BecomeLineFeeds()
        {
            Assert.AreEqual("a\nb", SectionViewerControl.WrapStatusBarToolTip("a\r\nb"));
        }
    }
}
