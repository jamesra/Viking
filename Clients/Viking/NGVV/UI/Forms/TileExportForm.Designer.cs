namespace Viking.UI.Forms
{
    partial class TileExportForm
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
            this.btnBrowse = new System.Windows.Forms.Button();
            this.labelPath = new System.Windows.Forms.Label();
            this.groupRange = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.numLastSection = new System.Windows.Forms.NumericUpDown();
            this.numFirstSection = new System.Windows.Forms.NumericUpDown();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.numDownsample = new System.Windows.Forms.NumericUpDown();
            this.checkExportAll = new System.Windows.Forms.CheckBox();
            this.textPath = new System.Windows.Forms.TextBox();
            this.groupRange.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numLastSection)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFirstSection)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDownsample)).BeginInit();
            this.SuspendLayout();
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(274, 12);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(53, 23);
            this.btnBrowse.TabIndex = 15;
            this.btnBrowse.Text = "...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // labelPath
            // 
            this.labelPath.AutoSize = true;
            this.labelPath.Location = new System.Drawing.Point(9, 17);
            this.labelPath.Name = "labelPath";
            this.labelPath.Size = new System.Drawing.Size(84, 13);
            this.labelPath.TabIndex = 13;
            this.labelPath.Text = "Output Directory";
            // 
            // groupRange
            // 
            this.groupRange.Controls.Add(this.label2);
            this.groupRange.Controls.Add(this.label1);
            this.groupRange.Controls.Add(this.numLastSection);
            this.groupRange.Controls.Add(this.numFirstSection);
            this.groupRange.Enabled = false;
            this.groupRange.Location = new System.Drawing.Point(12, 121);
            this.groupRange.Name = "groupRange";
            this.groupRange.Size = new System.Drawing.Size(304, 83);
            this.groupRange.TabIndex = 17;
            this.groupRange.TabStop = false;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(87, 47);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(112, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Last Section in Range";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(87, 21);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(111, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "First Section in Range";
            // 
            // numLastSection
            // 
            this.numLastSection.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::Viking.Properties.Settings.Default, "LastExportRangeEnd", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.numLastSection.Location = new System.Drawing.Point(6, 45);
            this.numLastSection.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.numLastSection.Name = "numLastSection";
            this.numLastSection.Size = new System.Drawing.Size(75, 20);
            this.numLastSection.TabIndex = 1;
            this.numLastSection.Value = global::Viking.Properties.Settings.Default.LastExportRangeEnd;
            // 
            // numFirstSection
            // 
            this.numFirstSection.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::Viking.Properties.Settings.Default, "LastExportRangeStart", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.numFirstSection.Location = new System.Drawing.Point(6, 19);
            this.numFirstSection.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.numFirstSection.Name = "numFirstSection";
            this.numFirstSection.Size = new System.Drawing.Size(75, 20);
            this.numFirstSection.TabIndex = 0;
            this.numFirstSection.Value = global::Viking.Properties.Settings.Default.LastExportRangeStart;
            // 
            // btnOK
            // 
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(18, 210);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 18;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(241, 210);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 19;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(9, 49);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(100, 13);
            this.label3.TabIndex = 20;
            this.label3.Text = "Downsample Level:";
            // 
            // numDownsample
            // 
            this.numDownsample.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::Viking.Properties.Settings.Default, "LastTileExportDownsampleLevel", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.numDownsample.Location = new System.Drawing.Point(116, 49);
            this.numDownsample.Maximum = new decimal(new int[] {
            1410065408,
            2,
            0,
            0});
            this.numDownsample.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numDownsample.Name = "numDownsample";
            this.numDownsample.Size = new System.Drawing.Size(120, 20);
            this.numDownsample.TabIndex = 21;
            this.numDownsample.Value = global::Viking.Properties.Settings.Default.LastTileExportDownsampleLevel;
            // 
            // checkExportAll
            // 
            this.checkExportAll.AutoSize = true;
            this.checkExportAll.Checked = global::Viking.Properties.Settings.Default.ExportAllChecked;
            this.checkExportAll.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkExportAll.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::Viking.Properties.Settings.Default, "ExportAllChecked", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.checkExportAll.Location = new System.Drawing.Point(12, 108);
            this.checkExportAll.Name = "checkExportAll";
            this.checkExportAll.Size = new System.Drawing.Size(151, 17);
            this.checkExportAll.TabIndex = 16;
            this.checkExportAll.Text = "Export Tiles for all sections";
            this.checkExportAll.UseVisualStyleBackColor = true;
            this.checkExportAll.CheckedChanged += new System.EventHandler(this.checkExportAll_CheckedChanged);
            // 
            // textPath
            // 
            this.textPath.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Viking.Properties.Settings.Default, "LastTileExportDirectory", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textPath.Location = new System.Drawing.Point(99, 14);
            this.textPath.Name = "textPath";
            this.textPath.Size = new System.Drawing.Size(169, 20);
            this.textPath.TabIndex = 14;
            this.textPath.Text = global::Viking.Properties.Settings.Default.LastTileExportDirectory;
            this.textPath.TextChanged += new System.EventHandler(this.textPath_TextChanged);
            // 
            // TileExportForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(339, 246);
            this.Controls.Add(this.numDownsample);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.checkExportAll);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.textPath);
            this.Controls.Add(this.labelPath);
            this.Controls.Add(this.groupRange);
            this.Name = "TileExportForm";
            this.Text = "Export Tiles Configuration";
            this.groupRange.ResumeLayout(false);
            this.groupRange.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numLastSection)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFirstSection)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDownsample)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.TextBox textPath;
        private System.Windows.Forms.Label labelPath;
        private System.Windows.Forms.CheckBox checkExportAll;
        private System.Windows.Forms.GroupBox groupRange;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numLastSection;
        private System.Windows.Forms.NumericUpDown numFirstSection;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown numDownsample;
    }
}