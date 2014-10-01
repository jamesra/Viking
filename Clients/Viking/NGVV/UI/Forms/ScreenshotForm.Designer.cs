namespace Viking.UI.Forms
{
    partial class ScreenshotForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ScreenshotForm));
            this.groupPosition = new System.Windows.Forms.GroupBox();
            this.numHeight = new System.Windows.Forms.NumericUpDown();
            this.numWidth = new System.Windows.Forms.NumericUpDown();
            this.numY = new System.Windows.Forms.NumericUpDown();
            this.numX = new System.Windows.Forms.NumericUpDown();
            this.labelHeight = new System.Windows.Forms.Label();
            this.labelWidth = new System.Windows.Forms.Label();
            this.labelY = new System.Windows.Forms.Label();
            this.labelX = new System.Windows.Forms.Label();
            this.labelDownsample = new System.Windows.Forms.Label();
            this.numDownsample = new System.Windows.Forms.NumericUpDown();
            this.labelDimensions = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.labelFilename = new System.Windows.Forms.Label();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.checkUseViewerDownsample = new System.Windows.Forms.CheckBox();
            this.textFilename = new System.Windows.Forms.TextBox();
            this.checkOverlays = new System.Windows.Forms.CheckBox();
            this.groupPosition.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDownsample)).BeginInit();
            this.SuspendLayout();
            // 
            // groupPosition
            // 
            this.groupPosition.Controls.Add(this.numHeight);
            this.groupPosition.Controls.Add(this.numWidth);
            this.groupPosition.Controls.Add(this.numY);
            this.groupPosition.Controls.Add(this.numX);
            this.groupPosition.Controls.Add(this.labelHeight);
            this.groupPosition.Controls.Add(this.labelWidth);
            this.groupPosition.Controls.Add(this.labelY);
            this.groupPosition.Controls.Add(this.labelX);
            this.groupPosition.Location = new System.Drawing.Point(16, 64);
            this.groupPosition.Name = "groupPosition";
            this.groupPosition.Size = new System.Drawing.Size(230, 129);
            this.groupPosition.TabIndex = 8;
            this.groupPosition.TabStop = false;
            this.groupPosition.Text = "Position";
            // 
            // numHeight
            // 
            this.numHeight.Location = new System.Drawing.Point(98, 97);
            this.numHeight.Maximum = new decimal(new int[] {
            -294967296,
            0,
            0,
            0});
            this.numHeight.Name = "numHeight";
            this.numHeight.Size = new System.Drawing.Size(88, 20);
            this.numHeight.TabIndex = 15;
            // 
            // numWidth
            // 
            this.numWidth.Location = new System.Drawing.Point(98, 71);
            this.numWidth.Maximum = new decimal(new int[] {
            -294967296,
            0,
            0,
            0});
            this.numWidth.Name = "numWidth";
            this.numWidth.Size = new System.Drawing.Size(88, 20);
            this.numWidth.TabIndex = 14;
            // 
            // numY
            // 
            this.numY.Location = new System.Drawing.Point(98, 45);
            this.numY.Maximum = new decimal(new int[] {
            -294967296,
            0,
            0,
            0});
            this.numY.Minimum = new decimal(new int[] {
            -294967296,
            0,
            0,
            -2147483648});
            this.numY.Name = "numY";
            this.numY.Size = new System.Drawing.Size(88, 20);
            this.numY.TabIndex = 13;
            // 
            // numX
            // 
            this.numX.Location = new System.Drawing.Point(98, 19);
            this.numX.Maximum = new decimal(new int[] {
            -294967296,
            0,
            0,
            0});
            this.numX.Minimum = new decimal(new int[] {
            -294967296,
            0,
            0,
            -2147483648});
            this.numX.Name = "numX";
            this.numX.Size = new System.Drawing.Size(88, 20);
            this.numX.TabIndex = 12;
            // 
            // labelHeight
            // 
            this.labelHeight.AutoSize = true;
            this.labelHeight.Location = new System.Drawing.Point(19, 99);
            this.labelHeight.Name = "labelHeight";
            this.labelHeight.Size = new System.Drawing.Size(73, 13);
            this.labelHeight.TabIndex = 11;
            this.labelHeight.Text = "Image Height:";
            // 
            // labelWidth
            // 
            this.labelWidth.AutoSize = true;
            this.labelWidth.Location = new System.Drawing.Point(19, 73);
            this.labelWidth.Name = "labelWidth";
            this.labelWidth.Size = new System.Drawing.Size(70, 13);
            this.labelWidth.TabIndex = 10;
            this.labelWidth.Text = "Image Width:";
            // 
            // labelY
            // 
            this.labelY.AutoSize = true;
            this.labelY.Location = new System.Drawing.Point(20, 47);
            this.labelY.Name = "labelY";
            this.labelY.Size = new System.Drawing.Size(51, 13);
            this.labelY.TabIndex = 9;
            this.labelY.Text = "Center Y:";
            // 
            // labelX
            // 
            this.labelX.AutoSize = true;
            this.labelX.Location = new System.Drawing.Point(19, 21);
            this.labelX.Name = "labelX";
            this.labelX.Size = new System.Drawing.Size(51, 13);
            this.labelX.TabIndex = 8;
            this.labelX.Text = "Center X:";
            // 
            // labelDownsample
            // 
            this.labelDownsample.AutoSize = true;
            this.labelDownsample.Location = new System.Drawing.Point(16, 208);
            this.labelDownsample.Name = "labelDownsample";
            this.labelDownsample.Size = new System.Drawing.Size(104, 13);
            this.labelDownsample.TabIndex = 9;
            this.labelDownsample.Text = "Downsample Factor:";
            // 
            // numDownsample
            // 
            this.numDownsample.Location = new System.Drawing.Point(126, 206);
            this.numDownsample.Maximum = new decimal(new int[] {
            65536,
            0,
            0,
            0});
            this.numDownsample.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numDownsample.Name = "numDownsample";
            this.numDownsample.Size = new System.Drawing.Size(76, 20);
            this.numDownsample.TabIndex = 16;
            this.numDownsample.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numDownsample.ValueChanged += new System.EventHandler(this.numDownsample_ValueChanged);
            // 
            // labelDimensions
            // 
            this.labelDimensions.AutoSize = true;
            this.labelDimensions.Location = new System.Drawing.Point(123, 242);
            this.labelDimensions.Name = "labelDimensions";
            this.labelDimensions.Size = new System.Drawing.Size(0, 13);
            this.labelDimensions.TabIndex = 18;
            // 
            // btnOK
            // 
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(22, 266);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(99, 24);
            this.btnOK.TabIndex = 19;
            this.btnOK.Text = "Capture";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(137, 266);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(99, 25);
            this.btnCancel.TabIndex = 20;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // labelFilename
            // 
            this.labelFilename.AutoSize = true;
            this.labelFilename.Location = new System.Drawing.Point(12, 18);
            this.labelFilename.Name = "labelFilename";
            this.labelFilename.Size = new System.Drawing.Size(26, 13);
            this.labelFilename.TabIndex = 22;
            this.labelFilename.Text = "File:";
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(208, 15);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(38, 20);
            this.btnBrowse.TabIndex = 24;
            this.btnBrowse.Text = "...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // checkUseViewerDownsample
            // 
            this.checkUseViewerDownsample.AutoSize = true;
            this.checkUseViewerDownsample.Checked = global::Viking.Properties.Settings.Default.LastScreenshotUseViewersDownsampleLevel;
            this.checkUseViewerDownsample.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkUseViewerDownsample.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::Viking.Properties.Settings.Default, "LastScreenshotUseViewersDownsampleLevel", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.checkUseViewerDownsample.Location = new System.Drawing.Point(19, 238);
            this.checkUseViewerDownsample.Name = "checkUseViewerDownsample";
            this.checkUseViewerDownsample.Size = new System.Drawing.Size(217, 17);
            this.checkUseViewerDownsample.TabIndex = 25;
            this.checkUseViewerDownsample.Text = "Use Viewer\'s Current Downsample Level";
            this.checkUseViewerDownsample.UseVisualStyleBackColor = true;
            this.checkUseViewerDownsample.CheckedChanged += new System.EventHandler(this.checkUseViewerDownsample_CheckedChanged);
            // 
            // textFilename
            // 
            this.textFilename.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Viking.Properties.Settings.Default, "LastScreenshotPath", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textFilename.Location = new System.Drawing.Point(51, 15);
            this.textFilename.Name = "textFilename";
            this.textFilename.Size = new System.Drawing.Size(151, 20);
            this.textFilename.TabIndex = 23;
            this.textFilename.Text = global::Viking.Properties.Settings.Default.LastScreenshotPath;
            // 
            // checkOverlays
            // 
            this.checkOverlays.AutoSize = true;
            this.checkOverlays.Checked = global::Viking.Properties.Settings.Default.LastScreenshotIncludeOverlays;
            this.checkOverlays.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkOverlays.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::Viking.Properties.Settings.Default, "LastScreenshotIncludeOverlays", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.checkOverlays.Location = new System.Drawing.Point(15, 41);
            this.checkOverlays.Name = "checkOverlays";
            this.checkOverlays.Size = new System.Drawing.Size(106, 17);
            this.checkOverlays.TabIndex = 21;
            this.checkOverlays.Text = "Include overlays:";
            this.checkOverlays.UseVisualStyleBackColor = true;
            // 
            // ScreenshotForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(253, 301);
            this.Controls.Add(this.checkUseViewerDownsample);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.textFilename);
            this.Controls.Add(this.labelFilename);
            this.Controls.Add(this.checkOverlays);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.labelDimensions);
            this.Controls.Add(this.numDownsample);
            this.Controls.Add(this.labelDownsample);
            this.Controls.Add(this.groupPosition);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "ScreenshotForm";
            this.Text = "Screenshot Parameters";
            this.Load += new System.EventHandler(this.ScreenshotForm_Load);
            this.groupPosition.ResumeLayout(false);
            this.groupPosition.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDownsample)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupPosition;
        private System.Windows.Forms.NumericUpDown numHeight;
        private System.Windows.Forms.NumericUpDown numWidth;
        private System.Windows.Forms.NumericUpDown numY;
        private System.Windows.Forms.NumericUpDown numX;
        private System.Windows.Forms.Label labelHeight;
        private System.Windows.Forms.Label labelWidth;
        private System.Windows.Forms.Label labelY;
        private System.Windows.Forms.Label labelX;
        private System.Windows.Forms.Label labelDownsample;
        private System.Windows.Forms.NumericUpDown numDownsample;
        private System.Windows.Forms.Label labelDimensions;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.CheckBox checkOverlays;
        private System.Windows.Forms.Label labelFilename;
        private System.Windows.Forms.TextBox textFilename;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.CheckBox checkUseViewerDownsample;

    }
}