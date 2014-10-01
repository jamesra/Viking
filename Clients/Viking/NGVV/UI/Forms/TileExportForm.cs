using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Viking.UI.Forms
{
    public partial class TileExportForm : Form
    {
        public String ExportPath;
        public Boolean ExportAll = true;
        public int FirstSectionInExport;
        public int LastSectionInExport;
        public int Downsample; 

        public TileExportForm()
        {
            InitializeComponent();
        }

        private void checkExportAll_CheckedChanged(object sender, EventArgs e)
        {
            groupRange.Enabled = !checkExportAll.Checked;
            ExportAll = checkExportAll.Checked;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            ExportPath = textPath.Text;
            FirstSectionInExport = (int)numFirstSection.Value;
            LastSectionInExport = (int)numLastSection.Value;
            Downsample = (int)numDownsample.Value; 
        }

        private void textPath_TextChanged(object sender, EventArgs e)
        {
            btnOK.Enabled = textPath.Text.Length > 0;
            
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {

        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.SelectedPath = textPath.Text;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    textPath.Text = dlg.SelectedPath; 
                }
            }
        }
    }
}
