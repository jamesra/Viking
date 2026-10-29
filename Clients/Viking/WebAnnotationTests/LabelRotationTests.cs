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
            vm.LoadCurrentSettings(10, 5, 5, 2.5f, 24, 4, 0.5, 3, 3, 0.5, 0.5, 0.33, 0.5, 1, 5, 12, 10, 0.0025, 0.002,
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

        /// <summary>
        /// Reset to Defaults then Cancel, as Viking's and Jotunn's dialogs run them: the reset writes the stored default
        /// (here only the rotation, standing in for <c>Global.AnnotationSettings.ResetToDefaults</c>, which would write every
        /// user setting) and relays labels once; the view model's reset that follows (an empty-name change, which the
        /// dialogs pass to the live settings) finds nothing left to relay; Cancel's revert relays them back, and the
        /// CancelClicked pass that follows (null) finds nothing changed. Ported from Legacy e3745889, where the view
        /// model's reset alone relays because Legacy's dialog does not write the stored value first.
        /// </summary>
        [TestMethod]
        public void ResetToDefaultsRelaysLabelsAndCancelRelaysThemBack()
        {
            AnnotationGlobal.AnnotationSettings.LabelRotation = 0.2;
            AnnotationPreferencesDialogViewModel vm = new();
            vm.LoadCurrentSettings(10, 5, 5, 2.5f, 24, 4, 0.5, 3, 3, 0.5, 0.5, 0.33, 0.5, 1, 5, 12, 10, 0.0025, 0.002,
                labelRotation: AnnotationGlobal.AnnotationSettings.LabelRotation);
            int relayouts = 0;
            vm.PropertyChanged += (_, e) => { if (AnnotationMenu.ApplyLiveLabelRotation(vm, e.PropertyName)) relayouts++; };

            if (AnnotationMenu.ResetStoredSettingsAndRelayLabels(ResetStoredRotation))
                relayouts++;
            vm.ResetToDefaults();

            Assert.AreEqual(1, relayouts, "Reset to Defaults relays labels once");
            Assert.AreEqual(AnnotationGlobal.AnnotationSettings.DEFAULT_LABEL_ROTATION, AnnotationGlobal.AnnotationSettings.LabelRotation, 1e-12);

            vm.RevertToOriginal();

            Assert.AreEqual(2, relayouts, "Cancel after a reset relays labels back");
            Assert.AreEqual(0.2, AnnotationGlobal.AnnotationSettings.LabelRotation, 1e-12);
            Assert.IsFalse(AnnotationMenu.ApplyLiveLabelRotation(vm, null), "The CancelClicked pass finds the rotation already restored");
        }

        [TestMethod]
        public void ResetToDefaultsAtTheDefaultRotationDoesNotRelay()
        {
            AnnotationGlobal.AnnotationSettings.LabelRotation = AnnotationGlobal.AnnotationSettings.DEFAULT_LABEL_ROTATION;
            AnnotationPreferencesDialogViewModel vm = new();
            int relayouts = 0;
            vm.PropertyChanged += (_, e) => { if (AnnotationMenu.ApplyLiveLabelRotation(vm, e.PropertyName)) relayouts++; };

            if (AnnotationMenu.ResetStoredSettingsAndRelayLabels(ResetStoredRotation))
                relayouts++;
            vm.ResetToDefaults();

            Assert.AreEqual(0, relayouts);
        }

        /// <summary>The rotation part of <c>Global.AnnotationSettings.ResetToDefaults</c>.</summary>
        private static void ResetStoredRotation() =>
            AnnotationGlobal.AnnotationSettings.LabelRotation = AnnotationGlobal.AnnotationSettings.DEFAULT_LABEL_ROTATION;

        /// <summary>
        /// The live relayout runs while no volume is open (and in tests) without touching the overlay. Other tests in this
        /// assembly leave a volume in <see cref="Viking.UI.State.volume"/>, so it is cleared here and put back.
        /// </summary>
        [TestMethod]
        public void RelayoutWithoutAVolumeDoesNothing()
        {
            var savedVolume = Viking.UI.State.volume;
            Viking.UI.State.volume = null;
            try
            {
                Assert.AreEqual(0, AnnotationMenu.RelayoutLoadedLabels(System.Threading.CancellationToken.None));
            }
            finally
            {
                Viking.UI.State.volume = savedVolume;
            }
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
