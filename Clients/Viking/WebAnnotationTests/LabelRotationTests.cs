using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation;
using WebAnnotation.WPF.Forms;
using AnnotationGlobal = WebAnnotation.Global;

namespace WebAnnotationTests
{
    /// <summary>
    /// The Label rotation preference (Annotation Preferences, Visuals, Text): how it maps to the rotation gain
    /// <see cref="LabelLayout.Fit(LabelContent, double)"/> takes, the dialog's readout, and its live preview, Cancel and
    /// reset. The stored value is put back after each test.
    /// </summary>
    [TestClass]
    public class LabelRotationTests
    {
        private double _previousRotation;

        [TestInitialize]
        public void RememberStoredValue() => _previousRotation = AnnotationGlobal.AnnotationSettings.LabelRotation;

        [TestCleanup]
        public void RestoreStoredValue() => AnnotationGlobal.AnnotationSettings.LabelRotation = _previousRotation;

        [TestMethod]
        public void NeverDefaultAndFreelyMapToNoRotationHalfAndZeroGain()
        {
            AnnotationGlobal.AnnotationSettings.LabelRotation = 0;
            Assert.IsTrue(double.IsPositiveInfinity(AnnotationGlobal.AnnotationSettings.LabelRotationGain), "Never keeps labels level");

            AnnotationGlobal.AnnotationSettings.LabelRotation = 0.5;
            Assert.AreEqual(0.5, AnnotationGlobal.AnnotationSettings.LabelRotationGain, 1e-12);
            Assert.AreEqual(LabelLayout.DefaultRotationGain, AnnotationGlobal.AnnotationSettings.LabelRotationGain, 1e-12);

            AnnotationGlobal.AnnotationSettings.LabelRotation = 1;
            Assert.AreEqual(0, AnnotationGlobal.AnnotationSettings.LabelRotationGain, 1e-12);
        }

        [TestMethod]
        public void TheStoredSettingIsClampedToNeverAndFreely()
        {
            AnnotationGlobal.AnnotationSettings.LabelRotation = 1.7;
            Assert.AreEqual(1, AnnotationGlobal.AnnotationSettings.LabelRotation);

            AnnotationGlobal.AnnotationSettings.LabelRotation = -0.2;
            Assert.AreEqual(0, AnnotationGlobal.AnnotationSettings.LabelRotation);

            AnnotationGlobal.AnnotationSettings.LabelRotation = double.NaN;
            Assert.AreEqual(AnnotationGlobal.AnnotationSettings.DEFAULT_LABEL_ROTATION, AnnotationGlobal.AnnotationSettings.LabelRotation);
        }

        [TestMethod]
        public void TheDialogDefaultMatchesTheStoredDefault()
        {
            Assert.AreEqual(AnnotationGlobal.AnnotationSettings.DEFAULT_LABEL_ROTATION, AnnotationPreferencesDialogViewModel.DefaultLabelRotation);
            Assert.AreEqual(AnnotationPreferencesDialogViewModel.DefaultLabelRotation, new AnnotationPreferencesDialogViewModel().LabelRotation);
        }

        [TestMethod]
        public void TheReadoutStatesTheRuleAtEachEndAndBetween()
        {
            Assert.AreEqual("Labels always stay level.", AnnotationPreferencesDialogViewModel.DescribeLabelRotation(0));
            Assert.AreEqual("Rotates when text grows ≥ 35% at 45°, ≥ 50% at 90°.", AnnotationPreferencesDialogViewModel.DescribeLabelRotation(0.5));
            Assert.AreEqual("Rotates when text grows ≥ 70% at 45°, ≥ 99% at 90°.", AnnotationPreferencesDialogViewModel.DescribeLabelRotation(0.01));
            Assert.AreEqual("Labels tilt whenever tilted text is any larger than level text.", AnnotationPreferencesDialogViewModel.DescribeLabelRotation(1));
        }

