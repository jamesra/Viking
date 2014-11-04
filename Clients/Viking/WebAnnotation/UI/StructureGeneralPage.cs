using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using Viking.Common;
using System.Diagnostics;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI
{
   

    [PropertyPage(typeof(Structure), 1)]
    public partial class StructureGeneralPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        Structure Obj;

        BindingList<WebAnnotationModel.ObjAttribute> ListTags = null;

        public StructureGeneralPage()
        {
            InitializeComponent();
        }

        protected override void OnInitPage()
        {
            base.OnInitPage();
        } 

        protected override void OnShowObject(object Object)
        {
            this.Obj = Object as Structure;
            Debug.Assert(this.Obj != null);

            this.textID.Text= this.Obj.ID.ToString();
            this.textLabel.Text = this.Obj.InfoLabel;

            this.ListTags = new BindingList<WebAnnotationModel.ObjAttribute>(new List<ObjAttribute>(this.Obj.Attributes));
            
             this.dataGridTags.DataSource = this.ListTags;
        }

        

        protected override void OnSaveChanges()
        {
            this.Obj.InfoLabel = this.textLabel.Text;

            RemoveBlankAttributesFromList(this.ListTags); 

            this.Obj.Attributes = this.ListTags;
        }

        private static void RemoveBlankAttributesFromList(BindingList<WebAnnotationModel.ObjAttribute> list)
        {
            for(int i = list.Count -1; i >= 0; i--)
            {
                WebAnnotationModel.ObjAttribute item = list[i]; 
                if(item.Name == null)
                {
                    list.RemoveAt(i);
                    continue;
                }

                if (item.Name.Length == 0)
                {
                    list.RemoveAt(i);
                    continue;
                }
            }

            return;
        }
        

        private void StructureGeneralPage_Load(object sender, EventArgs e)
        {

        }

        private void dataGridTags_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void dataGridTags_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                List<int> iDeleteRowList = new List<int>(dataGridTags.SelectedCells.Count);
                foreach(DataGridViewCell cell in dataGridTags.SelectedCells)
                {
                    if(iDeleteRowList.Contains(cell.RowIndex))
                        continue;

                    iDeleteRowList.Add(cell.RowIndex);
                }

                iDeleteRowList.Sort();
                iDeleteRowList.Reverse();

                foreach(int iDelRow in iDeleteRowList)
                {
                    //Don't delete the new row, it is an invalid operation.
                    if (dataGridTags.Rows[iDelRow].IsNewRow)
                        continue; 

                    dataGridTags.Rows.RemoveAt(iDelRow);
                }
            } 
        }

        private void dataGridTags_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            string dataval = e.FormattedValue as string;

            dataval = dataval.Trim();
            dataval = dataval.ToLower();

            //Do not allow two tags with the same name
            if (e.ColumnIndex > 0)
                return;

            //It is OK to leave a blank, and have multiple blanks. 
            //Blanks/Nulls are removed when the page is saved
            if(dataval == "")
            {
                return; 
            }

            for (int i = 0; i < dataGridTags.Rows.Count; i++)
            {
                if(i == e.RowIndex)
                    continue;

                string compareValue = dataGridTags.Rows[i].Cells[0].Value as string;
                if (compareValue == null)
                    continue;

                compareValue = compareValue.ToLower();

                if (compareValue == dataval)
                {
                   e.Cancel = true;
                   this.dataGridTags.Rows[e.RowIndex].Cells[0].ErrorText = "Duplicate tag names are not allowed";
                   return;
                }
            }

            this.dataGridTags.Rows[e.RowIndex].Cells[0].ErrorText = null; 
            e.Cancel = false;
        }

        private void dataGridTags_CellErrorTextChanged(object sender, DataGridViewCellEventArgs e)
        { 
            this.labelDataGridError.Text = this.dataGridTags.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText;
        }

        private void dataGridTags_RowErrorTextChanged(object sender, DataGridViewRowEventArgs e)
        {
            this.labelDataGridError.Text = e.Row.ErrorText;
        }
    }
}
