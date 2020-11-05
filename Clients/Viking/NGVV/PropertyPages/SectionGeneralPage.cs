using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Viking.Common;
using Viking.ViewModels;

namespace Viking.PropertyPages
{
    [PropertyPage(typeof(SectionViewModel), 1)]
    public partial class SectionGeneralPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        SectionViewModel Obj;

        public SectionGeneralPage()
        {
            InitializeComponent();
        }

        public static byte[] ConvertHexStringToByteArray(string hexString)
        {
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "The binary key cannot have an odd number of digits: {0}", hexString));
            }

            byte[] HexAsBytes = new byte[hexString.Length / 2];
            for (int index = 0; index < HexAsBytes.Length; index++)
            {
                string byteValue = hexString.Substring(index * 2, 2);
                HexAsBytes[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return HexAsBytes;
        }

        public void SetRTFText(string text)
        {


            try
            {

                //byte[] data = ConvertHexStringToByteArray(text);
                //string rtfText = System.Text.ASCIIEncoding.Default.GetString(data);
                richNotes.Rtf = text;
                //MemoryStream stream = new MemoryStream(data);
                //this.richNotes.LoadFile(stream, RichTextBoxStreamType.RichText);
            }
            catch
            {
                this.richNotes.Text = text;
            }
        }

        protected override void OnShowObject(object Object)
        {
            this.Obj = Object as SectionViewModel;
            Debug.Assert(this.Obj != null);

            this.textSectionNameNumber.Text = Obj.Number.ToString() + " : " + Obj.Name;

            try
            {

                SetRTFText(Obj.Notes);
            }
            catch
            {

            }

            List<int> SectionNumbers = new List<int>(UI.State.volume.SectionViewModels.Keys);
            SectionNumbers.Sort();

            int ReferenceAbove = int.MaxValue;
            int ReferenceBelow = int.MinValue;

            if (Obj.ReferenceSectionAbove != null)
                ReferenceAbove = Obj.ReferenceSectionAbove.Number;

            if (Obj.ReferenceSectionBelow != null)
                ReferenceBelow = Obj.ReferenceSectionBelow.Number;

            for (int iSection = 0; iSection < SectionNumbers.Count; iSection++)
            {
                int SectionNumber = SectionNumbers[iSection];
                if (SectionNumber < Obj.Number)
                {
                    listBelow.Items.Add(SectionNumber);
                    if (SectionNumber == ReferenceBelow)
                        listBelow.SelectedItem = SectionNumber;

                }
                else if (SectionNumber > Obj.Number)
                {
                    listAbove.Items.Add(SectionNumber);
                    if (SectionNumber == ReferenceAbove)
                        listAbove.SelectedItem = SectionNumber;
                }
            }
        }

        protected override void OnSaveChanges()
        {
            if (listAbove.SelectedItem != null)
            {
                Obj.ReferenceSectionAbove = Obj.VolumeViewModel.SectionViewModels[(int)listAbove.SelectedItem].section;
            }

            if (listBelow.SelectedItem != null)
            {
                Obj.ReferenceSectionBelow = Obj.VolumeViewModel.SectionViewModels[(int)listBelow.SelectedItem].section;
            }

        }

        private void richNotes_TextChanged(object sender, EventArgs e)
        {

        }

    }
}
