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
        /// <summary>
        /// Location ID in the Keep box, or -1 when the box is empty or not a number. Setting a value of 0 or less empties the box.
        /// </summary>
        public long KeepID
        {
            get => StructureIdText.TryParse(textKeepID.Text, out long id) ? id : -1;
            set => textKeepID.Text = StructureIdText.ToText(value);
        }

        /// <summary>
        /// Location ID in the Split box, or -1 when the box is empty or not a number. Setting a value of 0 or less empties the box.
        /// </summary>
        public long SplitID
        {
            get => StructureIdText.TryParse(textSplitID.Text, out long id) ? id : -1;
            set => textSplitID.Text = StructureIdText.ToText(value);
        }

        public SplitStructuresForm()
        {
            InitializeComponent();
        }

        private async Task<(bool Valid, string Reason)> IsIDValidAsync(string Input)
        {
            //Check if the string is empty, that is OK
            if (Input.Length == 0)
                return (true, null);

            //If string is not empty it needs to be a number
            if (!StructureIdText.TryParse(Input, out long ID))
                return (false, null);

            LocationObj obj = await Store.Locations.GetObjectByID(ID);
            if (obj is null)
                return (false, "No Location found");

            return (true, obj.Parent?.Label);
        }

        private async Task<(bool Valid, string Reason)> VerifyStructureMatchAsync()
        {
            if (!StructureIdText.TryParse(textKeepID.Text, out long KeepID) || !StructureIdText.TryParse(textSplitID.Text, out long SplitID))
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
            if (!StructureIdText.TryParse(textKeepID.Text, out long KeepID) || !StructureIdText.TryParse(textSplitID.Text, out long SplitID))
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
            if (!StructureIdText.TryParse(textKeepID.Text, out long KeepLocID) || !StructureIdText.TryParse(textSplitID.Text, out long MergeLocID))
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
                    MessageBox.Show(except.Message, "Split error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
