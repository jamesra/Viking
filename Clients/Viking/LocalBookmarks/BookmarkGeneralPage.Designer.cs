namespace LocalBookmarks
{
    partial class BookmarkGeneralPage
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
            System.Windows.Forms.Label labelComment;
            this.textName = new System.Windows.Forms.TextBox();
            this.labelName = new System.Windows.Forms.Label();
            this.richComment = new System.Windows.Forms.RichTextBox();
            labelComment = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // labelComment
            // 
            labelComment.AutoSize = true;
            labelComment.Location = new System.Drawing.Point(6, 162);
            labelComment.Name = "labelComment";
            labelComment.Size = new System.Drawing.Size(54, 13);
            labelComment.TabIndex = 4;
            labelComment.Text = "Comment:";
            // 
            // textName
            // 
            this.textName.Location = new System.Drawing.Point(48, 3);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(229, 20);
            this.textName.TabIndex = 1;
            // 
            // labelName
            // 
            this.labelName.AutoSize = true;
            this.labelName.Location = new System.Drawing.Point(6, 6);
            this.labelName.Name = "labelName";
            this.labelName.Size = new System.Drawing.Size(38, 13);
            this.labelName.TabIndex = 2;
            this.labelName.Text = "Name:";
            // 
            // richComment
            // 
            this.richComment.Location = new System.Drawing.Point(9, 178);
            this.richComment.Name = "richComment";
            this.richComment.Size = new System.Drawing.Size(260, 170);
            this.richComment.TabIndex = 2;
            this.richComment.Text = "";
            // 
            // BookmarkGeneralPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.richComment);
            this.Controls.Add(labelComment);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.labelName);
            this.Name = "BookmarkGeneralPage";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.RichTextBox richComment;
    }
}
