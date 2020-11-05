using System;
using System.ComponentModel;
using System.Windows.Forms;
using WebAnnotationModel;

namespace WebAnnotation.UI
{
    public partial class SplitStructuresForm : Form
    {
        public long KeepID
        {
            get
            {
                try
                {
                    return long.Parse(textKeepID.Text);
                }
                catch (FormatException)
                {
                    return -1;
                }
            }
            set
            {
                if (value > 0)
                    textKeepID.Text = value.ToString();
                else
                    textKeepID.Text = "";
            }
        }

        public long SplitID
        {
            get
            {
                try
                {
                    return long.Parse(textSplitID.Text);
                }
                catch (FormatException)
                {
                    return -1;
                }
            }
            set
            {
                if (value > 0)
                    textSplitID.Text = value.ToString();
                else
                    textKeepID.Text = "";
            }
        }

        public SplitStructuresForm()
        {
            InitializeComponent();
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

                LocationObj obj = Store.Locations.GetObjectByID(ID);
                if (obj == null)
                {
                    Reason = "No Location found";
                }

                Reason = obj.Parent.Label;
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
                SplitID = long.Parse(textSplitID.Text);
            }
            catch (FormatException)
            {
                Reason = "Input ID is not a number";
                return false;
            }

            if (KeepID == SplitID)
            {
                Reason = "Location ID's must not be equal";
                return false;
            }

            return VerifyStructureMatch(out Reason);
        }

        /// <summary>
        /// Return true if the structures in the Keep and Split are from the same structure
        /// </summary>
        /// <returns></returns>
        private bool VerifyStructureMatch(out string Reason)
        {
            Reason = null;

            long KeepID;
            long SplitID;
            try
            {
                KeepID = int.Parse(textKeepID.Text);
                SplitID = int.Parse(textSplitID.Text);
            }
            catch (FormatException)
            {
                Reason = "Input ID must be a number";
                return false;
            }

            LocationObj keepLoc = Store.Locations.GetObjectByID(KeepID);
            LocationObj splitLoc = Store.Locations.GetObjectByID(SplitID);

            if (keepLoc == null && splitLoc == null)
            {
                Reason = "Input IDs must be a valid location";
                return false;
            }

            if (keepLoc == null)
            {
                Reason = "Keep Location ID must be a valid location";
                return false;
            }

            if (splitLoc == null)
            {
                Reason = "Split Location ID must be a valid location";
                return false;
            }

            if (keepLoc.ParentID != splitLoc.ParentID)
            {
                Reason = String.Format("Location IDs must be from the same structure. Structure {0} not equal to {1}", keepLoc.ParentID, splitLoc.ParentID);
                return false;
            }

            return true;
        }

        private void textKeepID_TextChanged(object sender, EventArgs e)
        {
            string Reason;
            bool IDValid = IsIDValid(textKeepID.Text, out Reason);
            textKeepLabel.Text = Reason;

            UpdateUIForIDLabelTextChanged();
        }

        private void textSplitID_TextChanged(object sender, EventArgs e)
        {
            string Reason;
            bool IDValid = IsIDValid(textSplitID.Text, out Reason);
            textSplitLabel.Text = Reason;

            UpdateUIForIDLabelTextChanged();
        }

        private void UpdateUIForIDLabelTextChanged()
        {
            string Reason = null;
            if (!IsAllInputValid(out Reason))
            {
                textInfo.Text = Reason;
                btnSplit.Enabled = false;
            }
            else
            {
                textInfo.Text = null;
                btnSplit.Enabled = true;
            }
        }

        private void btnSplit_Click(object sender, EventArgs e)
        {
            int KeepLocID;
            int MergeLocID;

            try
            {
                KeepLocID = int.Parse(textKeepID.Text);
                MergeLocID = int.Parse(textSplitID.Text);
            }
            catch (FormatException)
            {
                return;
            }

            try
            {
                Store.Structures.SplitAtLocationLink(KeepLocID, MergeLocID);

            }
            catch (System.ServiceModel.FaultException<System.ServiceModel.ExceptionDetail> fe)
            {
                if (fe?.Detail?.InnerException != null)
                {
                    MessageBox.Show(fe.Detail.InnerException.Message, "Split error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    MessageBox.Show(fe.Detail.Message, "Split error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                return;
            }
            catch (Exception except)
            {
                if (except.Message != null)
                    MessageBox.Show("Split error", except.Message.ToString());

                return;
            }

            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void textKeepID_Validating(object sender, CancelEventArgs e)
        {
            e.Cancel = true;

            string Reason = null;
            if (!IsIDValid(textKeepID.Text, out Reason))
            {
                textKeepLabel.Text = Reason;
                return;
            }

            textKeepLabel.Text = "";
            e.Cancel = false;

            ValidateSplitButton();
        }

        private void textSplitID_Validating(object sender, CancelEventArgs e)
        {
            e.Cancel = true;

            string Reason = null;
            if (!IsIDValid(textSplitID.Text, out Reason))
            {
                textSplitLabel.Text = Reason;
                return;
            }

            textSplitLabel.Text = "";
            e.Cancel = false;

            ValidateSplitButton();
        }

        private void ValidateSplitButton()
        {
            string reason;
            if (!IsAllInputValid(out reason))
            {
                textInfo.Text = reason;
                btnSplit.Enabled = false;
            }
            else
            {
                btnSplit.Enabled = true;
                textInfo.Text = null;
            }
        }

        private void btnFlip_Click(object sender, EventArgs e)
        {

            long temp = KeepID;
            KeepID = SplitID;
            SplitID = temp;
        }
    }
}
