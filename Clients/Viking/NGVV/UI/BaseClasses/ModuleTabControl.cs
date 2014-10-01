using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics; 

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
#if !DEBUG
                try
                {
#endif 
                    object Obj = Activator.CreateInstance(ModuleTabType);
                    Viking.Common.ITabExtension ITab = Obj as Viking.Common.ITabExtension;
                    
                    TabPage Page = ITab.GetPage();
                    this.TabsModules.TabPages.Add(Page);
#if !DEBUG
                }
                catch (Exception Except)
                {
                    Trace.WriteLine("Error Loading Module Tab Control: " + ModuleTabType.ToString(), "UI");
                    Trace.WriteLine(Except.ToString(), "UI");
                    throw Except;
                }
#endif
            }
        }
    }
}
