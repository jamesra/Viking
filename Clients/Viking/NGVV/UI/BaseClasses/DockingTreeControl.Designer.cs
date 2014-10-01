namespace Viking.UI.BaseClasses
{
    partial class DockingTreeControl
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
            this.Tree = new Viking.UI.Controls.ObjectTreeView();
            this.SuspendLayout();
            // 
            // Tree
            // 
            this.Tree.AllowDrop = true;
            this.Tree.BackColor = System.Drawing.SystemColors.Control;
            this.Tree.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.Tree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Tree.Location = new System.Drawing.Point(0, 16);
            this.Tree.Name = "Tree";
            this.Tree.SelectedObject = null;
            this.Tree.Size = new System.Drawing.Size(335, 409);
            this.Tree.Sorted = true;
            this.Tree.TabIndex = 2;
            // 
            // DockingTreeControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.Controls.Add(this.Tree);
            this.Name = "DockingTreeControl";
            this.Load += new System.EventHandler(this.DockingTreeControl_Load);
            this.Controls.SetChildIndex(this.LabelTitle, 0);
            this.Controls.SetChildIndex(this.Tree, 0);
            this.ResumeLayout(false);

        }

        #endregion

        protected Viking.UI.Controls.ObjectTreeView Tree;

    }
}
