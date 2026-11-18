using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.WPF.Forms;
using AnnotationSettings = WebAnnotation.Global.AnnotationSettings;

namespace WebAnnotationTests
{
    /// <summary>
    /// The Auto-Segmentation Buttons preferences: the dialog clamps to the same ranges the stored settings do, and Cancel and
    /// Reset to Defaults cover them.
    /// </summary>
    [TestClass]
    public class AnnotationPreferencesProposalButtonsTests
    {
        [TestMethod]
        public void RangesMatchTheStoredSettings()
        {
            Assert.AreEqual(AnnotationSettings.MIN_AUTOPOLYGONIZE_BUTTON_SIZE_PIXELS, AnnotationPreferencesDialogViewModel.MinAutoPolygonizeButtonSizePixels);
            Assert.AreEqual(AnnotationSettings.MAX_AUTOPOLYGONIZE_BUTTON_SIZE_PIXELS, AnnotationPreferencesDialogViewModel.MaxAutoPolygonizeButtonSizePixels);
            Assert.AreEqual(AnnotationSettings.MIN_AUTOPOLYGONIZE_BUTTON_MIN_PIXELS, AnnotationPreferencesDialogViewModel.MinAutoPolygonizeButtonMinPixels);
            Assert.AreEqual(AnnotationSettings.MAX_AUTOPOLYGONIZE_BUTTON_MIN_PIXELS, AnnotationPreferencesDialogViewModel.MaxAutoPolygonizeButtonMinPixels);
            Assert.AreEqual(AnnotationSettings.MIN_AUTOPOLYGONIZE_BUTTON_OPACITY, AnnotationPreferencesDialogViewModel.MinAutoPolygonizeButtonOpacity);
            Assert.AreEqual(AnnotationSettings.MAX_AUTOPOLYGONIZE_BUTTON_OPACITY, AnnotationPreferencesDialogViewModel.MaxAutoPolygonizeButtonOpacity);
            Assert.AreEqual(AnnotationSettings.MIN_AUTOPOLYGONIZE_BUTTON_CONFIRM_DELAY_MS, AnnotationPreferencesDialogViewModel.MinAutoPolygonizeButtonConfirmDelayMs);
            Assert.AreEqual(AnnotationSettings.MAX_AUTOPOLYGONIZE_BUTTON_CONFIRM_DELAY_MS, AnnotationPreferencesDialogViewModel.MaxAutoPolygonizeButtonConfirmDelayMs);
            Assert.AreEqual(AnnotationSettings.MIN_AUTOPOLYGONIZE_BUTTON_CONFIRM_TIMEOUT_SECONDS, AnnotationPreferencesDialogViewModel.MinAutoPolygonizeButtonConfirmTimeoutSeconds);
            Assert.AreEqual(AnnotationSettings.MAX_AUTOPOLYGONIZE_BUTTON_CONFIRM_TIMEOUT_SECONDS, AnnotationPreferencesDialogViewModel.MaxAutoPolygonizeButtonConfirmTimeoutSeconds);
        }

        [TestMethod]
        public void ValuesOutsideTheRangeAreClamped()
        {
            var vm = new AnnotationPreferencesDialogViewModel
            {
                AutoPolygonizeButtonSizePixels = 1000,
                AutoPolygonizeButtonMinPixels = 0,
                AutoPolygonizeButtonOpacity = 0,
                AutoPolygonizeButtonConfirmDelayMs = 99999,
                AutoPolygonizeButtonConfirmTimeoutSeconds = -1,
            };

            Assert.AreEqual(128, vm.AutoPolygonizeButtonSizePixels);
            Assert.AreEqual(6, vm.AutoPolygonizeButtonMinPixels);
            Assert.AreEqual(0.3, vm.AutoPolygonizeButtonOpacity, 1e-9);
            Assert.AreEqual(2000, vm.AutoPolygonizeButtonConfirmDelayMs);
            Assert.AreEqual(1, vm.AutoPolygonizeButtonConfirmTimeoutSeconds, 1e-9);
        }

        [TestMethod]
        public void RevertToOriginal_RestoresTheAppliedValues()
        {
            var vm = new AnnotationPreferencesDialogViewModel { AutoPolygonizeButtonsEnabled = false, AutoPolygonizeButtonConfirmDelayMs = 800 };
            vm.Apply();
            vm.AutoPolygonizeButtonsEnabled = true;
            vm.AutoPolygonizeButtonConfirmDelayMs = 300;

            vm.RevertToOriginal();

            Assert.IsFalse(vm.AutoPolygonizeButtonsEnabled);
            Assert.AreEqual(800, vm.AutoPolygonizeButtonConfirmDelayMs);
        }

        [TestMethod]
        public void ResetToDefaults_RestoresTheShippedValues()
        {
            var vm = new AnnotationPreferencesDialogViewModel { AutoPolygonizeButtonsEnabled = false, AutoPolygonizeButtonSizePixels = 40 };

            vm.ResetToDefaults();

            Assert.IsTrue(vm.AutoPolygonizeButtonsEnabled);
            Assert.AreEqual(AnnotationSettings.DEFAULT_AUTOPOLYGONIZE_BUTTON_SIZE_PIXELS, vm.AutoPolygonizeButtonSizePixels);
            Assert.AreEqual(AnnotationSettings.DEFAULT_AUTOPOLYGONIZE_BUTTON_CONFIRM_DELAY_MS, vm.AutoPolygonizeButtonConfirmDelayMs);
            Assert.AreEqual(AnnotationSettings.DEFAULT_AUTOPOLYGONIZE_BUTTON_CONFIRM_TIMEOUT_SECONDS, vm.AutoPolygonizeButtonConfirmTimeoutSeconds, 1e-9);
        }
    }
}
