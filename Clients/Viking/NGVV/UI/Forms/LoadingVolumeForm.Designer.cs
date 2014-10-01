namespace Viking.UI.Forms
{
    partial class LoadingVolumeForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoadingVolumeForm));
            this.PanelProgress = new System.Windows.Forms.Panel();
            this.LabelInfo = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.TextCopyright = new System.Windows.Forms.RichTextBox();
            this.backgroundWorker = new System.ComponentModel.BackgroundWorker();
            this.LabelModules = new System.Windows.Forms.Label();
            this.ListModules = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // PanelProgress
            // 
            this.PanelProgress.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.PanelProgress.Location = new System.Drawing.Point(12, 291);
            this.PanelProgress.Name = "PanelProgress";
            this.PanelProgress.Size = new System.Drawing.Size(280, 32);
            this.PanelProgress.TabIndex = 3;
            this.PanelProgress.Paint += new System.Windows.Forms.PaintEventHandler(this.PanelProgress_Paint);
            // 
            // LabelInfo
            // 
            this.LabelInfo.Location = new System.Drawing.Point(9, 248);
            this.LabelInfo.Name = "LabelInfo";
            this.LabelInfo.Size = new System.Drawing.Size(291, 40);
            this.LabelInfo.TabIndex = 2;
            this.LabelInfo.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 36F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.Teal;
            this.label1.Location = new System.Drawing.Point(1, -1);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(307, 55);
            this.label1.TabIndex = 8;
            this.label1.Text = "Viking!";
            this.label1.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // TextCopyright
            // 
            this.TextCopyright.BackColor = System.Drawing.SystemColors.Control;
            this.TextCopyright.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.TextCopyright.Cursor = System.Windows.Forms.Cursors.Default;
            this.TextCopyright.Location = new System.Drawing.Point(12, 57);
            this.TextCopyright.Name = "TextCopyright";
            this.TextCopyright.ReadOnly = true;
            this.TextCopyright.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
            this.TextCopyright.Size = new System.Drawing.Size(280, 30);
            this.TextCopyright.TabIndex = 7;
            this.TextCopyright.Text = "Copyright 2008 James Anderson\nAll Rights Reserved";
            // 
            // backgroundWorker
            // 
            this.backgroundWorker.WorkerReportsProgress = true;
            this.backgroundWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker_DoWork);
            this.backgroundWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker_RunWorkerCompleted);
            this.backgroundWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker_ProgressChanged);
            // 
            // LabelModules
            // 
            this.LabelModules.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LabelModules.ForeColor = System.Drawing.SystemColors.WindowText;
            this.LabelModules.Location = new System.Drawing.Point(8, 90);
            this.LabelModules.Name = "LabelModules";
            this.LabelModules.Size = new System.Drawing.Size(296, 24);
            this.LabelModules.TabIndex = 9;
            this.LabelModules.Text = "Expansion Modules:";
            this.LabelModules.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // ListModules
            // 
            this.ListModules.BackColor = System.Drawing.SystemColors.Control;
            this.ListModules.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ListModules.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ListModules.ForeColor = System.Drawing.SystemColors.Highlight;
            this.ListModules.ItemHeight = 18;
            this.ListModules.Location = new System.Drawing.Point(20, 117);
            this.ListModules.Name = "ListModules";
            this.ListModules.SelectionMode = System.Windows.Forms.SelectionMode.None;
            this.ListModules.Size = new System.Drawing.Size(272, 128);
            this.ListModules.Sorted = true;
            this.ListModules.TabIndex = 10;
            // 
            // SplashForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(304, 330);
            this.Controls.Add(this.LabelModules);
            this.Controls.Add(this.ListModules);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.TextCopyright);
            this.Controls.Add(this.PanelProgress);
            this.Controls.Add(this.LabelInfo);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "SplashForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Initializing NGVV";
            this.Load += new System.EventHandler(this.SplashForm_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel PanelProgress;
        private System.Windows.Forms.Label LabelInfo;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RichTextBox TextCopyright;
        private System.ComponentModel.BackgroundWorker backgroundWorker;
        private System.Windows.Forms.Label LabelModules;
        private System.Windows.Forms.ListBox ListModules;
    }
}