namespace WebAnnotation.UI
{
    partial class StructureExtendedPropertiesPage
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.numConfidence = new System.Windows.Forms.NumericUpDown();
            this.labelConfidence = new System.Windows.Forms.Label();
            this.checkVerified = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.numConfidence)).BeginInit();
            this.SuspendLayout();
            // 
            // numConfidence
            // 
            this.numConfidence.DecimalPlaces = 2;
            this.numConfidence.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numConfidence.Location = new System.Drawing.Point(6, 17);
            this.numConfidence.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numConfidence.Name = "numConfidence";
            this.numConfidence.Size = new System.Drawing.Size(249, 20);
            this.numConfidence.TabIndex = 11;
            // 
            // labelConfidence
            // 
            this.labelConfidence.AutoSize = true;
            this.labelConfidence.Location = new System.Drawing.Point(3, 0);
            this.labelConfidence.Name = "labelConfidence";
            this.labelConfidence.Size = new System.Drawing.Size(64, 13);
            this.labelConfidence.TabIndex = 10;
            this.labelConfidence.Text = "Confidence:";
            // 
            // checkVerified
            // 
            this.checkVerified.AutoSize = true;
            this.checkVerified.Location = new System.Drawing.Point(6, 43);
            this.checkVerified.Name = "checkVerified";
            this.checkVerified.Size = new System.Drawing.Size(61, 17);
            this.checkVerified.TabIndex = 9;
            this.checkVerified.Text = "Verified";
            this.checkVerified.UseVisualStyleBackColor = true;
            // 
            // StructureExtendedPropertiesPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.numConfidence);
            this.Controls.Add(this.labelConfidence);
            this.Controls.Add(this.checkVerified);
            this.Name = "StructureExtendedPropertiesPage";
            ((System.ComponentModel.ISupportInitialize)(this.numConfidence)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NumericUpDown numConfidence;
        private System.Windows.Forms.Label labelConfidence;
        private System.Windows.Forms.CheckBox checkVerified;
    }
}
