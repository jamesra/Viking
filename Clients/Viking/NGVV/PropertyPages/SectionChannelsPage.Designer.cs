namespace Viking.PropertyPages
{
    partial class SectionChannelsPage
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
            this.channelSetupControl = new Viking.UI.Controls.ChannelSetupControl();
            this.SuspendLayout();
            // 
            // channelSetupControl
            // 
            this.channelSetupControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.channelSetupControl.Location = new System.Drawing.Point(0, 0);
            this.channelSetupControl.Name = "channelSetupControl";
            this.channelSetupControl.Size = new System.Drawing.Size(280, 360);
            this.channelSetupControl.TabIndex = 0;
            // 
            // SectionChannelsPage
            // 
            this.Controls.Add(this.channelSetupControl);
            this.Name = "SectionChannelsPage";
            this.ResumeLayout(false);

        }

        #endregion

        private Viking.UI.Controls.ChannelSetupControl channelSetupControl;

    }
}