        [TestMethod]
        public void TheReadoutFollowsTheSlider()
        {
            AnnotationPreferencesDialogViewModel vm = new();
            int changes = 0;
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.LabelRotationReadout)) changes++; };

            vm.LabelRotation = 0;

            Assert.AreEqual(1, changes);
            Assert.AreEqual("Labels always stay level.", vm.LabelRotationReadout);
        }

        [TestMethod]
        public void SliderValuesAreClampedAndRounded()
        {
            AnnotationPreferencesDialogViewModel vm = new() { LabelRotation = 1.4 };
            Assert.AreEqual(1, vm.LabelRotation);

            vm.LabelRotation = -1;
            Assert.AreEqual(0, vm.LabelRotation);

            vm.LabelRotation = 0.333333;
            Assert.AreEqual(0.33, vm.LabelRotation);
        }

        [TestMethod]
        public void LabelRotation_ReachesTheStoredSettingBeforeApply()
        {
            AnnotationGlobal.AnnotationSettings.LabelRotation = 0.5;
            AnnotationPreferencesDialogViewModel vm = new() { LabelRotation = 0.2 };

            AnnotationMenu.ApplyLiveSettings(vm, nameof(vm.LabelRotation));

            Assert.AreEqual(0.2, AnnotationGlobal.AnnotationSettings.LabelRotation, 1e-12);
        }

        [TestMethod]
        public void CancelPutsTheStoredRotationBack()
        {
            AnnotationGlobal.AnnotationSettings.LabelRotation = 0.7;
            AnnotationPreferencesDialogViewModel vm = new();
            vm.LoadCurrentSettings(10, 5, 5, 2.5f, 24, 4, 0.5, 3, 3, 0.5, 0.5, 0.33, 0.5, 1, 5, 0.03, 8, 6, 0.5, false, 10, 2,
                labelRotation: AnnotationGlobal.AnnotationSettings.LabelRotation);

            vm.LabelRotation = 0;
            AnnotationMenu.ApplyLiveSettings(vm, nameof(vm.LabelRotation));
            Assert.AreEqual(0, AnnotationGlobal.AnnotationSettings.LabelRotation);

            // The dialog's Cancel and title-bar X revert the view model, then reapply every live setting (null). Only this
            // setting is reapplied here: the null path also redraws, which needs the overlay a test cannot build.
            vm.RevertToOriginal();
            AnnotationMenu.ApplyLiveSettings(vm, nameof(vm.LabelRotation));

            Assert.AreEqual(0.7, vm.LabelRotation, 1e-12);
            Assert.AreEqual(0.7, AnnotationGlobal.AnnotationSettings.LabelRotation, 1e-12);
        }

        /// <summary>The live relayout runs while no volume is open (and in tests) without touching the overlay.</summary>
        [TestMethod]
        public void RelayoutWithoutAVolumeDoesNothing()
        {
            if (Viking.UI.State.volume != null)
                Assert.Inconclusive("Another test opened a volume");

            Assert.AreEqual(0, AnnotationMenu.RelayoutLoadedLabels(System.Threading.CancellationToken.None));
        }

        [TestMethod]
        public void CancelUndoesARotationChangeMadeAfterApply()
        {
            AnnotationPreferencesDialogViewModel vm = new() { LabelRotation = 0.9 };
            vm.Apply();

            vm.LabelRotation = 0.1;
            vm.RevertToOriginal();

            Assert.AreEqual(0.9, vm.LabelRotation, 1e-12);
        }

        [TestMethod]
        public void ResetToDefaultsPutsTheSliderInTheMiddle()
        {
            AnnotationPreferencesDialogViewModel vm = new() { LabelRotation = 0 };

            vm.ResetToDefaults();

            Assert.AreEqual(AnnotationPreferencesDialogViewModel.DefaultLabelRotation, vm.LabelRotation);
            Assert.AreEqual(AnnotationPreferencesDialogViewModel.DescribeLabelRotation(0.5), vm.LabelRotationReadout);
        }
    }
}
