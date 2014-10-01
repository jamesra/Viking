namespace WebAnnotation.UI
{
    partial class StructureBaseGeneralPage
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
            this.labelID = new System.Windows.Forms.Label();
            this.labelType = new System.Windows.Forms.Label();
            this.labelNotes = new System.Windows.Forms.Label();
            this.textNotes = new System.Windows.Forms.RichTextBox();
            this.linkType = new Viking.UI.Controls.ObjectLinkLabel();
            this.textID = new System.Windows.Forms.TextBox();
            this.checkVerified = new System.Windows.Forms.CheckBox();
            this.labelConfidence = new System.Windows.Forms.Label();
            this.numConfidence = new System.Windows.Forms.NumericUpDown();
            this.listTags = new System.Windows.Forms.ListBox();
            this.labelTags = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.numConfidence)).BeginInit();
            this.SuspendLayout();
            // 
            // labelID
            // 
            this.labelID.AutoSize = true;
            this.labelID.Location = new System.Drawing.Point(12, 17);
            this.labelID.Name = "labelID";
            this.labelID.Size = new System.Drawing.Size(21, 13);
            this.labelID.TabIndex = 0;
            this.labelID.Text = "ID:";
            // 
            // labelType
            // 
            this.labelType.AutoSize = true;
            this.labelType.Location = new System.Drawing.Point(12, 37);
            this.labelType.Name = "labelType";
            this.labelType.Size = new System.Drawing.Size(34, 13);
            this.labelType.TabIndex = 1;
            this.labelType.Text = "Type:";
            // 
            // labelNotes
            // 
            this.labelNotes.AutoSize = true;
            this.labelNotes.Location = new System.Drawing.Point(12, 240);
            this.labelNotes.Name = "labelNotes";
            this.labelNotes.Size = new System.Drawing.Size(38, 13);
            this.labelNotes.TabIndex = 2;
            this.labelNotes.Text = "Notes:";
            // 
            // textNotes
            // 
            this.textNotes.Location = new System.Drawing.Point(15, 256);
            this.textNotes.Name = "textNotes";
            this.textNotes.Size = new System.Drawing.Size(253, 92);
            this.textNotes.TabIndex = 3;
            this.textNotes.Text = "";
            // 
            // linkType
            // 
            this.linkType.Location = new System.Drawing.Point(15, 53);
            this.linkType.Name = "linkType";
            this.linkType.ReadOnly = false;
            this.linkType.Size = new System.Drawing.Size(253, 21);
            this.linkType.SourceObject = null;
            this.linkType.SourceType = null;
            this.linkType.TabIndex = 4;
            // 
            // textID
            // 
            this.textID.Location = new System.Drawing.Point(43, 14);
            this.textID.Name = "textID";
            this.textID.ReadOnly = true;
            this.textID.Size = new System.Drawing.Size(225, 20);
            this.textID.TabIndex = 5;
            // 
            // checkVerified
            // 
            this.checkVerified.AutoSize = true;
            this.checkVerified.Location = new System.Drawing.Point(15, 120);
            this.checkVerified.Name = "checkVerified";
            this.checkVerified.Size = new System.Drawing.Size(61, 17);
            this.checkVerified.TabIndex = 6;
            this.checkVerified.Text = "Verified";
            this.checkVerified.UseVisualStyleBackColor = true;
            // 
            // labelConfidence
            // 
            this.labelConfidence.AutoSize = true;
            this.labelConfidence.Location = new System.Drawing.Point(12, 77);
            this.labelConfidence.Name = "labelConfidence";
            this.labelConfidence.Size = new System.Drawing.Size(64, 13);
            this.labelConfidence.TabIndex = 7;
            this.labelConfidence.Text = "Confidence:";
            // 
            // numConfidence
            // 
            this.numConfidence.DecimalPlaces = 2;
            this.numConfidence.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numConfidence.Location = new System.Drawing.Point(15, 94);
            this.numConfidence.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numConfidence.Name = "numConfidence";
            this.numConfidence.Size = new System.Drawing.Size(249, 20);
            this.numConfidence.TabIndex = 8;
            // 
            // listTags
            // 
            this.listTags.FormattingEnabled = true;
            this.listTags.Location = new System.Drawing.Point(15, 168);
            this.listTags.Name = "listTags";
            this.listTags.Size = new System.Drawing.Size(253, 69);
            this.listTags.Sorted = true;
            this.listTags.TabIndex = 9;
            // 
            // labelTags
            // 
            this.labelTags.AutoSize = true;
            this.labelTags.Location = new System.Drawing.Point(15, 149);
            this.labelTags.Name = "labelTags";
            this.labelTags.Size = new System.Drawing.Size(34, 13);
            this.labelTags.TabIndex = 10;
            this.labelTags.Text = "Tags:";
            // 
            // StructureBaseGeneralPage
            // 
            this.Controls.Add(this.labelTags);
            this.Controls.Add(this.listTags);
            this.Controls.Add(this.numConfidence);
            this.Controls.Add(this.labelConfidence);
            this.Controls.Add(this.checkVerified);
            this.Controls.Add(this.textID);
            this.Controls.Add(this.linkType);
            this.Controls.Add(this.textNotes);
            this.Controls.Add(this.labelNotes);
            this.Controls.Add(this.labelType);
            this.Controls.Add(this.labelID);
            this.Name = "StructureBaseGeneralPage";
            ((System.ComponentModel.ISupportInitialize)(this.numConfidence)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelID;
        private System.Windows.Forms.Label labelType;
        private System.Windows.Forms.Label labelNotes;
        private System.Windows.Forms.RichTextBox textNotes;
        private Viking.UI.Controls.ObjectLinkLabel linkType;
        private System.Windows.Forms.TextBox textID;
        private System.Windows.Forms.CheckBox checkVerified;
        private System.Windows.Forms.Label labelConfidence;
        private System.Windows.Forms.NumericUpDown numConfidence;
        private System.Windows.Forms.ListBox listTags;
        private System.Windows.Forms.Label labelTags;
    }
}
