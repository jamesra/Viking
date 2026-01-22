using System;
using System.Windows.Forms;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI
{
    public partial class FindStructureNumberForm : Form
    {
        public FindStructureNumberForm()
        {
            InitializeComponent();
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            int StructureNumber;
            try
            {
                StructureNumber = numStructure.IntValue;
            }
            catch (FormatException)
            {
                return;
            }

            StructureObj structure = Store.Structures.GetObjectByID(StructureNumber);
            if (structure is null)
            {
                MessageBox.Show(this, "No structure found with that ID", "Error", MessageBoxButtons.OK);
                return;
            }

            Structure structView = new(structure);

            structView.ShowProperties();

            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e) => Close();
    }
}
