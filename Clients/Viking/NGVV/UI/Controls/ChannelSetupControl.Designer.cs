namespace Viking.UI.Controls
{
    partial class ChannelSetupControl
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
            this.groupChannels = new System.Windows.Forms.GroupBox();
            this.panelChannels = new System.Windows.Forms.Panel();
            this.buttonAddChannel = new System.Windows.Forms.Button();
            this.radioColor = new System.Windows.Forms.RadioButton();
            this.radioGreyscale = new System.Windows.Forms.RadioButton();
            this.groupChannels.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupChannels
            // 
            this.groupChannels.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.groupChannels.Controls.Add(this.panelChannels);
            this.groupChannels.Controls.Add(this.buttonAddChannel);
            this.groupChannels.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupChannels.Location = new System.Drawing.Point(0, 42);
            this.groupChannels.Name = "groupChannels";
            this.groupChannels.Size = new System.Drawing.Size(306, 246);
            this.groupChannels.TabIndex = 0;
            this.groupChannels.TabStop = false;
            this.groupChannels.Text = "Channel Setup";
            // 
            // panelChannels
            // 
            this.panelChannels.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelChannels.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelChannels.Location = new System.Drawing.Point(3, 16);
            this.panelChannels.Name = "panelChannels";
            this.panelChannels.Size = new System.Drawing.Size(300, 204);
            this.panelChannels.TabIndex = 2;
            // 
            // buttonAddChannel
            // 
            this.buttonAddChannel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttonAddChannel.Location = new System.Drawing.Point(3, 220);
            this.buttonAddChannel.Name = "buttonAddChannel";
            this.buttonAddChannel.Size = new System.Drawing.Size(300, 23);
            this.buttonAddChannel.TabIndex = 1;
            this.buttonAddChannel.Text = "Add Channel";
            this.buttonAddChannel.UseVisualStyleBackColor = true;
            this.buttonAddChannel.Click += new System.EventHandler(this.buttonAddChannel_Click);
            // 
            // radioColor
            // 
            this.radioColor.AutoSize = true;
            this.radioColor.Dock = System.Windows.Forms.DockStyle.Top;
            this.radioColor.Location = new System.Drawing.Point(0, 21);
            this.radioColor.Name = "radioColor";
            this.radioColor.Padding = new System.Windows.Forms.Padding(16, 4, 0, 0);
            this.radioColor.Size = new System.Drawing.Size(306, 21);
            this.radioColor.TabIndex = 6;
            this.radioColor.TabStop = true;
            this.radioColor.Text = "Color";
            this.radioColor.UseVisualStyleBackColor = true;
            this.radioColor.CheckedChanged += new System.EventHandler(this.radioColor_CheckedChanged);
            // 
            // radioGreyscale
            // 
            this.radioGreyscale.AutoSize = true;
            this.radioGreyscale.Dock = System.Windows.Forms.DockStyle.Top;
            this.radioGreyscale.Location = new System.Drawing.Point(0, 0);
            this.radioGreyscale.Name = "radioGreyscale";
            this.radioGreyscale.Padding = new System.Windows.Forms.Padding(16, 4, 0, 0);
            this.radioGreyscale.Size = new System.Drawing.Size(306, 21);
            this.radioGreyscale.TabIndex = 5;
            this.radioGreyscale.TabStop = true;
            this.radioGreyscale.Text = "Default";
            this.radioGreyscale.UseVisualStyleBackColor = true;
            this.radioGreyscale.CheckedChanged += new System.EventHandler(this.radioGreyscale_CheckedChanged);
            // 
            // ChannelSetupControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupChannels);
            this.Controls.Add(this.radioColor);
            this.Controls.Add(this.radioGreyscale);
            this.Name = "ChannelSetupControl";
            this.Size = new System.Drawing.Size(306, 288);
            this.groupChannels.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupChannels;
        private System.Windows.Forms.Panel panelChannels;
        private System.Windows.Forms.RadioButton radioColor;
        private System.Windows.Forms.RadioButton radioGreyscale;
        private System.Windows.Forms.Button buttonAddChannel;

    }
}