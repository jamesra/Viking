using Geometry;
using System;
using System.Windows.Forms;

namespace Viking.UI.Forms
{
    public partial class ScreenshotForm : Form
    {
        static readonly bool UseViewerDownsampleChecked = true;
        static double LastDownsampleValue = 1.0f;
        static int NextCaptureNumber = 0;
        static string LastFileNamePrefix = "ScreenShot";

        /// <summary>
        /// Rectangle to be captured by the screenshot
        /// </summary>
        public GridRectangle Rect;

        /// <summary>
        /// Downsample level to use when capturing screenshot
        /// </summary>
        public double Downsample = ScreenshotForm.LastDownsampleValue;

        private readonly double ViewerDownsample;

        public string Filename
        {
            get { return textFilename.Text; }
            set { textFilename.Text = value; }
        }

        /// <summary>
        /// Set to true if overlays are to be included in screenshot
        /// </summary>
        /// <param name="myRect"></param>
        /// <param name="?"></param>
        /// 
        public bool IncludeOverlays = false;

        private readonly int _Z;

        public ScreenshotForm(GridRectangle myRect, double Downsample, int Z)
        {
            this._Z = Z;
            this.Rect = myRect;

            ViewerDownsample = Downsample;

            if (ScreenshotForm.UseViewerDownsampleChecked)
                this.Downsample = Downsample;
            else
                this.Downsample = ScreenshotForm.LastDownsampleValue;

            InitializeComponent();
        }

        private void ScreenshotForm_Load(object sender, EventArgs e)
        {
            this.checkUseViewerDownsample.Checked = ScreenshotForm.UseViewerDownsampleChecked;
            this.numDownsample.Value = (decimal)this.Downsample;
            this.numX.Value = (decimal)Math.Round(this.Rect.Left + (this.Rect.Width / 2));
            this.numY.Value = (decimal)Math.Round(this.Rect.Bottom + (this.Rect.Height / 2));
            this.numWidth.Value = (decimal)Math.Round(this.Rect.Width / this.Downsample);
            this.numHeight.Value = (decimal)Math.Round(this.Rect.Height / this.Downsample);

            this.textFilename.Text = ScreenshotForm.LastFileNamePrefix + "_" + ScreenshotForm.NextCaptureNumber.ToString("d03") + ".png";

            UpdateDownsampleControls();
            this.Update();
        }

        private void UpdateDownsampleControls()
        {
            this.numDownsample.Enabled = !this.checkUseViewerDownsample.Checked;

            decimal width = numWidth.Value * (decimal)this.Downsample;
            decimal height = numHeight.Value * (decimal)this.Downsample;

            if (this.checkUseViewerDownsample.Checked)
            {
                this.Downsample = ViewerDownsample;
            }
            else
            {
                this.Downsample = (double)this.numDownsample.Value;
            }

            width /= (decimal)this.Downsample;
            height /= (decimal)this.Downsample;

            numWidth.Value = width;
            numHeight.Value = height;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            double ROIWidth = (double)this.numWidth.Value * this.Downsample;
            double ROIHeight = (double)this.numHeight.Value * this.Downsample;

            this.Rect = new GridRectangle(new GridVector2((double)this.numX.Value - ROIWidth / 2,
                                                          (double)this.numY.Value - ROIHeight / 2),
                                                          ROIWidth,
                                                          ROIHeight);

            ScreenshotForm.LastDownsampleValue = this.Downsample;
            this.IncludeOverlays = this.checkOverlays.Checked;

            //Write down the filename and remove the automatically appended number if needed
            ScreenshotForm.LastFileNamePrefix = System.IO.Path.GetFileNameWithoutExtension(textFilename.Text);
            string CaptureNumberString = "_" + ScreenshotForm.NextCaptureNumber.ToString("d03");
            if (ScreenshotForm.LastFileNamePrefix.EndsWith(CaptureNumberString))
            {
                int i = ScreenshotForm.LastFileNamePrefix.LastIndexOf(CaptureNumberString);
                ScreenshotForm.LastFileNamePrefix = ScreenshotForm.LastFileNamePrefix.Remove(i);
            }

            ScreenshotForm.LastFileNamePrefix = System.IO.Path.GetDirectoryName(textFilename.Text) +
                                                System.IO.Path.DirectorySeparatorChar +
                                                ScreenshotForm.LastFileNamePrefix;

            ScreenshotForm.NextCaptureNumber++;

            this.DialogResult = DialogResult.OK;
            this.Close();

            //Try to create a descriptive text file matching the image name
            try
            {
                string dirname = System.IO.Path.GetDirectoryName(this.Filename);
                string basename = System.IO.Path.GetFileNameWithoutExtension(this.Filename);
                string MetaFilename = System.IO.Path.Combine(dirname, basename + ".txt");
                using (System.IO.StreamWriter textFile = System.IO.File.CreateText(MetaFilename))
                {
                    double X = this.Rect.Left;
                    double Y = this.Rect.Bottom;
                    textFile.WriteLine("Filename:\t" + Filename);
                    textFile.WriteLine("X: " + X.ToString() + "\tY: " + Y.ToString() + "\tZ: " + this._Z.ToString());
                    textFile.WriteLine("Width: " + Rect.Width.ToString() + "\tHeight: " + Rect.Height.ToString());
                    textFile.WriteLine("Downsample: " + Downsample.ToString());
                }
            }
            catch (Exception except)
            {
                MessageBox.Show("Error creating meta-data file for screen capture:\n" + except.Message, "Error");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog browserDlg = new SaveFileDialog())
            {
                browserDlg.FileName = this.textFilename.Text;
                browserDlg.Title = "Choose screenshot file name";
                browserDlg.OverwritePrompt = true;
                browserDlg.Filter = "Portable Network Graphic|*.png";
                browserDlg.DefaultExt = "png";
                browserDlg.AddExtension = true;
                browserDlg.AutoUpgradeEnabled = true;
                browserDlg.CheckPathExists = true;
                DialogResult result = browserDlg.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    this.textFilename.Text = browserDlg.FileName;
                }
            }
        }

        private void numDownsample_ValueChanged(object sender, EventArgs e)
        {
            UpdateDownsampleControls();
        }

        private void checkUseViewerDownsample_CheckedChanged(object sender, EventArgs e)
        {
            UpdateDownsampleControls();
        }
    }
}
