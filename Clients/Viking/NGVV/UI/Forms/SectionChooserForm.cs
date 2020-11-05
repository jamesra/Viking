using System;
using System.Windows.Forms;
using Viking.ViewModels;

namespace Viking.UI.Forms
{
    public partial class SectionChooserForm : Form
    {
        public SectionViewModel SelectedSection = null;

        public SectionChooserForm()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (listSections.SelectedObject == null)
            {
                MessageBox.Show("Please select a section or press cancel.", "No section selected");
                return;
            }

            SelectedSection = listSections.SelectedObject as SectionViewModel;

            this.Close();
        }
    }
}
