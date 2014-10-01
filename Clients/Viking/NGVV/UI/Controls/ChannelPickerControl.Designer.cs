namespace Viking.UI.Controls
{
    partial class ChannelPickerControl
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
            this.colorDialog = new System.Windows.Forms.ColorDialog();
            this.panelControls = new System.Windows.Forms.Panel();
            this.comboChannel = new System.Windows.Forms.ComboBox();
            this.panelColor = new System.Windows.Forms.Panel();
            this.comboColor = new System.Windows.Forms.ComboBox();
            this.comboSection = new System.Windows.Forms.ComboBox();
            this.buttonDelete = new System.Windows.Forms.Button();
            this.panelLabels = new System.Windows.Forms.Panel();
            this.labelChannel = new System.Windows.Forms.Label();
            this.labelColor = new System.Windows.Forms.Label();
            this.labelSection = new System.Windows.Forms.Label();
            this.panelDeleteSpacer = new System.Windows.Forms.Panel();
            this.panelControls.SuspendLayout();
            this.panelLabels.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelControls
            // 
            this.panelControls.Controls.Add(this.comboChannel);
            this.panelControls.Controls.Add(this.panelColor);
            this.panelControls.Controls.Add(this.comboColor);
            this.panelControls.Controls.Add(this.comboSection);
            this.panelControls.Controls.Add(this.buttonDelete);
            this.panelControls.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelControls.Location = new System.Drawing.Point(0, 13);
            this.panelControls.Name = "panelControls";
            this.panelControls.Size = new System.Drawing.Size(320, 21);
            this.panelControls.TabIndex = 8;
            // 
            // comboChannel
            // 
            this.comboChannel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboChannel.FormattingEnabled = true;
            this.comboChannel.Location = new System.Drawing.Point(121, 0);
            this.comboChannel.Name = "comboChannel";
            this.comboChannel.Size = new System.Drawing.Size(100, 21);
            this.comboChannel.TabIndex = 16;
            this.comboChannel.SelectedValueChanged += new System.EventHandler(this.comboChannel_SelectedValueChanged);
            // 
            // panelColor
            // 
            this.panelColor.Dock = System.Windows.Forms.DockStyle.Right;
            this.panelColor.Location = new System.Drawing.Point(221, 0);
            this.panelColor.Name = "panelColor";
            this.panelColor.Size = new System.Drawing.Size(21, 21);
            this.panelColor.TabIndex = 18;
            // 
            // comboColor
            // 
            this.comboColor.Dock = System.Windows.Forms.DockStyle.Right;
            this.comboColor.FormattingEnabled = true;
            this.comboColor.Items.AddRange(new object[] {
            "White",
            "Red",
            "Green",
            "Blue",
            "Custom..."});
            this.comboColor.Location = new System.Drawing.Point(242, 0);
            this.comboColor.Name = "comboColor";
            this.comboColor.Size = new System.Drawing.Size(78, 21);
            this.comboColor.TabIndex = 17;
            this.comboColor.Text = "Black";
            this.comboColor.SelectedValueChanged += new System.EventHandler(this.comboColor_SelectedValueChanged);
            // 
            // comboSection
            // 
            this.comboSection.Dock = System.Windows.Forms.DockStyle.Left;
            this.comboSection.FormattingEnabled = true;
            this.comboSection.Location = new System.Drawing.Point(21, 0);
            this.comboSection.Name = "comboSection";
            this.comboSection.Size = new System.Drawing.Size(100, 21);
            this.comboSection.TabIndex = 15;
            this.comboSection.Text = "Selected Section";
            this.comboSection.SelectedValueChanged += new System.EventHandler(this.comboSection_SelectedValueChanged);
            // 
            // buttonDelete
            // 
            this.buttonDelete.Dock = System.Windows.Forms.DockStyle.Left;
            this.buttonDelete.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonDelete.ForeColor = System.Drawing.Color.DarkRed;
            this.buttonDelete.Location = new System.Drawing.Point(0, 0);
            this.buttonDelete.Name = "buttonDelete";
            this.buttonDelete.Size = new System.Drawing.Size(21, 21);
            this.buttonDelete.TabIndex = 14;
            this.buttonDelete.Text = "X";
            this.buttonDelete.UseVisualStyleBackColor = true;
            this.buttonDelete.Click += new System.EventHandler(this.buttonDelete_Click);
            // 
            // panelLabels
            // 
            this.panelLabels.Controls.Add(this.labelChannel);
            this.panelLabels.Controls.Add(this.labelColor);
            this.panelLabels.Controls.Add(this.labelSection);
            this.panelLabels.Controls.Add(this.panelDeleteSpacer);
            this.panelLabels.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelLabels.Location = new System.Drawing.Point(0, 0);
            this.panelLabels.Name = "panelLabels";
            this.panelLabels.Size = new System.Drawing.Size(320, 13);
            this.panelLabels.TabIndex = 9;
            // 
            // labelChannel
            // 
            this.labelChannel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelChannel.Location = new System.Drawing.Point(121, 0);
            this.labelChannel.Name = "labelChannel";
            this.labelChannel.Size = new System.Drawing.Size(100, 13);
            this.labelChannel.TabIndex = 9;
            this.labelChannel.Text = "Channel";
            // 
            // labelColor
            // 
            this.labelColor.Cursor = System.Windows.Forms.Cursors.No;
            this.labelColor.Dock = System.Windows.Forms.DockStyle.Right;
            this.labelColor.Location = new System.Drawing.Point(221, 0);
            this.labelColor.Name = "labelColor";
            this.labelColor.Size = new System.Drawing.Size(99, 13);
            this.labelColor.TabIndex = 10;
            this.labelColor.Text = "Color";
            // 
            // labelSection
            // 
            this.labelSection.Dock = System.Windows.Forms.DockStyle.Left;
            this.labelSection.Location = new System.Drawing.Point(21, 0);
            this.labelSection.Name = "labelSection";
            this.labelSection.Size = new System.Drawing.Size(100, 13);
            this.labelSection.TabIndex = 1;
            this.labelSection.Text = "Section";
            // 
            // panelDeleteSpacer
            // 
            this.panelDeleteSpacer.BackColor = System.Drawing.SystemColors.Control;
            this.panelDeleteSpacer.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelDeleteSpacer.Location = new System.Drawing.Point(0, 0);
            this.panelDeleteSpacer.Name = "panelDeleteSpacer";
            this.panelDeleteSpacer.Size = new System.Drawing.Size(21, 13);
            this.panelDeleteSpacer.TabIndex = 0;
            // 
            // ChannelPickerControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelControls);
            this.Controls.Add(this.panelLabels);
            this.Name = "ChannelPickerControl";
            this.Size = new System.Drawing.Size(320, 34);
            this.Load += new System.EventHandler(this.ChannelPickerControl_Load);
            this.panelControls.ResumeLayout(false);
            this.panelLabels.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ColorDialog colorDialog;
        private System.Windows.Forms.Panel panelControls;
        private System.Windows.Forms.Panel panelLabels;
        private System.Windows.Forms.Label labelSection;
        private System.Windows.Forms.Label labelColor;
        private System.Windows.Forms.Label labelChannel;
        private System.Windows.Forms.ComboBox comboChannel;
        private System.Windows.Forms.Panel panelColor;
        private System.Windows.Forms.ComboBox comboColor;
        private System.Windows.Forms.ComboBox comboSection;
        private System.Windows.Forms.Button buttonDelete;
        private System.Windows.Forms.Panel panelDeleteSpacer;
    }
}
