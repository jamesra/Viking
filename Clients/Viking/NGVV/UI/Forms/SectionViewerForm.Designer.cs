namespace Viking.UI.Forms
{
    partial class SectionViewerForm
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
//            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SectionViewerForm));
            this.SectionControl = new Viking.UI.Controls.SectionViewerControl();
            this.SuspendLayout();
            // 
            // SectionControl
            // 
            
            this.SectionControl.CurrentCommand = null;
            this.SectionControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SectionControl.Location = new System.Drawing.Point(0, 0);
            this.SectionControl.Name = "SectionControl";
            this.SectionControl.Section = null;
            this.SectionControl.Size = new System.Drawing.Size(284, 264);
            this.SectionControl.StatusMagnification = 2.2535211267605635;
            this.SectionControl.TabIndex = 0;
            this.SectionControl.Text = "sectionViewerControl1";
            // 
            // SectionViewerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 264);
            this.Controls.Add(this.SectionControl);
            this.Name = "SectionViewerForm";
            this.Text = "SectionViewerForm";
            this.ResumeLayout(false);

        }

        #endregion

        public Viking.UI.Controls.SectionViewerControl SectionControl;

    }
}