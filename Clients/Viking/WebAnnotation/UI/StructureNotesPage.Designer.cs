namespace WebAnnotation.UI
{
    partial class StructureNotesPage
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
            this.textNotes = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // textNotes
            // 
            this.textNotes.AutoWordSelection = true;
            this.textNotes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textNotes.Location = new System.Drawing.Point(0, 0);
            this.textNotes.Name = "textNotes";
            this.textNotes.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.textNotes.Size = new System.Drawing.Size(280, 360);
            this.textNotes.TabIndex = 5;
            this.textNotes.Text = "";
            // 
            // StructureNotesPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.textNotes);
            this.Name = "StructureNotesPage";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.RichTextBox textNotes;
    }
}
