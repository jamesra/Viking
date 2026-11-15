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
        public void TheOutlineIsFullOnLargeTextAndFadesInProportionOnSmallText()
        {
            float full = LabelOutlineEffect.FullOutlineTextHeight;

            Assert.AreEqual((1f, 8), LabelView.OutlineFade(full), "at the threshold");
            Assert.AreEqual((1f, 8), LabelView.OutlineFade(full * 3), "above it");
            Assert.AreEqual((1f, 8), LabelView.OutlineFade(float.NaN), "height not known");

            (float opacity, int copies) = LabelView.OutlineFade(full * 0.75f);
            Assert.AreEqual(0.75f, opacity, 1e-6);
            Assert.AreEqual(8, copies, "diagonals kept down to half the threshold");

            (opacity, copies) = LabelView.OutlineFade(full * 0.25f);
            Assert.AreEqual(0.25f, opacity, 1e-6);
            Assert.AreEqual(4, copies, "small text keeps only the four straight copies");

            Assert.AreEqual(0f, LabelView.OutlineFade(-5f).Opacity, "a negative height draws no outline");
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

        [TestMethod]
        public void OnAKnownFillTheTextContrastsWithTheFillAtEverySizeAndTheOutlineIsTheOther()
        {
            Color yellow = new(255, 255, 0);
            Color navy = new(20, 20, 120);
            Color black = new((byte)0, (byte)0, (byte)0, (byte)192);
            Color white = new((byte)255, (byte)255, (byte)255, (byte)192);

            foreach (LabelOutlineStyle style in new[] { LabelOutlineStyle.BlackTextWhiteOutline, LabelOutlineStyle.WhiteTextBlackOutline })
            {
                Assert.AreEqual((black, white), LabelView.AnnotationLabelColors(DefaultLabelBlack, yellow, style), $"light fill, {style}");
                Assert.AreEqual((white, black), LabelView.AnnotationLabelColors(DefaultLabelBlack, navy, style), $"dark fill, {style}");
            }
        }

        [TestMethod]
        public void EveryAnnotationLabelsOutlineStaysOffUntilTwiceTheFullOutlineHeightThenFadesIn()
        {
            float start = LabelView.FillOutlineStartHeight;
            float full = LabelView.FillOutlineFullHeight;

            Assert.AreEqual(2f * LabelOutlineEffect.FullOutlineTextHeight, start);
            Assert.AreEqual(0f, LabelView.AnnotationOutlineOpacity(10f));
            Assert.AreEqual(0f, LabelView.AnnotationOutlineOpacity(start), 1e-6f);
            Assert.AreEqual(0.5f, LabelView.AnnotationOutlineOpacity((start + full) / 2f), 1e-6f);
            Assert.AreEqual(1f, LabelView.AnnotationOutlineOpacity(full * 2f), 1e-6f);
            Assert.AreEqual(1f, LabelView.AnnotationOutlineOpacity(float.NaN), "unknown size");
        }

        [TestMethod]
        public void ColoredTextIsOutlinedInItsOwnHueAtAnotherBrightness()
        {
            Color red = new((byte)255, (byte)0, (byte)0, (byte)192);
            Color orange = new((byte)255, (byte)165, (byte)0, (byte)192);
            Color navy = new(20, 20, 120);

            (Color text, Color outline) = LabelView.AnnotationLabelColors(red, navy, LabelOutlineStyle.BlackTextWhiteOutline);
            Assert.AreEqual(red, text, "the color carries meaning");
            Assert.AreEqual(new Color((byte)255, (byte)166, (byte)166, (byte)192), outline, "dark red gets a pale red tint");

            (text, outline) = LabelView.AnnotationLabelColors(orange, null, LabelOutlineStyle.WhiteTextBlackOutline);
            Assert.AreEqual(orange, text);
            Assert.AreEqual(new Color((byte)76, (byte)50, (byte)0, (byte)192), outline, "light orange gets a deep orange shade, with or without a fill");
        }

        [TestMethod]
        public void BlackTextWithNoFillKeepsThePreferenceColors()
        {
            Assert.AreEqual(LabelView.OutlineColors(DefaultLabelBlack, LabelOutlineStyle.WhiteTextBlackOutline),
                LabelView.AnnotationLabelColors(DefaultLabelBlack, null, LabelOutlineStyle.WhiteTextBlackOutline));
        }

        [TestMethod]
        public void AGreyHasNoHueSoItsOutlineIsWhiteOrBlack()
        {
            Assert.AreEqual(Color.White, LabelView.OutlineInHue(new Color(128, 128, 128), light: true, 255));
            Assert.AreEqual(Color.Black, LabelView.OutlineInHue(new Color(128, 128, 128), light: false, 255));
        }

        [TestMethod]
        public void LinkLabelsContrastWithTheLinkAndAreOutlinedInItsHue()
        {
            (Color text, Color outline) = WebAnnotation.ViewModel.LocationLinkView.LabelColorsFor(new Color(255, 255, 0));
            Assert.AreEqual(Color.Black, text, "black on a yellow link");
            Assert.AreEqual(new Color(255, 255, 166), outline, "outlined in pale yellow");

            (text, outline) = WebAnnotation.ViewModel.LocationLinkView.LabelColorsFor(new Color(20, 20, 120));
            Assert.AreEqual(Color.White, text, "white on a navy link");
            Assert.AreEqual(new Color(13, 13, 76), outline, "outlined in deep navy");
        }
    }
}
