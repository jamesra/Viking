using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using Viking.UI.Controls;

namespace Viking.UI.Forms
{
    public partial class GoToLocationForm : Form
    {
        public float X
        {
            get => (float)textX.DoubleValue;
            set => textX.Text = value.ToString();
        }

        public float Y
        {
            get => (float)textY.DoubleValue;
            set => textY.Text = value.ToString();
        }

        public int Z
        {
            get => textZ.IntValue;
            set => textZ.Text = value.ToString();
        }

        public double Downsample
        {
            get => textDownsample.DoubleValue;
            set => textDownsample.Text = value.ToString();
        }

        public GoToLocationForm()
        {
            InitializeComponent();
        }

        private void GoToLocationForm_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Control && e.KeyCode == Keys.V) ||
                (e.Shift && e.KeyCode == Keys.Insert))
            {
                string Data = Clipboard.GetText();
                if (string.IsNullOrEmpty(Data))
                    return;

                NumericTextBox[] fields = { textX, textY, textZ, textDownsample };
                IReadOnlyList<string> numbers = ParseNumbers(Data, CultureInfo.CurrentCulture.NumberFormat, fields.Length);
                for (int i = 0; i < numbers.Count; i++)
                    fields[i].Text = numbers[i];
            }
        }

        /// <summary>
        /// Splits pasted text into number strings in reading order, for filling X, Y, Z, and downsample.
        /// A number is a run of digits, negative signs, group separators, and decimal separators of
        /// <paramref name="numberFormat"/>; any other character ends it. A number starts at a digit, or at a
        /// negative sign or decimal separator that leads into one, so "-5000.5" and ".5" keep their prefix.
        /// Trailing separators and signs are dropped, so "100, 200, 34" in a comma-group culture yields "34", not "34,".
        /// </summary>
        /// <param name="text">Pasted text, such as "X: 1000.5 Y: 200 Z: 12".</param>
        /// <param name="numberFormat">Culture whose separators and negative sign the text uses.</param>
        /// <param name="maxCount">Stop after this many numbers.</param>
        /// <returns>At most <paramref name="maxCount"/> number strings; empty when the text has no digits.</returns>
        internal static IReadOnlyList<string> ParseNumbers(string text, NumberFormatInfo numberFormat, int maxCount)
        {
            var numbers = new List<string>(maxCount);
            if (string.IsNullOrEmpty(text))
                return numbers;

            bool IsNumberChar(char c) =>
                char.IsDigit(c) ||
                numberFormat.NegativeSign.IndexOf(c) >= 0 ||
                numberFormat.NumberGroupSeparator.IndexOf(c) >= 0 ||
                numberFormat.NumberDecimalSeparator.IndexOf(c) >= 0;

            // A sign or separator starts a number only when the run of number characters after it reaches a digit.
            bool LeadsToDigit(int i)
            {
                for (; i < text.Length && IsNumberChar(text[i]); i++)
                {
                    if (char.IsDigit(text[i]))
                        return true;
                }

                return false;
            }

            int iStart = -1;
            for (int i = 0; i <= text.Length && numbers.Count < maxCount; i++)
            {
                if (i < text.Length && IsNumberChar(text[i]))
                {
                    if (iStart == -1 && LeadsToDigit(i))
                        iStart = i;
                }
                else if (iStart > -1)
                {
                    int iEnd = i;
                    while (!char.IsDigit(text[iEnd - 1]))
                        iEnd--;

                    numbers.Add(text.Substring(iStart, iEnd - iStart));
                    iStart = -1;
                }
            }

            return numbers;
        }


    }
}
