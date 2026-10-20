using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.WPF.Forms;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="AnnotationPreferencesDialogViewModel.AutoPolygonizeShimmerStyle"/>: it starts on Sweep so existing
    /// users see no change, Cancel restores the value the dialog opened with, Reset to Defaults goes back to Sweep, and an
    /// undefined value cannot be stored.
    /// </summary>
    [TestClass]
    public class AnnotationPreferencesShimmerStyleTests
    {
        [TestMethod]
        public void ShimmerStyle_DefaultsToSweep()
        {
            var vm = new AnnotationPreferencesDialogViewModel();

            Assert.AreEqual(AutoPolygonizeShimmerStyle.Sweep, AnnotationPreferencesDialogViewModel.DefaultAutoPolygonizeShimmerStyle);
            Assert.AreEqual(AutoPolygonizeShimmerStyle.Sweep, vm.AutoPolygonizeShimmerStyle);
        }

        [TestMethod]
        public void ShimmerStyle_RevertToOriginal_RestoresAppliedValue()
        {
            var vm = new AnnotationPreferencesDialogViewModel { AutoPolygonizeShimmerStyle = AutoPolygonizeShimmerStyle.Ripple };
            vm.Apply();
            vm.AutoPolygonizeShimmerStyle = AutoPolygonizeShimmerStyle.Random;

            vm.RevertToOriginal();

            Assert.AreEqual(AutoPolygonizeShimmerStyle.Ripple, vm.AutoPolygonizeShimmerStyle);
        }

        [TestMethod]
        public void ShimmerStyle_ResetToDefaults_RestoresSweep()
        {
            var vm = new AnnotationPreferencesDialogViewModel { AutoPolygonizeShimmerStyle = AutoPolygonizeShimmerStyle.Random };

            vm.ResetToDefaults();

            Assert.AreEqual(AutoPolygonizeShimmerStyle.Sweep, vm.AutoPolygonizeShimmerStyle);
        }

        [TestMethod]
        public void ShimmerStyle_UndefinedValue_FallsBackToDefault()
        {
            var vm = new AnnotationPreferencesDialogViewModel { AutoPolygonizeShimmerStyle = AutoPolygonizeShimmerStyle.Ripple };

            vm.AutoPolygonizeShimmerStyle = (AutoPolygonizeShimmerStyle)42;

            Assert.AreEqual(AutoPolygonizeShimmerStyle.Sweep, vm.AutoPolygonizeShimmerStyle);
        }
    }
}
