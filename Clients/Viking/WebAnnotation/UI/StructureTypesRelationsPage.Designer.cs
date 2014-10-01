namespace WebAnnotation.UI
{
    partial class StructureTypesRelationsPage
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
            this.linkParent = new Viking.UI.Controls.ObjectLinkLabel();
            this.listChildren = new Viking.UI.BaseClasses.ObjectListView();
            this.labelChildren = new System.Windows.Forms.Label();
            this.labelParent = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // linkParent
            // 
            this.linkParent.Location = new System.Drawing.Point(87, 12);
            this.linkParent.Name = "linkParent";
            this.linkParent.ReadOnly = false;
            this.linkParent.Size = new System.Drawing.Size(166, 21);
            this.linkParent.SourceObject = null;
            this.linkParent.SourceType = null;
            this.linkParent.TabIndex = 9;
            // 
            // listChildren
            // 
            this.listChildren.AllowColumnReorder = true;
            this.listChildren.DisplayType = null;
            this.listChildren.FullRowSelect = true;
            this.listChildren.Location = new System.Drawing.Point(13, 54);
            this.listChildren.Name = "listChildren";
            this.listChildren.Objects = new Viking.Common.IUIObject[0];
            //this.listChildren.SelectedObjects = new Viking.Common.IUIObject[0];
            this.listChildren.Size = new System.Drawing.Size(240, 185);
            this.listChildren.TabIndex = 8;
            this.listChildren.UseCompatibleStateImageBehavior = false;
            this.listChildren.View = System.Windows.Forms.View.Details;
            // 
            // labelChildren
            // 
            this.labelChildren.AutoSize = true;
            this.labelChildren.Location = new System.Drawing.Point(10, 38);
            this.labelChildren.Name = "labelChildren";
            this.labelChildren.Size = new System.Drawing.Size(65, 13);
            this.labelChildren.TabIndex = 7;
            this.labelChildren.Text = "Child Types:";
            // 
            // labelParent
            // 
            this.labelParent.AutoSize = true;
            this.labelParent.Location = new System.Drawing.Point(10, 11);
            this.labelParent.Name = "labelParent";
            this.labelParent.Size = new System.Drawing.Size(68, 13);
            this.labelParent.TabIndex = 6;
            this.labelParent.Text = "Parent Type:";
            // 
            // StructureTypesRelationsPage
            // 
            this.Controls.Add(this.linkParent);
            this.Controls.Add(this.listChildren);
            this.Controls.Add(this.labelChildren);
            this.Controls.Add(this.labelParent);
            this.Name = "StructureTypesRelationsPage";
            this.Size = new System.Drawing.Size(280, 360);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Viking.UI.Controls.ObjectLinkLabel linkParent;
        private Viking.UI.BaseClasses.ObjectListView listChildren;
        private System.Windows.Forms.Label labelChildren;
        private System.Windows.Forms.Label labelParent;
    }
}
