namespace Viking.UI.Controls
{
    partial class ObjectLinkLabel
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.txtName = new System.Windows.Forms.LinkLabel();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.Pict = new System.Windows.Forms.PictureBox();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.Pict)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panel1.Controls.Add(this.txtName);
            this.panel1.Controls.Add(this.btnBrowse);
            this.panel1.Controls.Add(this.Pict);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(160, 21);
            this.panel1.TabIndex = 1;
            // 
            // txtName
            // 
            this.txtName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtName.Location = new System.Drawing.Point(16, 0);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(116, 17);
            this.txtName.TabIndex = 5;
            this.txtName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.txtName.MouseUp += new System.Windows.Forms.MouseEventHandler(this.txtName_MouseUp);
            // 
            // btnBrowse
            // 
            this.btnBrowse.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnBrowse.Location = new System.Drawing.Point(132, 0);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(24, 17);
            this.btnBrowse.TabIndex = 6;
            this.btnBrowse.Text = "...";
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // Pict
            // 
            this.Pict.Dock = System.Windows.Forms.DockStyle.Left;
            this.Pict.Location = new System.Drawing.Point(0, 0);
            this.Pict.Name = "Pict";
            this.Pict.Size = new System.Drawing.Size(16, 17);
            this.Pict.TabIndex = 3;
            this.Pict.TabStop = false;
            // 
            // ObjectLinkLabel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel1);
            this.Name = "ObjectLinkLabel";
            this.Size = new System.Drawing.Size(160, 21);
            this.Load += new System.EventHandler(this.ObjectLinkLabel_Load);
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.Pict)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.LinkLabel txtName;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.PictureBox Pict;

    }
}
