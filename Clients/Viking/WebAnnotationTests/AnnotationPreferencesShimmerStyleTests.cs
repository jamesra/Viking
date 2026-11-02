using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.WPF.Forms;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="AnnotationPreferencesDialogViewModel.AutoPolygonizeShimmerStyle"/>: it starts on Random, Cancel
    /// restores the value the dialog opened with, Reset to Defaults goes back to Random, and an
    /// undefined value cannot be stored.
    /// </summary>
    [TestClass]
    public class AnnotationPreferencesShimmerStyleTests
    {
        [TestMethod]
        public void ShimmerStyle_DefaultsToRandom()
        {
            var vm = new AnnotationPreferencesDialogViewModel();

            Assert.AreEqual(AutoPolygonizeShimmerStyle.Random, AnnotationPreferencesDialogViewModel.DefaultAutoPolygonizeShimmerStyle);
            Assert.AreEqual(AutoPolygonizeShimmerStyle.Random, vm.AutoPolygonizeShimmerStyle);
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
        public void ShimmerStyle_ResetToDefaults_RestoresRandom()
        {
            var vm = new AnnotationPreferencesDialogViewModel { AutoPolygonizeShimmerStyle = AutoPolygonizeShimmerStyle.Sweep };

            vm.ResetToDefaults();

            Assert.AreEqual(AutoPolygonizeShimmerStyle.Random, vm.AutoPolygonizeShimmerStyle);
        }

        [TestMethod]
        public void ShimmerStyle_UndefinedValue_FallsBackToDefault()
        {
            var vm = new AnnotationPreferencesDialogViewModel { AutoPolygonizeShimmerStyle = AutoPolygonizeShimmerStyle.Ripple };

            vm.AutoPolygonizeShimmerStyle = (AutoPolygonizeShimmerStyle)42;

            Assert.AreEqual(AutoPolygonizeShimmerStyle.Random, vm.AutoPolygonizeShimmerStyle);
        }
    }
}
