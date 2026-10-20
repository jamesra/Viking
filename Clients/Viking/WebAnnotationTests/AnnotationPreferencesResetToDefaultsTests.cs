using System;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.WPF.Forms;
using AnnotationSettingsStore = WebAnnotation.Properties.Settings;

namespace WebAnnotationTests
{
    /// <summary>
    /// Annotation Preferences "Reset to Defaults" fills the dialog's view model, and OK or Apply then saves those values.
    /// They must match the shipped user-setting defaults (<see cref="DefaultSettingValueAttribute"/> on
    /// <c>WebAnnotation.Properties.Settings</c>), or a reset silently saves different values from a new install.
    /// </summary>
    [TestClass]
    public class AnnotationPreferencesResetToDefaultsTests
    {
        /// <summary>
        /// Settings whose effective default is not the attribute value: in Debug builds
        /// <c>Global.AnnotationSettings</c> treats an unset overlay preference as on, and the view model mirrors that.
        /// </summary>
        private static readonly string[] BuildDependentDefaults = { "AutoPolygonizeOverlayMasks", "AutoPolygonizeOverlayPrompts" };

        /// <summary>
        /// Shipped defaults that disagree with each other and are waiting on a decision (ledger D-PrefDesignerDefaults):
        /// Settings.settings, Global.AnnotationSettings.ResetToDefaults and the view model say 5, 6 and 0.5, but
        /// Settings.Designer.cs, which is what a new install gets, says 8, 10 and 1.0.
        /// </summary>
        private static readonly string[] UndecidedDefaults = { "SegmentationPointRadius", "PolygonPointRadius", "SmallestRenderedSize" };

        /// <summary>Settings the regression this test was written for got wrong; they must take part in the comparison.</summary>
        private static readonly string[] RequiredMatches =
        {
            nameof(AnnotationPreferencesDialogViewModel.NumClosedCurveInterpolationPointsForDisplay),
            nameof(AnnotationPreferencesDialogViewModel.PenSimplifyThreshold),
            nameof(AnnotationPreferencesDialogViewModel.AutoPolygonizeMaxDownsample),
        };

        private static AnnotationPreferencesDialogViewModel ResetViewModel()
        {
            var vm = new AnnotationPreferencesDialogViewModel();
            vm.ResetToDefaults();
            return vm;
        }

        [TestMethod]
        public void EveryResetValueMatchesTheShippedSettingDefault()
        {
            var vm = ResetViewModel();
            var compared = new System.Collections.Generic.List<string>();

            foreach (PropertyInfo setting in typeof(AnnotationSettingsStore).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var defaultAttribute = setting.GetCustomAttribute<DefaultSettingValueAttribute>();
                if (defaultAttribute is null || BuildDependentDefaults.Contains(setting.Name) || UndecidedDefaults.Contains(setting.Name))
                    continue;

                PropertyInfo vmProperty = typeof(AnnotationPreferencesDialogViewModel).GetProperty(setting.Name);
                if (vmProperty is null)
                    continue;

                object expected = TypeDescriptor.GetConverter(setting.PropertyType)
                    .ConvertFromString(null, CultureInfo.InvariantCulture, defaultAttribute.Value);
                object actual = vmProperty.GetValue(vm);

                if (expected is bool expectedFlag)
                    Assert.AreEqual(expectedFlag, (bool)actual, setting.Name);
                else
                    // float settings (opacities) are double in the view model; 1e-6 covers the float rounding of 0.33.
                    Assert.AreEqual(Convert.ToDouble(expected, CultureInfo.InvariantCulture),
                        Convert.ToDouble(actual, CultureInfo.InvariantCulture), 1e-6, setting.Name);

                compared.Add(setting.Name);
            }

            foreach (string name in RequiredMatches)
                CollectionAssert.Contains(compared, name, "Setting not compared: " + name);
        }

        [TestMethod]
        public void ResetMaxAutoSegmentDownsampleIsInsideTheSliderRange()
        {
            // AnnotationPreferencesDialog.xaml: the slider runs 1 to 4 in 0.25 steps.
            double value = ResetViewModel().AutoPolygonizeMaxDownsample;
            Assert.IsTrue(value >= 1.0 && value <= 4.0, $"Reset value {value} is outside the slider's 1 to 4 range.");
            Assert.AreEqual(0.0, Math.IEEERemainder(value * 4.0, 1.0), 1e-9, "Reset value is not on a 0.25 slider tick.");
        }
    }
}
