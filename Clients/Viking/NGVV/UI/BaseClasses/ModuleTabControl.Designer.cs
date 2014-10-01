namespace Viking.UI.BaseClasses
{
    partial class ModuleTabControl
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
            this.TabsModules = new System.Windows.Forms.TabControl();
            this.SuspendLayout();
            // 
            // TabsModules
            // 
            this.TabsModules.Alignment = System.Windows.Forms.TabAlignment.Bottom;
            this.TabsModules.Dock = System.Windows.Forms.DockStyle.Fill;
            this.TabsModules.Location = new System.Drawing.Point(0, 16);
            this.TabsModules.Multiline = true;
            this.TabsModules.Name = "TabsModules";
            this.TabsModules.SelectedIndex = 0;
            this.TabsModules.Size = new System.Drawing.Size(335, 409);
            this.TabsModules.TabIndex = 2;
            // 
            // ModuleTabControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.Controls.Add(this.TabsModules);
            this.Name = "ModuleTabControl";
            this.Load += new System.EventHandler(this.ModuleTabControl_Load);
            this.Controls.SetChildIndex(this.LabelTitle, 0);
            this.Controls.SetChildIndex(this.TabsModules, 0);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl TabsModules;
    }
}
