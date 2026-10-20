using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;

namespace Viking.UI.BaseClasses
{
    public partial class ModuleTabControl : Viking.UI.BaseClasses.DockableUserControl
    {
        [Browsable(true)]
        public Viking.Common.TABCATEGORY TabCategory = Viking.Common.TABCATEGORY.CUSTOM;

        public ModuleTabControl()
        {
            InitializeComponent();
        }

        private void ModuleTabControl_Load(object sender, EventArgs e)
        {
            foreach (System.Type ModuleTabType in Viking.Common.ExtensionManager.GetExtensionTabCategory(TabCategory))
            {
                try
                {
                    object? Obj = Activator.CreateInstance(ModuleTabType);
                    if (Obj is not Viking.Common.ITabExtension tab)
                        continue;

                    TabPage? Page = tab.GetPage();
                    if (Page != null)
                        this.TabsModules.TabPages.Add(Page);
                }
                catch (Exception except)
                {
                    // A failed annotation store (launch token denied) used to rethrow here while the
                    // main window was shown, which is the crash after a tools-page open missed the running instance.
                    Trace.WriteLine("Error Loading Module Tab Control: " + ModuleTabType, "UI");
                    Trace.WriteLine(except.ToString(), "UI");
                }
            }
        }
    }
}