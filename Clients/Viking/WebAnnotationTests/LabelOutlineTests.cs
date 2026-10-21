using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using System.Runtime.Serialization;
using VikingXNAGraphics;
using WebAnnotation.WPF.Forms;

namespace WebAnnotationTests
{
    /// <summary>
    /// Annotation text can be drawn with a thin outline for readability: black text with a white outline, or white text
    /// with a black outline. Pins the colors each style uses and the preferences dialog's checkbox and combo box.
    /// </summary>
    [TestClass]
    public class LabelOutlineTests
    {
        private static readonly Color DefaultLabelBlack = new((byte)0, (byte)0, (byte)0, (byte)192);

        [TestMethod]
        public void BlackTextWhiteOutlineLeavesTheTextAloneAndOutlinesItInWhite()
        {
            (Color text, Color outline) = LabelView.OutlineColors(DefaultLabelBlack, LabelOutlineStyle.BlackTextWhiteOutline);

            Assert.AreEqual(DefaultLabelBlack, text);
            Assert.AreEqual(new Color((byte)255, (byte)255, (byte)255, (byte)192), outline, "the outline keeps the text alpha");
        }

        [TestMethod]
        public void WhiteTextBlackOutlineTurnsBlackTextWhiteAndOutlinesItInBlack()
        {
            (Color text, Color outline) = LabelView.OutlineColors(DefaultLabelBlack, LabelOutlineStyle.WhiteTextBlackOutline);

            Assert.AreEqual(new Color((byte)255, (byte)255, (byte)255, (byte)192), text);
            Assert.AreEqual(DefaultLabelBlack, outline);
        }

        /// <summary>Orange marks an unverified terminal and red a parent ID, so swapping to white text must not erase that.</summary>
        [TestMethod]
        public void ColoredTextKeepsItsColorInBothStyles()
        {
            Color orange = new((byte)255, (byte)165, (byte)0, (byte)192);

            Assert.AreEqual(orange, LabelView.OutlineColors(orange, LabelOutlineStyle.BlackTextWhiteOutline).Text);
            Assert.AreEqual(orange, LabelView.OutlineColors(orange, LabelOutlineStyle.WhiteTextBlackOutline).Text);
        }

        [TestMethod]
        public void OffChangesNothing()
        {
            (Color text, Color outline) = LabelView.OutlineColors(DefaultLabelBlack, LabelOutlineStyle.Off);

            Assert.AreEqual(DefaultLabelBlack, text);
            Assert.AreEqual(DefaultLabelBlack, outline);
        }

        [TestMethod]
        public void OnlyLabelsThatAskForAnOutlineAreOutlined()
        {
            // The constructor needs a loaded font, so look at the default on an instance made without it.
            LabelView label = (LabelView)FormatterServices.GetUninitializedObject(typeof(LabelView));
            Assert.IsFalse(label.Outlined, "outlining is opt-in, so UI captions are unchanged");
            label.Outlined = true;
            Assert.IsTrue(label.Outlined);
        }

        [TestMethod]
        public void ThePreferencesDefaultToAnOutlineWithBlackTextAndAWhiteBorder()
        {
            AnnotationPreferencesDialogViewModel vm = new();

            Assert.IsTrue(vm.LabelOutlineEnabled);
            Assert.IsFalse(vm.LabelOutlineWhiteText);
            Assert.AreEqual(0, vm.LabelOutlineColorChoice);
        }

        [TestMethod]
        public void TheComboBoxChoiceAndTheSwapSettingStayInStep()
        {
            AnnotationPreferencesDialogViewModel vm = new();
            int changes = 0;
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.LabelOutlineColorChoice)) changes++; };

            vm.LabelOutlineColorChoice = 1;
            Assert.IsTrue(vm.LabelOutlineWhiteText);
            Assert.AreEqual(1, changes, "the combo box is told when the setting changes");

            vm.LabelOutlineWhiteText = false;
            Assert.AreEqual(0, vm.LabelOutlineColorChoice);
        }

        [TestMethod]
        public void CancelUndoesAnOutlineChangeMadeAfterApply()
        {
            AnnotationPreferencesDialogViewModel vm = new();
            vm.LabelOutlineEnabled = false;
            vm.LabelOutlineWhiteText = true;
            vm.Apply();

            vm.LabelOutlineEnabled = true;
            vm.LabelOutlineWhiteText = false;
            vm.RevertToOriginal();

            Assert.IsFalse(vm.LabelOutlineEnabled);
            Assert.IsTrue(vm.LabelOutlineWhiteText);
        }

        [TestMethod]
        public void ResetToDefaultsTurnsTheOutlineOnWithBlackText()
        {
            AnnotationPreferencesDialogViewModel vm = new();
            vm.LabelOutlineEnabled = false;
            vm.LabelOutlineWhiteText = true;

            vm.ResetToDefaults();

            Assert.IsTrue(vm.LabelOutlineEnabled);
            Assert.IsFalse(vm.LabelOutlineWhiteText);
        }
    }
}
