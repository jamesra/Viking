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
        private StructureType Obj = null;

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
            Obj = Object as StructureType;
            Debug.Assert(Obj != null);

            textName.Text = Obj.Name;

            textCode.Text = Obj.Code;

            textNotes.Text = Obj.Notes;

            textID.Text = Obj.ID.ToString();

            btnColor.BackColor = Obj.Color;
        }

        protected override void OnSaveChanges()
        {
            Obj.Name = textName.Text;

            Obj.Code = textCode.Text;

            Obj.Notes = textNotes.Text;

            Obj.Color = btnColor.BackColor;
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
