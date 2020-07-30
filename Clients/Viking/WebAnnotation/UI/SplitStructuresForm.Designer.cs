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
            this.btnFlip = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(338, 289);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(112, 35);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnSplit
            // 
            this.btnSplit.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSplit.Location = new System.Drawing.Point(100, 289);
            this.btnSplit.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnSplit.Name = "btnSplit";
            this.btnSplit.Size = new System.Drawing.Size(112, 35);
            this.btnSplit.TabIndex = 7;
            this.btnSplit.Text = "Split";
            this.btnSplit.UseVisualStyleBackColor = true;
            this.btnSplit.Click += new System.EventHandler(this.btnSplit_Click);
            // 
            // textSplitID
            // 
            this.textSplitID.Location = new System.Drawing.Point(100, 212);
            this.textSplitID.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textSplitID.Name = "textSplitID";
            this.textSplitID.Size = new System.Drawing.Size(148, 26);
            this.textSplitID.TabIndex = 12;
            this.textSplitID.TextChanged += new System.EventHandler(this.textSplitID_TextChanged);
            this.textSplitID.Validating += new System.ComponentModel.CancelEventHandler(this.textSplitID_Validating);
            // 
            // textKeepID
            // 
            this.textKeepID.Location = new System.Drawing.Point(100, 158);
            this.textKeepID.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textKeepID.Name = "textKeepID";
            this.textKeepID.Size = new System.Drawing.Size(148, 26);
            this.textKeepID.TabIndex = 11;
            this.textKeepID.TextChanged += new System.EventHandler(this.textKeepID_TextChanged);
            this.textKeepID.Validating += new System.ComponentModel.CancelEventHandler(this.textKeepID_Validating);
            // 
            // labelSplitID
            // 
            this.labelSplitID.AutoSize = true;
            this.labelSplitID.Location = new System.Drawing.Point(18, 217);
            this.labelSplitID.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelSplitID.Name = "labelSplitID";
            this.labelSplitID.Size = new System.Drawing.Size(65, 20);
            this.labelSplitID.TabIndex = 10;
            this.labelSplitID.Text = "Split ID:";
            // 
            // labelKeepID
            // 
            this.labelKeepID.AutoSize = true;
            this.labelKeepID.Location = new System.Drawing.Point(18, 163);
            this.labelKeepID.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelKeepID.Name = "labelKeepID";
            this.labelKeepID.Size = new System.Drawing.Size(71, 20);
            this.labelKeepID.TabIndex = 9;
            this.labelKeepID.Text = "Keep ID:";
            // 
            // textSplitLabel
            // 
            this.textSplitLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textSplitLabel.Location = new System.Drawing.Point(284, 223);
            this.textSplitLabel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textSplitLabel.Name = "textSplitLabel";
            this.textSplitLabel.ReadOnly = true;
            this.textSplitLabel.Size = new System.Drawing.Size(226, 19);
            this.textSplitLabel.TabIndex = 14;
            // 
            // textKeepLabel
            // 
            this.textKeepLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textKeepLabel.Location = new System.Drawing.Point(284, 169);
            this.textKeepLabel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textKeepLabel.Name = "textKeepLabel";
            this.textKeepLabel.ReadOnly = true;
            this.textKeepLabel.Size = new System.Drawing.Size(226, 19);
            this.textKeepLabel.TabIndex = 13;
            // 
            // textInfo
            // 
            this.textInfo.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textInfo.Location = new System.Drawing.Point(22, 118);
            this.textInfo.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textInfo.Name = "textInfo";
            this.textInfo.ReadOnly = true;
            this.textInfo.Size = new System.Drawing.Size(488, 19);
            this.textInfo.TabIndex = 15;
            // 
            // labelInstructions
            // 
            this.labelInstructions.AutoSize = true;
            this.labelInstructions.Location = new System.Drawing.Point(22, 20);
            this.labelInstructions.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelInstructions.Name = "labelInstructions";
            this.labelInstructions.Size = new System.Drawing.Size(449, 120);
            this.labelInstructions.TabIndex = 16;
            this.labelInstructions.Text = resources.GetString("labelInstructions.Text");
            // 
            // btnFlip
            // 
            this.btnFlip.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("btnFlip.BackgroundImage")));
            this.btnFlip.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnFlip.Location = new System.Drawing.Point(255, 169);
            this.btnFlip.Name = "btnFlip";
            this.btnFlip.Size = new System.Drawing.Size(57, 57);
            this.btnFlip.TabIndex = 17;
            this.btnFlip.UseVisualStyleBackColor = true;
            this.btnFlip.Click += new System.EventHandler(this.btnFlip_Click);
            // 
            // SplitStructuresForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(528, 343);
            this.Controls.Add(this.btnFlip);
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
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
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
        private System.Windows.Forms.Button btnFlip;
    }
}