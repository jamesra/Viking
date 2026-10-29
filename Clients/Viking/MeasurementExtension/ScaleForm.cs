using SIMeasurement;
using System;
using System.Globalization;
using System.Windows.Forms;

namespace MeasurementExtension
{
    /// <summary>
    /// Measurement > Set Scale: edits the unit of measure and units per pixel that measurements and the scale bar use. Opened
    /// by <see cref="MeasurementMenu.OnMenuSetScale"/>, which copies <see cref="UnitOfMeasure"/> and <see cref="UnitsPerPixel"/>
    /// into <see cref="Global"/> after OK.
    /// </summary>
    public partial class ScaleForm : Form
    {
        /// <summary>Largest units per pixel the box accepts or shows; far above any real volume scale.</summary>
        private const decimal MaxUnitsPerPixel = 1_000_000_000m;

        /// <summary>Fewest decimal places the box shows (the designer's original setting).</summary>
        private const int MinDecimalPlaces = 3;

        public SILengthUnits UnitOfMeasure { get; private set; }

        /// <summary>
        /// The scale given to the constructor, unchanged unless the user edited the number, so OK never rounds a volume's scale
        /// through the box's <see cref="decimal"/> or its decimal places.
        /// </summary>
        public double UnitsPerPixel { get; private set; }

        /// <summary>The value Load put in the box; a different value at OK means the user edited it.</summary>
        private decimal ShownUnitsPerPixel;

        public ScaleForm(SILengthUnits unitOfMeasure, double unitsPerPixel)
        {
            this.UnitOfMeasure = unitOfMeasure;
            this.UnitsPerPixel = unitsPerPixel;

            InitializeComponent();
        }

        /// <summary>
        /// Shows the scale with every digit its <see cref="decimal"/> has, sizing the box's decimal places and range from it; a
        /// scale outside the range (not positive, too small for a decimal, or over <see cref="MaxUnitsPerPixel"/>) shows the
        /// nearest end instead of throwing.
        /// </summary>
        private void ScaleForm_Load(object sender, EventArgs e)
        {
            this.comboUnits.Text = UnitOfMeasure.ToString();

            decimal exact = UnitsPerPixel > 0 ? (decimal)Math.Min(UnitsPerPixel, (double)MaxUnitsPerPixel) : 0m;
            int places = Math.Max(FractionDigits(exact), MinDecimalPlaces);

            numUnitsPerPixel.DecimalPlaces = places;
            numUnitsPerPixel.Minimum = new decimal(1, 0, 0, false, (byte)places);
            numUnitsPerPixel.Maximum = MaxUnitsPerPixel;
            ShownUnitsPerPixel = Math.Max(exact, numUnitsPerPixel.Minimum);
            numUnitsPerPixel.Value = ShownUnitsPerPixel;
        }

        /// <summary>Number of digits after the decimal point, ignoring trailing zeros.</summary>
        private static int FractionDigits(decimal value)
        {
            string text = value.ToString(CultureInfo.InvariantCulture);
            int point = text.IndexOf('.');
            return point < 0 ? 0 : text.TrimEnd('0').Length - point - 1;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (TryAccept())
                return;

            // Keep the dialog open so the unit can be corrected.
            this.DialogResult = DialogResult.None;
            MessageBox.Show(this, $"\"{comboUnits.Text}\" is not a unit of measure Viking knows. Pick one from the list, for example nm or um (case matters: mm is millimetres, Mm megametres).",
                "Set scale", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Stores the dialog's unit and, when the user edited the number, its units per pixel. Called by OK.
        /// </summary>
        /// <returns>False, changing nothing, when the unit text names no <see cref="SILengthUnits"/> member.</returns>
        internal bool TryAccept()
        {
            if (!TryParseUnit(comboUnits.Text, out SILengthUnits unit))
                return false;

            UnitOfMeasure = unit;
            if (numUnitsPerPixel.Value != ShownUnitsPerPixel)
                UnitsPerPixel = (double)numUnitsPerPixel.Value;
            return true;
        }

        /// <summary>
        /// Parses typed or picked unit text as an <see cref="SILengthUnits"/> name, ignoring surrounding spaces. Case-sensitive,
        /// because "mm" and "Mm" are different units; numbers and other text are refused. Units are ASCII ("um", not the Greek
        /// mu), because XNA could not draw Greek letters.
        /// </summary>
        internal static bool TryParseUnit(string? text, out SILengthUnits unit)
        {
            unit = default;
            string name = text?.Trim() ?? string.Empty;
            if (!Enum.IsDefined(typeof(SILengthUnits), name))
                return false;

            unit = (SILengthUnits)Enum.Parse(typeof(SILengthUnits), name);
            return true;
        }
    }
}
