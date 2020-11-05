using System;
using System.Diagnostics;
using System.Windows.Forms;
using Viking.Common;
using WebAnnotation.ViewModel;

namespace WebAnnotation.UI
{
    [PropertyPage(typeof(StructureType), 1)]
    public partial class StructureTypeGeneralPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        StructureType Obj = null;

        public StructureTypeGeneralPage()
        {
            InitializeComponent();
        }

        protected override void OnInitPage()
        {
            base.OnInitPage();
        }

        protected override void OnShowObject(object Object)
        {
            this.Obj = Object as StructureType;
            Debug.Assert(this.Obj != null);

            this.textName.Text = this.Obj.Name;

            this.textCode.Text = this.Obj.Code;

            this.textNotes.Text = this.Obj.Notes;

            this.textID.Text = this.Obj.ID.ToString();

            this.btnColor.BackColor = this.Obj.Color;
        }

        protected override void OnSaveChanges()
        {
            this.Obj.Name = this.textName.Text;

            this.Obj.Code = this.textCode.Text;

            this.Obj.Notes = this.textNotes.Text;

            this.Obj.Color = this.btnColor.BackColor;
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
