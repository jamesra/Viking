using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace Viking.UI.Forms
{
    public partial class InputBox : Form
    {
        private readonly Func<string, bool> IsServerValid;

        public string Value
        {
            get
            {
                return this.textInput.Text;
            }

            set
            {
                this.textInput.Text = value;
            }
        }
        public InputBox(string Instructions, string DefaultText, Func<string, bool> IsServerValid)
        {
            this.IsServerValid = IsServerValid;

            InitializeComponent();

            this.labelText.Text = Instructions;
            this.Value = DefaultText;
        }

        private void InputBox_Load(object sender, EventArgs e)
        {

        }

        private void textInput_Validating(object sender, CancelEventArgs e)
        {
            e.Cancel = !IsServerValid(this.Value);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
