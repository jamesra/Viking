using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.WPF.Forms;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the Viewer Preferences Cancel path: <see cref="ViewerPreferencesDialogViewModel.RevertToOriginal"/> must restore
    /// every value captured by <see cref="ViewerPreferencesDialogViewModel.LoadCurrentSettings"/> or the last
    /// <see cref="ViewerPreferencesDialogViewModel.Apply"/>, because the live overlay preview is re-applied from the
    /// view model when Cancel is clicked.
    /// </summary>
    [TestClass]
    public class ViewerPreferencesDialogViewModelTests
    {
        private const double Tolerance = 1e-9;

        private static ViewerPreferencesDialogViewModel LoadedViewModel()
        {
            var vm = new ViewerPreferencesDialogViewModel();
            vm.LoadCurrentSettings(
                sectionNumberOverlayEnabled: true,
                sectionNumberOverlayCount: 10,
                sectionNumberOverlayAcceleration: 20,
                sectionNumberOverlayEdge: "Left",
                sectionNumberOverlayOpacity: 0.7,
                sectionNumberOverlayMinOpacityNonCenter: 0.5,
                sectionNumberOverlayCenterMagnification: 3.0,
                pidProportionalGain: 2500,
                pidDerivativeGain: 4500,
                pidIntegralGain: 40,
                pidVelocityThreshold: 0.01,
                pidPositionThreshold: 0.02,
                textureLoadingWindow: 30,
                minTexturesToLoadFromQueue: 3,
                visibleTileSortIntervalMs: 1000,
                maxConcurrentTextureRequests: 0,
                loadAdjacentSectionTextures: true);
            return vm;
        }

        /// <summary>Sets every editable property to a value different from <see cref="LoadedViewModel"/>.</summary>
        private static void ChangeEverything(ViewerPreferencesDialogViewModel vm)
        {
            vm.SectionNumberOverlayEnabled = false;
            vm.SectionNumberOverlayCount = 20;
            vm.SectionNumberOverlayAcceleration = 60;
            vm.SectionNumberOverlayEdge = "Right";
            vm.SectionNumberOverlayOpacity = 0.2;
            vm.SectionNumberOverlayMinOpacityNonCenter = 0.1;
            vm.SectionNumberOverlayCenterMagnification = 1.5;
            vm.PidProportionalGain = 100;
            vm.PidDerivativeGain = 200;
            vm.PidIntegralGain = 10;
            vm.PidVelocityThreshold = 0.05;
            vm.PidPositionThreshold = 0.05;
            vm.TextureLoadingWindow = 50;
            vm.MinTexturesToLoadFromQueue = 10;
            vm.VisibleTileSortIntervalMs = 2000;
            vm.MaxConcurrentTextureRequests = 16;
            vm.LoadAdjacentSectionTextures = false;
        }

        private static void AssertLoadedValues(ViewerPreferencesDialogViewModel vm)
        {
            Assert.IsTrue(vm.SectionNumberOverlayEnabled);
            Assert.AreEqual(10, vm.SectionNumberOverlayCount);
            Assert.AreEqual(20, vm.SectionNumberOverlayAcceleration, Tolerance);
            Assert.AreEqual("Left", vm.SectionNumberOverlayEdge);
            Assert.AreEqual(0.7, vm.SectionNumberOverlayOpacity, Tolerance);
            Assert.AreEqual(0.5, vm.SectionNumberOverlayMinOpacityNonCenter, Tolerance);
            Assert.AreEqual(3.0, vm.SectionNumberOverlayCenterMagnification, Tolerance);
            Assert.AreEqual(2500, vm.PidProportionalGain, Tolerance);
            Assert.AreEqual(4500, vm.PidDerivativeGain, Tolerance);
            Assert.AreEqual(40, vm.PidIntegralGain, Tolerance);
            Assert.AreEqual(0.01, vm.PidVelocityThreshold, Tolerance);
            Assert.AreEqual(0.02, vm.PidPositionThreshold, Tolerance);
            Assert.AreEqual(30, vm.TextureLoadingWindow);
            Assert.AreEqual(3, vm.MinTexturesToLoadFromQueue);
            Assert.AreEqual(1000, vm.VisibleTileSortIntervalMs);
            Assert.AreEqual(0, vm.MaxConcurrentTextureRequests);
            Assert.IsTrue(vm.LoadAdjacentSectionTextures);
        }

        [TestMethod]
        public void RevertToOriginal_AfterChangingEveryProperty_RestoresEveryLoadedValue()
        {
            var vm = LoadedViewModel();
            ChangeEverything(vm);

            vm.RevertToOriginal();

            AssertLoadedValues(vm);
        }

        [TestMethod]
        public void RevertToOriginal_AfterApply_RestoresAppliedValuesNotLoadedOnes()
        {
            var vm = LoadedViewModel();
            ChangeEverything(vm);
            vm.Apply();
            double appliedMinOpacity = vm.SectionNumberOverlayMinOpacityNonCenter;
            double appliedMagnification = vm.SectionNumberOverlayCenterMagnification;

            vm.SectionNumberOverlayMinOpacityNonCenter = 0.7;
            vm.SectionNumberOverlayCenterMagnification = 4.0;
            vm.SectionNumberOverlayOpacity = 0.9;
            vm.RevertToOriginal();

            Assert.AreEqual(appliedMinOpacity, vm.SectionNumberOverlayMinOpacityNonCenter, Tolerance);
            Assert.AreEqual(appliedMagnification, vm.SectionNumberOverlayCenterMagnification, Tolerance);
            Assert.AreEqual(0.2, vm.SectionNumberOverlayOpacity, Tolerance);
        }

        /// <summary>
        /// Closing the dialog after Cancel must not undo anything more: a second revert leaves the snapshot values.
        /// </summary>
        [TestMethod]
        public void RevertToOriginal_RunTwice_LeavesLoadedValues()
        {
            var vm = LoadedViewModel();
            ChangeEverything(vm);

            vm.RevertToOriginal();
            vm.RevertToOriginal();

            AssertLoadedValues(vm);
        }
    }
}
