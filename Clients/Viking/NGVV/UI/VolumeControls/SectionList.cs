using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Viking.Common;
using System.Diagnostics;
using Viking.UI.Forms;
using Viking.VolumeModel;
using Viking.ViewModels;

namespace Viking.UI.Controls
{
    [Viking.Common.SupportedUITypesAttribute(typeof(Section))]
    [Viking.Common.ExtensionTab("Sections", Viking.Common.TABCATEGORY.ACTION)] 
    public partial class SectionList : Viking.UI.BaseClasses.DockingListControl
    {
        public SectionList()
        {
            InitializeComponent();

            this.Title = "Sections";
            this.ListItems.ShowPropertiesOnDoubleClick = false; 
        }

        private void SectionList_Load(object sender, EventArgs e)
        {
            if(UI.State.volume != null)
                this.ListItems.DisplayObjects(UI.State.volume.SectionViewModels.Values.ToArray() as IUIObject[]);
        }

        protected override void OnObjectDoubleClick(IUIObject obj)
        {
            SectionViewModel section = obj as SectionViewModel;
            Debug.Assert(section != null);

            SectionViewerForm.Show(section); 
        }
    }
}
