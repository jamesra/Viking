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
        private int _previousWaist;
        private bool _previousUnderMarkers;

        [TestInitialize]
        public void RememberStoredValues()
        {
            _previousWaist = AnnotationGlobal.AnnotationSettings.LocationLinkWaistPixels;
            _previousUnderMarkers = AnnotationGlobal.AnnotationSettings.LocationLinkDrawUnderMarkers;
        }

        [TestCleanup]
        public void RestoreStoredValues()
        {
            AnnotationGlobal.AnnotationSettings.LocationLinkWaistPixels = _previousWaist;
            AnnotationGlobal.AnnotationSettings.LocationLinkDrawUnderMarkers = _previousUnderMarkers;
        }

        [TestMethod]
        public void LinkWaistWidth_ReachesTheStoredSettingBeforeApply()
        {
            var vm = new AnnotationPreferencesDialogViewModel();
            vm.LocationLinkWaistPixels = 11;

            AnnotationMenu.ApplyLiveSettings(vm, nameof(vm.LocationLinkWaistPixels));

            Assert.AreEqual(11, AnnotationGlobal.AnnotationSettings.LocationLinkWaistPixels);
        }

        [TestMethod]
        public void LinksUnderMarkers_ReachesTheStoredSettingBeforeApply()
        {
            var vm = new AnnotationPreferencesDialogViewModel();
            vm.LocationLinkDrawUnderMarkers = !_previousUnderMarkers;

            AnnotationMenu.ApplyLiveSettings(vm, nameof(vm.LocationLinkDrawUnderMarkers));

            Assert.AreEqual(!_previousUnderMarkers, AnnotationGlobal.AnnotationSettings.LocationLinkDrawUnderMarkers);
        }

        [TestMethod]
        public void AChangedProperty_WritesOnlyItsOwnSetting()
        {
            AnnotationGlobal.AnnotationSettings.LocationLinkWaistPixels = 6;
            var vm = new AnnotationPreferencesDialogViewModel();
            vm.LocationLinkWaistPixels = 20;

            AnnotationMenu.ApplyLiveSettings(vm, nameof(vm.LocationLinkDrawUnderMarkers));

            Assert.AreEqual(6, AnnotationGlobal.AnnotationSettings.LocationLinkWaistPixels);
        }
    }
}
