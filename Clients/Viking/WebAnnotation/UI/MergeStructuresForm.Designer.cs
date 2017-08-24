namespace WebAnnotation.UI
{
    partial class MergeStructuresForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MergeStructuresForm));
            this.labelInstructions = new System.Windows.Forms.Label();
            this.labelKeepID = new System.Windows.Forms.Label();
            this.labelMergeID = new System.Windows.Forms.Label();
            this.textKeepID = new System.Windows.Forms.TextBox();
            this.textMergeID = new System.Windows.Forms.TextBox();
            this.btnMerge = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.textKeepLabel = new System.Windows.Forms.TextBox();
            this.textMergeLabel = new System.Windows.Forms.TextBox();
            this.textValidation = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // labelInstructions
            // 
            this.labelInstructions.AutoSize = true;
            this.labelInstructions.Location = new System.Drawing.Point(12, 9);
            this.labelInstructions.Name = "labelInstructions";
            this.labelInstructions.Size = new System.Drawing.Size(256, 78);
            this.labelInstructions.TabIndex = 0;
            this.labelInstructions.Text = resources.GetString("labelInstructions.Text");
            // 
            // labelKeepID
            // 
            this.labelKeepID.AutoSize = true;
            this.labelKeepID.Location = new System.Drawing.Point(12, 112);
            this.labelKeepID.Name = "labelKeepID";
            this.labelKeepID.Size = new System.Drawing.Size(95, 13);
            this.labelKeepID.TabIndex = 1;
            this.labelKeepID.Text = "Keep Structure ID:";
            // 
            // labelMergeID
            // 
            this.labelMergeID.AutoSize = true;
            this.labelMergeID.Location = new System.Drawing.Point(12, 147);
            this.labelMergeID.Name = "labelMergeID";
            this.labelMergeID.Size = new System.Drawing.Size(100, 13);
            this.labelMergeID.TabIndex = 2;
            this.labelMergeID.Text = "Merge Structure ID:";
            // 
            // textKeepID
            // 
            this.textKeepID.Location = new System.Drawing.Point(118, 109);
            this.textKeepID.Name = "textKeepID";
            this.textKeepID.Size = new System.Drawing.Size(65, 20);
            this.textKeepID.TabIndex = 3;
            this.textKeepID.TextChanged += new System.EventHandler(this.textKeepIDLabel_TextChanged);
            // 
            // textMergeID
            // 
            this.textMergeID.Location = new System.Drawing.Point(118, 144);
            this.textMergeID.Name = "textMergeID";
            this.textMergeID.Size = new System.Drawing.Size(65, 20);
            this.textMergeID.TabIndex = 4;
            this.textMergeID.TextChanged += new System.EventHandler(this.textMergeIDLabel_TextChanged);
            // 
            // btnMerge
            // 
            this.btnMerge.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnMerge.Enabled = false;
            this.btnMerge.Location = new System.Drawing.Point(67, 195);
            this.btnMerge.Name = "btnMerge";
            this.btnMerge.Size = new System.Drawing.Size(75, 23);
            this.btnMerge.TabIndex = 5;
            this.btnMerge.Text = "Merge";
            this.btnMerge.UseVisualStyleBackColor = true;
            this.btnMerge.Click += new System.EventHandler(this.btnMerge_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(222, 195);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // textKeepLabel
            // 
            this.textKeepLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textKeepLabel.Location = new System.Drawing.Point(189, 109);
            this.textKeepLabel.Multiline = true;
            this.textKeepLabel.Name = "textKeepLabel";
            this.textKeepLabel.ReadOnly = true;
            this.textKeepLabel.Size = new System.Drawing.Size(151, 29);
            this.textKeepLabel.TabIndex = 7;
            // 
            // textMergeLabel
            // 
            this.textMergeLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textMergeLabel.Location = new System.Drawing.Point(189, 144);
            this.textMergeLabel.Multiline = true;
            this.textMergeLabel.Name = "textMergeLabel";
            this.textMergeLabel.ReadOnly = true;
            this.textMergeLabel.Size = new System.Drawing.Size(151, 38);
            this.textMergeLabel.TabIndex = 8;
            // 
            // textValidation
            // 
            this.textValidation.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textValidation.Location = new System.Drawing.Point(15, 170);
            this.textValidation.Multiline = true;
            this.textValidation.Name = "textValidation";
            this.textValidation.ReadOnly = true;
            this.textValidation.Size = new System.Drawing.Size(325, 19);
            this.textValidation.TabIndex = 9;
            this.textValidation.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // MergeStructuresForm
            // 
            this.AcceptButton = this.btnMerge;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(352, 223);
            this.Controls.Add(this.textValidation);
            this.Controls.Add(this.textMergeLabel);
            this.Controls.Add(this.textKeepLabel);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnMerge);
            this.Controls.Add(this.textMergeID);
            this.Controls.Add(this.textKeepID);
            this.Controls.Add(this.labelMergeID);
            this.Controls.Add(this.labelKeepID);
            this.Controls.Add(this.labelInstructions);
            this.Name = "MergeStructuresForm";
            this.Text = "Merge Structures (Administrators Only)";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelInstructions;
        private System.Windows.Forms.Label labelKeepID;
        private System.Windows.Forms.Label labelMergeID;
        private System.Windows.Forms.TextBox textKeepID;
        private System.Windows.Forms.TextBox textMergeID;
        private System.Windows.Forms.Button btnMerge;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox textKeepLabel;
        private System.Windows.Forms.TextBox textMergeLabel;
        private System.Windows.Forms.TextBox textValidation;
    }
}