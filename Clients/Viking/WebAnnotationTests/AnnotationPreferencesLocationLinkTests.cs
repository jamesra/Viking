using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.WPF.Forms;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the Link visibility slider and the link end label check box in Annotation Preferences: the range the slider
    /// accepts, and that Cancel and Reset to Defaults treat both like the other preferences.
    /// </summary>
    [TestClass]
    public class AnnotationPreferencesLocationLinkTests
    {
        [TestMethod]
        public void NewViewModel_StartsAtTheStoredDefaults()
        {
            var vm = new AnnotationPreferencesDialogViewModel();
            Assert.AreEqual(AnnotationPreferencesDialogViewModel.DefaultLocationLinkVisibilityPercent, vm.LocationLinkVisibilityPercent);
            Assert.IsTrue(vm.LocationLinkLabels);
        }

        [TestMethod]
        public void LocationLinkVisibilityPercent_ClampsToTenThroughOneHundred()
        {
            var vm = new AnnotationPreferencesDialogViewModel();
            vm.LocationLinkVisibilityPercent = 0;
            Assert.AreEqual(10, vm.LocationLinkVisibilityPercent);
            vm.LocationLinkVisibilityPercent = 150;
            Assert.AreEqual(100, vm.LocationLinkVisibilityPercent);
        }

        [TestMethod]
        public void RevertToOriginal_RestoresTheAppliedLinkSettings()
        {
            var vm = new AnnotationPreferencesDialogViewModel();
            vm.LocationLinkVisibilityPercent = 85;
            vm.LocationLinkLabels = false;
            vm.Apply();

            vm.LocationLinkVisibilityPercent = 20;
            vm.LocationLinkLabels = true;
            vm.RevertToOriginal();

            Assert.AreEqual(85, vm.LocationLinkVisibilityPercent);
            Assert.IsFalse(vm.LocationLinkLabels);
        }

        [TestMethod]
        public void ResetToDefaults_RestoresTheLinkDefaults()
        {
            var vm = new AnnotationPreferencesDialogViewModel();
            vm.LocationLinkVisibilityPercent = 25;
            vm.LocationLinkLabels = false;
            vm.ResetToDefaults();

            Assert.AreEqual(AnnotationPreferencesDialogViewModel.DefaultLocationLinkVisibilityPercent, vm.LocationLinkVisibilityPercent);
            Assert.IsTrue(vm.LocationLinkLabels);
        }

        /// <summary>The dialog's default must be the one WebAnnotation stores, or Reset to Defaults would disagree with a new install.</summary>
        [TestMethod]
        public void TheDialogDefaultMatchesTheStoredDefault() =>
            Assert.AreEqual(WebAnnotation.Global.AnnotationSettings.DEFAULT_LOCATION_LINK_VISIBILITY_PERCENT,
                AnnotationPreferencesDialogViewModel.DefaultLocationLinkVisibilityPercent);
    }
}
