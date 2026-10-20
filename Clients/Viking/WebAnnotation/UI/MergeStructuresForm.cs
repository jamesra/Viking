using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

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
        private async Task<(bool Valid, string Reason)> VerifyTypeMatchAsync()
        {
            if (!StructureIdText.TryParse(textKeepID.Text, out long KeepID) || !StructureIdText.TryParse(textMergeID.Text, out long MergeID))
            {
                return (false, "Could not parse ID number");
            }

            StructureObj mergeStruct = await Store.Structures.GetObjectByID(MergeID);
            StructureObj keepStruct = await Store.Structures.GetObjectByID(KeepID);

            if (keepStruct is null && mergeStruct is null)
                return (false, "Input IDs must be a valid structures");
            if (keepStruct is null)
                return (false, "No structure matches Keep ID");
            if (mergeStruct is null)
                return (false, "No structure matches Merge ID");
            if (keepStruct.TypeID != mergeStruct.TypeID)
                return (false, string.Format("Merged structures must have the same type. Merged {1} is not a {0}", keepStruct.Type.Name, mergeStruct.Type.Name));

            return (true, null);
        }

        private async void btnMerge_Click(object sender, EventArgs e)
        {
            if (!StructureIdText.TryParse(textKeepID.Text, out long KeepID) || !StructureIdText.TryParse(textMergeID.Text, out long MergeID))
            {
                return;
            }

            try
            {
                await Store.Structures.Merge(KeepID, MergeID);
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
                MessageBox.Show(except.Message, "Merge error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e) => Close();

        private async Task<(bool Valid, string Reason)> IsIDValidAsync(string Input)
        {
            //Check if the string is empty, that is OK
            if (Input.Length == 0)
                return (true, null);

            //If string is not empty it needs to be a number
            if (!StructureIdText.TryParse(Input, out long ID))
                return (false, null);

            StructureObj obj = await Store.Structures.GetObjectByID(ID);
            if (obj is null)
                return (false, "No structure found");

            return (true, obj.Label);
        }

        private async Task<(bool Valid, string Reason)> IsAllInputValidAsync()
        {
            if (!StructureIdText.TryParse(textKeepID.Text, out long KeepID) || !StructureIdText.TryParse(textMergeID.Text, out long SplitID))
            {
                return (false, "Input ID is not a number");
            }

            if (KeepID == SplitID)
                return (false, "Cannot merge structure to itself");

            return await VerifyTypeMatchAsync();
        }

        private async void textIDLabel_TextChanged(object sender, EventArgs e)
        {
            await UpdateUIForIDLabelTextChangedAsync();
        }

        private async void textKeepIDLabel_TextChanged(object sender, EventArgs e)
        {
            var (IDValid, Reason) = await IsIDValidAsync(textKeepID.Text);
            textKeepLabel.Text = Reason;
            await UpdateUIForIDLabelTextChangedAsync();
        }

        private async void textMergeIDLabel_TextChanged(object sender, EventArgs e)
        {
            var (IDValid, Reason) = await IsIDValidAsync(textMergeID.Text);
            textMergeLabel.Text = Reason;
            await UpdateUIForIDLabelTextChangedAsync();
        }

        private async Task UpdateUIForIDLabelTextChangedAsync()
        {
            var (valid, Reason) = await IsAllInputValidAsync();
            if (!valid)
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
