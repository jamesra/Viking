namespace Viking.PropertyPages
{
    partial class SectionGeneralPage
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
            this.nameReferenceGroup = new System.Windows.Forms.GroupBox();
            this.listBelow = new System.Windows.Forms.ListBox();
            this.listAbove = new System.Windows.Forms.ListBox();
            this.labelBelow = new System.Windows.Forms.Label();
            this.labelAbove = new System.Windows.Forms.Label();
            this.richNotes = new System.Windows.Forms.RichTextBox();
            this.textSectionNameNumber = new System.Windows.Forms.TextBox();
            this.labelNumber = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.nameReferenceGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // nameReferenceGroup
            // 
            this.nameReferenceGroup.Controls.Add(this.listBelow);
            this.nameReferenceGroup.Controls.Add(this.listAbove);
            this.nameReferenceGroup.Controls.Add(this.labelBelow);
            this.nameReferenceGroup.Controls.Add(this.labelAbove);
            this.nameReferenceGroup.Location = new System.Drawing.Point(13, 183);
            this.nameReferenceGroup.Name = "nameReferenceGroup";
            this.nameReferenceGroup.Size = new System.Drawing.Size(256, 174);
            this.nameReferenceGroup.TabIndex = 2;
            this.nameReferenceGroup.TabStop = false;
            this.nameReferenceGroup.Text = "Reference Sections";
            // 
            // listBelow
            // 
            this.listBelow.Location = new System.Drawing.Point(75, 19);
            this.listBelow.Name = "listBelow";
            this.listBelow.Size = new System.Drawing.Size(165, 69);
            this.listBelow.TabIndex = 3;
            // 
            // listAbove
            // 
            this.listAbove.Location = new System.Drawing.Point(75, 94);
            this.listAbove.Name = "listAbove";
            this.listAbove.Size = new System.Drawing.Size(165, 69);
            this.listAbove.TabIndex = 2;
            // 
            // labelBelow
            // 
            this.labelBelow.AutoSize = true;
            this.labelBelow.Location = new System.Drawing.Point(18, 19);
            this.labelBelow.Name = "labelBelow";
            this.labelBelow.Size = new System.Drawing.Size(39, 13);
            this.labelBelow.TabIndex = 1;
            this.labelBelow.Text = "Below:";
            // 
            // labelAbove
            // 
            this.labelAbove.AutoSize = true;
            this.labelAbove.Location = new System.Drawing.Point(18, 94);
            this.labelAbove.Name = "labelAbove";
            this.labelAbove.Size = new System.Drawing.Size(41, 13);
            this.labelAbove.TabIndex = 0;
            this.labelAbove.Text = "Above:";
            // 
            // richNotes
            // 
            this.richNotes.Location = new System.Drawing.Point(13, 50);
            this.richNotes.Name = "richNotes";
            this.richNotes.ReadOnly = true;
            this.richNotes.Size = new System.Drawing.Size(256, 127);
            this.richNotes.TabIndex = 3;
            this.richNotes.Text = "";
            this.richNotes.TextChanged += new System.EventHandler(this.richNotes_TextChanged);
            // 
            // textSectionNameNumber
            // 
            this.textSectionNameNumber.Location = new System.Drawing.Point(15, 12);
            this.textSectionNameNumber.Name = "textSectionNameNumber";
            this.textSectionNameNumber.ReadOnly = true;
            this.textSectionNameNumber.Size = new System.Drawing.Size(254, 20);
            this.textSectionNameNumber.TabIndex = 1;
            // 
            // labelNumber
            // 
            this.labelNumber.AutoSize = true;
            this.labelNumber.Location = new System.Drawing.Point(9, 15);
            this.labelNumber.Name = "labelNumber";
            this.labelNumber.Size = new System.Drawing.Size(0, 13);
            this.labelNumber.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 34);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(35, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "Notes";
            // 
            // SectionGeneralPage
            // 
            this.Controls.Add(this.label1);
            this.Controls.Add(this.richNotes);
            this.Controls.Add(this.nameReferenceGroup);
            this.Controls.Add(this.textSectionNameNumber);
            this.Controls.Add(this.labelNumber);
            this.Name = "SectionGeneralPage";
            this.nameReferenceGroup.ResumeLayout(false);
            this.nameReferenceGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox nameReferenceGroup;
        private System.Windows.Forms.Label labelBelow;
        private System.Windows.Forms.Label labelAbove;
        private System.Windows.Forms.ListBox listBelow;
        private System.Windows.Forms.ListBox listAbove;
        private System.Windows.Forms.RichTextBox richNotes;
        private System.Windows.Forms.TextBox textSectionNameNumber;
        private System.Windows.Forms.Label labelNumber;
        private System.Windows.Forms.Label label1;
    }
}
