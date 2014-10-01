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
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textKeepID = new System.Windows.Forms.TextBox();
            this.textMergeID = new System.Windows.Forms.TextBox();
            this.btnMerge = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.textKeepLabel = new System.Windows.Forms.TextBox();
            this.textMergeLabel = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(338, 78);
            this.label1.TabIndex = 0;
            this.label1.Text = resources.GetString("label1.Text");
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 112);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(49, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Keep ID:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 147);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(54, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Merge ID:";
            // 
            // textKeepID
            // 
            this.textKeepID.Location = new System.Drawing.Point(67, 109);
            this.textKeepID.Name = "textKeepID";
            this.textKeepID.Size = new System.Drawing.Size(100, 20);
            this.textKeepID.TabIndex = 3;
            this.textKeepID.Validating += new System.ComponentModel.CancelEventHandler(this.textKeepID_Validating);
            // 
            // textMergeID
            // 
            this.textMergeID.Location = new System.Drawing.Point(67, 144);
            this.textMergeID.Name = "textMergeID";
            this.textMergeID.Size = new System.Drawing.Size(100, 20);
            this.textMergeID.TabIndex = 4;
            this.textMergeID.Validating += new System.ComponentModel.CancelEventHandler(this.textMergeID_Validating);
            // 
            // btnMerge
            // 
            this.btnMerge.Enabled = false;
            this.btnMerge.Location = new System.Drawing.Point(67, 188);
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
            this.btnCancel.Location = new System.Drawing.Point(225, 188);
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
            this.textKeepLabel.Location = new System.Drawing.Point(189, 116);
            this.textKeepLabel.Name = "textKeepLabel";
            this.textKeepLabel.ReadOnly = true;
            this.textKeepLabel.Size = new System.Drawing.Size(151, 13);
            this.textKeepLabel.TabIndex = 7;
            // 
            // textMergeLabel
            // 
            this.textMergeLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textMergeLabel.Location = new System.Drawing.Point(189, 151);
            this.textMergeLabel.Name = "textMergeLabel";
            this.textMergeLabel.ReadOnly = true;
            this.textMergeLabel.Size = new System.Drawing.Size(151, 13);
            this.textMergeLabel.TabIndex = 8;
            // 
            // MergeStructuresForm
            // 
            this.AcceptButton = this.btnMerge;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(352, 223);
            this.Controls.Add(this.textMergeLabel);
            this.Controls.Add(this.textKeepLabel);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnMerge);
            this.Controls.Add(this.textMergeID);
            this.Controls.Add(this.textKeepID);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Name = "MergeStructuresForm";
            this.Text = "Merge Structures (Administrators Only)";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textKeepID;
        private System.Windows.Forms.TextBox textMergeID;
        private System.Windows.Forms.Button btnMerge;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox textKeepLabel;
        private System.Windows.Forms.TextBox textMergeLabel;
    }
}