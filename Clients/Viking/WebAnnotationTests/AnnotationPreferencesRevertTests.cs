using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.WPF.Forms;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the Annotation Preferences Cancel path, which the Cancel button, Escape and the window's close box all run:
    /// <see cref="AnnotationPreferencesDialogViewModel.RevertToOriginal"/> restores the values from the last
    /// <see cref="AnnotationPreferencesDialogViewModel.Apply"/>, and running it again changes nothing.
    /// </summary>
    [TestClass]
    public class AnnotationPreferencesRevertTests
    {
        private const double Tolerance = 1e-9;

        /// <summary>A view model whose snapshot (what Cancel restores) was taken by Apply after editing.</summary>
        private static AnnotationPreferencesDialogViewModel AppliedViewModel()
        {
            var vm = new AnnotationPreferencesDialogViewModel();
            vm.NumSectionsInMemory = 12;
            vm.PolygonOpacityParentless = 0.4;
            vm.CircleOpacityWithParent = 0.6;
            vm.AutoPolygonizeMaxDownsample = 2.5;
            vm.SegmentationUseMaskInput = true;
            vm.Apply();
            return vm;
        }

        /// <summary>Live-preview edits made after Apply, which Cancel must undo.</summary>
        private static void PreviewChanges(AnnotationPreferencesDialogViewModel vm)
        {
            vm.NumSectionsInMemory = 30;
            vm.PolygonOpacityParentless = 0.1;
            vm.CircleOpacityWithParent = 0.9;
            vm.AutoPolygonizeMaxDownsample = 4.0;
            vm.SegmentationUseMaskInput = false;
        }

        private static void AssertAppliedValues(AnnotationPreferencesDialogViewModel vm)
        {
            Assert.AreEqual(12, vm.NumSectionsInMemory);
            Assert.AreEqual(0.4, vm.PolygonOpacityParentless, Tolerance);
            Assert.AreEqual(0.6, vm.CircleOpacityWithParent, Tolerance);
            Assert.AreEqual(2.5, vm.AutoPolygonizeMaxDownsample, Tolerance);
            Assert.IsTrue(vm.SegmentationUseMaskInput);
        }

        [TestMethod]
        public void RevertToOriginal_AfterApplyThenPreview_RestoresAppliedValues()
        {
            var vm = AppliedViewModel();
            PreviewChanges(vm);

            vm.RevertToOriginal();

            AssertAppliedValues(vm);
        }

        [TestMethod]
        public void RevertToOriginal_RunTwice_LeavesAppliedValues()
        {
            var vm = AppliedViewModel();
            PreviewChanges(vm);

            vm.RevertToOriginal();
            vm.RevertToOriginal();

            AssertAppliedValues(vm);
        }
    }
}
