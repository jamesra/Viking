using System;
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

        /// <summary>
        /// Return true if the structures in the Keep and Merge text boxes have the same type
        /// </summary>
        /// <returns></returns>
        private bool VerifyTypeMatch(out string Reason)
        {
            Reason = null;
            int KeepID;
            int MergeID;

            try
            {
                KeepID = int.Parse(textKeepID.Text);
                MergeID = int.Parse(textMergeID.Text);
            }
            catch (FormatException)
            {
                Reason = "Could not parse ID number";
                return false;
            }

            StructureObj mergeStruct = Store.Structures.GetObjectByID(MergeID);
            StructureObj keepStruct = Store.Structures.GetObjectByID(KeepID);

            if (keepStruct == null && mergeStruct == null)
            {
                Reason = "Input IDs must be a valid structures";
                return false;
            }

            if (keepStruct == null)
            {
                Reason = "No structure matches Keep ID";
                return false;
            }

            if (mergeStruct == null)
            {
                Reason = "No structure matches Merge ID";
                return false;
            }

            if (keepStruct.TypeID != mergeStruct.TypeID)
            {
                Reason = string.Format("Merged structures must have the same type. Merged {1} is not a {0}", keepStruct.Type.Name, mergeStruct.Type.Name);
                return false;
            }

            return true;
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
            catch (FormatException)
            {
                return;
            }

            try
            {
                Store.Structures.Merge(KeepID, MergeID);
            }
            catch (System.ServiceModel.FaultException<System.ServiceModel.ExceptionDetail> fe)
            {
                if (fe?.Detail?.InnerException != null)
                {
                    MessageBox.Show(fe.Detail.InnerException.Message, "Merge error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    MessageBox.Show(fe.Detail.Message, "Merge error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                return;
            }
            catch (Exception except)
            {
                MessageBox.Show("Merge error", except.Message.ToString());
                return;
            }
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private bool IsIDValid(string Input, out string Reason)
        {
            Reason = null;
            try
            {
                //Check if the string is empty, that is OK
                if (Input.Length == 0)
                {
                    return true;
                }

                //If string is not empty it needs to be a number
                int ID = int.Parse(Input);

                StructureObj obj = Store.Structures.GetObjectByID(ID, true);
                if (obj == null)
                {
                    Reason = "No structure found";
                    return false;
                }

                Reason = obj.Label;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private bool IsAllInputValid(out string Reason)
        {
            Reason = null;
            long KeepID;
            long SplitID;

            try
            {
                KeepID = long.Parse(textKeepID.Text);
                SplitID = long.Parse(textMergeID.Text);
            }
            catch (FormatException)
            {
                Reason = "Input ID is not a number";
                return false;
            }

            if (KeepID == SplitID)
            {
                Reason = "Cannot merge structure to itself";
                return false;
            }

            return VerifyTypeMatch(out Reason);
        }

        private void textIDLabel_TextChanged(object sender, EventArgs e)
        {
            string Reason = null;
            if (!IsAllInputValid(out Reason))
            {
                textValidation.Text = Reason;
                btnMerge.Enabled = false;
            }
            else
            {
                textValidation.Text = null;
                btnMerge.Enabled = true;
            }
        }

        private void textKeepIDLabel_TextChanged(object sender, EventArgs e)
        {
            string Reason;
            bool IDValid = IsIDValid(textKeepID.Text, out Reason);
            textKeepLabel.Text = Reason;

            UpdateUIForIDLabelTextChanged();
        }

        private void textMergeIDLabel_TextChanged(object sender, EventArgs e)
        {
            string Reason;
            bool IDValid = IsIDValid(textMergeID.Text, out Reason);
            textMergeLabel.Text = Reason;

            UpdateUIForIDLabelTextChanged();
        }

        private void UpdateUIForIDLabelTextChanged()
        {
            string Reason = null;
            if (!IsAllInputValid(out Reason))
            {
                textValidation.Text = Reason;
                btnMerge.Enabled = false;
            }
            else
            {
                textValidation.Text = null;
                btnMerge.Enabled = true;
            }
        }
    }
}
