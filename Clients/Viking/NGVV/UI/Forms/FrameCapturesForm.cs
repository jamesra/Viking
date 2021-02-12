using Geometry;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Viking.UI.Forms
{
    public struct FrameCapture
    {
        public string Filename;
        public double Z;
        public GridRectangle Rect;
        public double downsample;
        public bool IncludeOverlay;
    }

    public partial class FrameCapturesForm : Form
    {
        public FrameCapture[] Frames = new FrameCapture[0];

        public string Prefix = Properties.Settings.Default.FrameExportPrefix;
        public string Path = Properties.Settings.Default.FrameExportPath;
          
        private const int iX = 0;
        private const int iY = 1;
        private const int iZ = 2;
        private const int iWidth = 3;
        private const int iHeight = 4;
        private const int iDownsample = 5;

        public FrameCapturesForm()
        {
            InitializeComponent();
        }

        private void FrameCapturesForm_Load(object sender, EventArgs e)
        {
            if (Prefix == null)
            {
                this.textPrefix.Text = "";
            }

            if (String.IsNullOrEmpty(this.Path))
            {
                this.textPath.Text = System.Environment.CurrentDirectory;
            }
            else if(System.IO.Directory.Exists(this.Path) == false)
            {
                this.textPath.Text = System.Environment.CurrentDirectory;
            }
            else
                this.textPath.Text = Path;
        }

        private void FrameCapturesForm_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Control && e.KeyCode == Keys.V) ||
                (e.Shift && e.KeyCode == Keys.Insert))
            {
                string Data = Clipboard.GetText();
                string[] lines = Data.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                DataGridViewSelectedRowCollection rows = this.dataGridView.SelectedRows;
                int iStartRow = int.MaxValue;
                for (int i = 0; i < rows.Count; i++)
                {
                    //Delete count rows from the selected row
                    if (rows[i].Index < iStartRow)
                        iStartRow = rows[i].Index;
                }

                if (iStartRow == int.MaxValue)
                    iStartRow = 0;

                int iRow = 0;
                for (iRow = iStartRow; iRow < iStartRow + lines.Length; iRow++)
                {
                    string[] parts = lines[iRow - iStartRow].Split(new char[] { '\t' });
                    int iWriteRow = iRow;
                    if (this.dataGridView.Rows.Count <= iRow)
                    {
                        iWriteRow = this.dataGridView.Rows.Add();
                    }

                    for (int iCol = 0; iCol < parts.Length; iCol++)
                    {
                        if (iCol < this.dataGridView.ColumnCount)
                            this.dataGridView.Rows[iWriteRow].Cells[iCol].Value = parts[iCol];
                    }
                }
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            //These need to be initialized before we create frame structures
            this.Prefix = this.textPrefix.Text;
            
            Properties.Settings.Default.FrameExportPrefix = this.Prefix;
            
             
            this.Path = this.textPath.Text;

            if(System.IO.Directory.Exists(this.Path))
                Properties.Settings.Default.FrameExportPath = this.Path;

            Properties.Settings.Default.Save();

            int FirstFrameNum = System.Convert.ToInt32(this.numStartFrame.Value);

            //Walk each cell and create a screenshot object
            List<FrameCapture> listFrames = new List<FrameCapture>();
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                try
                {
                    FrameCapture frame = new FrameCapture();

                    double X = System.Convert.ToDouble(row.Cells[iX].Value);
                    double Y = System.Convert.ToDouble(row.Cells[iY].Value);
                    double Width = 512;
                    double Height = 512;
                    if (row.Cells[iWidth].Value != null)
                        Width = System.Convert.ToDouble(row.Cells[iWidth].Value);

                    if (row.Cells[iHeight].Value != null)
                        Height = System.Convert.ToDouble(row.Cells[iHeight].Value);

                    frame.downsample = 1;
                    if (row.Cells[iDownsample].Value != null)
                        frame.downsample = System.Convert.ToDouble(row.Cells[iDownsample].Value);

                    frame.IncludeOverlay = checkIncludeOverlays.Checked;

                    //Width and Height are the target image width and height.  Figure out what the ROI Width and Height should be
                    double ROIWidth = Width * frame.downsample;
                    double ROIHeight = Height * frame.downsample;

                    //Adjust frame so it is centered on X/Y coordinates
                    X -= ROIWidth / 2;
                    Y -= ROIHeight / 2;

                    frame.Rect = new GridRectangle(new GridVector2(X, Y),
                                                   ROIWidth,
                                                   ROIHeight);

                    string Filename = Prefix + (FirstFrameNum + row.Index).ToString() + ".png";
                    string FullpathFilename = System.IO.Path.Combine(Path, Filename);
                    frame.Z = System.Convert.ToDouble(row.Cells[iZ].Value);
                    frame.Filename = FullpathFilename;

                    listFrames.Add(frame);
                }
                catch (Exception)
                {
                    DialogResult result = MessageBox.Show("Could not parse row #" + row.Index.ToString() + " skipping", "Error", MessageBoxButtons.OKCancel);
                    //If they cancel then return without doing anything. 
                    if (result == DialogResult.Cancel)
                        return;
                }
            }
              
            this.Frames = listFrames.ToArray();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog browserDlg = new FolderBrowserDialog())
            {
                //browserDlg.SelectedPath = System.Environment.CurrentDirectory;
                browserDlg.Description = "Choose screenshot file name";
                DialogResult result = browserDlg.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    this.textPath.Text = browserDlg.SelectedPath;
                }
            }


        }
    }
}
