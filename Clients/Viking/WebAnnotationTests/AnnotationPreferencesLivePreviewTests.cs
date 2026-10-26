using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation;
using WebAnnotation.WPF.Forms;
using AnnotationGlobal = WebAnnotation.Global;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the Annotation Preferences live preview: a change to a live setting reaches
    /// <see cref="AnnotationGlobal.AnnotationSettings"/> before Apply, and a change to another property writes
    /// nothing else. The stored values are put back after each test.
    /// </summary>
    [TestClass]
    public class AnnotationPreferencesLivePreviewTests
    {
        private int _previousVisibility;
        private bool _previousOutline;

        [TestInitialize]
        public void RememberStoredValues()
        {
            _previousVisibility = AnnotationGlobal.AnnotationSettings.LocationLinkVisibilityPercent;
            _previousOutline = AnnotationGlobal.AnnotationSettings.LabelOutlineEnabled;
        }

        [TestCleanup]
        public void RestoreStoredValues()
        {
            AnnotationGlobal.AnnotationSettings.LocationLinkVisibilityPercent = _previousVisibility;
            AnnotationGlobal.AnnotationSettings.LabelOutlineEnabled = _previousOutline;
        }

        [TestMethod]
        public void LinkVisibility_ReachesTheStoredSettingBeforeApply()
        {
            var vm = new AnnotationPreferencesDialogViewModel();
            vm.LocationLinkVisibilityPercent = 33;

            AnnotationMenu.ApplyLiveSettings(vm, nameof(vm.LocationLinkVisibilityPercent));

            Assert.AreEqual(33, AnnotationGlobal.AnnotationSettings.LocationLinkVisibilityPercent);
        }

        [TestMethod]
        public void LabelOutline_ReachesTheStoredSettingBeforeApply()
        {
            var vm = new AnnotationPreferencesDialogViewModel();
            vm.LabelOutlineEnabled = !_previousOutline;

            AnnotationMenu.ApplyLiveSettings(vm, nameof(vm.LabelOutlineEnabled));

            Assert.AreEqual(!_previousOutline, AnnotationGlobal.AnnotationSettings.LabelOutlineEnabled);
        }

        [TestMethod]
        public void AChangedProperty_WritesOnlyItsOwnSetting()
        {
            AnnotationGlobal.AnnotationSettings.LocationLinkVisibilityPercent = 40;
            var vm = new AnnotationPreferencesDialogViewModel();
            vm.LocationLinkVisibilityPercent = 90;

            AnnotationMenu.ApplyLiveSettings(vm, nameof(vm.LabelOutlineEnabled));

            Assert.AreEqual(40, AnnotationGlobal.AnnotationSettings.LocationLinkVisibilityPercent);
        }
    }
}
