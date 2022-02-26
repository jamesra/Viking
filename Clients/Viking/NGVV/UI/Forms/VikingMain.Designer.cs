namespace Viking
{
    partial class VikingMain
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VikingMain));
            this.menuViewer = new System.Windows.Forms.MenuStrip();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.versionInfoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.vikingHomepageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.CacheCleaningTimer = new System.Windows.Forms.Timer(this.components);
            this.TabsModules = new Viking.UI.BaseClasses.ModuleTabControl();
            this.menuViewer.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuViewer
            // 
            this.menuViewer.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.helpToolStripMenuItem});
            this.menuViewer.Location = new System.Drawing.Point(0, 0);
            this.menuViewer.Name = "menuViewer";
            this.menuViewer.Size = new System.Drawing.Size(867, 24);
            this.menuViewer.TabIndex = 1;
            this.menuViewer.Text = "menuViewer";
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.versionInfoToolStripMenuItem,
            this.vikingHomepageToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // versionInfoToolStripMenuItem
            // 
            this.versionInfoToolStripMenuItem.Name = "versionInfoToolStripMenuItem";
            this.versionInfoToolStripMenuItem.Size = new System.Drawing.Size(169, 22);
            this.versionInfoToolStripMenuItem.Text = "Version Info";
            this.versionInfoToolStripMenuItem.Click += new System.EventHandler(this.versionInfoToolStripMenuItem_Click);
            // 
            // vikingHomepageToolStripMenuItem
            // 
            this.vikingHomepageToolStripMenuItem.Name = "vikingHomepageToolStripMenuItem";
            this.vikingHomepageToolStripMenuItem.Size = new System.Drawing.Size(169, 22);
            this.vikingHomepageToolStripMenuItem.Text = "Viking Homepage";
            this.vikingHomepageToolStripMenuItem.Click += new System.EventHandler(this.vikingHomepageToolStripMenuItem_Click);
            // 
            // CacheCleaningTimer
            // 
            this.CacheCleaningTimer.Enabled = true;
            this.CacheCleaningTimer.Interval = 60000;
            this.CacheCleaningTimer.Tick += new System.EventHandler(this.CacheCleaningTimer_Tick);
            // 
            // TabsModules
            // 
            this.TabsModules.AllowDrop = true;
            this.TabsModules.Dock = System.Windows.Forms.DockStyle.Left;
            this.TabsModules.Location = new System.Drawing.Point(0, 24);
            this.TabsModules.Name = "TabsModules";
            this.TabsModules.Size = new System.Drawing.Size(160, 628);
            this.TabsModules.TabIndex = 2;
            this.TabsModules.TabStop = false;
            this.TabsModules.Title = "Module Tabs";
            this.TabsModules.TitleVisible = false;
            // 
            // VikingMain
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(867, 652);
            this.Controls.Add(this.TabsModules);
            this.Controls.Add(this.menuViewer);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.IsMdiContainer = true;
            this.MainMenuStrip = this.menuViewer;
            this.Name = "VikingMain";
            this.Text = "Per aspera ad astra";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.VikingMain_FormClosed);
            this.Load += new System.EventHandler(this.VikingMain_Load);
            this.menuViewer.ResumeLayout(false);
            this.menuViewer.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuViewer;
        private Viking.UI.BaseClasses.ModuleTabControl TabsModules;
        private System.Windows.Forms.Timer CacheCleaningTimer;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem vikingHomepageToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem versionInfoToolStripMenuItem;
    }
}

