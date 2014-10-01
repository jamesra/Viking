namespace Viking.UI.Forms
{
    partial class PropertySheetForm
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PropertySheetForm));
            this.panel1 = new System.Windows.Forms.Panel();
            this.BtnApply = new System.Windows.Forms.Button();
            this.BtnCancel = new System.Windows.Forms.Button();
            this.BtnOK = new System.Windows.Forms.Button();
            this.ImagesToolBar = new System.Windows.Forms.ImageList(this.components);
            this.TabsProperty = new Viking.UI.Controls.PropertySheetControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.Tools = new Viking.UI.Controls.EnableToolBar();
            this.panel1.SuspendLayout();
            this.TabsProperty.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.BtnApply);
            this.panel1.Controls.Add(this.BtnCancel);
            this.panel1.Controls.Add(this.BtnOK);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 346);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(288, 40);
            this.panel1.TabIndex = 7;
            // 
            // BtnApply
            // 
            this.BtnApply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.BtnApply.Location = new System.Drawing.Point(200, 8);
            this.BtnApply.Name = "BtnApply";
            this.BtnApply.Size = new System.Drawing.Size(80, 24);
            this.BtnApply.TabIndex = 6;
            this.BtnApply.Text = "Apply";
            this.BtnApply.Click += new System.EventHandler(this.BtnApply_Click);
            // 
            // BtnCancel
            // 
            this.BtnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.BtnCancel.CausesValidation = false;
            this.BtnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.BtnCancel.Location = new System.Drawing.Point(104, 8);
            this.BtnCancel.Name = "BtnCancel";
            this.BtnCancel.Size = new System.Drawing.Size(80, 24);
            this.BtnCancel.TabIndex = 5;
            this.BtnCancel.Text = "Cancel";
            this.BtnCancel.Click += new System.EventHandler(this.BtnCancel_Click);
            // 
            // BtnOK
            // 
            this.BtnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.BtnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.BtnOK.Location = new System.Drawing.Point(8, 8);
            this.BtnOK.Name = "BtnOK";
            this.BtnOK.Size = new System.Drawing.Size(80, 24);
            this.BtnOK.TabIndex = 4;
            this.BtnOK.Text = "OK";
            this.BtnOK.Click += new System.EventHandler(this.BtnOK_Click);
            // 
            // ImagesToolBar
            // 
            this.ImagesToolBar.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
            this.ImagesToolBar.ImageSize = new System.Drawing.Size(16, 16);
            this.ImagesToolBar.TransparentColor = System.Drawing.Color.Transparent;
            // 
            // TabsProperty
            // 
            this.TabsProperty.Controls.Add(this.tabPage1);
            this.TabsProperty.Controls.Add(this.tabPage2);
            this.TabsProperty.DisplayType = null;
            this.TabsProperty.Dock = System.Windows.Forms.DockStyle.Fill;
            this.TabsProperty.Location = new System.Drawing.Point(0, 24);
            this.TabsProperty.Name = "TabsProperty";
            this.TabsProperty.SelectedIndex = 0;
            this.TabsProperty.Size = new System.Drawing.Size(288, 322);
            this.TabsProperty.TabIndex = 8;
            // 
            // tabPage1
            // 
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(280, 296);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "tabPage1";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(280, 296);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "tabPage2";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // Tools
            // 
            this.Tools.Dock = System.Windows.Forms.DockStyle.Top;
            this.Tools.Location = new System.Drawing.Point(0, 0);
            this.Tools.Name = "Tools";
            this.Tools.Size = new System.Drawing.Size(288, 24);
            this.Tools.TabIndex = 9;
            // 
            // PropertySheetForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(288, 386);
            this.Controls.Add(this.TabsProperty);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.Tools);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PropertySheetForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "PropertySheetForm";
            this.Load += new System.EventHandler(this.PropertySheetForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PropertySheetForm_FormClosing);
            this.panel1.ResumeLayout(false);
            this.TabsProperty.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        protected System.Windows.Forms.Button BtnApply;
        protected System.Windows.Forms.Button BtnCancel;
        protected System.Windows.Forms.Button BtnOK;
        private System.Windows.Forms.ImageList ImagesToolBar;
        private Viking.UI.Controls.PropertySheetControl TabsProperty;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private Viking.UI.Controls.EnableToolBar Tools;
    }
}