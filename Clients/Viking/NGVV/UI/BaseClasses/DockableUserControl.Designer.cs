namespace Viking.UI.BaseClasses
{
    partial class DockableUserControl
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
            this.LabelTitle = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // LabelTitle
            // 
            this.LabelTitle.AccessibleRole = System.Windows.Forms.AccessibleRole.TitleBar;
            this.LabelTitle.BackColor = System.Drawing.SystemColors.Highlight;
            this.LabelTitle.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.LabelTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.LabelTitle.ForeColor = System.Drawing.SystemColors.ControlText;
            this.LabelTitle.Location = new System.Drawing.Point(0, 0);
            this.LabelTitle.Name = "LabelTitle";
            this.LabelTitle.Size = new System.Drawing.Size(335, 16);
            this.LabelTitle.TabIndex = 1;
            this.LabelTitle.Text = "Title";
            this.LabelTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // DockableUserControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.LabelTitle);
            this.Name = "DockableUserControl";
            this.Size = new System.Drawing.Size(335, 425);
            this.ResumeLayout(false);

        }

        #endregion

        protected System.Windows.Forms.Label LabelTitle;
    }
}
