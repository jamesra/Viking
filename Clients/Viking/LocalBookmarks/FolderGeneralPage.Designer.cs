namespace LocalBookmarks
{
    partial class FolderGeneralPage
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
            this.labelName = new System.Windows.Forms.Label();
            this.textName = new System.Windows.Forms.TextBox();
            this.comboShape = new System.Windows.Forms.ComboBox();
            this.labelShape = new System.Windows.Forms.Label();
            this.labelColor = new System.Windows.Forms.Label();
            this.btnColor = new System.Windows.Forms.Button();
            this.colorDialog = new System.Windows.Forms.ColorDialog();
            this.SuspendLayout();
            // 
            // labelName
            // 
            this.labelName.AutoSize = true;
            this.labelName.Location = new System.Drawing.Point(4, 11);
            this.labelName.Name = "labelName";
            this.labelName.Size = new System.Drawing.Size(38, 13);
            this.labelName.TabIndex = 0;
            this.labelName.Text = "Name:";
            // 
            // textName
            // 
            this.textName.Location = new System.Drawing.Point(46, 8);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(229, 20);
            this.textName.TabIndex = 1;
            // 
            // comboShape
            // 
            this.comboShape.FormattingEnabled = true;
            this.comboShape.Items.AddRange(new object[] {
            "Inherit",
            "Star",
            "Arrow",
            "Ring"});
            this.comboShape.Location = new System.Drawing.Point(46, 35);
            this.comboShape.Name = "comboShape";
            this.comboShape.Size = new System.Drawing.Size(121, 21);
            this.comboShape.TabIndex = 2;
            this.comboShape.Text = "Inherit";
            // 
            // labelShape
            // 
            this.labelShape.AutoSize = true;
            this.labelShape.Location = new System.Drawing.Point(4, 38);
            this.labelShape.Name = "labelShape";
            this.labelShape.Size = new System.Drawing.Size(41, 13);
            this.labelShape.TabIndex = 3;
            this.labelShape.Text = "Shape:";
            // 
            // labelColor
            // 
            this.labelColor.AutoSize = true;
            this.labelColor.Location = new System.Drawing.Point(4, 67);
            this.labelColor.Name = "labelColor";
            this.labelColor.Size = new System.Drawing.Size(34, 13);
            this.labelColor.TabIndex = 4;
            this.labelColor.Text = "Color:";
            // 
            // btnColor
            // 
            this.btnColor.BackColor = System.Drawing.SystemColors.Control;
            this.btnColor.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnColor.Location = new System.Drawing.Point(44, 62);
            this.btnColor.Name = "btnColor";
            this.btnColor.Size = new System.Drawing.Size(75, 23);
            this.btnColor.TabIndex = 5;
            this.btnColor.UseVisualStyleBackColor = false;
            this.btnColor.Click += new System.EventHandler(this.btnColor_Click);
            // 
            // colorDialog
            // 
            this.colorDialog.AnyColor = true;
            this.colorDialog.FullOpen = true;
            // 
            // FolderGeneralPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.btnColor);
            this.Controls.Add(this.labelColor);
            this.Controls.Add(this.labelShape);
            this.Controls.Add(this.comboShape);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.labelName);
            this.Name = "FolderGeneralPage";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.ComboBox comboShape;
        private System.Windows.Forms.Label labelShape;
        private System.Windows.Forms.Label labelColor;
        private System.Windows.Forms.Button btnColor;
        private System.Windows.Forms.ColorDialog colorDialog;
    }
}
