namespace WebAnnotation.UI
{
    partial class SplitStructuresForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SplitStructuresForm));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnSplit = new System.Windows.Forms.Button();
            this.textSplitID = new System.Windows.Forms.TextBox();
            this.textKeepID = new System.Windows.Forms.TextBox();
            this.labelSplitID = new System.Windows.Forms.Label();
            this.labelKeepID = new System.Windows.Forms.Label();
            this.textSplitLabel = new System.Windows.Forms.TextBox();
            this.textKeepLabel = new System.Windows.Forms.TextBox();
            this.textInfo = new System.Windows.Forms.TextBox();
            this.labelInstructions = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(225, 188);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnSplit
            // 
            this.btnSplit.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSplit.Location = new System.Drawing.Point(67, 188);
            this.btnSplit.Name = "btnSplit";
            this.btnSplit.Size = new System.Drawing.Size(75, 23);
            this.btnSplit.TabIndex = 7;
            this.btnSplit.Text = "Split";
            this.btnSplit.UseVisualStyleBackColor = true;
            this.btnSplit.Click += new System.EventHandler(this.btnSplit_Click);
            // 
            // textSplitID
            // 
            this.textSplitID.Location = new System.Drawing.Point(67, 138);
            this.textSplitID.Name = "textSplitID";
            this.textSplitID.Size = new System.Drawing.Size(100, 20);
            this.textSplitID.TabIndex = 12;
            this.textSplitID.TextChanged += new System.EventHandler(this.textSplitID_TextChanged);
            this.textSplitID.Validating += new System.ComponentModel.CancelEventHandler(this.textSplitID_Validating);
            // 
            // textKeepID
            // 
            this.textKeepID.Location = new System.Drawing.Point(67, 103);
            this.textKeepID.Name = "textKeepID";
            this.textKeepID.Size = new System.Drawing.Size(100, 20);
            this.textKeepID.TabIndex = 11;
            this.textKeepID.TextChanged += new System.EventHandler(this.textKeepID_TextChanged);
            this.textKeepID.Validating += new System.ComponentModel.CancelEventHandler(this.textKeepID_Validating);
            // 
            // labelSplitID
            // 
            this.labelSplitID.AutoSize = true;
            this.labelSplitID.Location = new System.Drawing.Point(12, 141);
            this.labelSplitID.Name = "labelSplitID";
            this.labelSplitID.Size = new System.Drawing.Size(44, 13);
            this.labelSplitID.TabIndex = 10;
            this.labelSplitID.Text = "Split ID:";
            // 
            // labelKeepID
            // 
            this.labelKeepID.AutoSize = true;
            this.labelKeepID.Location = new System.Drawing.Point(12, 106);
            this.labelKeepID.Name = "labelKeepID";
            this.labelKeepID.Size = new System.Drawing.Size(49, 13);
            this.labelKeepID.TabIndex = 9;
            this.labelKeepID.Text = "Keep ID:";
            // 
            // textSplitLabel
            // 
            this.textSplitLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textSplitLabel.Location = new System.Drawing.Point(189, 145);
            this.textSplitLabel.Name = "textSplitLabel";
            this.textSplitLabel.ReadOnly = true;
            this.textSplitLabel.Size = new System.Drawing.Size(151, 13);
            this.textSplitLabel.TabIndex = 14;
            // 
            // textKeepLabel
            // 
            this.textKeepLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textKeepLabel.Location = new System.Drawing.Point(189, 110);
            this.textKeepLabel.Name = "textKeepLabel";
            this.textKeepLabel.ReadOnly = true;
            this.textKeepLabel.Size = new System.Drawing.Size(151, 13);
            this.textKeepLabel.TabIndex = 13;
            // 
            // textInfo
            // 
            this.textInfo.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textInfo.Location = new System.Drawing.Point(15, 77);
            this.textInfo.Name = "textInfo";
            this.textInfo.ReadOnly = true;
            this.textInfo.Size = new System.Drawing.Size(325, 13);
            this.textInfo.TabIndex = 15;
            // 
            // labelInstructions
            // 
            this.labelInstructions.AutoSize = true;
            this.labelInstructions.Location = new System.Drawing.Point(15, 13);
            this.labelInstructions.Name = "labelInstructions";
            this.labelInstructions.Size = new System.Drawing.Size(302, 78);
            this.labelInstructions.TabIndex = 16;
            this.labelInstructions.Text = resources.GetString("labelInstructions.Text");
            // 
            // SplitStructuresForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(352, 223);
            this.Controls.Add(this.labelInstructions);
            this.Controls.Add(this.textInfo);
            this.Controls.Add(this.textSplitLabel);
            this.Controls.Add(this.textKeepLabel);
            this.Controls.Add(this.textSplitID);
            this.Controls.Add(this.textKeepID);
            this.Controls.Add(this.labelSplitID);
            this.Controls.Add(this.labelKeepID);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSplit);
            this.Name = "SplitStructuresForm";
            this.Text = "Split Structures";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSplit;
        private System.Windows.Forms.TextBox textSplitID;
        private System.Windows.Forms.TextBox textKeepID;
        private System.Windows.Forms.Label labelSplitID;
        private System.Windows.Forms.Label labelKeepID;
        private System.Windows.Forms.TextBox textSplitLabel;
        private System.Windows.Forms.TextBox textKeepLabel;
        private System.Windows.Forms.TextBox textInfo;
        private System.Windows.Forms.Label labelInstructions;
    }
}