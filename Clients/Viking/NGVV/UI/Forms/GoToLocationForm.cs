using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace Viking.UI.Forms
{
    public partial class GoToLocationForm : Form
    {
        public float X
        {
            get { return (float)textX.DoubleValue; }
            set { textX.Text = value.ToString(); }
        }

        public float Y
        {
            get { return (float)textY.DoubleValue; }
            set { textY.Text = value.ToString(); }
        }

        public int Z
        {
            get { return textZ.IntValue; }
            set { textZ.Text = value.ToString(); }
        }

        public double Downsample
        {
            get { return textDownsample.DoubleValue; }
            set { textDownsample.Text = value.ToString(); }
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
                System.Globalization.NumberFormatInfo numberFormatInfo = System.Globalization.CultureInfo.CurrentCulture.NumberFormat;
                string decimalSeparator = numberFormatInfo.NumberDecimalSeparator;
                string groupSeparator = numberFormatInfo.NumberGroupSeparator;
                string negativeSign = numberFormatInfo.NegativeSign;

                string Data = Clipboard.GetText();

                if (Data == null)
                    return;

                if (Data.Length == 0)
                    return;

                //March along looking for digits, convert digits we find to values
                int i = 0;
                int iStart = -1;
                int iValue = 0; //How many values we've extracted. Stop after three
                do
                {
                    //Convert the first number
                    if (char.IsDigit(Data[i]))
                    {
                        if (iStart == -1)
                            iStart = i;
                    }
                    else if (negativeSign.Contains(Data[i]) == false &&
                            groupSeparator.Contains(Data[i]) == false &&
                            decimalSeparator.Contains(Data[i]) == false)
                    {
                        //If we are building a number then stop looking and convert, otherwise continue
                        if (iStart > -1)
                        {
                            string numString = Data.Substring(iStart, i - iStart);

                            switch (iValue)
                            {
                                case 0:
                                    textX.Text = numString;
                                    break;
                                case 1:
                                    textY.Text = numString;
                                    break;
                                case 2:
                                    textZ.Text = numString;
                                    break;
                                case 3:
                                    textDownsample.Text = numString;
                                    break;
                                default:
                                    Debug.Assert(false, "Parsed more than three numbers during paste");
                                    break;
                            }

                            iValue++;
                            iStart = -1;
                        }
                    }

                    i++;
                }
                while (i < Data.Length && iValue < 4);

                //If we are building a number then stop looking and convert, otherwise continue
                if (iStart > -1)
                {
                    string numString = Data.Substring(iStart, i - iStart);

                    switch (iValue)
                    {
                        case 0:
                            textX.Text = numString;
                            break;
                        case 1:
                            textY.Text = numString;
                            break;
                        case 2:
                            textZ.Text = numString;
                            break;
                        case 3:
                            textDownsample.Text = numString;
                            break;
                        default:
                            Debug.Assert(false, "Parsed more than three numbers during paste");
                            break;
                    }

                    iValue++;
                    iStart = -1;
                }

                //string[] lines = Data.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }


    }
}
