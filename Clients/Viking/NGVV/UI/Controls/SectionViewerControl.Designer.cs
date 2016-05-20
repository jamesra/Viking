namespace Viking.UI.Controls
{
    partial class SectionViewerControl
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

                if(_OverlayBackgroundDepthState != null)
                    _OverlayBackgroundDepthState.Dispose();
                if (_DrawSectionDepthState != null)
                    _DrawSectionDepthState.Dispose();
                if (_DepthDisabledState != null)
                    _DepthDisabledState.Dispose();
                if (_OverlayDepthState != null)
                    _OverlayDepthState.Dispose();
                if (_defaultDepthState != null)
                    _defaultDepthState.Dispose(); 
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
            this.components = new System.ComponentModel.Container();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.timerTileCacheCheckpoint = new System.Windows.Forms.Timer(this.components);
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.menuVolume = new System.Windows.Forms.ToolStripMenuItem();
            this.menuVolumeTransforms = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.menuClearCache = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSection = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSectionChannel = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSectionUseSpecific = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSectionShowMesh = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSetupChannels = new System.Windows.Forms.ToolStripMenuItem();
            this.menuColorizeTiles = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSectionShowTileMesh = new System.Windows.Forms.ToolStripMenuItem();
            this.menuCommands = new System.Windows.Forms.ToolStripMenuItem();
            this.menuGoToLocation = new System.Windows.Forms.ToolStripMenuItem();
            this.menuCaptureScreen = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExportFrames = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExportTiles = new System.Windows.Forms.ToolStripMenuItem();
            this.timerHelpTextChange = new System.Windows.Forms.Timer(this.components);
            this.menuShowCommandHelp = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // timer
            // 
            this.timer.Enabled = true;
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // timerTileCacheCheckpoint
            // 
            this.timerTileCacheCheckpoint.Enabled = true;
            this.timerTileCacheCheckpoint.Interval = 10000;
            this.timerTileCacheCheckpoint.Tick += new System.EventHandler(this.timerTileCacheCheckpoint_Tick);
            // 
            // menuStrip
            // 
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuVolume,
            this.menuSection,
            this.menuCommands});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(284, 24);
            this.menuStrip.TabIndex = 1;
            this.menuStrip.Text = "menuStrip1";
            this.menuStrip.Visible = false;
            // 
            // menuVolume
            // 
            this.menuVolume.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuVolumeTransforms,
            this.toolStripSeparator1,
            this.menuClearCache});
            this.menuVolume.Name = "menuVolume";
            this.menuVolume.Size = new System.Drawing.Size(60, 20);
            this.menuVolume.Text = "Volume";
            this.menuVolume.DropDownOpening += new System.EventHandler(this.menuVolume_DropDownOpening);
            // 
            // menuVolumeTransforms
            // 
            this.menuVolumeTransforms.Name = "menuVolumeTransforms";
            this.menuVolumeTransforms.Size = new System.Drawing.Size(173, 22);
            this.menuVolumeTransforms.Text = "Transform";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(170, 6);
            // 
            // menuClearCache
            // 
            this.menuClearCache.Name = "menuClearCache";
            this.menuClearCache.Size = new System.Drawing.Size(173, 22);
            this.menuClearCache.Text = "Clear Image Cache";
            this.menuClearCache.Click += new System.EventHandler(this.menuClearTextureCache_Click);
            // 
            // menuSection
            // 
            this.menuSection.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuSectionChannel,
            this.menuSectionUseSpecific,
            this.menuSectionShowMesh,
            this.menuSetupChannels,
            this.menuColorizeTiles,
            this.menuSectionShowTileMesh});
            this.menuSection.Name = "menuSection";
            this.menuSection.Size = new System.Drawing.Size(58, 20);
            this.menuSection.Text = "Section";
            this.menuSection.DropDownOpening += new System.EventHandler(this.menuSection_DropDownOpening);
            // 
            // menuSectionChannel
            // 
            this.menuSectionChannel.Name = "menuSectionChannel";
            this.menuSectionChannel.Size = new System.Drawing.Size(220, 22);
            this.menuSectionChannel.Text = "Channel";
            // 
            // menuSectionUseSpecific
            // 
            this.menuSectionUseSpecific.Name = "menuSectionUseSpecific";
            this.menuSectionUseSpecific.Size = new System.Drawing.Size(220, 22);
            this.menuSectionUseSpecific.Text = "Section Specific Transforms";
            this.menuSectionUseSpecific.Click += new System.EventHandler(this.useSectionSpecificTransformsToolStripMenuItem_Click);
            // 
            // menuSectionShowMesh
            // 
            this.menuSectionShowMesh.Name = "menuSectionShowMesh";
            this.menuSectionShowMesh.Size = new System.Drawing.Size(220, 22);
            this.menuSectionShowMesh.Text = "Show Mesh";
            this.menuSectionShowMesh.Click += new System.EventHandler(this.menuSectionShowMesh_Click_1);
            // 
            // menuSetupChannels
            // 
            this.menuSetupChannels.Name = "menuSetupChannels";
            this.menuSetupChannels.Size = new System.Drawing.Size(220, 22);
            this.menuSetupChannels.Text = "Setup Channels...";
            this.menuSetupChannels.Click += new System.EventHandler(this.menuSetupChannels_Click);
            // 
            // menuColorizeTiles
            // 
            this.menuColorizeTiles.Name = "menuColorizeTiles";
            this.menuColorizeTiles.Size = new System.Drawing.Size(220, 22);
            this.menuColorizeTiles.Text = "Colorize Tiles";
            this.menuColorizeTiles.Click += new System.EventHandler(this.menuColorizeTiles_Click);
            // 
            // menuSectionShowTileMesh
            // 
            this.menuSectionShowTileMesh.Name = "menuSectionShowTileMesh";
            this.menuSectionShowTileMesh.Size = new System.Drawing.Size(220, 22);
            this.menuSectionShowTileMesh.Text = "Show Tile Mesh";
            this.menuSectionShowTileMesh.Click += new System.EventHandler(this.menuSectionShowTileMesh_Click);
            // 
            // menuCommands
            // 
            this.menuCommands.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuGoToLocation,
            this.menuCaptureScreen,
            this.menuExportFrames,
            this.menuExportTiles,
            this.menuShowCommandHelp});
            this.menuCommands.Name = "menuCommands";
            this.menuCommands.Size = new System.Drawing.Size(81, 20);
            this.menuCommands.Text = "Commands";
            // 
            // menuGoToLocation
            // 
            this.menuGoToLocation.Name = "menuGoToLocation";
            this.menuGoToLocation.Size = new System.Drawing.Size(191, 22);
            this.menuGoToLocation.Text = "Go To Location";
            this.menuGoToLocation.Click += new System.EventHandler(this.menuGoToLocation_Click);
            // 
            // menuCaptureScreen
            // 
            this.menuCaptureScreen.Name = "menuCaptureScreen";
            this.menuCaptureScreen.Size = new System.Drawing.Size(191, 22);
            this.menuCaptureScreen.Text = "Capture Screen";
            this.menuCaptureScreen.Click += new System.EventHandler(this.menuCaptureScreen_Click);
            // 
            // menuExportFrames
            // 
            this.menuExportFrames.Name = "menuExportFrames";
            this.menuExportFrames.Size = new System.Drawing.Size(191, 22);
            this.menuExportFrames.Text = "Export Frames";
            this.menuExportFrames.Click += new System.EventHandler(this.menuExportFrames_Click);
            // 
            // menuExportTiles
            // 
            this.menuExportTiles.AccessibleDescription = "Export Tiles";
            this.menuExportTiles.Name = "menuExportTiles";
            this.menuExportTiles.Size = new System.Drawing.Size(191, 22);
            this.menuExportTiles.Text = "Export Tiles";
            this.menuExportTiles.Click += new System.EventHandler(this.menuExportTiles_Click);
            // 
            // timerHelpTextChange
            // 
            this.timerHelpTextChange.Enabled = true;
            this.timerHelpTextChange.Interval = 5000;
            this.timerHelpTextChange.Tick += new System.EventHandler(this.timerHelpTextChange_Tick);
            // 
            // menuShowCommandHelp
            // 
            this.menuShowCommandHelp.Checked = true;
            this.menuShowCommandHelp.CheckState = System.Windows.Forms.CheckState.Checked;
            this.menuShowCommandHelp.Name = "menuShowCommandHelp";
            this.menuShowCommandHelp.Size = new System.Drawing.Size(191, 22);
            this.menuShowCommandHelp.Text = "Show Command Help";
            this.menuShowCommandHelp.Click += new System.EventHandler(this.menuShowCommandHelp_Click);
            // 
            // SectionViewerControl
            // 
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.SectionViewerControl_KeyDown);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.SectionViewerControl_MouseDown);
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.Timer timerTileCacheCheckpoint;
        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem menuVolume;
        private System.Windows.Forms.ToolStripMenuItem menuVolumeTransforms;
        private System.Windows.Forms.ToolStripMenuItem menuSection;
        private System.Windows.Forms.ToolStripMenuItem menuSectionChannel;
        private System.Windows.Forms.ToolStripMenuItem menuSectionUseSpecific;
        private System.Windows.Forms.ToolStripMenuItem menuSectionShowMesh;
        private System.Windows.Forms.ToolStripMenuItem menuSetupChannels;
        private System.Windows.Forms.ToolStripMenuItem menuCommands;
        private System.Windows.Forms.ToolStripMenuItem menuGoToLocation;
        private System.Windows.Forms.ToolStripMenuItem menuCaptureScreen;
        private System.Windows.Forms.ToolStripMenuItem menuExportFrames;
        private System.Windows.Forms.ToolStripMenuItem menuColorizeTiles;
        private System.Windows.Forms.ToolStripMenuItem menuExportTiles;
        private System.Windows.Forms.ToolStripMenuItem menuSectionShowTileMesh;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem menuClearCache;
        private System.Windows.Forms.Timer timerHelpTextChange;
        private System.Windows.Forms.ToolStripMenuItem menuShowCommandHelp;
    }
}
