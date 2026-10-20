using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Viking.UI.Forms
{
    public struct FrameCapture
    {
        public string Filename;
        public double Z;
        public Rectangle Rect;
        public double downsample;
        public bool IncludeOverlay;
    }

    public partial class FrameCapturesForm : Form
    {
        public FrameCapture[] Frames = [];

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
            if (Prefix is null)
            {
                this.textPrefix.Text = "";
            }

            if (String.IsNullOrEmpty(this.Path))
            {
                this.textPath.Text = System.Environment.CurrentDirectory;
            }
            else
            {
                this.textPath.Text = System.IO.Directory.Exists(this.Path) == false
                    ? Environment.CurrentDirectory
                    : Environment.ExpandEnvironmentVariables(Path);
            }
        }

        private void FrameCapturesForm_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Control && e.KeyCode == Keys.V) ||
                (e.Shift && e.KeyCode == Keys.Insert))
            {
                string Data = Clipboard.GetText();
                string[] lines = Data.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

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
                    string[] parts = lines[iRow - iStartRow].Split(['\t']);
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

            if (System.IO.Directory.Exists(this.Path))
                Properties.Settings.Default.FrameExportPath = this.Path;

            Properties.Settings.Default.Save();

            int FirstFrameNum = System.Convert.ToInt32(this.numStartFrame.Value);

            //Walk each cell and create a screenshot object
            List<FrameCapture> listFrames = [];
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                try
                {
                    object?[] cells = new object?[row.Cells.Count];
                    for (int iCol = 0; iCol < cells.Length; iCol++)
                        cells[iCol] = row.Cells[iCol].Value;

                    string Filename = Prefix + (FirstFrameNum + row.Index).ToString() + ".png";
                    FrameCapture? frame = ParseFrameRow(cells, checkIncludeOverlays.Checked, System.IO.Path.Combine(Path, Filename));
                    if (frame.HasValue)
                        listFrames.Add(frame.Value);
                }
                catch (Exception)
                {
                    DialogResult result = MessageBox.Show("Could not parse row #" + row.Index.ToString() + " skipping", "Error", MessageBoxButtons.OKCancel);
                    //If they cancel then return without doing anything. 
                    if (result == DialogResult.Cancel)
                        return;
                }
            }

            this.Frames = [.. listFrames];
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// Turns one row of the frame grid into a <see cref="FrameCapture"/> centered on the row's X and Y.
        /// </summary>
        /// <param name="cells">Cell values in grid column order: X, Y, Z, image width, image height, downsample.</param>
        /// <param name="includeOverlay">Whether the frame is rendered with annotation overlays.</param>
        /// <param name="filename">Full path of the PNG the frame is written to.</param>
        /// <returns>The frame, or <c>null</c> when every cell is blank (the grid's new-row placeholder or an empty row).</returns>
        /// <exception cref="FormatException">X, Y, or Z is blank while another cell is not, or a cell is not a number.</exception>
        /// <remarks>Width and height default to 512 and downsample to 1 when their cells are null, empty, or white space.</remarks>
        internal static FrameCapture? ParseFrameRow(IReadOnlyList<object?> cells, bool includeOverlay, string filename)
        {
            static bool IsBlank(object? value) => value is null || (value is string s && string.IsNullOrWhiteSpace(s));
            object? Cell(int i) => i < cells.Count ? cells[i] : null;

            if (cells.All(IsBlank))
                return null;

            //Convert.ToDouble(null) is 0, so a blank X, Y, or Z would silently become 0 instead of an error
            double Required(int i) => IsBlank(Cell(i))
                ? throw new FormatException($"Column {i} is required")
                : System.Convert.ToDouble(Cell(i));

            double X = Required(iX);
            double Y = Required(iY);
            //A pasted spreadsheet row leaves trailing optional cells as "", which Convert.ToDouble rejects
            double Optional(int i, double defaultValue) => IsBlank(Cell(i)) ? defaultValue : System.Convert.ToDouble(Cell(i));

            double Width = Optional(iWidth, 512);
            double Height = Optional(iHeight, 512);
            double downsample = Optional(iDownsample, 1);

            //Width and Height are the target image width and height.  Figure out what the ROI Width and Height should be
            double ROIWidth = Width * downsample;
            double ROIHeight = Height * downsample;

            //Adjust frame so it is centered on X/Y coordinates
            return new FrameCapture
            {
                Filename = filename,
                Z = Required(iZ),
                Rect = new Rectangle(new Vector2(X - (ROIWidth / 2), Y - (ROIHeight / 2)), ROIWidth, ROIHeight),
                downsample = downsample,
                IncludeOverlay = includeOverlay
            };
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using FolderBrowserDialog browserDlg = new();
            //browserDlg.SelectedPath = System.Environment.CurrentDirectory;
            browserDlg.Description = "Choose screenshot file name";
            DialogResult result = browserDlg.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                this.textPath.Text = Environment.ExpandEnvironmentVariables(browserDlg.SelectedPath);
            }


        }
    }
}
