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
            get => StructureIdText.TryParse(textKeepID.Text, out long id) ? id : -1;
            set
            {
                textKeepID.Text = value > 0 ? value.ToString() : "";
            }
        }

        public long SplitID
        {
            get => StructureIdText.TryParse(textSplitID.Text, out long id) ? id : -1;
            set
            {
                if (value > 0)
                {
                    textSplitID.Text = value.ToString();
                }
                else
                {
                    textKeepID.Text = "";
                }
            }
        }

        public SplitStructuresForm()
        {
            InitializeComponent();
        }

        private bool IsIDValid(string Input, out string Reason)
        {
            Reason = null;

            //Check if the string is empty, that is OK
            if (Input.Length == 0)
            {
                return true;
            }

            //If string is not empty it needs to be a number
            if (!StructureIdText.TryParse(Input, out long ID))
            {
                return false;
            }

            LocationObj obj = Store.Locations.GetObjectByID(ID);
            if (obj is null)
            {
                Reason = "No Location found";
                return false;
            }

            Reason = obj.Parent.Label;
            return true;
        }

        private bool IsAllInputValid(out string Reason)
        {
            Reason = null;

            if (!StructureIdText.TryParse(textKeepID.Text, out long KeepID) || !StructureIdText.TryParse(textSplitID.Text, out long SplitID))
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

            if (!StructureIdText.TryParse(textKeepID.Text, out long KeepID) || !StructureIdText.TryParse(textSplitID.Text, out long SplitID))
            {
                Reason = "Input ID must be a number";
                return false;
            }

            LocationObj keepLoc = Store.Locations.GetObjectByID(KeepID);
            LocationObj splitLoc = Store.Locations.GetObjectByID(SplitID);

            if (keepLoc is null && splitLoc is null)
            {
                Reason = "Input IDs must be a valid location";
                return false;
            }

            if (keepLoc is null)
            {
                Reason = "Keep Location ID must be a valid location";
                return false;
            }

            if (splitLoc is null)
            {
                Reason = "Split Location ID must be a valid location";
                return false;
            }

            if (keepLoc.ParentID != splitLoc.ParentID)
            {
                Reason =
                    $"Location IDs must be from the same structure. Structure {keepLoc.ParentID} not equal to {splitLoc.ParentID}";
                return false;
            }

            return true;
        }

        private void textKeepID_TextChanged(object sender, EventArgs e)
        {
            bool IDValid = IsIDValid(textKeepID.Text, out string Reason);
            textKeepLabel.Text = Reason;

            UpdateUIForIDLabelTextChanged();
        }

        private void textSplitID_TextChanged(object sender, EventArgs e)
        {
            bool IDValid = IsIDValid(textSplitID.Text, out string Reason);
            textSplitLabel.Text = Reason;

            UpdateUIForIDLabelTextChanged();
        }

        private void UpdateUIForIDLabelTextChanged()
        {
            if (!IsAllInputValid(out string Reason))
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
            if (!StructureIdText.TryParse(textKeepID.Text, out long KeepLocID) || !StructureIdText.TryParse(textSplitID.Text, out long MergeLocID))
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
                {
                    MessageBox.Show("Split error", except.Message.ToString());
                }

                return;
            }

            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e) => Close();

        private void textKeepID_Validating(object sender, CancelEventArgs e)
        {
            e.Cancel = true;

            if (!IsIDValid(textKeepID.Text, out string Reason))
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

            if (!IsIDValid(textSplitID.Text, out string Reason))
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
            if (!IsAllInputValid(out string reason))
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

        private void btnFlip_Click(object sender, EventArgs e) => (SplitID, KeepID) = (KeepID, SplitID);
    }
}
