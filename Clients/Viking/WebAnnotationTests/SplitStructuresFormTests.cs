using System;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins how the Split structure dialog's KeepID and SplitID setters and its Flip button move IDs between the two text boxes.
    /// The boxes are only given text that is not a number, so the dialog's TextChanged validation never looks a location up
    /// in <c>Store</c> (which would start the annotation stores and call the server).
    /// </summary>
    [TestClass]
    public class SplitStructuresFormTests
    {
        private static TextBox Box(Form form, string name) => (TextBox)form.Controls.Find(name, true)[0];

        [TestMethod]
        public void ClearingSplitIdEmptiesTheSplitBoxAndLeavesKeepAlone()
        {
            using SplitStructuresForm form = new();
            Box(form, "textKeepID").Text = "keep";
            Box(form, "textSplitID").Text = "split";

            form.SplitID = -1;

            Assert.AreEqual("", Box(form, "textSplitID").Text);
            Assert.AreEqual("keep", Box(form, "textKeepID").Text);
        }

        [TestMethod]
        public void ClearingKeepIdEmptiesTheKeepBoxAndLeavesSplitAlone()
        {
            using SplitStructuresForm form = new();
            Box(form, "textKeepID").Text = "keep";
            Box(form, "textSplitID").Text = "split";

            form.KeepID = 0;

            Assert.AreEqual("", Box(form, "textKeepID").Text);
            Assert.AreEqual("split", Box(form, "textSplitID").Text);
        }

        /// <summary>
        /// Regression: with the Keep box empty, Flip left the Split box's text in place because the SplitID setter cleared the
        /// Keep box instead. With a real Split ID such as 200, both boxes then read 200 and Split was disabled.
        /// </summary>
        [TestMethod]
        public void FlipWithEmptyKeepBoxClearsTheSplitBox()
        {
            using SplitStructuresForm form = new();
            Box(form, "textSplitID").Text = "not an id";

            typeof(SplitStructuresForm)
                .GetMethod("btnFlip_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, [null, EventArgs.Empty]);

            Assert.AreEqual("", Box(form, "textSplitID").Text);
            Assert.AreEqual("", Box(form, "textKeepID").Text);
        }
    }
}
