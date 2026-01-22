using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using Viking.Common;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI
{


    [PropertyPage(typeof(Structure), 1)]
    public partial class StructureGeneralPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private Structure Obj;
        private BindingList<WebAnnotationModel.ObjAttribute>? ListTags = null;

        public StructureGeneralPage()
        {
            InitializeComponent();
        }

        protected override void OnInitPage() => base.OnInitPage();

        protected override void OnShowObject(object Object)
        {
            Obj = Object as Structure;
            Debug.Assert(Obj != null);

            textID.Text = Obj.ID.ToString();
            textLabel.Text = Obj.InfoLabel;
            linkType.Text = Obj.Type.Name;

            ListTags = new BindingList<WebAnnotationModel.ObjAttribute>([.. Obj.Attributes]);

            dataGridTags.DataSource = ListTags;
        }



        protected override void OnSaveChanges()
        {
            Obj.InfoLabel = textLabel.Text;

            RemoveBlankAttributesFromList(ListTags);

            Obj.Attributes = ListTags;
        }

        private static void RemoveBlankAttributesFromList(BindingList<WebAnnotationModel.ObjAttribute> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                WebAnnotationModel.ObjAttribute item = list[i];
                if (item.Name is null)
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
                List<int> iDeleteRowList = new(dataGridTags.SelectedCells.Count);
                foreach (DataGridViewCell cell in dataGridTags.SelectedCells)
                {
                    if (iDeleteRowList.Contains(cell.RowIndex))
                    {
                        continue;
                    }

                    iDeleteRowList.Add(cell.RowIndex);
                }

                iDeleteRowList.Sort();
                iDeleteRowList.Reverse();

                foreach (int iDelRow in iDeleteRowList)
                {
                    //Don't delete the new row, it is an invalid operation.
                    if (dataGridTags.Rows[iDelRow].IsNewRow)
                    {
                        continue;
                    }

                    dataGridTags.Rows.RemoveAt(iDelRow);
                }

                e.Handled = true;
            }
            else
            {
                e.Handled = false;
            }

        }

        private void dataGridTags_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            string dataval = e.FormattedValue as string;

            dataval = dataval.Trim();
            dataval = dataval.ToLower();

            //Do not allow two tags with the same name
            if (e.ColumnIndex > 0)
            {
                return;
            }

            //It is OK to leave a blank, and have multiple blanks. 
            //Blanks/Nulls are removed when the page is saved
            if (dataval == "")
            {
                return;
            }

            for (int i = 0; i < dataGridTags.Rows.Count; i++)
            {
                if (i == e.RowIndex)
                {
                    continue;
                }

                if (dataGridTags.Rows[i].Cells[0].Value is not string compareValue)
                {
                    continue;
                }

                compareValue = compareValue.ToLower();

                if (compareValue == dataval)
                {
                    e.Cancel = true;
                    dataGridTags.Rows[e.RowIndex].Cells[0].ErrorText = "Duplicate tag names are not allowed";
                    return;
                }
            }

            dataGridTags.Rows[e.RowIndex].Cells[0].ErrorText = null;
            e.Cancel = false;
        }

        private void DataGridTags_CellErrorTextChanged(object sender, DataGridViewCellEventArgs e) => labelDataGridError.Text = dataGridTags.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText;

        private void DataGridTags_RowErrorTextChanged(object sender, DataGridViewRowEventArgs e) => labelDataGridError.Text = e.Row.ErrorText;
    }
}
