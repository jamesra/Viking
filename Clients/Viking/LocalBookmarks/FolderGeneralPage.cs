using System;
using System.Windows.Forms;
using VikingXNAGraphics;
using VikingXNAWinForms;

namespace LocalBookmarks
{
    [Viking.Common.PropertyPage(typeof(FolderUIObj))]
    public partial class FolderGeneralPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        FolderUIObj folder;

        public FolderGeneralPage()
        {
            InitializeComponent();

            textName.Focus();
        }

        protected override void OnShowObject(object Object)
        {
            folder = Object as FolderUIObj;
            textName.Text = folder.Name;
            btnColor.BackColor = folder.Color.ToSystemColor();
            comboShape.Text = folder.Shape.ToShapeString();
        }

        protected override void OnInitPage()
        {
            textName.Text = folder.Name;
            btnColor.BackColor = folder.Color.ToSystemColor();
            comboShape.Text = folder.Shape.ToShapeString();
        }

        protected override void OnSaveChanges()
        {
            folder.Name = textName.Text;
            folder.Color = btnColor.BackColor.ToXNAColor();
            folder.Shape = comboShape.Text.ToShape();
        }

        protected override void OnCancelChanges()
        {
            return;
        }

        private void btnColor_Click(object sender, EventArgs e)
        {
            colorDialog.Color = btnColor.BackColor;

            DialogResult result = colorDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                btnColor.BackColor = colorDialog.Color;
            }
        }
    }
}
