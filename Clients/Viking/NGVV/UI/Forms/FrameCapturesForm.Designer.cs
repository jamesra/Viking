namespace Viking.UI.Forms
{
    partial class FrameCapturesForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrameCapturesForm));
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.colX = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colY = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colZ = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colImageWidth = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colImageHeight = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDownSample = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.labelPath = new System.Windows.Forms.Label();
            this.labelFilePrefix = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.numStartFrame = new System.Windows.Forms.NumericUpDown();
            this.checkIncludeOverlays = new System.Windows.Forms.CheckBox();
            this.textPath = new System.Windows.Forms.TextBox();
            this.textPrefix = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStartFrame)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToResizeRows = false;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colX,
            this.colY,
            this.colZ,
            this.colImageWidth,
            this.colImageHeight,
            this.colDownSample});
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Top;
            this.dataGridView.Location = new System.Drawing.Point(0, 0);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.Size = new System.Drawing.Size(373, 206);
            this.dataGridView.TabIndex = 0;
            this.dataGridView.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FrameCapturesForm_KeyDown);
            // 
            // colX
            // 
            this.colX.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle1.Format = "N2";
            dataGridViewCellStyle1.NullValue = "0";
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.colX.DefaultCellStyle = dataGridViewCellStyle1;
            this.colX.HeaderText = "X";
            this.colX.MaxInputLength = 32;
            this.colX.Name = "colX";
            this.colX.ToolTipText = "Center of frame in X coordinates";
            // 
            // colY
            // 
            this.colY.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle2.Format = "N2";
            dataGridViewCellStyle2.NullValue = "0";
            this.colY.DefaultCellStyle = dataGridViewCellStyle2;
            this.colY.HeaderText = "Y";
            this.colY.MaxInputLength = 32;
            this.colY.Name = "colY";
            this.colY.ToolTipText = "Center of frame in Y coordinates";
            // 
            // colZ
            // 
            this.colZ.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle3.Format = "N0";
            dataGridViewCellStyle3.NullValue = "0";
            this.colZ.DefaultCellStyle = dataGridViewCellStyle3;
            this.colZ.HeaderText = "Z";
            this.colZ.MaxInputLength = 32;
            this.colZ.Name = "colZ";
            this.colZ.ToolTipText = "Section #";
            // 
            // colImageWidth
            // 
            this.colImageWidth.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle4.Format = "N0";
            dataGridViewCellStyle4.NullValue = "512";
            this.colImageWidth.DefaultCellStyle = dataGridViewCellStyle4;
            this.colImageWidth.HeaderText = "Image Width";
            this.colImageWidth.MaxInputLength = 32;
            this.colImageWidth.Name = "colImageWidth";
            this.colImageWidth.ToolTipText = "Width of Image";
            // 
            // colImageHeight
            // 
            this.colImageHeight.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle5.Format = "N0";
            dataGridViewCellStyle5.NullValue = "512";
            this.colImageHeight.DefaultCellStyle = dataGridViewCellStyle5;
            this.colImageHeight.HeaderText = "Image Height";
            this.colImageHeight.MaxInputLength = 32;
            this.colImageHeight.Name = "colImageHeight";
            this.colImageHeight.ToolTipText = "Height of Image";
            // 
            // colDownSample
            // 
            this.colDownSample.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle6.Format = "N2";
            dataGridViewCellStyle6.NullValue = "1";
            this.colDownSample.DefaultCellStyle = dataGridViewCellStyle6;
            this.colDownSample.HeaderText = "Down Sample";
            this.colDownSample.MaxInputLength = 32;
            this.colDownSample.Name = "colDownSample";
            this.colDownSample.ToolTipText = "Downsample level of frame";
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(242, 216);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(53, 23);
            this.btnBrowse.TabIndex = 12;
            this.btnBrowse.Text = "...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // btnExport
            // 
            this.btnExport.Location = new System.Drawing.Point(12, 300);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(75, 23);
            this.btnExport.TabIndex = 11;
            this.btnExport.Text = "Export";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // labelPath
            // 
            this.labelPath.AutoSize = true;
            this.labelPath.Location = new System.Drawing.Point(12, 219);
            this.labelPath.Name = "labelPath";
            this.labelPath.Size = new System.Drawing.Size(32, 13);
            this.labelPath.TabIndex = 9;
            this.labelPath.Text = "Path:";
            // 
            // labelFilePrefix
            // 
            this.labelFilePrefix.AutoSize = true;
            this.labelFilePrefix.Location = new System.Drawing.Point(12, 245);
            this.labelFilePrefix.Name = "labelFilePrefix";
            this.labelFilePrefix.Size = new System.Drawing.Size(55, 13);
            this.labelFilePrefix.TabIndex = 7;
            this.labelFilePrefix.Text = "File Prefix:";
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(286, 299);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 13;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 271);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(66, 13);
            this.label1.TabIndex = 14;
            this.label1.Text = "1st Frame #:";
            // 
            // numStartFrame
            // 
            this.numStartFrame.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::Viking.Properties.Settings.Default, "ExportFramesFirstFrameNumber", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.numStartFrame.Location = new System.Drawing.Point(78, 269);
            this.numStartFrame.Maximum = new decimal(new int[] {
            10000000,
            0,
            0,
            0});
            this.numStartFrame.Name = "numStartFrame";
            this.numStartFrame.Size = new System.Drawing.Size(100, 20);
            this.numStartFrame.TabIndex = 15;
            this.numStartFrame.Value = global::Viking.Properties.Settings.Default.ExportFramesFirstFrameNumber;
            // 
            // checkIncludeOverlays
            // 
            this.checkIncludeOverlays.AutoSize = true;
            this.checkIncludeOverlays.Checked = global::Viking.Properties.Settings.Default.ExportFramesIncludeOverlays;
            this.checkIncludeOverlays.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::Viking.Properties.Settings.Default, "ExportFramesIncludeOverlays", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.checkIncludeOverlays.Location = new System.Drawing.Point(242, 276);
            this.checkIncludeOverlays.Name = "checkIncludeOverlays";
            this.checkIncludeOverlays.Size = new System.Drawing.Size(105, 17);
            this.checkIncludeOverlays.TabIndex = 16;
            this.checkIncludeOverlays.Text = "Include Overlays";
            this.checkIncludeOverlays.UseVisualStyleBackColor = true;
            // 
            // textPath
            // 
            this.textPath.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Viking.Properties.Settings.Default, "LastFrameCaptureExportDirectory", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textPath.Location = new System.Drawing.Point(78, 216);
            this.textPath.Name = "textPath";
            this.textPath.Size = new System.Drawing.Size(168, 20);
            this.textPath.TabIndex = 10;
            this.textPath.Text = global::Viking.Properties.Settings.Default.LastFrameCaptureExportDirectory;
            // 
            // textPrefix
            // 
            this.textPrefix.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Viking.Properties.Settings.Default, "FrameCaptureDefaultPrefix", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textPrefix.Location = new System.Drawing.Point(78, 242);
            this.textPrefix.Name = "textPrefix";
            this.textPrefix.Size = new System.Drawing.Size(100, 20);
            this.textPrefix.TabIndex = 8;
            this.textPrefix.Text = global::Viking.Properties.Settings.Default.FrameCaptureDefaultPrefix;
            // 
            // FrameCapturesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(373, 334);
            this.Controls.Add(this.checkIncludeOverlays);
            this.Controls.Add(this.numStartFrame);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.btnExport);
            this.Controls.Add(this.textPath);
            this.Controls.Add(this.labelPath);
            this.Controls.Add(this.textPrefix);
            this.Controls.Add(this.labelFilePrefix);
            this.Controls.Add(this.dataGridView);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FrameCapturesForm";
            this.Text = "Frame Capture Setup";
            this.Load += new System.EventHandler(this.FrameCapturesForm_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FrameCapturesForm_KeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStartFrame)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.TextBox textPath;
        private System.Windows.Forms.Label labelPath;
        private System.Windows.Forms.TextBox textPrefix;
        private System.Windows.Forms.Label labelFilePrefix;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numStartFrame;
        private System.Windows.Forms.DataGridViewTextBoxColumn colX;
        private System.Windows.Forms.DataGridViewTextBoxColumn colY;
        private System.Windows.Forms.DataGridViewTextBoxColumn colZ;
        private System.Windows.Forms.DataGridViewTextBoxColumn colImageWidth;
        private System.Windows.Forms.DataGridViewTextBoxColumn colImageHeight;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDownSample;
        private System.Windows.Forms.CheckBox checkIncludeOverlays;
    }
}