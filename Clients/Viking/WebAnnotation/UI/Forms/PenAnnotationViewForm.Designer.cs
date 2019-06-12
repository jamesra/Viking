namespace WebAnnotation.UI.Forms
{
    partial class PenAnnotationViewForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PenAnnotationViewForm));
            this.SectionView = new Viking.UI.Controls.SectionViewerControl();
            this.SuspendLayout();
            // 
            // SectionView
            // 
            this.SectionView.ColorizeTiles = false;
            this.SectionView.CurrentCommand = null;
            this.SectionView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SectionView.Downsample = 256D;
            this.SectionView.Location = new System.Drawing.Point(0, 0);
            this.SectionView.MaximumSize = new System.Drawing.Size(4096, 4096);
            this.SectionView.Name = "SectionView";
            this.SectionView.Scene = null;
            this.SectionView.Section = null;
            this.SectionView.Size = new System.Drawing.Size(800, 450);
            this.SectionView.StatusMagnification = 256D;
            this.SectionView.StatusPosition = ((Geometry.GridVector2)(resources.GetObject("SectionView.StatusPosition")));
            this.SectionView.TabIndex = 0;
            this.SectionView.Text = "sectionViewerControl1";
            // 
            // PenAnnotationViewForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.SectionView);
            this.Name = "PenAnnotationViewForm";
            this.Text = "PenAnnotationViewForm";
            this.ResumeLayout(false);

        }

        #endregion

        private Viking.UI.Controls.SectionViewerControl SectionView;
    }
}