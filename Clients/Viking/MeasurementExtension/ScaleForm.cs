using System;
using System.Windows.Forms;

namespace MeasurementExtension
{
    public partial class ScaleForm : Form
    {
        public string UnitsOfMeasure;
        public double UnitsPerPixel;

        public ScaleForm(string unitsOfMeasure, double unitsPerPixel)
        {
            this.UnitsOfMeasure = unitsOfMeasure;
            this.UnitsPerPixel = unitsPerPixel;

            InitializeComponent();
        }

        private void ScaleForm_Load(object sender, EventArgs e)
        {
            this.comboUnits.Text = UnitsOfMeasure;
            //TODO: Populate combo box?
            this.numUnitsPerPixel.Value = System.Convert.ToDecimal(UnitsPerPixel);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            //Can't use greek letters, XNA doesn't like it.  Did not investigate
            this.UnitsOfMeasure = comboUnits.Text;
            this.UnitsPerPixel = System.Convert.ToDouble(numUnitsPerPixel.Value);
        }
    }
}
