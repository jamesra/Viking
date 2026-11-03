using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.WPF.Forms;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the barrel button preference in the Annotation Preferences dialog: the default, Cancel, Reset to Defaults, and that a
    /// missing value can never leave the user without a barrel button.
    /// </summary>
    [TestClass]
    public class AnnotationPreferencesBarrelButtonTests
    {
        [TestMethod]
        public void Default_IsTheWindowsBarrelButton()
        {
            var vm = new AnnotationPreferencesDialogViewModel();

            Assert.AreEqual("Barrel", vm.BarrelButton);
            Assert.AreEqual("Pen barrel button (Windows default)", vm.BarrelButtonDescription);
        }

        [TestMethod]
        public void SettingEraser_ChangesTheDescription()
        {
            var vm = new AnnotationPreferencesDialogViewModel { BarrelButton = "Eraser" };

            Assert.AreEqual("Pen eraser button", vm.BarrelButtonDescription);
        }

        [TestMethod]
        public void SettingEmpty_FallsBackToBarrel()
        {
            var vm = new AnnotationPreferencesDialogViewModel { BarrelButton = "Eraser" };

            vm.BarrelButton = "";

            Assert.AreEqual("Barrel", vm.BarrelButton);
        }

        [TestMethod]
        public void RevertToOriginal_RestoresTheAppliedButton()
        {
            var vm = new AnnotationPreferencesDialogViewModel { BarrelButton = "Eraser" };
            vm.Apply();
            vm.BarrelButton = "Barrel";

            vm.RevertToOriginal();

            Assert.AreEqual("Eraser", vm.BarrelButton);
        }

        [TestMethod]
        public void ResetToDefaults_ChoosesTheWindowsBarrelButton()
        {
            var vm = new AnnotationPreferencesDialogViewModel { BarrelButton = "Eraser" };

            vm.ResetToDefaults();

            Assert.AreEqual("Barrel", vm.BarrelButton);
        }

        [TestMethod]
        public void StylusButtonCapture_MapsThePenButtonsAndIgnoresOthers()
        {
            Assert.AreEqual("Barrel", StylusButtonCapture.NameFor(System.Windows.Input.StylusPointProperties.BarrelButton.Id));
            Assert.AreEqual("Eraser", StylusButtonCapture.NameFor(System.Windows.Input.StylusPointProperties.SecondaryTipButton.Id));
            Assert.IsNull(StylusButtonCapture.NameFor(System.Guid.NewGuid()));
        }
    }
}
