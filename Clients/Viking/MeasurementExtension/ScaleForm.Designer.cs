namespace MeasurementExtension
{
    partial class ScaleForm
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
            System.Windows.Forms.Label labelUnits;
            System.Windows.Forms.Label labelUnitsPerPixel;
            this.comboUnits = new System.Windows.Forms.ComboBox();
            this.numUnitsPerPixel = new System.Windows.Forms.NumericUpDown();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            labelUnits = new System.Windows.Forms.Label();
            labelUnitsPerPixel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.numUnitsPerPixel)).BeginInit();
            this.SuspendLayout();
            // 
            // labelUnits
            // 
            labelUnits.AutoSize = true;
            labelUnits.Location = new System.Drawing.Point(17, 16);
            labelUnits.Name = "labelUnits";
            labelUnits.Size = new System.Drawing.Size(90, 13);
            labelUnits.TabIndex = 0;
            labelUnits.Text = "Units of Measure:";
            // 
            // labelUnitsPerPixel
            // 
            labelUnitsPerPixel.AutoSize = true;
            labelUnitsPerPixel.Location = new System.Drawing.Point(17, 49);
            labelUnitsPerPixel.Name = "labelUnitsPerPixel";
            labelUnitsPerPixel.Size = new System.Drawing.Size(76, 13);
            labelUnitsPerPixel.TabIndex = 2;
            labelUnitsPerPixel.Text = "Units per pixel:";
            // 
            // comboUnits
            // 
            this.comboUnits.FormattingEnabled = true;
            this.comboUnits.Items.AddRange(new object[] {
            "nm",
            "um",
            "mm",
            "cm",
            "m",
            "km"});
            this.comboUnits.Location = new System.Drawing.Point(127, 13);
            this.comboUnits.Name = "comboUnits";
            this.comboUnits.Size = new System.Drawing.Size(77, 21);
            this.comboUnits.TabIndex = 1;
            this.comboUnits.Text = "nm";
            // 
            // numUnitsPerPixel
            // 
            this.numUnitsPerPixel.DecimalPlaces = 3;
            this.numUnitsPerPixel.Location = new System.Drawing.Point(127, 47);
            this.numUnitsPerPixel.Name = "numUnitsPerPixel";
            this.numUnitsPerPixel.Size = new System.Drawing.Size(77, 20);
            this.numUnitsPerPixel.TabIndex = 3;
            this.numUnitsPerPixel.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // btnOK
            // 
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(127, 82);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 4;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(8, 82);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // ScaleForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(216, 114);
            this.ControlBox = false;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.numUnitsPerPixel);
            this.Controls.Add(labelUnitsPerPixel);
            this.Controls.Add(this.comboUnits);
            this.Controls.Add(labelUnits);
            this.Name = "ScaleForm";
            this.Text = "Set scale";
            this.Load += new System.EventHandler(this.ScaleForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.numUnitsPerPixel)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox comboUnits;
        private System.Windows.Forms.NumericUpDown numUnitsPerPixel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
    }
}