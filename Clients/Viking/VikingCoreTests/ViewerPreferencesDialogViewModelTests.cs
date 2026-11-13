using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Viking.UI.WPF.Forms;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the Viewer Preferences Cancel path: <see cref="ViewerPreferencesDialogViewModel.RevertToOriginal"/> must restore
    /// every value captured by <see cref="ViewerPreferencesDialogViewModel.LoadCurrentSettings"/> or the last
    /// <see cref="ViewerPreferencesDialogViewModel.Apply"/>, because the live overlay preview is re-applied from the
    /// view model when Cancel is clicked. Also pins what Reset to Defaults restores against the shipped settings defaults.
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
        /// The texture loader runs 32 workers, so the dialog stops at 32: a typed larger value and a value saved by an
        /// older Viking (which allowed up to 256) both become 32, and Cancel restores the clamped saved value.
        /// </summary>
        [TestMethod]
        public void MaxConcurrentTextureRequests_AboveThirtyTwo_IsClampedOnEditAndLoad()
        {
            Assert.AreEqual(32, ViewerPreferencesDialogViewModel.MaxMaxConcurrentTextureRequests);
            var vm = LoadedViewModel();
            Assert.AreEqual(32, vm.MaxConcurrentTextureRequestsMaximum);

            vm.MaxConcurrentTextureRequests = 33;
            Assert.AreEqual(32, vm.MaxConcurrentTextureRequests);
            vm.MaxConcurrentTextureRequests = 31;
            Assert.AreEqual(31, vm.MaxConcurrentTextureRequests);

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
                maxConcurrentTextureRequests: 256,
                loadAdjacentSectionTextures: true);
            Assert.AreEqual(32, vm.MaxConcurrentTextureRequests);

            vm.MaxConcurrentTextureRequests = 8;
            vm.RevertToOriginal();
            Assert.AreEqual(32, vm.MaxConcurrentTextureRequests);
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

        /// <summary>
        /// Reset to Defaults restores the values a new install starts with (Viking's app.config): 25 sections, acceleration
        /// 1.0, PID P500 D300 I0.01, velocity and position thresholds 0.01 (decision D-ViewerPrefDefaults).
        /// </summary>
        [TestMethod]
        public void ResetToDefaults_RestoresAppConfigOverlayAndPidValues()
        {
            var vm = LoadedViewModel();
            ChangeEverything(vm);

            vm.ResetToDefaults();

            Assert.AreEqual(4, vm.SectionNumberOverlayCount);
            Assert.AreEqual(1.0, vm.SectionNumberOverlayAcceleration, Tolerance);
            Assert.AreEqual(500, vm.PidProportionalGain, Tolerance);
            Assert.AreEqual(300, vm.PidDerivativeGain, Tolerance);
            Assert.AreEqual(0.01, vm.PidIntegralGain, Tolerance);
            Assert.AreEqual(0.01, vm.PidVelocityThreshold, Tolerance);
            Assert.AreEqual(0.01, vm.PidPositionThreshold, Tolerance);
        }

        /// <summary>
        /// The overlay and PID defaults live in four places: the view model's Reset to Defaults constants, app.config (what
        /// a new install actually starts with), Settings.settings, and the generated Settings class's default attributes
        /// (used when a setting is missing from the config). All four must name the same value.
        /// </summary>
        [TestMethod]
        public void OverlayAndPidDefaults_AgreeAcrossViewModelAppConfigAndSettings()
        {
            var expected = new (string Setting, double Value)[]
            {
                ("SectionNumberOverlayCount", ViewerPreferencesDialogViewModel.DefaultSectionNumberOverlayCount),
                ("SectionNumberOverlayAcceleration", ViewerPreferencesDialogViewModel.DefaultAcceleration),
                ("SectionNumberOverlayPidProportionalGain", ViewerPreferencesDialogViewModel.DefaultPidProportionalGain),
                ("SectionNumberOverlayPidDerivativeGain", ViewerPreferencesDialogViewModel.DefaultPidDerivativeGain),
                ("SectionNumberOverlayPidIntegralGain", ViewerPreferencesDialogViewModel.DefaultPidIntegralGain),
                ("SectionNumberOverlayPidVelocityThreshold", ViewerPreferencesDialogViewModel.DefaultPidVelocityThreshold),
                ("SectionNumberOverlayPidPositionThreshold", ViewerPreferencesDialogViewModel.DefaultPidPositionThreshold),
            };

            string vikingCore = Path.Combine(ChangelogTests.FindRepositoryRoot(), "Clients", "Viking", "NGVV");
            XDocument appConfig = XDocument.Load(Path.Combine(vikingCore, "app.config"));
            XDocument settingsFile = XDocument.Load(Path.Combine(vikingCore, "Properties", "Settings.settings"));
            XNamespace settingsNs = settingsFile.Root!.Name.Namespace;

            foreach ((string setting, double value) in expected)
            {
                string appConfigValue = appConfig.Descendants("setting")
                    .Single(e => (string?)e.Attribute("name") == setting).Element("value")!.Value;
                string settingsFileValue = settingsFile.Descendants(settingsNs + "Setting")
                    .Single(e => (string?)e.Attribute("Name") == setting).Element(settingsNs + "Value")!.Value;
                string attributeValue = typeof(Viking.Properties.Settings).GetProperty(setting)!
                    .GetCustomAttribute<DefaultSettingValueAttribute>()!.Value;

                Assert.AreEqual(value, ParseInvariant(appConfigValue), Tolerance, $"app.config {setting}");
                Assert.AreEqual(value, ParseInvariant(settingsFileValue), Tolerance, $"Settings.settings {setting}");
                Assert.AreEqual(value, ParseInvariant(attributeValue), Tolerance, $"Settings.Designer.cs {setting}");
            }
        }

        /// <summary>
        /// The overlay has an on/off setting per kind of volume: on by default for aligned or annotated volumes, off for plain
        /// image stacks. Reset to Defaults restores the default of the open volume's kind.
        /// </summary>
        [TestMethod]
        public void ResetToDefaults_ShowsTheOverlayOnlyForAlignedOrAnnotatedVolumes()
        {
            var vm = LoadedViewModel();
            vm.SectionNumberOverlayEnabled = false;
            vm.ResetToDefaults();
            Assert.IsTrue(vm.SectionNumberOverlayEnabled, "aligned or annotated volume");

            vm = LoadedViewModel();
            vm.OpenVolumeIsImageStack = true;
            vm.SectionNumberOverlayEnabled = true;
            vm.ResetToDefaults();
            Assert.IsFalse(vm.SectionNumberOverlayEnabled, "plain image stack");
        }

        /// <summary>The two overlay on/off defaults agree across the view model, app.config, Settings.settings and Settings.Designer.cs.</summary>
        [TestMethod]
        public void OverlayOnOffDefaults_AgreeAcrossViewModelAppConfigAndSettings()
        {
            var expected = new (string Setting, bool Value)[]
            {
                ("SectionNumberOverlayEnabled", ViewerPreferencesDialogViewModel.DefaultSectionNumberOverlayEnabled),
                ("SectionNumberOverlayEnabledImageStacks", ViewerPreferencesDialogViewModel.DefaultSectionNumberOverlayEnabledImageStacks),
            };

            string vikingCore = Path.Combine(ChangelogTests.FindRepositoryRoot(), "Clients", "Viking", "NGVV");
            XDocument appConfig = XDocument.Load(Path.Combine(vikingCore, "app.config"));
            XDocument settingsFile = XDocument.Load(Path.Combine(vikingCore, "Properties", "Settings.settings"));
            XNamespace settingsNs = settingsFile.Root!.Name.Namespace;

            foreach ((string setting, bool value) in expected)
            {
                string appConfigValue = appConfig.Descendants("setting")
                    .Single(e => (string?)e.Attribute("name") == setting).Element("value")!.Value;
                string settingsFileValue = settingsFile.Descendants(settingsNs + "Setting")
                    .Single(e => (string?)e.Attribute("Name") == setting).Element(settingsNs + "Value")!.Value;
                string attributeValue = typeof(Viking.Properties.Settings).GetProperty(setting)!
                    .GetCustomAttribute<DefaultSettingValueAttribute>()!.Value;

                Assert.AreEqual(value, bool.Parse(appConfigValue), $"app.config {setting}");
                Assert.AreEqual(value, bool.Parse(settingsFileValue), $"Settings.settings {setting}");
                Assert.AreEqual(value, bool.Parse(attributeValue), $"Settings.Designer.cs {setting}");
            }
        }

        private static double ParseInvariant(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
