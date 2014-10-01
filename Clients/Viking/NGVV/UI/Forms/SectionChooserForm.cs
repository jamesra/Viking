using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Viking.VolumeModel; 

namespace Viking.UI.Forms
{
    public partial class SectionChooserForm : Form
    {
        public Section SelectedSection = null; 

        public SectionChooserForm()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if(listSections.SelectedObject == null)
            {
                MessageBox.Show("Please select a section or press cancel.", "No section selected"); 
                return; 
            }

            SelectedSection = listSections.SelectedObject as Section; 

            this.Close(); 
        }
    }
}
