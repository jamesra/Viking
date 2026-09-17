using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace WebAnnotationTests
{
    /// <summary>
    /// Medium-feasibility dialog smoke: WinForms dialogs we can construct headlessly,
    /// plus optional FlaUI attach when Viking.exe is already running (menus only — no canvas).
    /// </summary>
    [TestClass]
    public class DialogSmokeTests
    {
        [TestMethod]
        public void MergeStructuresForm_ConstructsOnStaThread()
        {
            RunOnSta(() =>
            {
                using var form = new WebAnnotation.UI.MergeStructuresForm();
                Assert.IsNotNull(form);
                Assert.IsFalse(string.IsNullOrWhiteSpace(form.Text));
            });
        }

        private static void RunOnSta(Action action)
        {
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { error = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "STA dialog smoke timed out");
            Assert.IsNull(error, error?.ToString());
        }

        /// <summary>
        /// Attach to a running Viking process and verify the Help → Version Info menu path exists.
        /// Skips when Viking is not running (does not launch the app).
        /// </summary>
        [TestMethod]
        public void FlaUI_RunningViking_HelpMenuContainsVersionInfo()
        {
            var proc = Process.GetProcessesByName("Viking")
                .Concat(Process.GetProcessesByName("VikingAU"))
                .FirstOrDefault(p => !p.HasExited && p.MainWindowHandle != IntPtr.Zero);

            if (proc == null)
            {
                Assert.Inconclusive(
                    "Start Viking.exe (or VikingAU) with a visible main window to run FlaUI menu smoke.");
            }

            using var automation = new UIA3Automation();
            using var app = FlaUI.Core.Application.Attach(proc);
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(5));
            Assert.IsNotNull(window, "Could not find Viking main window via FlaUI.");

            var help = window.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.MenuItem).And(cf.ByName("Help")));
            Assert.IsNotNull(help, "Help menu item not found on Viking main window.");

            help.AsMenuItem().Expand();
            var versionInfo = window.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.MenuItem).And(cf.ByName("Version Info")));
            Assert.IsNotNull(versionInfo, "Help → Version Info menu item not found.");
        }
    }
}
