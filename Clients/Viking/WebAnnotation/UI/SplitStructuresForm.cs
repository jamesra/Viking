using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

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
                textKeepID.Text = value > 0 ? value.ToString() : "";
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

        private async Task<(bool Valid, string Reason)> IsIDValidAsync(string Input)
        {
            try
            {
                if (Input.Length == 0)
                    return (true, null);

                int ID = int.Parse(Input);
                LocationObj obj = await Store.Locations.GetObjectByID(ID);
                if (obj is null)
                    return (true, "No Location found");

                return (true, obj.Parent.Label);
            }
            catch (FormatException)
            {
                return (false, null);
            }
        }

        private async Task<(bool Valid, string Reason)> VerifyStructureMatchAsync()
        {
            long KeepID;
            long SplitID;
            try
            {
                KeepID = int.Parse(textKeepID.Text);
                SplitID = int.Parse(textSplitID.Text);
            }
            catch (FormatException)
            {
                return (false, "Input ID must be a number");
            }

            LocationObj keepLoc = await Store.Locations.GetObjectByID(KeepID);
            LocationObj splitLoc = await Store.Locations.GetObjectByID(SplitID);

            if (keepLoc is null && splitLoc is null)
                return (false, "Input IDs must be a valid location");
            if (keepLoc is null)
                return (false, "Keep Location ID must be a valid location");
            if (splitLoc is null)
                return (false, "Split Location ID must be a valid location");
            if (keepLoc.ParentID != splitLoc.ParentID)
                return (false, $"Location IDs must be from the same structure. Structure {keepLoc.ParentID} not equal to {splitLoc.ParentID}");

            return (true, null);
        }

        private async Task<(bool Valid, string Reason)> IsAllInputValidAsync()
        {
            long KeepID;
            long SplitID;
            try
            {
                KeepID = long.Parse(textKeepID.Text);
                SplitID = long.Parse(textSplitID.Text);
            }
            catch (FormatException)
            {
                return (false, "Input ID is not a number");
            }

            if (KeepID == SplitID)
                return (false, "Location ID's must not be equal");

            return await VerifyStructureMatchAsync();
        }

        private async void textKeepID_TextChanged(object sender, EventArgs e)
        {
            var (IDValid, Reason) = await IsIDValidAsync(textKeepID.Text);
            textKeepLabel.Text = Reason;
            await UpdateUIForIDLabelTextChangedAsync();
        }

        private async void textSplitID_TextChanged(object sender, EventArgs e)
        {
            var (IDValid, Reason) = await IsIDValidAsync(textSplitID.Text);
            textSplitLabel.Text = Reason;
            await UpdateUIForIDLabelTextChangedAsync();
        }

        private async Task UpdateUIForIDLabelTextChangedAsync()
        {
            var (valid, Reason) = await IsAllInputValidAsync();
            if (!valid)
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

        private async void btnSplit_Click(object sender, EventArgs e)
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
                await Store.Structures.SplitStructureAtLocationLink(KeepLocID, MergeLocID);

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

        private async void textKeepID_Validating(object sender, CancelEventArgs e)
        {
            e.Cancel = true;

            var (valid, Reason) = await IsIDValidAsync(textKeepID.Text);
            if (!valid)
            {
                textKeepLabel.Text = Reason;
                return;
            }

            textKeepLabel.Text = "";
            e.Cancel = false;

            await ValidateSplitButtonAsync();
        }

        private async void textSplitID_Validating(object sender, CancelEventArgs e)
        {
            e.Cancel = true;

            var (valid, Reason) = await IsIDValidAsync(textSplitID.Text);
            if (!valid)
            {
                textSplitLabel.Text = Reason;
                return;
            }

            textSplitLabel.Text = "";
            e.Cancel = false;

            await ValidateSplitButtonAsync();
        }

        private async Task ValidateSplitButtonAsync()
        {
            var (valid, reason) = await IsAllInputValidAsync();
            if (!valid)
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
