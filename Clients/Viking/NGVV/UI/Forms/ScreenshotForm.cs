using Geometry;
using System;
using System.Windows.Forms;

namespace Viking.UI.Forms
{
    public partial class ScreenshotForm : Form
    {
        static bool UseViewerDownsampleChecked = true;
        static double LastDownsampleValue = 1.0f;
        static int NextCaptureNumber
        {
            get => Properties.Settings.Default.ScreenShotNumber;
            set => Properties.Settings.Default.ScreenShotNumber = value;
        }

        static string LastFileNamePrefix
        {
            get => Properties.Settings.Default.ScreenShotFilePrefix;
            set => Properties.Settings.Default.ScreenShotFilePrefix = value;
        }


        /// <summary>
        /// The string to append to the next filename captured
        /// </summary>
        static string NextCaptureNumberString => "_" + ScreenshotForm.NextCaptureNumber.ToString("d03");

        /// <summary>
        /// Rectangle to be captured by the screenshot
        /// </summary>
        public Rectangle Rect;

        /// <summary>
        /// Downsample level to use when capturing screenshot
        /// </summary>
        public double Downsample = ScreenshotForm.LastDownsampleValue;

        private readonly double ViewerDownsample;

        public string Filename
        {
            get => Environment.ExpandEnvironmentVariables(System.IO.Path.Combine(textFolder.Text, textFilename.Text));
            set
            {
                textFolder.Text = System.IO.Path.GetDirectoryName(value);
                textFilename.Text = System.IO.Path.GetFileName(value);
            }
        }

        /// <summary>
        /// Set to true if overlays are to be included in screenshot
        /// </summary>
        /// <param name="myRect"></param>
        /// <param name="?"></param>
        /// 
        public bool IncludeOverlays = false;

        private readonly int _Z;

        public ScreenshotForm(Rectangle myRect, double Downsample, int Z)
        {
            this._Z = Z;
            this.Rect = myRect;

            ViewerDownsample = Downsample;

            this.Downsample = ScreenshotForm.UseViewerDownsampleChecked ? Downsample : ScreenshotForm.LastDownsampleValue;

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
            ScreenshotForm.UseViewerDownsampleChecked = this.checkUseViewerDownsample.Checked;
            this.numDownsample.Enabled = !this.checkUseViewerDownsample.Checked;

            decimal width = numWidth.Value * (decimal)this.Downsample;
            decimal height = numHeight.Value * (decimal)this.Downsample;

            this.Downsample = checkUseViewerDownsample.Checked ? ViewerDownsample : (double)this.numDownsample.Value;

            width /= (decimal)this.Downsample;
            height /= (decimal)this.Downsample;

            numWidth.Value = width;
            numHeight.Value = height;
        }

        private static string StringCaptureNumberFromName(string filename)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(filename);
            string captureNumberString = NextCaptureNumberString;
            if (name.EndsWith(captureNumberString))
            {
                int i = name.LastIndexOf(captureNumberString);
                return name.Remove(i);
            }

            return name;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            double ROIWidth = (double)this.numWidth.Value * this.Downsample;
            double ROIHeight = (double)this.numHeight.Value * this.Downsample;

            this.Rect = new Rectangle(new Vector2((double)this.numX.Value - ROIWidth / 2,
                                                          (double)this.numY.Value - ROIHeight / 2),
                                                          ROIWidth,
                                                          ROIHeight);

            ScreenshotForm.LastDownsampleValue = this.Downsample;
            this.IncludeOverlays = this.checkOverlays.Checked;

            //Write down the filename and remove the automatically appended number if needed
            ScreenshotForm.LastFileNamePrefix = System.IO.Path.GetFileNameWithoutExtension(textFilename.Text);
            string CaptureNumberString = NextCaptureNumberString;
            if (ScreenshotForm.LastFileNamePrefix.EndsWith(CaptureNumberString))
            {
                int i = ScreenshotForm.LastFileNamePrefix.LastIndexOf(CaptureNumberString);
                ScreenshotForm.LastFileNamePrefix = ScreenshotForm.LastFileNamePrefix.Remove(i);
            }

            ScreenshotForm.LastFileNamePrefix = StringCaptureNumberFromName(this.textFilename.Text);
            ScreenshotForm.NextCaptureNumber++;

            this.DialogResult = DialogResult.OK;
            Properties.Settings.Default.Save();
            this.Close();

            //Try to create a descriptive text file matching the image name
            try
            {
                string dirname = this.textFolder.Text;
                string expandedDirname = Environment.ExpandEnvironmentVariables(dirname);
                string basename = System.IO.Path.GetFileNameWithoutExtension(textFilename.Text);
                string expandedBasename = Environment.ExpandEnvironmentVariables(basename);
                string MetaFilename = System.IO.Path.Combine(expandedDirname, expandedBasename + ".txt");
                using System.IO.StreamWriter textFile = System.IO.File.CreateText(MetaFilename);
                double X = this.Rect.Left;
                double Y = this.Rect.Bottom;
                textFile.WriteLine("Filename:\t" + Filename);
                textFile.WriteLine("X: " + X.ToString() + "\tY: " + Y.ToString() + "\tZ: " + this._Z.ToString());
                textFile.WriteLine("Width: " + Rect.Width.ToString() + "\tHeight: " + Rect.Height.ToString());
                textFile.WriteLine("Downsample: " + Downsample.ToString());
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
            using SaveFileDialog browserDlg = new();
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
                this.textFilename.Text = System.IO.Path.GetFileNameWithoutExtension(browserDlg.FileName);

                this.textFilename.Text = System.IO.Path.GetDirectoryName(browserDlg.FileName);
            }
        }

        private void numDownsample_ValueChanged(object sender, EventArgs e) => UpdateDownsampleControls();

        private void checkUseViewerDownsample_CheckedChanged(object sender, EventArgs e) => UpdateDownsampleControls();

        private void textFolder_TextChanged(object sender, EventArgs e) => Properties.Settings.Default.LastScreenshotPath = textFolder.Text;
    }
}
