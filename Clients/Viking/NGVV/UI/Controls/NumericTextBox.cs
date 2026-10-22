using System;
using System.Windows.Forms;

namespace Viking.UI.Controls
{
    public class NumericTextBox : TextBox
    {
        bool allowSpace = false;

        // Restricts the entry of characters to digits (including hex), the negative sign,
        // the decimal point, and editing keystrokes (backspace).
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            System.Globalization.NumberFormatInfo numberFormatInfo = System.Globalization.CultureInfo.CurrentCulture.NumberFormat;
            string decimalSeparator = numberFormatInfo.NumberDecimalSeparator;
            string groupSeparator = numberFormatInfo.NumberGroupSeparator;
            string negativeSign = numberFormatInfo.NegativeSign;

            string keyInput = e.KeyChar.ToString();

            if (Char.IsDigit(e.KeyChar))
            {
                // Digits are OK
            }
            else if (keyInput.Equals(decimalSeparator) || keyInput.Equals(groupSeparator) ||
             keyInput.Equals(negativeSign))
            {
                // Decimal separator is OK
            }
            else if (e.KeyChar == '\b')
            {
                // Backspace key is OK
            }
            //    else if ((ModifierKeys & (Keys.Control | Keys.Alt)) != 0)
            //    {
            //     // Let the edit control handle control and alt key combinations
            //    }
            else if (this.allowSpace && e.KeyChar == ' ')
            {

            }
            else
            {
                // Consume this invalid key and beep
                e.Handled = true;
                //    MessageBeep();
            }
        }

        /// <summary>
        /// Text parsed as an <see cref="int"/> in the current culture, or 0 when it is empty, malformed, or out of
        /// range. The key filter admits any run of digits, so overflow is reachable and must not throw.
        /// </summary>
        public int IntValue => int.TryParse(this.Text, out int val) ? val : 0;

        /// <summary>
        /// Text parsed as a <see cref="double"/> in the current culture, or 0 when it is empty, malformed, or out of range.
        /// </summary>
        public double DoubleValue => double.TryParse(this.Text, out double val) ? val : 0;

        public bool AllowSpace
        {
            set => this.allowSpace = value;

            get => this.allowSpace;
        }

    }
}
