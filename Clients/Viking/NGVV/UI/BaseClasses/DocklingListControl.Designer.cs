namespace Viking.UI.BaseClasses
{
    partial class DockingListControl
    {
        protected Viking.UI.BaseClasses.ObjectListView ListItems;
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
            this.ListItems = new Viking.UI.BaseClasses.ObjectListView();
            this.SuspendLayout();
            // 
            // LabelTitle
            // 
            this.LabelTitle.Size = new System.Drawing.Size(112, 16);
            // 
            // ListItems
            // 
            this.ListItems.Alignment = System.Windows.Forms.ListViewAlignment.Default;
            this.ListItems.AllowColumnReorder = true;
            this.ListItems.BackColor = System.Drawing.SystemColors.Control;
            this.ListItems.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.ListItems.DisplayType = null;
            this.ListItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ListItems.FullRowSelect = true;
            this.ListItems.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.ListItems.Location = new System.Drawing.Point(0, 16);
            this.ListItems.Name = "ListItems";
            this.ListItems.Objects = new Viking.Common.IUIObject[0];
            this.ListItems.Size = new System.Drawing.Size(112, 168);
            this.ListItems.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.ListItems.TabIndex = 7;
            this.ListItems.UseCompatibleStateImageBehavior = false;
            this.ListItems.View = System.Windows.Forms.View.Details;
            this.ListItems.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.ListItems_MouseDoubleClick);
            this.ListItems.Resize += new System.EventHandler(this.DockingList_Resize);
            this.ListItems.SelectedIndexChanged += new System.EventHandler(this.ListItems_SelectedIndexChanged);
            this.ListItems.MouseDown += new System.Windows.Forms.MouseEventHandler(this.ListItems_MouseDown);
            // 
            // DocklingListControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.Controls.Add(this.ListItems);
            this.Name = "DocklingListControl";
            this.Size = new System.Drawing.Size(112, 184);
            this.Resize += new System.EventHandler(this.DockingList_Resize);
            this.Controls.SetChildIndex(this.LabelTitle, 0);
            this.Controls.SetChildIndex(this.ListItems, 0);
            this.ResumeLayout(false);

        }

        #endregion
    }
}
