using System;
using System.Diagnostics;
using System.Linq;
using Viking.Common;
using Viking.UI.Forms;
using Viking.ViewModels;
using Viking.VolumeModel;

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
            if (UI.State.volume != null)
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
