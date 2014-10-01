using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WebAnnotationModel;

namespace WebAnnotation.UI
{
    public partial class MergeStructuresForm : Form
    {
        public MergeStructuresForm()
        {
            InitializeComponent();
        }

        private void textKeepID_Validating(object sender, CancelEventArgs e)
        {
            btnMerge.Enabled = false;
            e.Cancel = true;

            try
            {
                //Check if the string is empty, that is OK
                if (textKeepID.Text.Length == 0)
                {
                    textKeepLabel.Text = "";
                    e.Cancel = false;
                    return;
                }

                if(textMergeID.Text == textKeepID.Text)
                {
                    textKeepLabel.Text = "Cannot merge structure to itself";
                    return;
                }

                //If string is not empty it needs to be a number
                int KeepID = int.Parse(textKeepID.Text);

                StructureObj keepStruct = Store.Structures.GetObjectByID(KeepID);
                if (keepStruct == null)
                {
                    textKeepLabel.Text = "Structure ID not found";
                    return;
                }

                textKeepLabel.Text = keepStruct.Label; 

                if (!VerifyTypeMatch())
                {
                    textKeepLabel.Text = textKeepLabel.Text + "Structure type mismatch";
                    e.Cancel = false; 
                    return;
                }

                btnMerge.Enabled = true;
                e.Cancel = false;
            }
            catch (FormatException)
            {
                e.Cancel = true;
            }
        }

        private void textMergeID_Validating(object sender, CancelEventArgs e)
        {
            btnMerge.Enabled = false;
            e.Cancel = true; 

            try
            {
                //Check if the string is empty, that is OK
                if (textMergeID.Text.Length == 0)
                {
                    textMergeLabel.Text = "";
                    e.Cancel = false;
                    return;
                }

                if(textMergeID.Text == textKeepID.Text)
                {
                    textMergeLabel.Text = "Cannot merge structure to itself";
                    return;
                }

                //If string is not empty it needs to be a number
                int MergeID = int.Parse(textMergeID.Text);

                StructureObj mergeStruct = Store.Structures.GetObjectByID(MergeID);
                if (mergeStruct == null)
                {
                    textMergeLabel.Text = "Structure ID not found";
                    return;
                }

                textMergeLabel.Text = mergeStruct.Label; 

                if (!VerifyTypeMatch())
                {
                    textMergeLabel.Text = textMergeLabel.Text +  " Structure type mismatch";
                    e.Cancel = false; 
                    return;
                }

                
                btnMerge.Enabled = true;
                e.Cancel = false;
            }
            catch (FormatException)
            {
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Return true if the structures in the Keep and Merge text boxes have the same type
        /// </summary>
        /// <returns></returns>
        private bool VerifyTypeMatch()
        {
            int KeepID;
            int MergeID;

            try
            {
                KeepID = int.Parse(textKeepID.Text);
                MergeID = int.Parse(textMergeID.Text);
            }
            catch (FormatException)
            {
                return false;
            }

            StructureObj mergeStruct = Store.Structures.GetObjectByID(MergeID);
            StructureObj keepStruct = Store.Structures.GetObjectByID(KeepID);

            if (keepStruct == null && mergeStruct == null)
            {
                return true;
            }

            if (keepStruct == null || mergeStruct == null)
            {
                return false;
            }

            return keepStruct.Type == mergeStruct.Type;
        }

        private void btnMerge_Click(object sender, EventArgs e)
        {
            int KeepID;
            int MergeID;

            try
            {
                KeepID = int.Parse(textKeepID.Text);
                MergeID = int.Parse(textMergeID.Text);
            }
            catch(FormatException)
            {
                return; 
            }

            long result = Store.Structures.Merge(KeepID, MergeID);
            if (result != 0)
            {
                MessageBox.Show("Merge error", "Code: " + result.ToString());
            }
                

            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close(); 
        }
    }
}
