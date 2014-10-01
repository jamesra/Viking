namespace Viking.UI.Forms
{
    partial class GenericProgressForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GenericProgressForm));
            this.PanelProgress = new System.Windows.Forms.Panel();
            this.LabelInfo = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // PanelProgress
            // 
            this.PanelProgress.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.PanelProgress.Location = new System.Drawing.Point(7, 52);
            this.PanelProgress.Name = "PanelProgress";
            this.PanelProgress.Size = new System.Drawing.Size(280, 32);
            this.PanelProgress.TabIndex = 5;
            this.PanelProgress.Paint += new System.Windows.Forms.PaintEventHandler(this.PanelProgress_Paint);
            // 
            // LabelInfo
            // 
            this.LabelInfo.Location = new System.Drawing.Point(4, 9);
            this.LabelInfo.Name = "LabelInfo";
            this.LabelInfo.Size = new System.Drawing.Size(291, 40);
            this.LabelInfo.TabIndex = 4;
            this.LabelInfo.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(115, 90);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(72, 21);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // GenericProgressForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(303, 128);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.PanelProgress);
            this.Controls.Add(this.LabelInfo);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "GenericProgressForm";
            this.Text = "Progress";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel PanelProgress;
        private System.Windows.Forms.Label LabelInfo;
        private System.Windows.Forms.Button btnCancel;
    }
}